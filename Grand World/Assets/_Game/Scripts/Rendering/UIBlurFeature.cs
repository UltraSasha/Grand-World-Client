using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GrandWorld.Rendering
{
    /// <summary>
    /// Размытие кадра для полупрозрачных панелей интерфейса («матовое стекло»).
    /// После постобработки кадр уменьшается и размывается фильтром Kawase,
    /// результат публикуется как глобальная текстура _UIBlurTexture, которую читает шейдер панели.
    /// Работает, только когда на экране есть хотя бы одна панель с размытием (<see cref="UIBlurPanel"/>).
    /// </summary>
    public class UIBlurFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Шейдер размытия (Hidden/GrandWorld/KawaseBlur)")]
            public Shader shader;
            [Tooltip("Во сколько раз уменьшать кадр перед размытием")]
            [Range(1, 8)] public int downsample = 4;
            [Tooltip("Количество проходов размытия")]
            [Range(1, 8)] public int iterations = 4;
            [Tooltip("Шаг выборки, в текселях уменьшенного кадра")]
            [Range(0.5f, 3f)] public float spread = 1f;
        }

        public Settings settings = new Settings();

        Material _material;
        BlurPass _pass;

        /// <summary>Сколько панелей с размытием сейчас на экране.</summary>
        public static int ActivePanels { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ActivePanels = 0;

        public override void Create()
        {
            _pass = new BlurPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (ActivePanels <= 0) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (_material == null)
            {
                if (settings.shader == null) settings.shader = Shader.Find("Hidden/GrandWorld/KawaseBlur");
                if (settings.shader == null || !settings.shader.isSupported) return;
                _material = CoreUtils.CreateEngineMaterial(settings.shader);
            }
            _pass.Setup(_material, settings);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        class BlurPass : ScriptableRenderPass
        {
            static readonly int GlobalTextureId = Shader.PropertyToID("_UIBlurTexture");
            static readonly int BlurParamsId = Shader.PropertyToID("_BlurParams");

            Material _material;
            Settings _settings;

            class PassData
            {
                public TextureHandle source;
                public Material material;
                public Vector4 parameters; // xy — размер текселя источника, z — шаг
            }

            public void Setup(Material material, Settings settings)
            {
                _material = material;
                _settings = settings;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                var sourceDesc = renderGraph.GetTextureDesc(source);

                var desc = sourceDesc;
                desc.width = Mathf.Max(1, sourceDesc.width / _settings.downsample);
                desc.height = Mathf.Max(1, sourceDesc.height / _settings.downsample);
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.clearBuffer = false;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.useMipMap = false;
                desc.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                if (!SystemInfo.IsFormatSupported(desc.colorFormat, GraphicsFormatUsage.Render))
                    desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;

                desc.name = "_UIBlurA";
                TextureHandle a = renderGraph.CreateTexture(desc);
                desc.name = "_UIBlurB";
                TextureHandle b = renderGraph.CreateTexture(desc);

                // Первый проход: уменьшение исходного кадра
                var srcTexel = new Vector2(1f / sourceDesc.width, 1f / sourceDesc.height);
                AddBlurPass(renderGraph, source, a, new Vector4(srcTexel.x, srcTexel.y, 0.5f, 0f), _settings.iterations == 0);

                // Последующие проходы Kawase с растущим шагом
                var texel = new Vector2(1f / desc.width, 1f / desc.height);
                TextureHandle current = a, other = b;
                for (int i = 0; i < _settings.iterations; i++)
                {
                    float step = (i + 0.5f) * _settings.spread;
                    bool last = i == _settings.iterations - 1;
                    AddBlurPass(renderGraph, current, other, new Vector4(texel.x, texel.y, step, 0f), last);
                    (current, other) = (other, current);
                }
            }

            void AddBlurPass(RenderGraph renderGraph, TextureHandle src, TextureHandle dst, Vector4 parameters, bool publish)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("UI Blur", out var data))
                {
                    data.source = src;
                    data.material = _material;
                    data.parameters = parameters;
                    builder.UseTexture(src, AccessFlags.Read);
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                    if (publish) builder.SetGlobalTextureAfterPass(dst, GlobalTextureId);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true); // параметры размытия передаются глобальной переменной
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        // Параметры через глобальную переменную буфера команд — порядок проходов сохраняется
                        ctx.cmd.SetGlobalVector(BlurParamsId, d.parameters);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0);
                    });
                }
            }
        }
    }
}
