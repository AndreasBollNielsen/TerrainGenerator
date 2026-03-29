Shader "FullScreen/RaymarchDepthPass"
{
    

    SubShader
{
    Tags { "RenderPipeline" = "HDRenderPipeline" }

    Pass
    {
        Name "RaymarchDepth"
        ZWrite On
        ZTest Always
        Cull Off
        ColorMask 0

        HLSLPROGRAM
        #pragma target 4.5
        #pragma vertex Vert
        #pragma fragment Frag

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"


        TEXTURE2D(_RaymarchDepthTex);
        SAMPLER(sampler_RaymarchDepthTex);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            o.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return o;
        }

        float Frag(Varyings input) : SV_Depth
        {
            float rayDepth = SAMPLE_TEXTURE2D(
                _RaymarchDepthTex,
                sampler_RaymarchDepthTex,
                input.uv
            ).r;

            // No hit → far plane (reverse Z)
            if (rayDepth <= 0.0001)
                return 0.0;

            // Manual linear eye depth → device depth
            float deviceDepth =
                (1.0 / rayDepth - _ZBufferParams.w) / _ZBufferParams.z;

            return saturate(deviceDepth);
        }
// float Frag(Varyings input) : SV_Depth
// {
//     float d = SAMPLE_TEXTURE2D(
//         _RaymarchDepthTex,
//         sampler_RaymarchDepthTex,
//         input.uv
//     ).r;

//     // Hvis RT'en er korrekt bundet:
//     // hit → near plane
//     // no hit → far plane
//     return d > 0.0 ? 1.0 : 0.0;
// }

        ENDHLSL
    }
}

    
    Fallback Off
}
