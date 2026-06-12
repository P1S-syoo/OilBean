// 탁한 강물 셰이더 — 절차 노이즈 2겹 스크롤(흐름) + 마루 거품 + 메인광 스페큘러, 안개 적용
// 텍스처 없이 월드 XZ 기반이라 수면 평면 크기·UV와 무관하게 연속
Shader "Game/MurkyWaterFlow"
{
    Properties
    {
        _BaseColor ("Base (얕은 물)", Color) = (0.10, 0.16, 0.16, 1)
        _DeepColor ("Deep (깊은 물)", Color) = (0.045, 0.085, 0.095, 1)
        _FoamColor ("Foam (거품)", Color) = (0.50, 0.56, 0.56, 1)
        _FlowDir ("Flow Dir (xy=방향)", Vector) = (1, 0.18, 0, 0)
        _FlowSpeed ("Flow Speed", Float) = 0.35
        _WaveScale ("Wave Scale (노이즈 밀도)", Float) = 0.10
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.16
        _SpecStrength ("Spec Strength", Float) = 0.22
        _WaveHeight ("Wave Height (정점 진폭 m)", Float) = 0.28
        _WaveFreq ("Wave Freq (정점 파장 밀도)", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off   // 수중(아래)에서 올려다봐도 수면이 보이도록 양면 렌더
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FlowDir;
                float _FlowSpeed;
                float _WaveScale;
                float _FoamAmount;
                float _SpecStrength;
                float _WaveHeight;
                float _WaveFreq;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float fogCoord:TEXCOORD1; };

            // 정점 웨이브 — 월드 XZ 기반 3겹 사인 합(파장·방향·속도 다르게) → 평면 크기와 무관
            float WaveY(float3 ws)
            {
                float t = _Time.y;
                float w = sin(ws.x * _WaveFreq + t * 1.1) * 0.6
                        + sin(dot(ws.xz, float2(0.7, 0.7)) * _WaveFreq * 1.7 + t * 1.7) * 0.25
                        + sin(ws.z * _WaveFreq * 2.3 + t * 0.7) * 0.15;
                return w * _WaveHeight;
            }

            // 2D 해시 → 밸류 노이즈(텍스처 불필요)
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                ws.y += WaveY(ws);
                OUT.positionCS = TransformWorldToHClip(ws);
                OUT.positionWS = ws;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target
            {
                float2 uv = IN.positionWS.xz * _WaveScale;
                float2 dir = normalize(_FlowDir.xy + 1e-4);
                float2 flow = dir * (_Time.y * _FlowSpeed);

                // 2겹 노이즈를 다른 속도·반대 방향으로 스크롤 → 물결 간섭(흐름)
                float n1 = VNoise(uv + flow);
                float n2 = VNoise(uv * 2.3 - flow * 1.6 + 13.7);
                float h = n1 * 0.65 + n2 * 0.35;

                // 높이 그라디언트로 가짜 법선(요철) — 별도 노멀맵 없이 굴절감
                float3 n = normalize(float3((n1 - n2) * 0.55, 1.0, (n2 - n1) * 0.35));

                // 물색: 깊은 색↔밝은 색 보간 + 마루 거품
                half3 albedo = lerp(_DeepColor.rgb, _BaseColor.rgb, h);
                float foam = smoothstep(1.0 - _FoamAmount, 1.0, h);
                albedo = lerp(albedo, _FoamColor.rgb, foam);

                // 메인광 확산 + 스페큘러 글린트
                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction)) * 0.7 + 0.3;
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 halfDir = normalize(L.direction + viewDir);
                float spec = pow(saturate(dot(n, halfDir)), 64.0) * _SpecStrength;
                half3 color = albedo * (L.color * ndl + SampleSH(n) * 0.4 + 0.12) + L.color * spec;

                color = MixFog(color, IN.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Depth Priming/뎁스 프리패스 호환 — 이 패스가 없으면 일부 URP 설정에서 불투명이 스킵됨
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // SRP Batcher 호환 — 모든 패스의 UnityPerMaterial 레이아웃이 일치해야 배칭됨
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FlowDir;
                float _FlowSpeed;
                float _WaveScale;
                float _FoamAmount;
                float _SpecStrength;
                float _WaveHeight;
                float _WaveFreq;
            CBUFFER_END

            struct ADepth { float4 positionOS:POSITION; };
            struct VDepth { float4 positionCS:SV_POSITION; };

            // ForwardLit의 WaveY와 동일 — 뎁스 패스가 어긋나면 수면 깊이가 갈라짐
            float WaveYDepth(float3 ws)
            {
                float t = _Time.y;
                float w = sin(ws.x * _WaveFreq + t * 1.1) * 0.6
                        + sin(dot(ws.xz, float2(0.7, 0.7)) * _WaveFreq * 1.7 + t * 1.7) * 0.25
                        + sin(ws.z * _WaveFreq * 2.3 + t * 0.7) * 0.15;
                return w * _WaveHeight;
            }

            VDepth vertDepth(ADepth IN)
            {
                VDepth OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                ws.y += WaveYDepth(ws);
                OUT.positionCS = TransformWorldToHClip(ws);
                return OUT;
            }

            half fragDepth(VDepth IN):SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
