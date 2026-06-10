// 월드좌표 triplanar 지형 셰이더 — 텍스처가 블록 경계 없이 지형 전체에 연속(타일 반복 제거)
// 윗면(법선 위)은 _TopMap, 옆면은 _SideMap을 월드 XZ/XY/ZY 투영으로 샘플
Shader "Game/TriplanarTerrain"
{
    Properties
    {
        _TopMap ("Top (윗면)", 2D) = "white" {}
        _SideMap ("Side (옆면)", 2D) = "white" {}
        _WorldScale ("World Scale (텍스처 1장 = N유닛)", Float) = 6
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TopMap);  SAMPLER(sampler_TopMap);
            TEXTURE2D(_SideMap); SAMPLER(sampler_SideMap);

            CBUFFER_START(UnityPerMaterial)
                float _WorldScale;
                float4 _Tint;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 w = IN.positionWS / max(_WorldScale, 0.001);
                float3 an = abs(n);

                // 옆면: X평면(zy)·Z평면(xy) 투영 블렌드
                float wx = an.x, wz = an.z;
                float s = max(wx + wz, 1e-4);
                half3 side = (SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, w.zy).rgb * wx
                            + SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, w.xy).rgb * wz) / s;
                // 윗면: Y평면(xz) 투영
                half3 top = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, w.xz).rgb;

                float upW = saturate((an.y - 0.5) * 4.0);   // 위/아래 향하면 top
                half3 albedo = lerp(side, top, upW) * _Tint.rgb;

                // 간단 라이팅(메인광 + 환경)
                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction)) * 0.8 + 0.2;
                half3 color = albedo * (L.color * ndl + SampleSH(n) * 0.5 + 0.15);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
