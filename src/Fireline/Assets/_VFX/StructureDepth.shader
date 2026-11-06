// Draws building faces (Structure front, top and sides) as solid, depth-writing sprites.
//
// Normal sprites don't write depth, so they're layered purely by draw order. That breaks for
// buildings that touch or overlap: whichever building draws second paints all of its faces,
// including a side face that should be hidden, over the first one. With depth, the GPU picks
// the nearest surface per pixel, so overlapping buildings, L-shapes made of several Structures,
// and characters walking behind corners all occlude correctly. (Characters keep their normal
// sprite shader: it already depth-tests against these faces.)
//
// Pixels are fully opaque or cut out (no soft transparency), which suits pixel art.
// Structure swaps its faces back to their normal sprite material while FadeWhenBehind is
// fading them, since a cutout shader can't be see-through.
Shader "Fireline/StructureDepth"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        half _Cutoff;
        // Set by SpriteRenderer for flipX/flipY; reads as zero if not provided, so treat zero as "not flipped".
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

        half4 SampleCutout(Varyings input)
        {
            half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            clip(sprite.a - _Cutoff);
            return half4(sprite.rgb, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Off // the left side face is seen from behind (it shows the side art mirrored)
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target { return SampleCutout(input); }
            ENDHLSL
        }

        // Lets the faces show up in the camera depth texture, for any effects that read it.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target { SampleCutout(input); return 0; }
            ENDHLSL
        }
    }
}
