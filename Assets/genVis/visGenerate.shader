Shader "GI/VisLightMap"
{

    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("BaseColor",Color)=(0.5,0.5,1.0,1.0)
    }
    SubShader
    {
        ZTest Always
        ZWrite off
        Cull off
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VoxelPassVertex
            #pragma fragment VoxelPassFragment
            #include "visGen.hlsl"
            #pragma enable_d3d11_debug_symbols
            ENDHLSL
        }
    }
}