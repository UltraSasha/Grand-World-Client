using UnityEngine;
using UnityEngine.UI;
using GrandWorld.Player;

namespace GrandWorld.UI
{
    /// <summary>
    /// Экранное управление для телефонов: виртуальный джойстик слева (On-Screen Stick),
    /// обзор свайпом по правой половине экрана, кнопки прыжка, спринта, приседа и паузы.
    /// Джойстик и кнопки эмулируют геймпад, поэтому работают через те же действия ввода.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour
    {
        [Tooltip("Показывать экранное управление в редакторе (для проверки без телефона)")]
        [SerializeField] bool showInEditor;
        [SerializeField] PlayerInputHandler playerInput;
        [Tooltip("Кнопка паузы в углу экрана")]
        [SerializeField] Button pauseButton;
        [Tooltip("Прозрачность элементов управления")]
        [Range(0.2f, 1f)] [SerializeField] float opacity = 0.75f;

        static bool _editorOverride;

        /// <summary>Нужно ли показывать экранное управление на этом устройстве.</summary>
        public static bool ShouldShow => Application.isMobilePlatform || _editorOverride;

        void Awake()
        {
            _editorOverride = Application.isEditor && showInEditor;
            if (pauseButton != null && playerInput != null)
                pauseButton.onClick.AddListener(playerInput.RequestPause);

            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = opacity;

            gameObject.SetActive(ShouldShow);
        }
    }
}
