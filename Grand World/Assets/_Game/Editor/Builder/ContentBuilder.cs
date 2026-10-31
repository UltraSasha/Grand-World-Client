using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using GrandWorld.Player;
using GrandWorld.Rendering;
using static GrandWorld.EditorTools.BuilderPaths;

namespace GrandWorld.EditorTools
{
    /// <summary>
    /// Генерация ассетов: спрайты UI, материалы, шрифты TMP, Animator Controller,
    /// профили постобработки, террейн, окружение и префаб игрока.
    /// </summary>
    public static class ContentBuilder
    {
        // ---------------------------------------------------------------- спрайты и текстуры

        public static Sprite RoundedSprite => AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/UI_Rounded.png");
        public static Sprite PillSprite => AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/UI_Pill.png");
        public static Sprite CircleSprite => AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/UI_Circle.png");
        public static Sprite RingSprite => AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/UI_Ring.png");
        public static Sprite GlowSprite => AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/UI_Glow.png");
        public static Sprite GradientSprite => AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + "/UI_GradientLeft.png");

        public static void BuildTextures()
        {
            // Скруглённый прямоугольник 128x128, радиус 28 — фон панелей и кнопок (9-slice)
            SavePng(RoundedRect(128, 128, 28f), TexturesDir + "/UI_Rounded.png", true, new Vector4(32, 32, 32, 32));
            // «Пилюля» — дорожки ползунков и переключатели
            SavePng(RoundedRect(64, 32, 16f), TexturesDir + "/UI_Pill.png", true, new Vector4(16, 15, 16, 15));
            SavePng(Circle(128, 0f), TexturesDir + "/UI_Circle.png", true);
            SavePng(Circle(256, 0.86f), TexturesDir + "/UI_Ring.png", true);
            SavePng(Glow(128), TexturesDir + "/UI_Glow.png", true, new Vector4(48, 48, 48, 48));
            SavePng(HorizontalGradient(256, 8), TexturesDir + "/UI_GradientLeft.png", true);
            SavePng(GroundTexture(512), TexturesDir + "/Ground_Albedo.png", false, default, false, TextureWrapMode.Repeat);
            SavePng(RockTexture(256), TexturesDir + "/Rock_Albedo.png", false, default, false, TextureWrapMode.Repeat);
        }

        static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        static Texture2D RoundedRect(int w, int h, float r)
        {
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = Mathf.Max(r - px, 0f, px - (w - r)), dy = Mathf.Max(r - py, 0f, py - (h - r));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
            }
            t.Apply();
            return t;
        }

        static Texture2D Circle(int size, float inner)
        {
            var t = NewTex(size, size);
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (inner > 0f) a *= Mathf.Clamp01(d - r * inner + 0.5f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            return t;
        }

        static Texture2D Glow(int size)
        {
            var t = NewTex(size, size);
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - c) - c * 0.25f) / (c * 0.75f);
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - c) - c * 0.25f) / (c * 0.75f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 2.2f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            return t;
        }

        static Texture2D HorizontalGradient(int w, int h)
        {
            var t = NewTex(w, h);
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Pow(1f - x / (float)(w - 1), 1.5f);
                for (int y = 0; y < h; y++) t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            return t;
        }

        static float Fbm(float x, float y, int octaves)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 11.3f * i);
                f *= 2.03f; a *= 0.5f;
            }
            return v;
        }

        /// <summary>Тайлящаяся текстура земли: трава с пятнами сухой травы и почвы.</summary>
        static Texture2D GroundTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var grassA = new Color(0.16f, 0.27f, 0.07f);
            var grassB = new Color(0.27f, 0.36f, 0.11f);
            var soil = new Color(0.28f, 0.22f, 0.14f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Шум в торе — текстура бесшовная
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float nx = Mathf.Cos(u) * 1.6f + 10f, ny = Mathf.Sin(u) * 1.6f + 10f;
                float nz = Mathf.Cos(v) * 1.6f + 20f, nw = Mathf.Sin(v) * 1.6f + 20f;
                float large = Fbm(nx + nz * 0.5f, ny + nw * 0.5f, 4);
                float fine = Fbm((nx + nw) * 6f, (ny + nz) * 6f, 2);
                Color c = Color.Lerp(grassA, grassB, Mathf.SmoothStep(0.3f, 0.7f, large));
                c = Color.Lerp(c, soil, Mathf.SmoothStep(0.62f, 0.75f, large) * 0.6f);
                c *= Mathf.Lerp(0.85f, 1.12f, fine);
                c.a = 1f;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static Texture2D RockTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float n = Fbm(Mathf.Cos(u) * 3f + Mathf.Sin(v), Mathf.Sin(u) * 3f + Mathf.Cos(v) * 2f, 5);
                float g = Mathf.Lerp(0.32f, 0.58f, n);
                t.SetPixel(x, y, new Color(g, g * 0.97f, g * 0.92f, 1f));
            }
            t.Apply();
            return t;
        }

        // ---------------------------------------------------------------- шрифты

        public static TMP_FontAsset FontRegular => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsDir + "/NotoSans-Regular SDF.asset");
        public static TMP_FontAsset FontBold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontsDir + "/NotoSans-Bold SDF.asset");

        public static void BuildFonts()
        {
            // Ресурсы TextMeshPro (шейдеры, настройки) — один раз
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                string pkg = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
                if (System.IO.File.Exists(System.IO.Path.GetFullPath(pkg))) AssetDatabase.ImportPackage(pkg, false);
                else Debug.LogWarning("[Grand World] Не найден пакет TMP Essential Resources: " + pkg);
            }
            CreateFont(FontRegularTtf, FontsDir + "/NotoSans-Regular SDF.asset");
            CreateFont(FontBoldTtf, FontsDir + "/NotoSans-Bold SDF.asset");
        }

        static void CreateFont(string ttfPath, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogError("[Grand World] Не найден шрифт " + ttfPath); return; }
            // Динамический атлас: кириллица и любые символы добавляются по мере использования
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            EnsureFolder(FontsDir);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            // Заранее добавляем частые символы, чтобы не было рывков при первом показе
            fa.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
                                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?%×·‹›—-+()«»/");
            EditorUtility.SetDirty(fa);
        }

        // ---------------------------------------------------------------- материалы

        public static Material GrassMaterial => AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Grass.mat");
        public static Material BlurPanelMaterial => AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/UI_BlurPanel.mat");
        public static Material RockMaterial => AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Rock.mat");

        public static void BuildMaterials()
        {
            var grass = new Material(Shader.Find("GrandWorld/Grass")) { name = "Grass", enableInstancing = true };
            CreateOrReplace(grass, MaterialsDir + "/Grass.mat");

            var blur = new Material(Shader.Find("GrandWorld/UI/BlurPanel")) { name = "UI_BlurPanel" };
            CreateOrReplace(blur, MaterialsDir + "/UI_BlurPanel.mat");

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var rock = new Material(lit) { name = "Rock" };
            rock.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/Rock_Albedo.png"));
            rock.SetColor("_BaseColor", new Color(0.82f, 0.82f, 0.8f));
            rock.SetFloat("_Smoothness", 0.15f);
            CreateOrReplace(rock, MaterialsDir + "/Rock.mat");
        }

        // ---------------------------------------------------------------- аниматор

        public const string ControllerPath = AnimationDir + "/Player.controller";

        public static AnimatorController BuildAnimator()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(PlayerFbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
            AnimationClip Clip(string n)
            {
                if (!clips.TryGetValue(n, out var c)) Debug.LogError("[Grand World] В Player.fbx нет клипа " + n);
                return c;
            }

            EnsureFolder(AnimationDir);
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var ps = ctrl.parameters;
            ps.First(p => p.name == "Grounded").defaultBool = true;
            ctrl.parameters = ps;

            // IK-проход нужен для подстройки стоп
            var layers = ctrl.layers;
            layers[0].iKPass = true;
            ctrl.layers = layers;
            var sm = ctrl.layers[0].stateMachine;

            // Пороги Blend Tree = естественные скорости клипов (см. PlayerMovement)
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree locoTree, 0);
            locoTree.blendType = BlendTreeType.Simple1D;
            locoTree.blendParameter = "Speed";
            locoTree.useAutomaticThresholds = false;
            locoTree.AddChild(Clip("Idle"), 0f);
            locoTree.AddChild(Clip("Walk"), 1.46f);
            locoTree.AddChild(Clip("Run"), 4.07f);
            locoTree.AddChild(Clip("Sprint"), 6.63f);

            var crouch = ctrl.CreateBlendTreeInController("Crouch", out BlendTree crouchTree, 0);
            crouchTree.blendType = BlendTreeType.Simple1D;
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;
            crouchTree.AddChild(Clip("CrouchIdle"), 0f);
            crouchTree.AddChild(Clip("CrouchWalk"), 1.22f);

            var jumpStart = sm.AddState("JumpStart");
            jumpStart.motion = Clip("JumpStart");
            var jumpAir = sm.AddState("JumpAir");
            jumpAir.motion = Clip("JumpAir");
            var land = sm.AddState("Land");
            land.motion = Clip("Land");
            sm.defaultState = loco;

            // Раскладка в окне Animator
            PlaceState(sm, loco, new Vector3(300, 0));
            PlaceState(sm, crouch, new Vector3(300, 140));
            PlaceState(sm, jumpStart, new Vector3(600, -120));
            PlaceState(sm, jumpAir, new Vector3(860, 0));
            PlaceState(sm, land, new Vector3(600, 120));

            AnimatorStateTransition T(AnimatorState from, AnimatorState to, float duration, bool exitTime = false, float exit = 0f)
            {
                var t = from.AddTransition(to);
                t.duration = duration;
                t.hasExitTime = exitTime;
                t.exitTime = exit;
                t.hasFixedDuration = true;
                return t;
            }

            T(loco, crouch, 0.18f).AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            T(crouch, loco, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");

            foreach (var ground in new[] { loco, crouch })
            {
                var j = T(ground, jumpStart, 0.05f);
                j.offset = 0.45f; // пропускаем замах — толчок совпадает с моментом прыжка
                j.AddCondition(AnimatorConditionMode.If, 0, "Jump");
                // Сход с края без прыжка
                var fall = T(ground, jumpAir, 0.25f);
                fall.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
                fall.AddCondition(AnimatorConditionMode.Less, -3f, "VerticalSpeed");
            }

            T(jumpStart, jumpAir, 0.12f, true, 0.9f);
            var quickLand = T(jumpStart, land, 0.08f);
            quickLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            quickLand.AddCondition(AnimatorConditionMode.Less, -0.5f, "VerticalSpeed");

            var toLand = T(jumpAir, land, 0.06f);
            toLand.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            toLand.AddCondition(AnimatorConditionMode.Less, 2.5f, "Speed");
            var toRun = T(jumpAir, loco, 0.15f);
            toRun.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            toRun.AddCondition(AnimatorConditionMode.Greater, 2.5f, "Speed");

            T(land, loco, 0.2f, true, 0.75f);
            var landMove = T(land, loco, 0.15f);
            landMove.AddCondition(AnimatorConditionMode.Greater, 1.0f, "Speed");
            var landCrouch = T(land, crouch, 0.15f);
            landCrouch.AddCondition(AnimatorConditionMode.If, 0, "Crouch");

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void PlaceState(AnimatorStateMachine sm, AnimatorState state, Vector3 pos)
        {
            var states = sm.states;
            for (int i = 0; i < states.Length; i++)
                if (states[i].state == state) states[i].position = pos;
            sm.states = states;
        }

        // ---------------------------------------------------------------- постобработка

        public static VolumeProfile ProfilePc => AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingDir + "/PostFX_PC.asset");
        public static VolumeProfile ProfileMobile => AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingDir + "/PostFX_Mobile.asset");
        public static VolumeProfile ProfileMenuPc => AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingDir + "/PostFX_Menu_PC.asset");
        public static VolumeProfile ProfileMenuMobile => AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingDir + "/PostFX_Menu_Mobile.asset");

        public static void BuildVolumeProfiles()
        {
            SaveProfile(RenderingDir + "/PostFX_PC.asset", false, false);
            SaveProfile(RenderingDir + "/PostFX_Mobile.asset", true, false);
            SaveProfile(RenderingDir + "/PostFX_Menu_PC.asset", false, true);
            SaveProfile(RenderingDir + "/PostFX_Menu_Mobile.asset", true, true);
        }

        static void SaveProfile(string path, bool mobile, bool menu)
        {
            EnsureFolder(RenderingDir);
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            // Мягкая кинематографичная цветокоррекция: тёплые света, чуть холодные тени
            var color = p.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(14f);
            color.saturation.Override(10f);
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            wb.tint.Override(2f);
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.96f, 0.99f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.95f, 0f));

            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(mobile ? 0.45f : 0.65f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(!mobile);

            var vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(menu ? 0.32f : 0.22f);
            vignette.smoothness.Override(0.45f);

            var blur = p.Add<MotionBlur>(true);
            blur.intensity.Override(mobile ? 0.08f : 0.12f);
            blur.clamp.Override(0.03f);
            blur.quality.Override(mobile ? MotionBlurQuality.Low : MotionBlurQuality.Medium);
            blur.active = !menu;

            if (menu)
            {
                var dof = p.Add<DepthOfField>(true);
                if (mobile)
                {
                    dof.mode.Override(DepthOfFieldMode.Gaussian);
                    dof.gaussianStart.Override(6f);
                    dof.gaussianEnd.Override(22f);
                    dof.gaussianMaxRadius.Override(1.2f);
                }
                else
                {
                    dof.mode.Override(DepthOfFieldMode.Bokeh);
                    dof.focusDistance.Override(4.3f);
                    dof.aperture.Override(2.2f);
                    dof.focalLength.Override(55f);
                }
            }

            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
        }

        // ---------------------------------------------------------------- террейн и окружение

        public const string EnvironmentPrefab = PrefabsDir + "/Environment.prefab";
        public const float AreaSize = 50f;

        public static GameObject BuildEnvironmentPrefab()
        {
            EnsureFolder(EnvironmentDir);
            EnsureFolder(PrefabsDir);

            // --- Террейн 70x70 м: ровный центр 50x50 и мягкие холмы по краям
            const float size = 70f;
            const int res = 129;
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(size, 8f, size);
            var heights = new float[res, res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = (x / (float)(res - 1) - 0.5f) * size, wz = (y / (float)(res - 1) - 0.5f) * size;
                float edge = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AreaSize * 0.5f - 2f, size * 0.5f, edge));
                float gentle = (Fbm(wx * 0.045f + 5f, wz * 0.045f + 9f, 3) - 0.5f) * 0.6f;   // ±0.3 м неровности
                float hills = Fbm(wx * 0.06f + 1f, wz * 0.06f + 2f, 3) * 4.5f * rim;
                heights[y, x] = Mathf.Clamp01((1.0f + gentle + hills) / td.size.y);
            }
            td.SetHeights(0, 0, heights);

            var layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/Ground_Albedo.png"),
                tileSize = new Vector2(6f, 6f),
                smoothness = 0.05f,
                metallic = 0f,
            };
            layer = CreateOrReplace(layer, EnvironmentDir + "/Ground.terrainlayer");
            td.terrainLayers = new[] { layer };
            td = CreateOrReplace(td, EnvironmentDir + "/Terrain.asset");

            var root = new GameObject("Environment");
            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "Terrain";
            terrainGo.transform.SetParent(root.transform, false);
            terrainGo.transform.position = new Vector3(-size * 0.5f, -1f, -size * 0.5f);
            var terrain = terrainGo.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 80f;
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp != null && rp.defaultTerrainMaterial != null) terrain.materialTemplate = rp.defaultTerrainMaterial;

            // --- Трава
            var grassGo = new GameObject("Grass");
            grassGo.transform.SetParent(root.transform, false);
            var grass = grassGo.AddComponent<GrassRenderer>();
            var gso = new SerializedObject(grass);
            gso.FindProperty("terrain").objectReferenceValue = terrain;
            gso.FindProperty("material").objectReferenceValue = GrassMaterial;
            gso.FindProperty("areaSize").vector2Value = new Vector2(64f, 64f);
            gso.ApplyModifiedPropertiesWithoutUndo();

            // --- Невидимые стены по краям участка 50x50
            var walls = new GameObject("Bounds");
            walls.transform.SetParent(root.transform, false);
            float half = AreaSize * 0.5f;
            AddWall(walls.transform, "North", new Vector3(0, 5, half + 0.5f), new Vector3(AreaSize + 2, 14, 1));
            AddWall(walls.transform, "South", new Vector3(0, 5, -half - 0.5f), new Vector3(AreaSize + 2, 14, 1));
            AddWall(walls.transform, "East", new Vector3(half + 0.5f, 5, 0), new Vector3(1, 14, AreaSize + 2));
            AddWall(walls.transform, "West", new Vector3(-half - 0.5f, 5, 0), new Vector3(1, 14, AreaSize + 2));

            // --- Несколько валунов — ориентиры и проверка того, что камера не проходит сквозь объекты
            var rocks = new GameObject("Rocks");
            rocks.transform.SetParent(root.transform, false);
            var rng = new System.Random(7);
            Vector3[] spots =
            {
                new Vector3(7, 0, 9), new Vector3(-11, 0, 5), new Vector3(-6, 0, -13), new Vector3(14, 0, -8),
                new Vector3(-18, 0, 16), new Vector3(19, 0, 17), new Vector3(2, 0, -21),
            };
            foreach (var s in spots)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "Rock";
                rock.transform.SetParent(rocks.transform, false);
                float scale = 0.8f + (float)rng.NextDouble() * 1.8f;
                rock.transform.localScale = new Vector3(scale * 1.4f, scale * 0.8f, scale);
                rock.transform.rotation = Quaternion.Euler(rng.Next(-12, 12), rng.Next(0, 360), rng.Next(-12, 12));
                float y = terrain.SampleHeight(s) + terrainGo.transform.position.y;
                rock.transform.position = new Vector3(s.x, y + scale * 0.15f, s.z);
                rock.GetComponent<Renderer>().sharedMaterial = RockMaterial;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void AddWall(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Wall_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ---------------------------------------------------------------- игрок

        public const string PlayerPrefab = PrefabsDir + "/Player.prefab";

        public static GameObject BuildPlayerPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbx);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);
            int playerLayer = LayerMask.NameToLayer(ProjectSetup.PlayerLayerName);

            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;

            var input = root.AddComponent<PlayerInputHandler>();
            var so = new SerializedObject(input);
            so.FindProperty("actions").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();

            var movement = root.AddComponent<PlayerMovement>();
            var anim = root.AddComponent<PlayerAnimation>();

            var modelGo = (GameObject)PrefabUtility.InstantiatePrefab(model);
            modelGo.name = "Model";
            modelGo.transform.SetParent(root.transform, false);
            // Модель из Blender после импорта смотрит в -Z — разворачиваем лицом вперёд (+Z)
            modelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var animator = modelGo.GetComponent<Animator>();
            if (animator == null) animator = modelGo.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            var aso = new SerializedObject(anim);
            aso.FindProperty("animator").objectReferenceValue = animator;
            int groundMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;
            aso.FindProperty("groundMask").intValue = groundMask;
            aso.ApplyModifiedPropertiesWithoutUndo();
            var mso = new SerializedObject(movement);
            mso.FindProperty("groundMask").intValue = groundMask;
            mso.ApplyModifiedPropertiesWithoutUndo();

            if (playerLayer >= 0)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = playerLayer;

            EnsureFolder(PrefabsDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
