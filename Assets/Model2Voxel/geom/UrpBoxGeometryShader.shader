Shader "Custom/UrpBoxGeometryShader"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BoxSize ("BoxSize", float) = 0.5
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
        #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
        ENDHLSL
 
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma require geometry

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma vertex vert
            #pragma geometry geom 
            #pragma fragment frag
            #pragma shader_feature _ALPHATEST_ON

            #include "./UrpBoxGeom.hlsl"

            ENDHLSL
        }
 
        Pass {

            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma require geometry

            #pragma multi_compile_shadowcaster

            #define SHADOW_CASTER_PASS

            #pragma vertex vert
            #pragma geometry geom 
            #pragma fragment frag

            #include "./UrpBoxGeom.hlsl"

            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
