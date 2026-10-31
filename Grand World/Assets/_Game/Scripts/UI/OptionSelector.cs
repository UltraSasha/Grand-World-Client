using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace GrandWorld.UI
{
    /// <summary>
    /// Переключатель вариантов «‹ Значение ›». Меняется стрелками/кликами по краям,
    /// стрелками клавиатуры и влево/вправо на геймпаде, когда элемент выделен.
    /// </summary>
    public class OptionSelector : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Tooltip("Текст текущего значения")]
        [SerializeField] TMP_Text valueLabel;
        [Tooltip("Варианты")]
        [SerializeField] string[] options = { "Низкое", "Среднее", "Высокое" };
        [SerializeField] int index;

        public event Action<int> ValueChanged;

        public int Index => index;

        public void SetOptions(string[] values, int selected)
        {
            options = values;
            SetIndexWithoutNotify(selected);
        }

        public void SetIndexWithoutNotify(int i)
        {
            if (options == null || options.Length == 0) return;
            index = (i % options.Length + options.Length) % options.Length;
            if (valueLabel != null) valueLabel.text = options[index];
        }

        void Step(int dir)
        {
            SetIndexWithoutNotify(index + dir);
            ValueChanged?.Invoke(index);
            if (valueLabel != null) StartCoroutine(UITween.Run(0.18f, UITween.EaseOutBack,
                k => valueLabel.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, k)));
        }

        public override void OnMove(AxisEventData e)
        {
            if (!IsInteractable()) { base.OnMove(e); return; }
            switch (e.moveDir)
            {
                case MoveDirection.Left: Step(-1); e.Use(); break;
                case MoveDirection.Right: Step(1); e.Use(); break;
                default: base.OnMove(e); break;
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            // Клик по левой трети — назад, по остальной части — вперёд
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local);
            var rect = ((RectTransform)transform).rect;
            Step(local.x < rect.xMin + rect.width / 3f ? -1 : 1);
        }

        public void OnSubmit(BaseEventData e)
        {
            if (IsInteractable()) Step(1);
        }
    }
}
