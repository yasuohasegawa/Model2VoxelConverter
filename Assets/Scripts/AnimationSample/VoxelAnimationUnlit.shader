Shader "Custom/VoxelAnimationUnlit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("BaseColor",Color) = (1, 1, 1, 1)
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
 
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float _Cutoff;  
        CBUFFER_END
        ENDHLSL
 
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma shader_feature _ALPHATEST_ON


            StructuredBuffer<float3> _modifiedVertices;
            struct Attributes
            {
                float4 vertex    : POSITION;
                float2 uv        : TEXCOORD0;
                float4 color    : COLOR;
                float3 normal     : NORMAL;
                uint vertexID    : SV_VertexID;
            };
 
            struct Varyings
            {
                float4 vertex     : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 color    : COLOR;
                float3 normal : POSITION1;
            };
         
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
 
            Varyings VSMain(Attributes IN)
            {
                Varyings OUT;
                float3 modifiedVertex = _modifiedVertices[IN.vertexID];
                OUT.vertex = TransformObjectToHClip(modifiedVertex); // IN.vertex.xyz
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.normal = TransformObjectToWorldNormal(IN.normal);
                return OUT;
            }
 
            float4 PSMain(Varyings IN) : SV_Target
            {
                float4 col = IN.color;
                Light mainLight = GetMainLight();

                // dot product between normal and light direction for
                // standard diffuse (Lambert) lighting
                float nl = max(0, dot(IN.normal, mainLight.direction));
                float3 diffuseLight = mainLight.color * nl;
                col.rgb*=diffuseLight+_BaseColor.rgb;
                return col;
                //float2 uv = IN.uv.xy  * _BaseMap_ST.xy + _BaseMap_ST.zw;
                //return _BaseMap.Sample(sampler_BaseMap, uv);
            }
            ENDHLSL
        }
 
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]
 
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
 
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
 
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
 
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            ZTest LEqual
 
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
