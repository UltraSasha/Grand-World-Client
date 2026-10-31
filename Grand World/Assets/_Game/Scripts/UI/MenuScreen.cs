using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GrandWorld.UI
{
    /// <summary>
    /// Экран меню (главный, настройки, пауза) с анимацией появления и скрытия:
    /// панель проявляется, а элементы из списка выезжают по очереди.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MenuScreen : MonoBehaviour
    {
        [Tooltip("Элементы, появляющиеся по очереди (кнопки, строки настроек)")]
        [SerializeField] RectTransform[] staggered;
        [Tooltip("Смещение, из которого выезжают элементы, пиксели")]
        [SerializeField] Vector2 slideOffset = new Vector2(-60f, 0f);
        [Tooltip("Задержка между элементами, сек.")]
        [SerializeField] float stagger = 0.05f;
        [Tooltip("Длительность анимации одного элемента, сек.")]
        [SerializeField] float itemDuration = 0.45f;
        [Tooltip("Длительность проявления/скрытия панели, сек.")]
        [SerializeField] float fadeDuration = 0.25f;
        [Tooltip("Что выделить при показе (для геймпада и клавиатуры)")]
        [SerializeField] Selectable firstSelected;

        CanvasGroup _group;
        Vector2[] _basePositions;
        CanvasGroup[] _itemGroups;
        Coroutine _routine;

        public bool IsVisible { get; private set; }

        public Selectable FirstSelected
        {
            get => firstSelected;
            set => firstSelected = value;
        }

        void Awake() => Init();

        void Init()
        {
            if (_group != null) return;
            _group = GetComponent<CanvasGroup>();
            _basePositions = new Vector2[staggered?.Length ?? 0];
            _itemGroups = new CanvasGroup[_basePositions.Length];
            for (int i = 0; i < _basePositions.Length; i++)
            {
                _basePositions[i] = staggered[i].anchoredPosition;
                _itemGroups[i] = staggered[i].GetComponent<CanvasGroup>();
                if (_itemGroups[i] == null) _itemGroups[i] = staggered[i].gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>Мгновенно скрыть без анимации.</summary>
        public void HideImmediate()
        {
            Init();
            if (_routine != null) StopCoroutine(_routine);
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            IsVisible = false;
            gameObject.SetActive(false);
        }

        public void Show(float delay = 0f)
        {
            Init();
            gameObject.SetActive(true);
            IsVisible = true;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(delay));
        }

        public void Hide()
        {
            Init();
            if (!gameObject.activeInHierarchy) return;
            IsVisible = false;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(HideRoutine());
        }

        IEnumerator ShowRoutine(float delay)
        {
            _group.interactable = false;
            _group.blocksRaycasts = true;
            for (int i = 0; i < _basePositions.Length; i++)
            {
                staggered[i].anchoredPosition = _basePositions[i] + slideOffset;
                _itemGroups[i].alpha = 0f;
            }
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            for (int i = 0; i < _basePositions.Length; i++)
                StartCoroutine(UITween.SlideIn(staggered[i], _itemGroups[i], _basePositions[i], slideOffset, itemDuration, stagger * i));
            yield return UITween.Fade(_group, 1f, fadeDuration);

            _group.interactable = true;
            if (firstSelected != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
            }
            _routine = null;
        }

        IEnumerator HideRoutine()
        {
            _group.interactable = false;
            yield return UITween.Fade(_group, 0f, fadeDuration * 0.8f);
            _group.blocksRaycasts = false;
            for (int i = 0; i < _basePositions.Length; i++) staggered[i].anchoredPosition = _basePositions[i];
            gameObject.SetActive(false);
            _routine = null;
        }
    }
}
