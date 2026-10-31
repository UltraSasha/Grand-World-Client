using UnityEngine;

namespace GrandWorld.Player
{
    /// <summary>
    /// Связывает движение персонажа с Animator: скорость для Blend Tree, присед, прыжок,
    /// наклон корпуса при поворотах на бегу и IK стоп на неровной поверхности.
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int JumpId = Animator.StringToHash("Jump");

        [Header("Ссылки")]
        [Tooltip("Animator модели персонажа (по умолчанию ищется в дочерних объектах)")]
        [SerializeField] Animator animator;

        [Header("Сглаживание")]
        [Tooltip("Время сглаживания параметра скорости, сек.")]
        [SerializeField] float speedDampTime = 0.08f;

        [Header("Наклон корпуса")]
        [Tooltip("Максимальный наклон в сторону поворота, градусы")]
        [SerializeField] float maxLean = 14f;
        [Tooltip("Градусов наклона на каждые 100 град/с поворота при беге")]
        [SerializeField] float leanPerTurnRate = 6f;
        [Tooltip("Скорость, начиная с которой появляется наклон, м/с")]
        [SerializeField] float leanMinSpeed = 2f;
        [Tooltip("Резкость сглаживания наклона")]
        [SerializeField] float leanSharpness = 8f;

        [Header("IK стоп")]
        [Tooltip("Включить подстройку стоп под рельеф")]
        [SerializeField] bool footIk = true;
        [Tooltip("Слои земли для IK")]
        [SerializeField] LayerMask groundMask = ~0;
        [Tooltip("Высота щиколотки над подошвой, м")]
        [SerializeField] float ankleHeight = 0.1f;
        [Tooltip("Насколько далеко вниз искать землю под стопой, м")]
        [SerializeField] float footRayLength = 0.6f;
        [Tooltip("Скорость сглаживания смещения таза")]
        [SerializeField] float pelvisSharpness = 10f;

        PlayerMovement _movement;
        float _lean;
        float _pelvisOffset;
        float _ikWeight;

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // Пересылка OnAnimatorIK с объекта модели на этот компонент
                var relay = animator.GetComponent<AnimatorIKRelay>();
                if (relay == null) relay = animator.gameObject.AddComponent<AnimatorIKRelay>();
                relay.Target = this;
            }
        }

        void OnEnable()
        {
            if (_movement == null) return;
            _movement.Jumped += OnJumped;
            _movement.Landed += OnLanded;
        }

        void OnDisable()
        {
            if (_movement == null) return;
            _movement.Jumped -= OnJumped;
            _movement.Landed -= OnLanded;
        }

        void Update()
        {
            if (animator == null || _movement == null) return;
            float dt = Time.deltaTime;

            animator.SetFloat(SpeedId, _movement.HorizontalSpeed, speedDampTime, dt);
            animator.SetBool(GroundedId, _movement.IsGrounded);
            animator.SetBool(CrouchId, _movement.IsCrouching);
            animator.SetFloat(VerticalSpeedId, _movement.Velocity.y);

            // Наклон: сильнее на большой скорости, в сторону поворота
            float speedFactor = Mathf.InverseLerp(leanMinSpeed, _movement.SprintSpeed, _movement.HorizontalSpeed);
            float targetLean = Mathf.Clamp(-_movement.TurnRate / 100f * leanPerTurnRate * speedFactor, -maxLean, maxLean);
            if (!_movement.IsGrounded) targetLean *= 0.3f;
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-leanSharpness * dt));

            // IK стоп полностью работает стоя, на ходу плавно отключается
            float targetIk = footIk && _movement.IsGrounded && _movement.HorizontalSpeed < 0.3f ? 1f : 0f;
            _ikWeight = Mathf.MoveTowards(_ikWeight, targetIk, dt * 4f);
        }

        void LateUpdate()
        {
            // Наклон корпуса: после того как Animator записал позу, поворачиваем позвоночник
            // вокруг оси «вперёд» персонажа
            if (animator == null || !animator.isHuman || Mathf.Abs(_lean) < 0.01f) return;
            Quaternion roll = Quaternion.AngleAxis(_lean * 0.5f, transform.forward);
            RotateBone(HumanBodyBones.Spine, roll);
            RotateBone(HumanBodyBones.Chest, roll);
        }

        void RotateBone(HumanBodyBones bone, Quaternion worldRotation)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null) t.rotation = worldRotation * t.rotation;
        }

        void OnJumped()
        {
            if (animator != null) animator.SetTrigger(JumpId);
        }

        void OnLanded(float impactSpeed)
        {
            // Незабранный триггер прыжка не должен сработать после приземления
            if (animator != null) animator.ResetTrigger(JumpId);
        }

        /// <summary>Вызывается из AnimatorIKRelay в IK-проходе.</summary>
        internal void OnAnimatorIKPass()
        {
            if (animator == null || !animator.isHuman) return;

            // --- IK стоп
            if (_ikWeight <= 0.001f)
            {
                _pelvisOffset = Mathf.Lerp(_pelvisOffset, 0f, 1f - Mathf.Exp(-pelvisSharpness * Time.deltaTime));
                return;
            }
            SolveFoot(AvatarIKGoal.LeftFoot, out float leftDelta);
            SolveFoot(AvatarIKGoal.RightFoot, out float rightDelta);

            // Таз опускаем на величину самой низкой стопы, чтобы обе ноги доставали до земли
            float target = Mathf.Min(0f, Mathf.Min(leftDelta, rightDelta));
            _pelvisOffset = Mathf.Lerp(_pelvisOffset, target, 1f - Mathf.Exp(-pelvisSharpness * Time.deltaTime));
            animator.bodyPosition += Vector3.up * _pelvisOffset * _ikWeight;
        }

        void SolveFoot(AvatarIKGoal goal, out float heightDelta)
        {
            heightDelta = 0f;
            Vector3 footPos = animator.GetIKPosition(goal);
            Vector3 origin = new Vector3(footPos.x, transform.position.y + footRayLength * 0.5f, footPos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, footRayLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Сохраняем высоту стопы из анимации, но отсчитываем её от найденной земли
                heightDelta = hit.point.y - transform.position.y;
                float animHeight = footPos.y - transform.position.y;
                animator.SetIKPositionWeight(goal, _ikWeight);
                animator.SetIKRotationWeight(goal, _ikWeight);
                animator.SetIKPosition(goal, new Vector3(footPos.x, hit.point.y + Mathf.Max(animHeight, ankleHeight * 0.5f), footPos.z));
                Quaternion align = Quaternion.FromToRotation(Vector3.up, hit.normal);
                animator.SetIKRotation(goal, align * animator.GetIKRotation(goal));
            }
        }
    }
}
