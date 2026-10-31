// Процедурная трава для URP: травинки рисуются инстансингом (Graphics.RenderMeshPrimitives),
// данные каждой травинки (позиция, поворот, высота, оттенок) читаются из StructuredBuffer.
// Ветер — смещение верхушек волнами и порывами; цвет — градиент от корня к кончику с вариациями.
Shader "GrandWorld/Grass"
{
    Properties
    {
        _BaseColor ("Root Color", Color) = (0.10, 0.22, 0.05, 1)
        _TipColor ("Tip Color", Color) = (0.55, 0.78, 0.28, 1)
        _DryColor ("Dry Tip Color", Color) = (0.78, 0.74, 0.38, 1)
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.6
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.4
        _WindScale ("Wind Wave Scale", Range(0.01, 1)) = 0.12
        _WindDirection ("Wind Direction (XZ)", Vector) = (1, 0, 0.4, 0)
        _Translucency ("Translucency", Range(0, 1)) = 0.45
        _FadeStart ("Fade Start Distance", Float) = 35
        _FadeEnd ("Fade End Distance", Float) = 50
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Blade
            {
                float3 position; // корень травинки в мире
                float yaw;       // поворот вокруг Y, радианы
                float height;    // множитель высоты
                float width;     // множитель ширины
                float hue;       // 0..1 — вариация цвета
                float bend;      // собственный наклон травинки
            };

            StructuredBuffer<Blade> _Blades;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DryColor;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float4 _WindDirection;
                half _Translucency;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION; // меш травинки: x — ширина, y — высота (0..1)
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;  // rgb — цвет, a — высота вдоль травинки
                half fogFactor : TEXCOORD3;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings Vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Blade b = _Blades[instanceID];
                float s, c;
                sincos(b.yaw, s, c);

                float t = input.positionOS.y;                 // 0 у корня, 1 у кончика
                float3 local = float3(input.positionOS.x * b.width, t * b.height, 0);

                // Собственный изгиб травинки (кончик отклонён вперёд по её оси)
                local.z += b.bend * t * t * b.height;

                float3 dirLocal = float3(local.x * c + local.z * s, local.y, -local.x * s + local.z * c);
                float3 posWS = b.position + dirLocal;

                // Ветер: крупные волны + мелкая дрожь, сильнее у кончика
                float2 windDir = normalize(_WindDirection.xz + 1e-4);
                float time = _Time.y * _WindSpeed;
                float wave = Noise(b.position.xz * _WindScale - windDir * time * 0.6);
                float gust = sin(dot(b.position.xz, windDir) * 0.35 - time * 1.7) * 0.5 + 0.5;
                float flutter = sin(time * 6.0 + b.hue * 40.0) * 0.08;
                float sway = (wave * 0.8 + gust * 0.5 + flutter) * _WindStrength * t * t * b.height;
                posWS.xz += windDir * sway;
                posWS.y -= sway * sway * 0.3; // кончик немного опускается при сильном наклоне

                Varyings o;
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                // Нормаль слегка наклонена вверх — трава освещается мягче, без тёмных боков
                float3 bladeNormal = float3(s, 0, c);
                o.normalWS = normalize(lerp(bladeNormal, float3(0, 1, 0), 0.6));

                half3 tip = lerp(_TipColor.rgb, _DryColor.rgb, saturate(b.hue * 1.6 - 0.6));
                tip *= lerp(0.85, 1.15, frac(b.hue * 7.13));
                o.color = half4(lerp(_BaseColor.rgb, tip, smoothstep(0.0, 1.0, t)), t);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                // Плавное исчезновение вдали — граница травы не заметна
                float dist = distance(i.positionWS, GetCameraPositionWS());
                float fade = 1.0 - saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
                float dither = Hash(i.positionCS.xy);
                clip(fade - dither * 0.999);

                half3 n = normalize(isFront ? i.normalWS : -i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half shadow = light.shadowAttenuation;

                half ndl = saturate(dot(n, light.direction)) * 0.7 + 0.3;
                // Просвечивание: свет сзади подсвечивает кончики
                half3 viewDir = normalize(GetCameraPositionWS() - i.positionWS);
                half back = pow(saturate(dot(-viewDir, light.direction)), 3.0) * _Translucency * i.color.a;

                half3 ambient = SampleSH(n) * 0.9;
                // Фальшивое затенение у корня — густая трава темнее внизу
                half ao = lerp(0.45, 1.0, i.color.a);
                half3 lit = i.color.rgb * (ambient + light.color * (ndl + back) * shadow) * ao;
                lit = MixFog(lit, i.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
