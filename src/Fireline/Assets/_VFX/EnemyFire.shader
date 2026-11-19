// Keeps the animated sprite's UVs and alpha intact; the fire motion only changes its light.
Shader "Fireline/EnemyFire"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [Header(Glow)]
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Strength", Range(0,12)) = 3.5
        _TintGlow ("Keep Glow Across Enemy Tints", Range(0,1)) = 1
        _HotCore ("Hot Core Contrast", Range(0,1)) = 0.35
        [Header(Fire Motion)]
        _FlickerAmount ("Flicker Amount", Range(0,0.5)) = 0.12
        _FlickerSpeed ("Flicker Speed", Range(0,20)) = 8
        _FlowStrength ("Rising Flame Shimmer", Range(0,0.5)) = 0.12
        _FlowScale ("Flame Detail Scale", Range(1,20)) = 6
        _FlowSpeed ("Flame Rise Speed", Range(0,5)) = 1.6
        _EmberStrength ("Ember Glints", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Pass
        {
            Name "EnemyFire"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float _EmissionIntensity, _TintGlow, _HotCore, _FlickerAmount, _FlickerSpeed;
                float _FlowStrength, _FlowScale, _FlowSpeed, _EmberStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float2 localPosition : TEXCOORD1;
                float phase : TEXCOORD2;
                half fog : TEXCOORD3;
            };
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = float3(input.positionOS.xy * unity_SpriteProps.xy, input.positionOS.z);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                output.localPosition = input.positionOS.xy;
                // A smooth per-object phase avoids synchronized crowds and per-sprite material instances.
                output.phase = dot(TransformObjectToWorld(float3(0,0,0)), float3(1.37,2.11,0.83));
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 sprite = art * input.color;
                float time = _Time.y * _FlickerSpeed;
                float flicker = 1.0 + _FlickerAmount *
                    (0.65 * sin(time + input.phase) + 0.35 * sin(time * 2.31 + input.phase * 1.73));
                float2 flow = input.localPosition * _FlowScale;
                flow.y -= _Time.y * _FlowSpeed;
                flow.x += input.phase * 0.13;
                float shimmer = 1.0 + (Noise(flow) * 2.0 - 1.0) * _FlowStrength;
                float glint = pow(saturate(Noise(flow * 2.7 + 17.0)), 12.0) * _EmberStrength * 6.0;
                float brightness = max(art.r, max(art.g, art.b));
                float tintedBrightness = max(sprite.r, max(sprite.g, sprite.b));
                // Dark definition tints retain their hue while still reaching HDR bloom brightness.
                half3 glow = sprite.rgb * lerp(1.0, brightness / max(tintedBrightness, 0.0001), _TintGlow);
                float core = lerp(1.0, 0.55 + brightness * 0.9, _HotCore);
                half3 emission = glow * _EmissionColor.rgb * _EmissionIntensity *
                    flicker * (shimmer * core + glint);
                return half4(MixFog(sprite.rgb + emission, input.fog), sprite.a);
            }
            ENDHLSL
        }
    }
}
