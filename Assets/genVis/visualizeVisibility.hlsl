#ifndef CUSTOM_VOXEL_PASS_INCLUDED
#define CUSTOM_VOXEL_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#define EPS 0.01
#define STEPS 64

TEXTURE2D(_LightTex1);
SAMPLER(sampler_LightTex1);
TEXTURE2D(_LightTex2);
SAMPLER(sampler_LightTex2);
TEXTURE2D(_LightTex3);
SAMPLER(sampler_LightTex3);
TEXTURE2D(_LightTex4);
SAMPLER(sampler_LightTex4);

TEXTURE2D(_VisTex1);
SAMPLER(sampler_VisTex1);
TEXTURE2D(_VisTex2);
SAMPLER(sampler_VisTex2);
TEXTURE2D(_VisTex3);
SAMPLER(sampler_VisTex3);
TEXTURE2D(_VisTex4);
SAMPLER(sampler_VisTex4);

struct Attributes
{
    float4 positionOS : POSITION;
    float4 normalOS: NORMAL;
    float2 baseUV : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS :SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS: VAR_NORMAL;
    float2 baseUV : VAR_BASE_UV;
};

Varyings visualizeVertex(Attributes input)
{
    Varyings output;
    output.positionWS=TransformObjectToWorld(input.positionOS);
    output.positionCS=TransformWorldToHClip(output.positionWS);
    output.normalWS=TransformObjectToWorldNormal(input.normalOS);
    output.baseUV=input.baseUV;
    return output;
}


float4 visualizeFragment (Varyings input):SV_TARGET
{
    
    float col=dot(tex2D(sampler_VisTex1,input.baseUV),tex2D(sampler_LightTex1,input.baseUV))+dot(tex2D(sampler_VisTex2,input.baseUV),tex2D(sampler_LightTex2,input.baseUV))+
        dot(tex2D(sampler_VisTex3,input.baseUV),tex2D(sampler_LightTex3,input.baseUV))+dot(tex2D(sampler_VisTex4,input.baseUV),tex2D(sampler_LightTex4,input.baseUV));
    return float4(col/PI,0,0,1); 
}
#endif