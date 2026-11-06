// Diffuse URP 3D lighting shared by buildings and transparent world sprites.
#ifndef FIRELINE_SPRITE_LIGHTING_INCLUDED
#define FIRELINE_SPRITE_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
half _Cutoff;
half4 _FlashColor;
half _FlashAmount;
CBUFFER_END

struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
struct Varyings {
 float4 positionCS : SV_POSITION;
 float2 uv : TEXCOORD0;
 half4 color : COLOR;
 float3 positionWS : TEXCOORD1;
 half3 normalWS : TEXCOORD2;
 half fog : TEXCOORD3;
};
Varyings Vert(Attributes input) {
 Varyings output;
 float2 flip = unity_SpriteProps.xy;
 output.positionWS = TransformObjectToWorld(float3(input.positionOS.xy * flip, input.positionOS.z));
 output.positionCS = TransformWorldToHClip(output.positionWS);
 // Inverse transpose handles skewed building sides; no mesh normals are required.
 output.normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
 output.uv = input.uv;
 output.color = input.color * unity_SpriteColor;
 output.fog = ComputeFogFactor(output.positionCS.z);
 return output;
}
half4 SampleSprite(Varyings input) {
 half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
#if defined(FIRELINE_CUTOUT)
 clip(sprite.a - _Cutoff);
 sprite.a = 1;
#endif
 return sprite;
}
half4 LitFrag(Varyings input) : SV_Target {
 half4 sprite = SampleSprite(input);
 InputData data = (InputData)0;
 data.positionWS = input.positionWS;
 half3 normal = normalize(input.normalWS);
 half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
 // Both sides receive light, including mirrored side art and flipX/flipY sprites.
 data.normalWS = dot(normal,view) < 0 ? -normal : normal;
 data.viewDirectionWS = view;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
 data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
 data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
 data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
 data.bakedGI = SampleSH(data.normalWS);
 data.vertexLighting = VertexLighting(input.positionWS, data.normalWS);
 data.shadowMask = half4(1,1,1,1);
 SurfaceData surface = (SurfaceData)0;
 surface.albedo = sprite.rgb;
 surface.alpha = sprite.a;
 surface.occlusion = 1;
 surface.normalTS = half3(0,0,1);
 half4 color = UniversalFragmentBlinnPhong(data,surface);
#if defined(FIRELINE_FLASH)
 color.rgb = lerp(color.rgb, _FlashColor.rgb, saturate(_FlashAmount * _FlashColor.a));
#endif
 color.rgb = MixFog(color.rgb,input.fog);
 return color;
}
#endif
