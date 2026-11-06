// Receives URP 3D lights. Sprite sorting and depth testing are preserved.
Shader "Fireline/StructureDepth"
{
 Properties {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
  HLSLINCLUDE
  #define FIRELINE_CUTOUT 1

  #include "SpriteLighting.hlsl"
  ENDHLSL
  Pass {
   Name "ForwardLit"
   Tags { "LightMode"="UniversalForwardOnly" }
   Blend Off
   ZWrite On
   ZTest LEqual
   Cull Off
   HLSLPROGRAM
   #pragma target 3.5
#pragma vertex Vert
#pragma fragment LitFrag
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile_fog
   ENDHLSL
  }
  Pass {
   Name "DepthOnly"
   Tags { "LightMode"="DepthOnly" }
   ZWrite On
   ColorMask R
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   half4 DepthFrag(Varyings input) : SV_Target { SampleSprite(input); return 0; }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On
   ZTest LEqual
   ColorMask 0
   Cull Off
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection;
   float3 _LightPosition;
   Varyings ShadowVert(Attributes input) {
    Varyings output = Vert(input);
    float3 direction = _LightDirection;
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    direction = normalize(_LightPosition - output.positionWS);
#endif
    half3 normal = dot(output.normalWS,direction) < 0 ? -output.normalWS : output.normalWS;
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,normal,direction));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
   }
   half4 DepthFrag(Varyings input) : SV_Target { SampleSprite(input); return 0; }
   ENDHLSL
  }
 }
}
