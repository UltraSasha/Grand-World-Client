using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GrandWorld.Core;

namespace GrandWorld.UI
{
    /// <summary>
    /// Панель настроек: громкость, чувствительность камеры, качество графики,
    /// полноэкранный режим (только ПК), размытие в движении и инверсия оси Y.
    /// Используется и в главном меню, и в меню паузы.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Элементы")]
        [SerializeField] Slider volume;
        [SerializeField] TMP_Text volumeValue;
        [SerializeField] Slider sensitivity;
        [SerializeField] TMP_Text sensitivityValue;
        [SerializeField] OptionSelector quality;
        [SerializeField] Toggle fullscreen;
        [Tooltip("Строка с переключателем полноэкранного режима — скрывается на телефонах")]
        [SerializeField] GameObject fullscreenRow;
        [SerializeField] Toggle motionBlur;
        [SerializeField] Toggle invertY;
        [SerializeField] Button back;

        [Tooltip("Элемент, выделяемый при открытии панели (для геймпада)")]
        [SerializeField] Selectable firstSelected;

        /// <summary>Нажата кнопка «Назад».</summary>
        public event Action BackRequested;

        public Selectable FirstSelected => firstSelected != null ? firstSelected : volume;

        static readonly string[] QualityNames = { "Низкое", "Среднее", "Высокое" };

        void Awake()
        {
            volume.minValue = 0f; volume.maxValue = 1f;
            sensitivity.minValue = 0.1f; sensitivity.maxValue = 3f;

            volume.onValueChanged.AddListener(v => { SettingsManager.SetMasterVolume(v); RefreshLabels(); });
            sensitivity.onValueChanged.AddListener(v =>
            {
                // Один ползунок управляет мышью, стиком и свайпом сразу
                SettingsManager.SetMouseSensitivity(v);
                SettingsManager.SetStickSensitivity(v);
                SettingsManager.SetTouchSensitivity(v);
                RefreshLabels();
            });
            quality.ValueChanged += i => SettingsManager.SetQuality((SettingsManager.QualityLevel)i);
            fullscreen.onValueChanged.AddListener(SettingsManager.SetFullscreen);
            motionBlur.onValueChanged.AddListener(SettingsManager.SetMotionBlur);
            invertY.onValueChanged.AddListener(SettingsManager.SetInvertY);
            back.onClick.AddListener(() => BackRequested?.Invoke());

            if (fullscreenRow != null) fullscreenRow.SetActive(!Application.isMobilePlatform);
        }

        void OnEnable() => Refresh();

        /// <summary>Синхронизирует элементы с текущими настройками без вызова событий.</summary>
        public void Refresh()
        {
            volume.SetValueWithoutNotify(SettingsManager.MasterVolume);
            sensitivity.SetValueWithoutNotify(SettingsManager.MouseSensitivity);
            quality.SetOptions(QualityNames, (int)SettingsManager.Quality);
            fullscreen.SetIsOnWithoutNotify(SettingsManager.Fullscreen);
            motionBlur.SetIsOnWithoutNotify(SettingsManager.MotionBlur);
            invertY.SetIsOnWithoutNotify(SettingsManager.InvertY);
            RefreshLabels();
        }

        void RefreshLabels()
        {
            if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(volume.value * 100f) + "%";
            if (sensitivityValue != null) sensitivityValue.text = sensitivity.value.ToString("0.0") + "×";
        }
    }
}
