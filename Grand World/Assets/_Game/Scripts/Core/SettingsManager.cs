using System;
using UnityEngine;

namespace GrandWorld.Core
{
    /// <summary>
    /// Пользовательские настройки игры. Хранятся в PlayerPrefs и применяются при старте.
    /// Остальные системы подписываются на <see cref="Changed"/>, чтобы реагировать на изменения.
    /// </summary>
    public static class SettingsManager
    {
        // Ключи PlayerPrefs
        const string KeyVolume = "gw.volume";
        const string KeyMouseSens = "gw.mouseSensitivity";
        const string KeyStickSens = "gw.stickSensitivity";
        const string KeyTouchSens = "gw.touchSensitivity";
        const string KeyInvertY = "gw.invertY";
        const string KeyQuality = "gw.quality";
        const string KeyFullscreen = "gw.fullscreen";
        const string KeyMotionBlur = "gw.motionBlur";

        /// <summary>Уровни качества графики (индексы совпадают с Quality Settings проекта).</summary>
        public enum QualityLevel { Low = 0, Medium = 1, High = 2 }

        public static float MasterVolume { get; private set; } = 0.8f;
        /// <summary>Множитель чувствительности мыши (1 = по умолчанию).</summary>
        public static float MouseSensitivity { get; private set; } = 1f;
        /// <summary>Множитель чувствительности правого стика геймпада.</summary>
        public static float StickSensitivity { get; private set; } = 1f;
        /// <summary>Множитель чувствительности свайпа по экрану.</summary>
        public static float TouchSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static QualityLevel Quality { get; private set; } = QualityLevel.High;
        public static bool Fullscreen { get; private set; } = true;
        public static bool MotionBlur { get; private set; } = true;

        /// <summary>Вызывается после любого изменения и применения настроек.</summary>
        public static event Action Changed;

        static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            _loaded = false;
            Changed = null;
            Load();
            Apply();
        }

        /// <summary>Значения по умолчанию зависят от платформы.</summary>
        static QualityLevel DefaultQuality => Application.isMobilePlatform ? QualityLevel.Medium : QualityLevel.High;

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            MouseSensitivity = PlayerPrefs.GetFloat(KeyMouseSens, 1f);
            StickSensitivity = PlayerPrefs.GetFloat(KeyStickSens, 1f);
            TouchSensitivity = PlayerPrefs.GetFloat(KeyTouchSens, 1f);
            InvertY = PlayerPrefs.GetInt(KeyInvertY, 0) == 1;
            Quality = (QualityLevel)Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, (int)DefaultQuality), 0, 2);
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            // Размытие в движении на телефонах по умолчанию выключено — экономим производительность
            MotionBlur = PlayerPrefs.GetInt(KeyMotionBlur, Application.isMobilePlatform ? 0 : 1) == 1;
            _loaded = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
            PlayerPrefs.SetFloat(KeyMouseSens, MouseSensitivity);
            PlayerPrefs.SetFloat(KeyStickSens, StickSensitivity);
            PlayerPrefs.SetFloat(KeyTouchSens, TouchSensitivity);
            PlayerPrefs.SetInt(KeyInvertY, InvertY ? 1 : 0);
            PlayerPrefs.SetInt(KeyQuality, (int)Quality);
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyMotionBlur, MotionBlur ? 1 : 0);
            PlayerPrefs.Save();
        }

        // --- Сеттеры: меняют значение, применяют и сохраняют ---

        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); Commit(); }
        public static void SetMouseSensitivity(float v) { MouseSensitivity = Mathf.Clamp(v, 0.1f, 3f); Commit(); }
        public static void SetStickSensitivity(float v) { StickSensitivity = Mathf.Clamp(v, 0.1f, 3f); Commit(); }
        public static void SetTouchSensitivity(float v) { TouchSensitivity = Mathf.Clamp(v, 0.1f, 3f); Commit(); }
        public static void SetInvertY(bool v) { InvertY = v; Commit(); }
        public static void SetQuality(QualityLevel v) { Quality = v; Commit(); }
        public static void SetFullscreen(bool v) { Fullscreen = v; Commit(); }
        public static void SetMotionBlur(bool v) { MotionBlur = v; Commit(); }

        static void Commit()
        {
            Apply();
            Save();
        }

        /// <summary>Применяет настройки к движку и оповещает подписчиков.</summary>
        public static void Apply()
        {
            if (!_loaded) Load();

            AudioListener.volume = MasterVolume;

            if (QualitySettings.GetQualityLevel() != (int)Quality && (int)Quality < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel((int)Quality, true);

            if (!Application.isMobilePlatform && !Application.isEditor)
            {
                var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode)
                {
                    if (Fullscreen)
                    {
                        var res = Screen.currentResolution;
                        Screen.SetResolution(res.width, res.height, mode);
                    }
                    else
                    {
                        Screen.SetResolution(Mathf.RoundToInt(Screen.currentResolution.width * 0.75f),
                                             Mathf.RoundToInt(Screen.currentResolution.height * 0.75f), mode);
                    }
                }
            }

            // Стабильная частота кадров: на ПК — по вертикальной синхронизации, на телефоне — 60 при возможности
            if (Application.isMobilePlatform)
                Application.targetFrameRate = Quality == QualityLevel.Low ? 30 : 60;

            Changed?.Invoke();
        }
    }
}
