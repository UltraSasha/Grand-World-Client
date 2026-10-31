using System;
using UnityEngine;

namespace GrandWorld.Player
{
    /// <summary>
    /// Передвижение персонажа на CharacterController: движение относительно камеры,
    /// разгон и торможение с инерцией, плавный разворот, прыжок, гравитация,
    /// проверка земли, склоны и присед.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        // Скорости совпадают с естественной скоростью клипов анимации — так ноги не скользят
        [Header("Скорости, м/с")]
        [Tooltip("Медленный шаг (неполное отклонение стика)")]
        [SerializeField] float walkSpeed = 1.46f;
        [Tooltip("Обычный бег (W или полное отклонение стика)")]
        [SerializeField] float runSpeed = 4.07f;
        [Tooltip("Спринт (Left Ctrl / двойное W / нажатие левого стика)")]
        [SerializeField] float sprintSpeed = 6.63f;
        [Tooltip("Ходьба вприсядку (Left Shift / B)")]
        [SerializeField] float crouchSpeed = 1.22f;
        [Tooltip("Порог отклонения стика, ниже которого персонаж идёт шагом")]
        [Range(0.1f, 0.9f)] [SerializeField] float walkInputThreshold = 0.55f;

        [Header("Инерция")]
        [Tooltip("Ускорение при разгоне, м/с²")]
        [SerializeField] float acceleration = 14f;
        [Tooltip("Замедление при торможении, м/с²")]
        [SerializeField] float deceleration = 18f;
        [Tooltip("Доля управляемости в воздухе (0 — нет, 1 — как на земле)")]
        [Range(0f, 1f)] [SerializeField] float airControl = 0.35f;
        [Tooltip("Время сглаживания разворота корпуса, сек.")]
        [SerializeField] float turnSmoothTime = 0.1f;

        [Header("Прыжок и гравитация")]
        [Tooltip("Высота прыжка, м")]
        [SerializeField] float jumpHeight = 1.1f;
        [Tooltip("Ускорение свободного падения, м/с² (отрицательное)")]
        [SerializeField] float gravity = -22f;
        [Tooltip("Максимальная скорость падения, м/с")]
        [SerializeField] float maxFallSpeed = 40f;
        [Tooltip("Можно прыгнуть в течение этого времени после схода с края, сек.")]
        [SerializeField] float coyoteTime = 0.12f;
        [Tooltip("Нажатие прыжка запоминается на это время до приземления, сек.")]
        [SerializeField] float jumpBuffer = 0.15f;

        [Header("Земля и склоны")]
        [Tooltip("Слои, считающиеся землёй")]
        [SerializeField] LayerMask groundMask = ~0;
        [Tooltip("Скорость соскальзывания со слишком крутых склонов, м/с")]
        [SerializeField] float slideSpeed = 6f;

        [Header("Присед")]
        [Tooltip("Высота капсулы стоя, м")]
        [SerializeField] float standHeight = 1.8f;
        [Tooltip("Высота капсулы в приседе, м")]
        [SerializeField] float crouchHeight = 1.2f;
        [Tooltip("Скорость изменения высоты капсулы, м/с")]
        [SerializeField] float heightChangeSpeed = 4f;

        [Header("Ссылки")]
        [Tooltip("Камера, относительно которой считается направление движения (по умолчанию — главная)")]
        [SerializeField] Transform cameraTransform;

        // --- Состояние для анимации и других систем
        public float HorizontalSpeed { get; private set; }
        public Vector3 Velocity => _velocity;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        /// <summary>Угловая скорость разворота, град/с (для наклона корпуса).</summary>
        public float TurnRate { get; private set; }
        /// <summary>Номинальные скорости — нужны аниматору для порогов Blend Tree.</summary>
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float SprintSpeed => sprintSpeed;
        public float CrouchSpeed => crouchSpeed;

        public event Action Jumped;
        /// <summary>Приземление; параметр — вертикальная скорость удара (положительная).</summary>
        public event Action<float> Landed;

        CharacterController _cc;
        PlayerInputHandler _input;
        Vector3 _velocity;          // горизонтальная + вертикальная скорость
        float _turnVelocity;
        float _lastGroundedTime = -10f;
        float _lastJumpPressedTime = -10f;
        Vector3 _groundNormal = Vector3.up;
        bool _wasGrounded;
        float _prevYaw;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputHandler>();
            _cc.height = standHeight;
            _cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
            _prevYaw = transform.eulerAngles.y;
        }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 move = _input != null ? _input.Move : Vector2.zero;
            bool wantsCrouch = _input != null && _input.CrouchHeld;
            bool wantsSprint = _input != null && _input.SprintHeld;
            if (_input != null && _input.ConsumeJump()) _lastJumpPressedTime = Time.time;

            UpdateGround();
            UpdateCrouch(wantsCrouch, dt);

            // --- Желаемое направление относительно камеры (только по горизонтали)
            Vector3 camForward = Vector3.forward, camRight = Vector3.right;
            if (cameraTransform != null)
            {
                camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                if (camForward.sqrMagnitude < 0.01f) camForward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up).normalized;
                camRight = Vector3.Cross(Vector3.up, camForward);
            }
            Vector3 wishDir = camForward * move.y + camRight * move.x;
            float inputMag = Mathf.Clamp01(wishDir.magnitude);
            if (inputMag > 0.001f) wishDir /= Mathf.Max(inputMag, 0.001f);

            // --- Целевая скорость по состоянию
            float targetSpeed = 0f;
            IsSprinting = false;
            if (inputMag > 0.05f)
            {
                if (IsCrouching) targetSpeed = crouchSpeed;
                else if (wantsSprint && move.y > 0.3f) { targetSpeed = sprintSpeed; IsSprinting = true; }
                else targetSpeed = inputMag < walkInputThreshold ? walkSpeed : runSpeed;
            }

            // --- Горизонтальная скорость с инерцией
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            Vector3 targetHorizontal = wishDir * targetSpeed;
            float rate = targetHorizontal.sqrMagnitude > horizontal.sqrMagnitude ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            horizontal = Vector3.MoveTowards(horizontal, targetHorizontal, rate * dt);

            // --- Разворот корпуса в сторону движения
            if (inputMag > 0.05f)
            {
                float targetYaw = Mathf.Atan2(wishDir.x, wishDir.z) * Mathf.Rad2Deg;
                float smooth = IsGrounded ? turnSmoothTime : turnSmoothTime * 2.5f;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _turnVelocity, smooth);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            float currentYaw = transform.eulerAngles.y;
            TurnRate = Mathf.Lerp(TurnRate, Mathf.DeltaAngle(_prevYaw, currentYaw) / dt, 1f - Mathf.Exp(-10f * dt));
            _prevYaw = currentYaw;

            // --- Вертикаль: прыжок и гравитация
            // Смещение за кадр считаем по средней скорости (метод трапеций) —
            // высота прыжка не зависит от частоты кадров
            float vy = _velocity.y;
            float moveVy;
            bool canJump = Time.time - _lastGroundedTime <= coyoteTime && vy <= 0.1f;
            if (Time.time - _lastJumpPressedTime <= jumpBuffer && canJump && !IsCrouching)
            {
                float jumpVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                vy = jumpVelocity + gravity * dt;
                moveVy = (jumpVelocity + vy) * 0.5f;
                _lastJumpPressedTime = -10f;
                _lastGroundedTime = -10f;
                IsGrounded = false;
                Jumped?.Invoke();
            }
            else if (IsGrounded && vy < 0f)
            {
                vy = -2f; // прижимаем к земле, чтобы не подпрыгивать на неровностях и спусках
                moveVy = vy;
            }
            else
            {
                float newVy = Mathf.Max(vy + gravity * dt, -maxFallSpeed);
                moveVy = (vy + newVy) * 0.5f;
                vy = newVy;
            }

            // --- Слишком крутой склон: соскальзываем вниз
            Vector3 slide = Vector3.zero;
            float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
            if (IsGrounded && slopeAngle > _cc.slopeLimit + 1f)
            {
                Vector3 down = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
                slide = down * slideSpeed;
            }

            _velocity = new Vector3(horizontal.x, vy, horizontal.z);
            Vector3 step = new Vector3(_velocity.x, moveVy, _velocity.z) + slide;
            CollisionFlags flags = _cc.Move(step * dt);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;

            HorizontalSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
        }

        void UpdateGround()
        {
            // Проверка земли: сфера чуть ниже основания капсулы
            float radius = _cc.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (radius + 0.05f);
            bool hit = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit info,
                                          0.05f + _cc.skinWidth + 0.08f, groundMask, QueryTriggerInteraction.Ignore);
            IsGrounded = (hit || _cc.isGrounded) && _velocity.y <= 0.1f;
            _groundNormal = hit ? info.normal : Vector3.up;

            if (IsGrounded)
            {
                _lastGroundedTime = Time.time;
                if (!_wasGrounded) Landed?.Invoke(-_velocity.y);
            }
            _wasGrounded = IsGrounded;
        }

        void UpdateCrouch(bool wantsCrouch, float dt)
        {
            if (wantsCrouch && IsGrounded) IsCrouching = true;
            else if (!wantsCrouch && IsCrouching && CanStandUp()) IsCrouching = false;

            float target = IsCrouching ? crouchHeight : standHeight;
            float h = Mathf.MoveTowards(_cc.height, target, heightChangeSpeed * dt);
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        bool CanStandUp()
        {
            // Есть ли место над головой, чтобы встать
            float r = _cc.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (_cc.height - r);
            float distance = standHeight - _cc.height;
            if (distance <= 0.01f) return true;
            return !Physics.SphereCast(bottom, r, Vector3.up, out _, distance, groundMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Мгновенно переставить персонажа (например, при старте сцены).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _cc.enabled = true;
            _velocity = Vector3.zero;
            _prevYaw = yaw;
        }
    }
}
