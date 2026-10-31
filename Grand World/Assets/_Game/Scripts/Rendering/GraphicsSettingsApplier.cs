using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GrandWorld.Core;

namespace GrandWorld.Rendering
{
    /// <summary>
    /// Применяет настройки графики к камере и глобальному Volume сцены:
    /// сглаживание по уровню качества, размытие в движении, профиль постобработки для ПК/телефона.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GraphicsSettingsApplier : MonoBehaviour
    {
        [Tooltip("Глобальный Volume сцены")]
        [SerializeField] Volume globalVolume;
        [Tooltip("Профиль постобработки для ПК (максимальные эффекты)")]
        [SerializeField] VolumeProfile pcProfile;
        [Tooltip("Профиль постобработки для телефонов (облегчённый)")]
        [SerializeField] VolumeProfile mobileProfile;
        [Tooltip("Разрешить размытие в движении в этой сцене (в меню не нужно)")]
        [SerializeField] bool allowMotionBlur = true;

        Camera _camera;
        UniversalAdditionalCameraData _data;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _data = _camera.GetUniversalAdditionalCameraData();
            if (globalVolume != null)
            {
                var profile = Application.isMobilePlatform && mobileProfile != null ? mobileProfile : pcProfile;
                // Работаем с глубокой копией профиля, чтобы не менять ассет на диске
                if (profile != null) globalVolume.profile = CloneProfile(profile);
            }
        }

        static VolumeProfile CloneProfile(VolumeProfile source)
        {
            var copy = ScriptableObject.CreateInstance<VolumeProfile>();
            copy.name = source.name + " (runtime)";
            foreach (var component in source.components)
                copy.components.Add(Instantiate(component));
            copy.isDirty = true;
            return copy;
        }

        void OnEnable()
        {
            SettingsManager.Changed += Apply;
            Apply();
        }

        void OnDisable() => SettingsManager.Changed -= Apply;

        void Apply()
        {
            if (_data == null) return;
            var q = SettingsManager.Quality;
            bool mobile = Application.isMobilePlatform;

            // Сглаживание: TAA на ПК (высокое), SMAA (среднее), FXAA (низкое и телефоны)
            if (q == SettingsManager.QualityLevel.High && !mobile)
            {
                _data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                _data.taaSettings.quality = TemporalAAQuality.High;
            }
            else if (q == SettingsManager.QualityLevel.Low || mobile && q != SettingsManager.QualityLevel.High)
            {
                _data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
            else
            {
                _data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                _data.antialiasingQuality = AntialiasingQuality.High;
            }
            _data.renderPostProcessing = true;

            if (globalVolume != null && globalVolume.profile != null)
            {
                if (globalVolume.profile.TryGet(out MotionBlur blur))
                    blur.active = allowMotionBlur && SettingsManager.MotionBlur && q != SettingsManager.QualityLevel.Low;
                // На низком качестве отключаем самые дорогие эффекты
                if (globalVolume.profile.TryGet(out Bloom bloom))
                    bloom.highQualityFiltering.Override(q == SettingsManager.QualityLevel.High && !mobile);
            }
        }
    }
}
