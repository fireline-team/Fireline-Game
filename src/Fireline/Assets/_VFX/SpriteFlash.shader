// Blends a sprite toward one color (white by default) while keeping its shape.
// Used for the enemy hit flash: HordeEnemy swaps to a material with this shader
// for a few frames when it takes damage, then swaps back.
// Flash Strength: 0 = normal sprite, 1 = solid flash color.
Shader "Fireline/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _FlashColor;
            half _FlashAmount;
            // Set by SpriteRenderer for flipX/flipY. If it isn't provided it reads as zero,
            // so treat zero as "not flipped" instead of collapsing the sprite.
            float2 _Flip;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 flip = (_Flip.x == 0.0 && _Flip.y == 0.0) ? float2(1.0, 1.0) : _Flip;
                float3 position = float3(input.positionOS.xy * flip, input.positionOS.z);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                // Mix the sprite's own colors toward the flash color, keeping its transparency.
                half3 rgb = lerp(sprite.rgb, _FlashColor.rgb, _FlashAmount * _FlashColor.a);
                return half4(rgb, sprite.a);
            }
            ENDHLSL
        }
    }
}
