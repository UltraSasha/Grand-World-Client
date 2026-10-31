using System;
using System.Collections;
using UnityEngine;

namespace GrandWorld.UI
{
    /// <summary>
    /// Небольшая библиотека анимаций интерфейса на корутинах (без DOTween).
    /// Все анимации идут в реальном времени, поэтому работают и на паузе.
    /// </summary>
    public static class UITween
    {
        /// <summary>Плавное замедление в конце (easeOutCubic).</summary>
        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        /// <summary>Плавный разгон и замедление (easeInOutCubic).</summary>
        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
        /// <summary>Лёгкий «перелёт» в конце (easeOutBack).</summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        /// <summary>Универсальная анимация: вызывает step(k) с k от 0 до 1.</summary>
        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> step, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                step(ease(t / duration));
                yield return null;
            }
            step(1f);
        }

        /// <summary>Прозрачность группы.</summary>
        public static IEnumerator Fade(CanvasGroup g, float to, float duration, float delay = 0f)
        {
            float from = g.alpha;
            return Run(duration, EaseInOut, k => g.alpha = Mathf.LerpUnclamped(from, to, k), delay);
        }

        /// <summary>Появление элемента: выезд из точки end + offset в end и проявление.</summary>
        public static IEnumerator SlideIn(RectTransform rt, CanvasGroup g, Vector2 end, Vector2 offset, float duration, float delay = 0f)
        {
            Vector2 start = end + offset;
            rt.anchoredPosition = start;
            if (g != null) g.alpha = 0f;
            return Run(duration, EaseOut, k =>
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
                if (g != null) g.alpha = k;
            }, delay);
        }

        /// <summary>Масштаб элемента.</summary>
        public static IEnumerator Scale(Transform t, Vector3 to, float duration, Func<float, float> ease = null)
        {
            Vector3 from = t.localScale;
            return Run(duration, ease ?? EaseOut, k => t.localScale = Vector3.LerpUnclamped(from, to, k));
        }
    }
}
