using UnityEngine;

namespace GrandWorld.CameraSystem
{
    /// <summary>
    /// Медленный облёт камеры вокруг точки — живой фон главного меню.
    /// Добавляет лёгкое «дыхание» по высоте и расстоянию, чтобы движение не казалось механическим.
    /// </summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        [Tooltip("Точка, вокруг которой летает камера")]
        [SerializeField] Transform pivot;
        [Tooltip("Смещение точки взгляда относительно pivot, м")]
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 1.1f, 0f);
        [Tooltip("Расстояние до точки, м")]
        [SerializeField] float distance = 4.2f;
        [Tooltip("Высота камеры над pivot, м")]
        [SerializeField] float height = 1.6f;
        [Tooltip("Скорость облёта, градусов в секунду")]
        [SerializeField] float degreesPerSecond = 4f;
        [Tooltip("Амплитуда «дыхания» расстояния, м")]
        [SerializeField] float breathe = 0.35f;
        [Tooltip("Период «дыхания», сек.")]
        [SerializeField] float breathePeriod = 14f;
        [Tooltip("Смещение кадра по горизонтали (персонаж не в центре, а справа от меню), м")]
        [SerializeField] float sideShift = -1.0f;

        float _angle = 210f;

        void LateUpdate()
        {
            if (pivot == null) return;
            _angle += degreesPerSecond * Time.unscaledDeltaTime;
            float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / breathePeriod);
            float d = distance + breathe * phase;
            Quaternion rot = Quaternion.Euler(0f, _angle, 0f);
            Vector3 focus = pivot.position + lookOffset;
            Vector3 pos = focus + rot * new Vector3(0f, 0f, -d) + Vector3.up * (height - lookOffset.y + 0.15f * phase);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(focus - pos, Vector3.up);
            // Сдвигаем кадр вбок, чтобы персонаж оказался справа от панели меню
            transform.position += transform.right * sideShift;
        }
    }
}
