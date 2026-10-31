using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GrandWorld.Rendering;

namespace GrandWorld.EditorTools
{
    /// <summary>
    /// Настройка проекта: Player Settings, слои, уровни качества (Low/Medium/High),
    /// Renderer Features и удаление шаблонных файлов.
    /// </summary>
    public static class ProjectSetup
    {
        public const string PlayerLayerName = "Player";
        const string BundleId = "com.grandworld.burrbedy";

        const string MobileAsset = "Assets/Settings/Mobile_RPAsset.asset";
        const string PcAsset = "Assets/Settings/PC_RPAsset.asset";
        const string MobileRenderer = "Assets/Settings/Mobile_Renderer.asset";
        const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
        const string MediumAsset = "Assets/Settings/Medium_RPAsset.asset";
        const string MediumRenderer = "Assets/Settings/Medium_Renderer.asset";

        public static void Run()
        {
            RemoveTemplateFiles();
            SetupPlayerSettings();
            SetupLayers();
            SetupRenderPipelines();
            SetupQualityLevels();
            AssetDatabase.SaveAssets();
            Debug.Log("[Grand World] Настройка проекта завершена");
        }

        static void RemoveTemplateFiles()
        {
            foreach (var path in new[]
                     {
                         "Assets/Scenes/SampleScene.unity", "Assets/Scenes", "Assets/TutorialInfo", "Assets/Readme.asset",
                         "Assets/InputSystem_Actions.inputactions", "Assets/Settings/SampleSceneProfile.asset",
                     })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "Grand World";
            PlayerSettings.productName = "Grand World";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // ПК: по умолчанию полноэкранное окно, можно менять размер окна
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            // Android: ARM64 + IL2CPP, Android 8.0+, только альбомная ориентация, Vulkan с запасным GLES3
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == PlayerLayerName) return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = PlayerLayerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
        }

        static void SetupRenderPipelines()
        {
            // Среднее качество: копия ПК-ассета и ПК-рендерера с облегчёнными настройками
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MediumRenderer) == null)
                AssetDatabase.CopyAsset(PcRenderer, MediumRenderer);
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MediumAsset) == null)
                AssetDatabase.CopyAsset(PcAsset, MediumAsset);
            AssetDatabase.Refresh();

            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcAsset);
            var medium = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MediumAsset);
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileAsset);

            // Средний ассет должен ссылаться на средний рендерер
            var so = new SerializedObject(medium);
            var list = so.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(MediumRenderer);
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Тени и HDR
            pc.shadowDistance = 70f;
            pc.shadowCascadeCount = 4;
            pc.supportsHDR = true;
            medium.shadowDistance = 45f;
            medium.shadowCascadeCount = 2;
            medium.supportsHDR = true;
            mobile.shadowDistance = 28f;
            mobile.shadowCascadeCount = 1;
            SetShadowResolution(pc, 4096);
            SetShadowResolution(medium, 2048);
            SetShadowResolution(mobile, 1024);
            foreach (var a in new[] { pc, medium, mobile }) EditorUtility.SetDirty(a);

            // Размытие интерфейса — во всех рендерерах
            var blurShader = Shader.Find("Hidden/GrandWorld/KawaseBlur");
            foreach (var path in new[] { PcRenderer, MediumRenderer, MobileRenderer })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var feature = EnsureFeature<UIBlurFeature>(data, "UIBlur");
                feature.settings.shader = blurShader;
                feature.settings.downsample = path == MobileRenderer ? 4 : 3;
                feature.settings.iterations = path == MobileRenderer ? 3 : 4;
                EditorUtility.SetDirty(feature);
            }

            // В среднем рендерере SSAO считаем в половинном разрешении
            var mediumData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MediumRenderer);
            foreach (var f in mediumData.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
            {
                var fso = new SerializedObject(f);
                var down = fso.FindProperty("m_Settings.Downsample");
                if (down != null) down.boolValue = true;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetShadowResolution(UniversalRenderPipelineAsset asset, int resolution)
        {
            var so = new SerializedObject(asset);
            var p = so.FindProperty("m_MainLightShadowmapResolution");
            if (p != null) p.intValue = resolution;
            var soft = so.FindProperty("m_SoftShadowsSupported");
            if (soft != null) soft.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Добавляет Renderer Feature в рендерер, если её ещё нет.</summary>
        static T EnsureFeature<T>(UniversalRendererData data, string name) where T : ScriptableRendererFeature
        {
            var existing = data.rendererFeatures.OfType<T>().FirstOrDefault();
            if (existing != null) return existing;

            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return feature;
        }

        static void SetupQualityLevels()
        {
            var assets = new[]
            {
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MobileAsset),
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MediumAsset),
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcAsset),
            };
            string[] names = { "Low", "Medium", "High" };

            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            // Берём за основу существующий уровень «PC», копируя его до трёх уровней
            while (levels.arraySize < 3) levels.InsertArrayElementAtIndex(levels.arraySize - 1);
            while (levels.arraySize > 3) levels.DeleteArrayElementAtIndex(levels.arraySize - 1);
            for (int i = 0; i < 3; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = names[i];
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                level.FindPropertyRelative("vSyncCount").intValue = 1;
                level.FindPropertyRelative("antiAliasing").intValue = 0;
                level.FindPropertyRelative("anisotropicTextures").intValue = i == 0 ? 0 : 2;
                level.FindPropertyRelative("globalTextureMipmapLimit").intValue = i == 0 ? 1 : 0;
                level.FindPropertyRelative("lodBias").floatValue = i == 0 ? 0.7f : i == 1 ? 1f : 1.5f;
                level.FindPropertyRelative("skinWeights").intValue = i == 0 ? 2 : 4;
            }
            qs.FindProperty("m_CurrentQuality").intValue = 2;

            // Уровень по умолчанию для платформ: Android — среднее, ПК — высокое
            var defaults = qs.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var e = defaults.GetArrayElementAtIndex(i);
                string platform = e.FindPropertyRelative("first").stringValue;
                e.FindPropertyRelative("second").intValue = platform == "Android" || platform == "iPhone" ? 1 : 2;
            }
            qs.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = assets[2];
            QualitySettings.SetQualityLevel(2, true);
        }
    }
}
