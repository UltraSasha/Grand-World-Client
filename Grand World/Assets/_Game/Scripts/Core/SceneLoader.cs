using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace GrandWorld.Core
{
    /// <summary>
    /// Асинхронная загрузка сцен с затемнением и экраном загрузки.
    /// Создаётся автоматически при первом обращении и живёт между сценами.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        [Tooltip("Длительность затемнения/проявления экрана, сек.")]
        [SerializeField] float fadeDuration = 0.45f;
        [Tooltip("Минимальное время показа экрана загрузки, чтобы он не мелькал, сек.")]
        [SerializeField] float minLoadingTime = 0.8f;

        static SceneLoader _instance;
        CanvasGroup _fade;
        CanvasGroup _loadingGroup;
        Image _progressFill;
        TextMeshProUGUI _hint;
        bool _busy;

        /// <summary>Идёт ли сейчас переход между сценами.</summary>
        public static bool IsLoading => _instance != null && _instance._busy;

        /// <summary>Вызывается после загрузки новой сцены, до её проявления.</summary>
        public static event Action<string> SceneLoaded;

        static readonly string[] Hints =
        {
            "Нажмите Esc, чтобы поставить игру на паузу",
            "Двойное нажатие W — спринт",
            "Shift — красться",
            "Настройте чувствительность камеры в настройках",
        };

        public static SceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SceneLoader]");
                    _instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                    _instance.BuildUI();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            SceneLoaded = null;
        }

        /// <summary>Загрузить сцену по имени с затемнением и экраном загрузки.</summary>
        public static void Load(string sceneName)
        {
            var loader = Instance;
            if (loader._busy) return;
            loader.StartCoroutine(loader.LoadRoutine(sceneName));
        }

        /// <summary>Проявить экран из чёрного (используется при старте игры).</summary>
        public static void FadeIn(float delay = 0f)
        {
            var loader = Instance;
            loader._fade.alpha = 1f;
            loader.StartCoroutine(loader.FadeRoutine(1f, 0f, delay));
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            _busy = true;
            _fade.blocksRaycasts = true;

            // 1. Затемняем текущую сцену
            yield return FadeRoutine(0f, 1f, 0f);

            // 2. Показываем экран загрузки поверх чёрного
            _hint.text = Hints[UnityEngine.Random.Range(0, Hints.Length)];
            _progressFill.fillAmount = 0f;
            yield return FadeGroup(_loadingGroup, 0f, 1f, 0.25f);

            Time.timeScale = 1f;
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            float started = Time.unscaledTime;
            float shown = 0f;
            while (op.progress < 0.9f || Time.unscaledTime - started < minLoadingTime)
            {
                // Плавно двигаем полосу к реальному прогрессу
                float target = Mathf.Clamp01(op.progress / 0.9f) * Mathf.Clamp01((Time.unscaledTime - started) / minLoadingTime);
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2f);
                _progressFill.fillAmount = shown;
                yield return null;
            }
            _progressFill.fillAmount = 1f;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            SceneLoaded?.Invoke(sceneName);
            yield return null; // даём сцене один кадр на инициализацию

            // 3. Прячем экран загрузки и проявляем новую сцену
            yield return FadeGroup(_loadingGroup, 1f, 0f, 0.2f);
            yield return FadeRoutine(1f, 0f, 0.05f);
            _fade.blocksRaycasts = false;
            _busy = false;
        }

        IEnumerator FadeRoutine(float from, float to, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            yield return FadeGroup(_fade, from, to, fadeDuration);
        }

        static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                g.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }
            g.alpha = to;
        }

        /// <summary>Интерфейс затемнения и загрузки строится кодом — никаких префабов не нужно.</summary>
        void BuildUI()
        {
            var canvasGo = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Чёрный слой затемнения
            var fadeGo = CreateChild(canvasGo.transform, "Fade");
            Stretch(fadeGo);
            fadeGo.AddComponent<Image>().color = Color.black;
            _fade = fadeGo.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;

            // Экран загрузки: надпись, полоса прогресса, подсказка
            var loadingGo = CreateChild(canvasGo.transform, "Loading");
            Stretch(loadingGo);
            _loadingGroup = loadingGo.AddComponent<CanvasGroup>();
            _loadingGroup.alpha = 0f;
            _loadingGroup.blocksRaycasts = false;

            var title = CreateText(loadingGo.transform, "Title", "ЗАГРУЗКА", 54, FontStyles.Bold, new Vector2(0, -40));
            title.characterSpacing = 18f;

            var barBg = CreateChild(loadingGo.transform, "Bar");
            var bgRt = barBg.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.sizeDelta = new Vector2(620, 6);
            bgRt.anchoredPosition = new Vector2(0, 150);
            barBg.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var fill = CreateChild(barBg.transform, "Fill");
            Stretch(fill);
            _progressFill = fill.AddComponent<Image>();
            _progressFill.color = new Color(1f, 0.78f, 0.45f, 1f);
            _progressFill.sprite = WhiteSprite();
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;

            _hint = CreateText(loadingGo.transform, "Hint", "", 26, FontStyles.Normal, new Vector2(0, 100));
            _hint.color = new Color(1f, 1f, 1f, 0.6f);
            var hintRt = _hint.rectTransform;
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            title.rectTransform.anchoredPosition = new Vector2(0, 220);
        }

        static Sprite _white;
        static Sprite WhiteSprite()
        {
            if (_white == null)
                _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _white;
        }

        static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style, Vector2 pos)
        {
            var go = CreateChild(parent, name);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(1200, 80);
            rt.anchoredPosition = pos;
            return t;
        }
    }
}
