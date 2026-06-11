// 수중 헤이즈 막 — 수면 아래 잠긴 빌딩 하부에 물 톤을 입히는 반투명 단색(침수 도시 연속감)
// LightMode 태그 없는 단일 패스(SRPDefaultUnlit) — 2D/3D 렌더러 모두에서 그려짐
Shader "Game/UnderwaterHaze"
{
    Properties
    {
        _Color ("Color", Color) = (0.06, 0.28, 0.30, 0.45)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
