// Receives URP 3D lights. Sprite sorting and depth testing are preserved.
Shader "Fireline/SpriteFlash"
{
 Properties {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

  _FlashColor ("Flash Color", Color) = (1,1,1,1)
  _FlashAmount ("Flash Strength", Range(0,1)) = 0.5
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
  HLSLINCLUDE
  #define _SURFACE_TYPE_TRANSPARENT 1
  #define FIRELINE_FLASH 1
  #include "SpriteLighting.hlsl"
  ENDHLSL
  Pass {
   Name "ForwardLit"
   Tags { "LightMode"="UniversalForwardOnly" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
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

 }
}
