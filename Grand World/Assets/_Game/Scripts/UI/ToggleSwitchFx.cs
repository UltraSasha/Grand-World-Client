using UnityEngine;
using UnityEngine.UI;

namespace GrandWorld.UI
{
    /// <summary>Переключатель в стиле «свитч»: кружок плавно переезжает влево/вправо.</summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleSwitchFx : MonoBehaviour
    {
        [Tooltip("Кружок переключателя")]
        [SerializeField] RectTransform knob;
        [Tooltip("Отступ кружка от края, пиксели")]
        [SerializeField] float padding = 5f;
        [Tooltip("Резкость анимации")]
        [SerializeField] float sharpness = 16f;

        Toggle _toggle;
        float _t;

        void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _t = _toggle.isOn ? 1f : 0f;
            Apply();
        }

        void OnEnable()
        {
            if (_toggle != null) { _t = _toggle.isOn ? 1f : 0f; Apply(); }
        }

        void Update()
        {
            float target = _toggle.isOn ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(Mathf.Lerp(_t, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime)), target, 0.001f);
            Apply();
        }

        void Apply()
        {
            if (knob == null) return;
            float width = ((RectTransform)transform).rect.width;
            float x = Mathf.Lerp(padding, width - knob.rect.width - padding, _t);
            knob.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
