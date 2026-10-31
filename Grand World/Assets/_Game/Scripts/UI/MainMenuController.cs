using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using GrandWorld.Core;

namespace GrandWorld.UI
{
    /// <summary>
    /// Главное меню: «Играть», «Настройки», «Выход». Анимированное появление,
    /// переходы между экранами и затемнение при запуске игры.
    /// Работает с мышью, геймпадом и касаниями.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Экраны")]
        [SerializeField] MenuScreen mainScreen;
        [SerializeField] MenuScreen settingsScreen;
        [SerializeField] SettingsPanel settingsPanel;

        [Header("Кнопки")]
        [SerializeField] Button playButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Header("Заголовок")]
        [Tooltip("Логотип/название игры, появляется первым")]
        [SerializeField] CanvasGroup title;
        [Tooltip("Подпись внизу экрана (версия, подсказка управления)")]
        [SerializeField] CanvasGroup footer;

        [Header("Ввод")]
        [Tooltip("Действие «Назад» (Esc / B) из карты UI")]
        [SerializeField] InputActionReference backAction;

        bool _starting;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            // На телефонах выход из приложения делается системной кнопкой — прячем «Выход»
            if (Application.isMobilePlatform && exitButton != null) exitButton.gameObject.SetActive(false);

            playButton.onClick.AddListener(Play);
            settingsButton.onClick.AddListener(OpenSettings);
            if (exitButton != null) exitButton.onClick.AddListener(Exit);
            settingsPanel.BackRequested += CloseSettings;

            settingsScreen.HideImmediate();
            mainScreen.HideImmediate();
            StartCoroutine(Intro());
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
        }

        IEnumerator Intro()
        {
            if (title != null) title.alpha = 0f;
            if (footer != null) footer.alpha = 0f;
            SceneLoader.FadeIn();
            yield return new WaitForSecondsRealtime(0.35f);

            if (title != null)
            {
                var rt = (RectTransform)title.transform;
                Vector2 end = rt.anchoredPosition;
                StartCoroutine(UITween.SlideIn(rt, title, end, new Vector2(0f, 40f), 0.9f));
            }
            mainScreen.Show(0.35f);
            if (footer != null) StartCoroutine(UITween.Fade(footer, 1f, 0.8f, 0.9f));
        }

        void Play()
        {
            if (_starting) return;
            _starting = true;
            mainScreen.Hide();
            if (title != null) StartCoroutine(UITween.Fade(title, 0f, 0.3f));
            SceneLoader.Load(SceneLoader.GameScene);
        }

        void OpenSettings()
        {
            mainScreen.Hide();
            settingsScreen.Show(0.12f);
        }

        void CloseSettings()
        {
            settingsScreen.Hide();
            mainScreen.Show(0.12f);
        }

        void OnBack(InputAction.CallbackContext _)
        {
            if (settingsScreen.IsVisible) CloseSettings();
        }

        void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
