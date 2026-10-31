using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using GrandWorld.Core;

namespace GrandWorld.Rendering
{
    /// <summary>
    /// Густая трава на GPU: травинки раскладываются по участку Terrain, делятся на чанки,
    /// каждый чанк рисуется одним вызовом Graphics.RenderMeshPrimitives.
    /// Невидимые и далёкие чанки отсекаются, плотность и дальность зависят от качества графики.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10000)] // после Cinemachine — отсечение по актуальному положению камеры
    public class GrassRenderer : MonoBehaviour
    {
        [Header("Участок")]
        [Tooltip("Terrain, по высоте которого растёт трава")]
        [SerializeField] Terrain terrain;
        [Tooltip("Центр участка травы в мире")]
        [SerializeField] Vector3 areaCenter = Vector3.zero;
        [Tooltip("Размер участка травы, м")]
        [SerializeField] Vector2 areaSize = new Vector2(56f, 56f);
        [Tooltip("Размер чанка, м")]
        [SerializeField] float chunkSize = 8f;

        [Header("Травинки")]
        [SerializeField] Material material;
        [Tooltip("Травинок на квадратный метр при высоком качестве")]
        [SerializeField] float density = 60f;
        [Tooltip("Высота травинки, м (мин/макс)")]
        [SerializeField] Vector2 heightRange = new Vector2(0.22f, 0.5f);
        [Tooltip("Ширина травинки у корня, м (мин/макс)")]
        [SerializeField] Vector2 widthRange = new Vector2(0.025f, 0.05f);
        [Tooltip("Максимальный крутизна склона, на котором растёт трава, градусы")]
        [SerializeField] float maxSlope = 40f;
        [Tooltip("Зерно генератора случайных чисел")]
        [SerializeField] int seed = 12345;

        [Header("Дальность по уровню качества (низкое/среднее/высокое), м")]
        [SerializeField] Vector3 drawDistance = new Vector3(22f, 35f, 50f);
        [Tooltip("Доля плотности по уровню качества (низкое/среднее/высокое)")]
        [SerializeField] Vector3 densityScale = new Vector3(0.3f, 0.6f, 1f);
        [Tooltip("Доля травинок в дальних чанках (LOD)")]
        [Range(0.05f, 1f)] [SerializeField] float farChunkFraction = 0.35f;

        [StructLayout(LayoutKind.Sequential)]
        struct Blade
        {
            public Vector3 position;
            public float yaw, height, width, hue, bend;
        }

        class Chunk
        {
            public Bounds bounds;
            public GraphicsBuffer buffer;
            public int count;
            public MaterialPropertyBlock props;
        }

        static readonly int BladesId = Shader.PropertyToID("_Blades");
        static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");

        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly Plane[] _planes = new Plane[6];
        Mesh _bladeMesh;
        Material _runtimeMaterial;
        int _builtQuality = -1;
        bool _supported = true;

        void OnEnable()
        {
            _supported = SystemInfo.supportsInstancing && SystemInfo.maxComputeBufferInputsVertex > 0;
            if (!_supported)
            {
                Debug.LogWarning("GrassRenderer: устройство не поддерживает буферы в вершинном шейдере — трава отключена.");
                return;
            }
            SettingsManager.Changed += OnSettingsChanged;
            Build();
        }

        void OnDisable()
        {
            SettingsManager.Changed -= OnSettingsChanged;
            Release();
        }

        bool _dirty;

        // Пересборка откладывается до Update — в OnValidate нельзя безопасно уничтожать объекты
        void OnValidate() => _dirty = true;

        void OnSettingsChanged()
        {
            if ((int)SettingsManager.Quality != _builtQuality) { Release(); Build(); }
        }

        int QualityIndex => Application.isPlaying ? (int)SettingsManager.Quality : 2;

        void Build()
        {
            if (material == null) return;
            _builtQuality = QualityIndex;
            _bladeMesh = CreateBladeMesh();
            _runtimeMaterial = new Material(material) { name = material.name + " (runtime)" };

            float scale = densityScale[_builtQuality];
            float distance = drawDistance[_builtQuality];
            _runtimeMaterial.SetFloat(FadeStartId, distance * 0.7f);
            _runtimeMaterial.SetFloat(FadeEndId, distance);

            var rng = new System.Random(seed);
            int nx = Mathf.CeilToInt(areaSize.x / chunkSize), nz = Mathf.CeilToInt(areaSize.y / chunkSize);
            Vector3 origin = areaCenter - new Vector3(areaSize.x, 0f, areaSize.y) * 0.5f;
            float cosMaxSlope = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
            var blades = new List<Blade>(4096);

            for (int cz = 0; cz < nz; cz++)
            for (int cx = 0; cx < nx; cx++)
            {
                blades.Clear();
                float x0 = origin.x + cx * chunkSize, z0 = origin.z + cz * chunkSize;
                float w = Mathf.Min(chunkSize, origin.x + areaSize.x - x0);
                float d = Mathf.Min(chunkSize, origin.z + areaSize.y - z0);
                int count = Mathf.RoundToInt(w * d * density * scale);
                float minY = float.MaxValue, maxY = float.MinValue;

                for (int i = 0; i < count; i++)
                {
                    float x = x0 + (float)rng.NextDouble() * w;
                    float z = z0 + (float)rng.NextDouble() * d;
                    // Пятна густоты: в некоторых местах трава реже и ниже — выглядит естественнее
                    float patch = Mathf.PerlinNoise(x * 0.09f + 13.1f, z * 0.09f + 7.7f);
                    if (rng.NextDouble() > Mathf.Lerp(0.35f, 1f, patch)) continue;

                    float y = areaCenter.y;
                    if (terrain != null)
                    {
                        Vector3 p = new Vector3(x, 0f, z);
                        y = terrain.SampleHeight(p) + terrain.transform.position.y;
                        Vector3 tp = p - terrain.transform.position;
                        Vector3 n = terrain.terrainData.GetInterpolatedNormal(tp.x / terrain.terrainData.size.x, tp.z / terrain.terrainData.size.z);
                        if (n.y < cosMaxSlope) continue;
                    }
                    float h = Mathf.Lerp(heightRange.x, heightRange.y, (float)rng.NextDouble()) * Mathf.Lerp(0.6f, 1.1f, patch);
                    blades.Add(new Blade
                    {
                        position = new Vector3(x, y - 0.02f, z),
                        yaw = (float)rng.NextDouble() * Mathf.PI * 2f,
                        height = h,
                        width = Mathf.Lerp(widthRange.x, widthRange.y, (float)rng.NextDouble()),
                        hue = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.05f + 3.3f, z * 0.05f + 9.1f) * 0.8f + (float)rng.NextDouble() * 0.35f),
                        bend = Mathf.Lerp(0.1f, 0.45f, (float)rng.NextDouble()),
                    });
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y + h);
                }
                if (blades.Count == 0) continue;

                // Перемешиваем, чтобы при LOD (рисуем первые N) травинки прореживались равномерно
                for (int i = blades.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (blades[i], blades[j]) = (blades[j], blades[i]);
                }

                var chunk = new Chunk
                {
                    count = blades.Count,
                    buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, blades.Count, Marshal.SizeOf<Blade>()),
                    props = new MaterialPropertyBlock(),
                };
                chunk.buffer.SetData(blades);
                chunk.props.SetBuffer(BladesId, chunk.buffer);
                // Запас по границам на раскачивание ветром
                var center = new Vector3(x0 + w * 0.5f, (minY + maxY) * 0.5f, z0 + d * 0.5f);
                chunk.bounds = new Bounds(center, new Vector3(w + 1f, maxY - minY + 1f, d + 1f));
                _chunks.Add(chunk);
            }
        }

        void Release()
        {
            foreach (var c in _chunks) c.buffer?.Release();
            _chunks.Clear();
            if (_bladeMesh != null) DestroyImmediate(_bladeMesh);
            if (_runtimeMaterial != null) DestroyImmediate(_runtimeMaterial);
            _bladeMesh = null;
            _runtimeMaterial = null;
        }

        void LateUpdate()
        {
            if (_dirty && _supported)
            {
                _dirty = false;
                Release();
                Build();
            }
            if (!_supported || _chunks.Count == 0 || _runtimeMaterial == null) return;

            Camera cam = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (cam == null) return;

            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            float maxDist = drawDistance[_builtQuality];
            Vector3 camPos = cam.transform.position;

            var rp = new RenderParams(_runtimeMaterial)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };

            foreach (var c in _chunks)
            {
                float dist = Mathf.Sqrt(c.bounds.SqrDistance(camPos));
                if (dist > maxDist) continue;
                // Запас к границам: верхушки качаются ветром, камера может резко повернуться
                var testBounds = c.bounds;
                testBounds.Expand(2f);
                if (!GeometryUtility.TestPlanesAABB(_planes, testBounds)) continue;

                // Дальние чанки рисуем с частью травинок
                float lod = Mathf.Lerp(1f, farChunkFraction, Mathf.InverseLerp(maxDist * 0.35f, maxDist, dist));
                int count = Mathf.Max(1, Mathf.RoundToInt(c.count * lod));
                rp.worldBounds = c.bounds;
                rp.matProps = c.props;
                Graphics.RenderMeshPrimitives(rp, _bladeMesh, 0, count);
            }
        }

        /// <summary>Меш травинки: сужающаяся к кончику полоска из нескольких сегментов.</summary>
        static Mesh CreateBladeMesh()
        {
            const int segments = 4;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float half = 0.5f * (1f - t * t * 0.85f);
                if (i == segments)
                {
                    verts.Add(new Vector3(0f, 1f, 0f));
                    uvs.Add(new Vector2(0.5f, 1f));
                    break;
                }
                verts.Add(new Vector3(-half, t, 0f));
                verts.Add(new Vector3(half, t, 0f));
                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));
            }
            for (int i = 0; i < segments - 1; i++)
            {
                int a = i * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            int last = (segments - 1) * 2;
            tris.AddRange(new[] { last, verts.Count - 1, last + 1 });

            var mesh = new Mesh { name = "GrassBlade", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.up * 0.5f, Vector3.one * 2f);
            return mesh;
        }
    }
}
