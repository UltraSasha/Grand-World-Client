using UnityEngine;
using GrandWorld.Player;

namespace GrandWorld.CameraSystem
{
    /// <summary>
    /// Управляет точкой, за которой следует камера Cinemachine (Third Person Follow):
    /// плавно догоняет персонажа и поворачивается по вводу обзора.
    /// Cinemachine сам ставит камеру позади и чуть выше этой точки и не даёт ей проходить сквозь объекты.
    /// </summary>
    [DefaultExecutionOrder(-100)] // раньше CinemachineBrain, чтобы камера получала актуальную точку в том же кадре
    public class CameraController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Персонаж, за которым следим")]
        [SerializeField] Transform target;
        [Tooltip("Источник ввода обзора")]
        [SerializeField] PlayerInputHandler input;

        [Header("Положение")]
        [Tooltip("Высота точки слежения над ногами персонажа стоя, м")]
        [SerializeField] float standHeight = 1.55f;
        [Tooltip("Высота точки слежения в приседе, м")]
        [SerializeField] float crouchHeight = 1.1f;
        [Tooltip("Время сглаживания следования за персонажем, сек.")]
        [SerializeField] float followSmoothTime = 0.06f;
        [Tooltip("Время сглаживания изменения высоты (присед), сек.")]
        [SerializeField] float heightSmoothTime = 0.18f;

        [Header("Поворот")]
        [Tooltip("Минимальный угол наклона камеры (взгляд вверх), градусы")]
        [SerializeField] float minPitch = -35f;
        [Tooltip("Максимальный угол наклона камеры (взгляд вниз), градусы")]
        [SerializeField] float maxPitch = 70f;
        [Tooltip("Резкость сглаживания поворота (больше — отзывчивее)")]
        [SerializeField] float rotationSharpness = 25f;

        float _yaw, _pitch = 12f;
        float _smoothYaw, _smoothPitch;
        float _height;
        float _heightVelocity;
        Vector3 _followVelocity;
        PlayerMovement _movement;

        public float Yaw => _smoothYaw;

        void Start()
        {
            if (target != null)
            {
                _movement = target.GetComponent<PlayerMovement>();
                if (input == null) input = target.GetComponent<PlayerInputHandler>();
                _yaw = _smoothYaw = target.eulerAngles.y;
                _height = standHeight;
                transform.position = target.position + Vector3.up * _height;
            }
            _smoothPitch = _pitch;
            transform.rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0f);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale <= 0f) return; // на паузе камера стоит

            // --- Поворот по вводу
            if (input != null)
            {
                Vector2 look = input.LookDelta;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);
            }
            float k = 1f - Mathf.Exp(-rotationSharpness * dt);
            _smoothYaw = Mathf.LerpAngle(_smoothYaw, _yaw, k);
            _smoothPitch = Mathf.Lerp(_smoothPitch, _pitch, k);
            transform.rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0f);

            // --- Следование за персонажем
            float targetHeight = _movement != null && _movement.IsCrouching ? crouchHeight : standHeight;
            _height = Mathf.SmoothDamp(_height, targetHeight, ref _heightVelocity, heightSmoothTime, Mathf.Infinity, dt);
            Vector3 desired = target.position + Vector3.up * _height;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _followVelocity, followSmoothTime, Mathf.Infinity, dt);
        }

        /// <summary>Мгновенно поставить камеру за спину персонажа.</summary>
        public void SnapBehindTarget()
        {
            if (target == null) return;
            _yaw = _smoothYaw = target.eulerAngles.y;
            _pitch = _smoothPitch = 12f;
            transform.SetPositionAndRotation(target.position + Vector3.up * standHeight, Quaternion.Euler(_pitch, _yaw, 0f));
        }
    }
}
