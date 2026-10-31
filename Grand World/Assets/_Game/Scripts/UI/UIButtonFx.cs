using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace GrandWorld.UI
{
    /// <summary>
    /// Реакция кнопки на наведение, выделение геймпадом и нажатие:
    /// плавный масштаб, свечение и смена цвета текста. Работает в реальном времени (и на паузе).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIButtonFx : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [Tooltip("Масштаб при наведении/выделении")]
        [SerializeField] float hoverScale = 1.04f;
        [Tooltip("Масштаб при нажатии")]
        [SerializeField] float pressScale = 0.96f;
        [Tooltip("Резкость анимации")]
        [SerializeField] float sharpness = 18f;
        [Tooltip("Изображение свечения (рамка/подсветка), прозрачность которого анимируется")]
        [SerializeField] Graphic glow;
        [Tooltip("Максимальная прозрачность свечения")]
        [Range(0f, 1f)] [SerializeField] float glowAlpha = 1f;
        [Tooltip("Текст кнопки")]
        [SerializeField] TMP_Text label;
        [SerializeField] Color labelNormal = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] Color labelHighlighted = Color.white;
        [Tooltip("Смещение текста вправо при наведении, пиксели")]
        [SerializeField] float labelShift = 8f;

        bool _hover, _selected, _pressed;
        float _glow;
        Vector2 _labelBase;
        Selectable _selectable;

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (label != null) _labelBase = label.rectTransform.anchoredPosition;
            ApplyImmediate();
        }

        void OnDisable()
        {
            _hover = _selected = _pressed = false;
            ApplyImmediate();
        }

        void ApplyImmediate()
        {
            _glow = 0f;
            transform.localScale = Vector3.one;
            if (glow != null) SetAlpha(glow, 0f);
            if (label != null)
            {
                label.color = labelNormal;
                label.rectTransform.anchoredPosition = _labelBase;
            }
        }

        void Update()
        {
            bool interactable = _selectable == null || _selectable.IsInteractable();
            bool active = interactable && (_hover || _selected);
            float targetScale = _pressed && interactable ? pressScale : active ? hoverScale : 1f;
            float k = 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);

            float s = Mathf.Lerp(transform.localScale.x, targetScale, k);
            transform.localScale = new Vector3(s, s, 1f);

            _glow = Mathf.Lerp(_glow, active ? 1f : 0f, k);
            if (glow != null) SetAlpha(glow, _glow * glowAlpha);
            if (label != null)
            {
                label.color = Color.Lerp(labelNormal, labelHighlighted, _glow);
                label.rectTransform.anchoredPosition = _labelBase + new Vector2(labelShift * _glow, 0f);
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            // Наведение мышью делает кнопку выбранной — чтобы мышь и геймпад не подсвечивали разные кнопки
            if (_selectable != null && EventSystem.current != null && _selectable.IsInteractable())
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; }
        public void OnPointerDown(PointerEventData e) => _pressed = true;
        public void OnPointerUp(PointerEventData e) => _pressed = false;
        public void OnSelect(BaseEventData e) => _selected = true;
        public void OnDeselect(BaseEventData e) => _selected = false;
    }
}
