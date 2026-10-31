using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using GrandWorld.CameraSystem;
using GrandWorld.Player;
using GrandWorld.Rendering;
using GrandWorld.UI;
using static GrandWorld.EditorTools.BuilderPaths;
using UF = GrandWorld.EditorTools.UIFactory;

namespace GrandWorld.EditorTools
{
    /// <summary>
    /// Генератор прототипа: меню Tools → Grand World.
    /// «Build All» настраивает проект, создаёт ассеты, префабы, сцены MainMenu и Game и Build Settings.
    /// Все шаги можно повторять — существующие ассеты пересоздаются.
    /// </summary>
    public static class SceneBuilder
    {
        [MenuItem("Tools/Grand World/Build All", priority = 0)]
        public static void BuildAll()
        {
            ProjectSetup.Run();
            BuildContent();
            BuildScenes();
        }

        [MenuItem("Tools/Grand World/1. Project Setup", priority = 20)]
        public static void SetupProject() => ProjectSetup.Run();

        [MenuItem("Tools/Grand World/2. Build Content", priority = 21)]
        public static void BuildContent()
        {
            ContentBuilder.BuildTextures();
            ContentBuilder.BuildFonts();
            ContentBuilder.BuildMaterials();
            ContentBuilder.BuildVolumeProfiles();
            var ctrl = ContentBuilder.BuildAnimator();
            ContentBuilder.BuildEnvironmentPrefab();
            ContentBuilder.BuildPlayerPrefab(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Grand World] Ассеты созданы");
        }

        [MenuItem("Tools/Grand World/3. Build Scenes", priority = 22)]
        public static void BuildScenes()
        {
            EnsureFolder(ScenesDir);
            BuildMainMenuScene();
            BuildGameScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(GameScene, true),
            };
            EditorSceneManager.OpenScene(MainMenuScene);
            Debug.Log("[Grand World] Сцены собраны");
        }

        // ---------------------------------------------------------------- общее для сцен

        static InputActionAsset Actions => AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);

        /// <summary>Солнце, небо с атмосферным рассеиванием, мягкий окружающий свет и туман.</summary>
        static Light SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sunGo.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            var sunData = sunGo.AddComponent<UniversalAdditionalLightData>();
            sunData.softShadowQuality = SoftShadowQuality.High;

            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky" };
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.6f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.32f, 0.34f, 0.3f));
            sky.SetFloat("_Exposure", 1.15f);
            sky = CreateOrReplace(sky, MaterialsDir + "/Sky.mat");

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            // Трёхцветное окружающее освещение — не требует запекания и одинаково на всех платформах
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.66f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.22f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.68f, 0.74f, 0.8f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            Lightmapping.lightingSettings = GetLightingSettings();
            return sun;
        }

        static LightingSettings GetLightingSettings()
        {
            const string path = RenderingDir + "/Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null)
            {
                ls = new LightingSettings { name = "Lighting", bakedGI = false, realtimeGI = false, autoGenerate = false };
                AssetDatabase.CreateAsset(ls, path);
            }
            return ls;
        }

        static Volume CreateGlobalVolume(VolumeProfile profile)
        {
            var go = new GameObject("Global Volume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 1;
            v.sharedProfile = profile;
            return v;
        }

        static Camera CreateMainCamera(Volume volume, VolumeProfile pc, VolumeProfile mobile, bool motionBlur)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 55f;
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var applier = go.AddComponent<GraphicsSettingsApplier>();
            var so = new SerializedObject(applier);
            so.FindProperty("globalVolume").objectReferenceValue = volume;
            so.FindProperty("pcProfile").objectReferenceValue = pc;
            so.FindProperty("mobileProfile").objectReferenceValue = mobile;
            so.FindProperty("allowMotionBlur").boolValue = motionBlur;
            so.ApplyModifiedPropertiesWithoutUndo();
            return cam;
        }

        static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[Grand World] Нет поля {property} у {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string property, IList<Object> values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- главное меню

        static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ContentBuilder.EnvironmentPrefab));

            // Персонаж в покое — «герой» фона
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbx));
            model.name = "MenuCharacter";
            model.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 160f, 0f));
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ContentBuilder.ControllerPath);
            animator.applyRootMotion = false;

            var volume = CreateGlobalVolume(ContentBuilder.ProfileMenuPc);
            var cam = CreateMainCamera(volume, ContentBuilder.ProfileMenuPc, ContentBuilder.ProfileMenuMobile, false);
            var orbit = cam.gameObject.AddComponent<MenuCameraOrbit>();
            Set(orbit, "pivot", model.transform);

            UIFactory.EventSystem(Actions);
            BuildMainMenuUI();

            EditorSceneManager.SaveScene(scene, MainMenuScene);
        }

        static void BuildMainMenuUI()
        {
            var canvas = UF.Canvas("MenuCanvas", 0);
            var root = (RectTransform)canvas.transform;

            // Затемнение слева — текст читается на любом фоне
            var shade = UF.Image(UF.Rect("Shade", root), ContentBuilder.GradientSprite, new Color(0.02f, 0.03f, 0.05f, 0.6f));
            shade.rectTransform.anchorMin = new Vector2(0, 0);
            shade.rectTransform.anchorMax = new Vector2(0, 1);
            shade.rectTransform.pivot = new Vector2(0, 0.5f);
            shade.rectTransform.sizeDelta = new Vector2(1250, 0);

            // Стеклянная колонна меню
            var panel = UF.Rect("Panel", root);
            panel.anchorMin = new Vector2(0, 0);
            panel.anchorMax = new Vector2(0, 1);
            panel.pivot = new Vector2(0, 0.5f);
            panel.offsetMin = new Vector2(80, 80);
            panel.offsetMax = new Vector2(80 + 640, -80);
            UF.Glass(panel, UF.GlassTint);

            // Заголовок
            var title = UF.Rect("Title", panel);
            UF.Place(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -64), new Vector2(560, 170));
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            var t1 = UF.Text(UF.Place(UF.Rect("Name", title), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(560, 100)),
                          "GRAND WORLD", 64, true);
            t1.characterSpacing = 4f;
            var t2 = UF.Text(UF.Place(UF.Rect("Subtitle", title), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -104), new Vector2(560, 40)),
                          "ROLEPLAY  ·  ПРОТОТИП", 22, true, TextAlignmentOptions.MidlineLeft, UF.Accent);
            t2.characterSpacing = 14f;
            var line = UF.Image(UF.Place(UF.Rect("Line", title), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -158), new Vector2(64, 3)),
                             ContentBuilder.PillSprite, new Color(1, 1, 1, 0.35f));

            // --- Экран: главный
            var mainRt = ScreenArea(panel, "MainScreen", 290, 110);
            var mainScreen = mainRt.gameObject.AddComponent<MenuScreen>();
            var play = UF.MenuButton(mainRt, "Play", "Играть", new Vector2(528, 76));
            var settings = UF.MenuButton(mainRt, "Settings", "Настройки", new Vector2(528, 76));
            var exit = UF.MenuButton(mainRt, "Exit", "Выход", new Vector2(528, 76));
            var buttons = new[] { play, settings, exit };
            for (int i = 0; i < buttons.Length; i++)
                UF.Place((RectTransform)buttons[i].transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 88f), new Vector2(528, 76));
            LinkVertical(buttons);
            SetArray(mainScreen, "staggered", new Object[] { buttons[0].transform, buttons[1].transform, buttons[2].transform });
            Set(mainScreen, "firstSelected", play);

            // --- Экран: настройки
            var settingsRt = ScreenArea(panel, "SettingsScreen", 250, 90);
            var settingsScreen = settingsRt.gameObject.AddComponent<MenuScreen>();
            var settingsPanel = BuildSettingsPanel(settingsRt, 528, out var settingsRows, out var firstSetting);
            SetArray(settingsScreen, "staggered", settingsRows);
            Set(settingsScreen, "firstSelected", firstSetting);

            // Подвал
            var footer = UF.Rect("Footer", panel);
            UF.Place(footer, new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 40), new Vector2(560, 40));
            var footerGroup = footer.gameObject.AddComponent<CanvasGroup>();
            UF.Text(UF.Stretch(UF.Rect("Text", footer)), "v0.1  ·  Enter / A — выбрать   ·   Esc / B — назад", 19, false,
                 TextAlignmentOptions.MidlineLeft, UF.TextDim);

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            Set(controller, "mainScreen", mainScreen);
            Set(controller, "settingsScreen", settingsScreen);
            Set(controller, "settingsPanel", settingsPanel);
            Set(controller, "playButton", play);
            Set(controller, "settingsButton", settings);
            Set(controller, "exitButton", exit);
            Set(controller, "title", titleGroup);
            Set(controller, "footer", footerGroup);
            Set(controller, "backAction", UF.Ref(Actions, "UI/Back"));
        }

        /// <summary>Область для экрана внутри колонны меню (под заголовком).</summary>
        static RectTransform ScreenArea(RectTransform panel, string name, float top, float bottom)
        {
            var rt = UF.Rect(name, panel);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(56, bottom);
            rt.offsetMax = new Vector2(-56, -top);
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        static void LinkVertical(IList<Selectable> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i - 1 + items.Count) % items.Count],
                    selectOnDown = items[(i + 1) % items.Count],
                };
                items[i].navigation = nav;
            }
        }

        static void LinkVertical(Button[] items)
        {
            var list = new List<Selectable>();
            foreach (var b in items) list.Add(b);
            LinkVertical(list);
        }

        /// <summary>Панель настроек: заголовок, строки и кнопка «Назад».</summary>
        static SettingsPanel BuildSettingsPanel(RectTransform parent, float width, out Object[] rows, out Selectable first)
        {
            var header = UF.Text(UF.Place(UF.Rect("Header", parent), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 60)),
                              "Настройки", 44, true);

            var rowVolume = UF.Row(parent, "Volume", "Громкость", width);
            var volume = UF.Slider(rowVolume, out var volumeValue);
            var rowSens = UF.Row(parent, "Sensitivity", "Чувствительность камеры", width);
            var sens = UF.Slider(rowSens, out var sensValue);
            var rowQuality = UF.Row(parent, "Quality", "Качество графики", width);
            var quality = UF.Selector(rowQuality);
            var rowFull = UF.Row(parent, "Fullscreen", "Полноэкранный режим", width);
            var full = UF.Toggle(rowFull);
            var rowBlur = UF.Row(parent, "MotionBlur", "Размытие в движении", width);
            var blur = UF.Toggle(rowBlur);
            var rowInvert = UF.Row(parent, "InvertY", "Инвертировать ось Y", width);
            var invert = UF.Toggle(rowInvert);
            var back = UF.MenuButton(parent, "Back", "‹  Назад", new Vector2(width, 68));

            var ordered = new RectTransform[] { header.rectTransform, rowVolume, rowSens, rowQuality, rowFull, rowBlur, rowInvert, (RectTransform)back.transform };
            for (int i = 1; i < ordered.Length; i++)
                UF.Place(ordered[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -76 - (i - 1) * 64f - (i == ordered.Length - 1 ? 14f : 0f)),
                      ordered[i].sizeDelta);

            // Навигация геймпадом сверху вниз (полноэкранный режим пропускается на телефоне автоматически — он скрыт)
            var selectables = new List<Selectable> { volume, sens, quality, full, blur, invert, back };
            for (int i = 0; i < selectables.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = selectables[(i - 1 + selectables.Count) % selectables.Count];
                nav.selectOnDown = selectables[(i + 1) % selectables.Count];
                selectables[i].navigation = nav;
            }

            var panel = parent.gameObject.AddComponent<SettingsPanel>();
            Set(panel, "volume", volume);
            Set(panel, "volumeValue", volumeValue);
            Set(panel, "sensitivity", sens);
            Set(panel, "sensitivityValue", sensValue);
            Set(panel, "quality", quality);
            Set(panel, "fullscreen", full);
            Set(panel, "fullscreenRow", rowFull.gameObject);
            Set(panel, "motionBlur", blur);
            Set(panel, "invertY", invert);
            Set(panel, "back", back);
            Set(panel, "firstSelected", volume);

            rows = new Object[ordered.Length];
            for (int i = 0; i < ordered.Length; i++) rows[i] = ordered[i];
            first = volume;
            return panel;
        }

        // ---------------------------------------------------------------- игра

        static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ContentBuilder.EnvironmentPrefab));

            var volume = CreateGlobalVolume(ContentBuilder.ProfilePc);
            var cam = CreateMainCamera(volume, ContentBuilder.ProfilePc, ContentBuilder.ProfileMobile, true);
            var brain = cam.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);

            // Игрок
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ContentBuilder.PlayerPrefab));
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.2f, -2f), Quaternion.identity);
            var movement = player.GetComponent<PlayerMovement>();
            Set(movement, "cameraTransform", cam.transform);
            var input = player.GetComponent<PlayerInputHandler>();

            // Точка слежения камеры
            var rootGo = new GameObject("CameraRoot");
            var camController = rootGo.AddComponent<CameraController>();
            Set(camController, "target", player.transform);
            Set(camController, "input", input);

            // Камера от третьего лица: сзади и чуть сверху, обходит препятствия
            var vcamGo = new GameObject("CM Player Camera");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = rootGo.transform;
            var lens = vcam.Lens;
            lens.FieldOfView = 55f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 400f;
            vcam.Lens = lens;
            var tpf = vcamGo.AddComponent<CinemachineThirdPersonFollow>();
            tpf.ShoulderOffset = new Vector3(0.45f, 0.1f, 0f);
            tpf.VerticalArmLength = 0.2f;
            tpf.CameraSide = 1f;
            tpf.CameraDistance = 3.8f;
            tpf.Damping = new Vector3(0.1f, 0.25f, 0.3f);
            int playerLayer = LayerMask.NameToLayer(ProjectSetup.PlayerLayerName);
            var avoid = tpf.AvoidObstacles;
            avoid.Enabled = true;
            avoid.CollisionFilter = playerLayer >= 0 ? ~(1 << playerLayer) & ~(1 << LayerMask.NameToLayer("UI")) : ~0;
            avoid.CameraRadius = 0.22f;
            avoid.DampingIntoCollision = 0.05f;
            avoid.DampingFromCollision = 0.4f;
            tpf.AvoidObstacles = avoid;

            UIFactory.EventSystem(Actions);
            var mobile = BuildMobileControls(input);
            BuildPauseUI(input, mobile);

            EditorSceneManager.SaveScene(scene, GameScene);
        }

        static GameObject BuildMobileControls(PlayerInputHandler input)
        {
            var canvas = UF.Canvas("MobileCanvas", 20);
            var root = UF.Stretch(UF.Rect("MobileControls", canvas.transform));
            var ui = root.gameObject.AddComponent<MobileControlsUI>();
            Set(ui, "playerInput", input);

            // Обзор свайпом — правая часть экрана (под кнопками)
            var look = UF.Rect("LookArea", root);
            look.anchorMin = new Vector2(0.4f, 0f);
            look.anchorMax = new Vector2(1f, 1f);
            look.offsetMin = look.offsetMax = Vector2.zero;
            UF.Image(look, null, new Color(0, 0, 0, 0), true);
            Set(look.gameObject.AddComponent<TouchLookArea>(), "playerInput", input);

            // Джойстик
            var stickBase = UF.Image(UF.Place(UF.Rect("StickBase", root), Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(240, 240), new Vector2(280, 280)),
                                  ContentBuilder.RingSprite, new Color(1, 1, 1, 0.35f));
            UF.Image(UF.Place(UF.Rect("Fill", stickBase.rectTransform), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270, 270)),
                  ContentBuilder.CircleSprite, new Color(0, 0, 0, 0.18f));
            var knob = UF.Image(UF.Place(UF.Rect("Knob", stickBase.rectTransform), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120)),
                             ContentBuilder.CircleSprite, new Color(1, 1, 1, 0.75f), true);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            var sso = new SerializedObject(stick);
            sso.FindProperty("m_ControlPath").stringValue = "<Gamepad>/leftStick";
            sso.FindProperty("m_MovementRange").floatValue = 90f;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // Кнопки действий
            RoundButton(root, "Jump", "Прыжок", new Vector2(-190, 200), 170, "<Gamepad>/buttonSouth");
            RoundButton(root, "Sprint", "Спринт", new Vector2(-400, 140), 124, "<Gamepad>/leftStickPress");
            RoundButton(root, "Crouch", "Присед", new Vector2(-190, 410), 124, "<Gamepad>/buttonEast");

            // Пауза
            var pauseRt = UF.Place(UF.Rect("Pause", root), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-80, -80), new Vector2(92, 92));
            var pauseImg = UF.Image(pauseRt, ContentBuilder.CircleSprite, new Color(0, 0, 0, 0.35f), true);
            UF.Text(UF.Stretch(UF.Rect("Label", pauseRt)), "II", 36, true, TextAlignmentOptions.Center);
            var pauseButton = pauseRt.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseImg;
            Set(ui, "pauseButton", pauseButton);

            return root.gameObject;
        }

        static void RoundButton(RectTransform root, string name, string label, Vector2 pos, float size, string controlPath)
        {
            var rt = UF.Place(UF.Rect(name, root), new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            UF.Image(rt, ContentBuilder.CircleSprite, new Color(0, 0, 0, 0.32f), true);
            UF.Image(UF.Stretch(UF.Rect("Ring", rt)), ContentBuilder.RingSprite, new Color(1, 1, 1, 0.55f));
            UF.Text(UF.Stretch(UF.Rect("Label", rt)), label, size > 150 ? 28 : 22, true, TextAlignmentOptions.Center);
            var b = rt.gameObject.AddComponent<OnScreenButton>();
            var so = new SerializedObject(b);
            so.FindProperty("m_ControlPath").stringValue = controlPath;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPauseUI(PlayerInputHandler input, GameObject mobileControls)
        {
            var canvas = UF.Canvas("PauseCanvas", 50);
            var root = (RectTransform)canvas.transform;

            // Размытый затемнённый фон на весь экран
            var backdrop = UF.Stretch(UF.Rect("Backdrop", root));
            var bimg = UF.Image(backdrop, null, new Color(0.03f, 0.04f, 0.06f, 0.35f), true);
            bimg.material = ContentBuilder.BlurPanelMaterial;
            backdrop.gameObject.AddComponent<UIBlurPanel>();
            var backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();

            // Экран паузы — компактная стеклянная карточка по центру
            var pauseRt = UF.Place(UF.Rect("PauseScreen", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 520));
            pauseRt.gameObject.AddComponent<CanvasGroup>();
            UF.Glass(pauseRt, new Color(0.06f, 0.08f, 0.11f, 0.3f));
            var pauseScreen = pauseRt.gameObject.AddComponent<MenuScreen>();
            var title = UF.Text(UF.Place(UF.Rect("Title", pauseRt), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(520, 80)),
                             "Пауза", 56, true, TextAlignmentOptions.Center);
            var resume = UF.MenuButton(pauseRt, "Resume", "Продолжить", new Vector2(520, 76));
            var settings = UF.MenuButton(pauseRt, "Settings", "Настройки", new Vector2(520, 76));
            var toMenu = UF.MenuButton(pauseRt, "MainMenu", "В главное меню", new Vector2(520, 76));
            var buttons = new[] { resume, settings, toMenu };
            for (int i = 0; i < buttons.Length; i++)
                UF.Place((RectTransform)buttons[i].transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170 - i * 88f), new Vector2(520, 76));
            LinkVertical(buttons);
            SetArray(pauseScreen, "staggered", new Object[] { title.rectTransform, buttons[0].transform, buttons[1].transform, buttons[2].transform });
            Set(pauseScreen, "firstSelected", resume);
            var so = new SerializedObject(pauseScreen);
            so.FindProperty("slideOffset").vector2Value = new Vector2(0, -30);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Экран настроек в паузе
            var settingsRt = UF.Place(UF.Rect("SettingsScreen", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 760));
            settingsRt.gameObject.AddComponent<CanvasGroup>();
            UF.Glass(settingsRt, new Color(0.06f, 0.08f, 0.11f, 0.3f));
            var content = UF.Rect("Content", settingsRt);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(64, 40);
            content.offsetMax = new Vector2(-64, -48);
            var settingsScreen = settingsRt.gameObject.AddComponent<MenuScreen>();
            var settingsPanel = BuildSettingsPanel(content, 552, out var rows, out var first);
            SetArray(settingsScreen, "staggered", rows);
            Set(settingsScreen, "firstSelected", first);

            var controller = canvas.gameObject.AddComponent<PauseMenuController>();
            Set(controller, "playerInput", input);
            Set(controller, "backdrop", backdropGroup);
            Set(controller, "pauseScreen", pauseScreen);
            Set(controller, "settingsScreen", settingsScreen);
            Set(controller, "settingsPanel", settingsPanel);
            Set(controller, "mobileControls", mobileControls);
            Set(controller, "resumeButton", resume);
            Set(controller, "settingsButton", settings);
            Set(controller, "mainMenuButton", toMenu);
            Set(controller, "backAction", UF.Ref(Actions, "UI/Back"));
        }
    }
}
