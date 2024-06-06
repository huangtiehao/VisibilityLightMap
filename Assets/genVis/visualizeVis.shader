Shader "GI/visualizeVisShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _VisTex1 ("visCoe1", 2D) = "white" {}
        _VisTex2 ("visCoe2", 2D) = "white" {}
        _VisTex3 ("visCoe3", 2D) = "white" {}
        _VisTex4 ("visCoe4", 2D) = "white" {}
        _LightTex1 ("lightCoe1", 2D) = "white" {}
        _LightTex2 ("lightCoe2", 2D) = "white" {}
        _LightTex3 ("lightCoe3", 2D) = "white" {}
        _LightTex4 ("lightCoe4", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #include "visualizeVisibility.hlsl"
            #pragma target 5.1
            #pragma vertex visualizeVertex
            #pragma fragment visualizeFragment

            #pragma enable_d3d11_debug_symbols
            ENDHLSL
        }
    }
}
