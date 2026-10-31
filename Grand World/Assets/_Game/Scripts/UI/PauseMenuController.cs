using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using GrandWorld.Core;
using GrandWorld.Player;

namespace GrandWorld.UI
{
    /// <summary>
    /// Пауза в игре (Esc / Start / экранная кнопка): «Продолжить», «Настройки», «В главное меню».
    /// Останавливает время, освобождает курсор и отключает игровой ввод.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] PlayerInputHandler playerInput;
        [Tooltip("Затемняющий/размывающий фон паузы")]
        [SerializeField] CanvasGroup backdrop;
        [SerializeField] MenuScreen pauseScreen;
        [SerializeField] MenuScreen settingsScreen;
        [SerializeField] SettingsPanel settingsPanel;
        [Tooltip("Экранное управление — прячется на паузе")]
        [SerializeField] GameObject mobileControls;

        [Header("Кнопки")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button mainMenuButton;

        [Header("Ввод")]
        [Tooltip("Действие «Назад» (Esc / B) из карты UI — закрывает настройки или паузу")]
        [SerializeField] InputActionReference backAction;

        public bool IsPaused { get; private set; }
        Coroutine _backdropRoutine;
        int _pausedFrame = -1;

        void Start()
        {
            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(OpenSettings);
            mainMenuButton.onClick.AddListener(ToMainMenu);
            settingsPanel.BackRequested += CloseSettings;
            if (playerInput != null) playerInput.PausePressed += TogglePause;

            pauseScreen.HideImmediate();
            settingsScreen.HideImmediate();
            if (backdrop != null)
            {
                backdrop.alpha = 0f;
                backdrop.blocksRaycasts = false;
                backdrop.gameObject.SetActive(false);
            }
            SetCursorForGameplay(true);
        }

        void OnEnable()
        {
            if (backAction != null)
            {
                backAction.action.Enable();
                backAction.action.performed += OnBack;
            }
        }

        void OnDisable()
        {
            if (backAction != null) backAction.action.performed -= OnBack;
            if (playerInput != null) playerInput.PausePressed -= TogglePause;
        }

        void OnDestroy()
        {
            // Не оставляем игру замороженной при выгрузке сцены
            Time.timeScale = 1f;
        }

        void OnApplicationFocus(bool focus)
        {
            // При возврате в окно игры курсор снова захватывается
            if (focus && !IsPaused) SetCursorForGameplay(true);
        }

        void TogglePause()
        {
            if (SceneLoader.IsLoading) return;
            if (IsPaused)
            {
                // Esc на экране настроек возвращает в паузу, а не в игру
                if (settingsScreen.IsVisible) CloseSettings();
                else Resume();
            }
            else Pause();
        }

        void OnBack(InputAction.CallbackContext ctx)
        {
            // Esc и Start обрабатываются через PausePressed; здесь — только кнопка B геймпада
            if (!IsPaused || Time.frameCount == _pausedFrame) return;
            if (ctx.control.device is Keyboard || ctx.control.name == "start") return;
            if (settingsScreen.IsVisible) CloseSettings();
            else Resume();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            _pausedFrame = Time.frameCount;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (playerInput != null) playerInput.SetGameplayEnabled(false);
            if (mobileControls != null) mobileControls.SetActive(false);
            SetCursorForGameplay(false);
            ShowBackdrop(true);
            pauseScreen.Show();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            pauseScreen.Hide();
            settingsScreen.Hide();
            ShowBackdrop(false);
            if (playerInput != null) playerInput.SetGameplayEnabled(true);
            if (mobileControls != null) mobileControls.SetActive(MobileControlsUI.ShouldShow);
            SetCursorForGameplay(true);
        }

        void OpenSettings()
        {
            pauseScreen.Hide();
            settingsScreen.Show(0.1f);
        }

        void CloseSettings()
        {
            settingsScreen.Hide();
            pauseScreen.Show(0.1f);
        }

        void ToMainMenu()
        {
            pauseScreen.Hide();
            AudioListener.pause = false;
            SceneLoader.Load(SceneLoader.MainMenuScene);
        }

        void ShowBackdrop(bool show)
        {
            if (backdrop == null) return;
            if (_backdropRoutine != null) StopCoroutine(_backdropRoutine);
            backdrop.gameObject.SetActive(true);
            backdrop.blocksRaycasts = show;
            _backdropRoutine = StartCoroutine(UITween.Run(0.25f, UITween.EaseInOut, k =>
            {
                backdrop.alpha = show ? k : 1f - k;
                if (!show && k >= 1f) backdrop.gameObject.SetActive(false);
            }));
        }

        static void SetCursorForGameplay(bool gameplay)
        {
            // На телефонах курсора нет — не трогаем
            if (Application.isMobilePlatform) return;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }
    }
}
