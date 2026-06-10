using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Varco {
    // SyncFace 블렌드셰이프 결과 (weightMat: [프레임 수 x 포즈 수], faceNames: ARKit 블렌드셰이프 이름 순서)
    public class VarcoBlendshapeResult {
        public int exportFps;
        public int numPoses;
        public int numFrames;
        public List<string> faceNames;
        public List<List<float>> weightMat;

        // 특정 프레임의 블렌드셰이프 가중치를 SkinnedMeshRenderer에 적용 (이름 일치 기준, 0~1 -> 0~100 스케일)
        public void ApplyFrame(SkinnedMeshRenderer renderer, int frame) {
            try {
                if (frame < 0 || frame >= numFrames) {
                    return;
                }
                List<float> weights = weightMat[frame];
                for (int i = 0; i < faceNames.Count; i++) {
                    int idx = renderer.sharedMesh.GetBlendShapeIndex(faceNames[i]);
                    if (idx >= 0) {
                        renderer.SetBlendShapeWeight(idx, weights[i] * 100f);
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"블렌드셰이프 적용 실패 (frame {frame}): {e.Message}");
            }
        }
    }

    // SyncFace API 행동단위 래퍼 - 음성에서 표정 애니메이션 생성
    // 주의: 응답의 weightMat이 2차원 배열이라 JsonUtility로 파싱 불가 -> Newtonsoft.Json 패키지 필요 (com.unity.nuget.newtonsoft-json)
    public static class VarcoSyncFace {
        // 음성으로 블렌드셰이프 애니메이션 생성 (lip_style: balanced/clear/minimal/moderate, face_style: natural/energetic/stoic/timid, emotion: neutral/angry/happy/sad/surprise, 실패 시 null)
        public static async Task<VarcoBlendshapeResult> GenerateBlendshape(
            AudioClip voice,
            int fps = 30,
            string lipStyle = "balanced",
            string faceStyle = "natural",
            string emotion = "neutral",
            bool neck = false,
            bool eye = false) {
            string b64 = VarcoAudioUtil.ClipToWavBase64(voice);
            if (b64 == null) {
                return null;
            }
            var body = new Dictionary<string, object> {
                { "id", Guid.NewGuid().ToString() },
                { "audio", b64 },
                { "fps", fps },
                { "lip_style", lipStyle },
                { "face_style", faceStyle },
                { "emotion", emotion },
                { "neck", neck ? "on" : "off" },
                { "eye", eye ? "on" : "off" }
            };
            string json = await VarcoApiClient.PostJsonAsync("/fa/asfa/v1.1/blendshape", JsonConvert.SerializeObject(body));
            if (json == null) {
                return null;
            }
            try {
                BlendshapeEnvelope envelope = JsonConvert.DeserializeObject<BlendshapeEnvelope>(json);
                if (envelope == null || !envelope.success) {
                    Debug.LogError($"SyncFace 생성 실패 응답: {json}");
                    return null;
                }
                return envelope.blendshape;
            } catch (Exception e) {
                Debug.LogError($"SyncFace 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        private class BlendshapeEnvelope {
            public string id;
            public bool success;
            public VarcoBlendshapeResult blendshape;
            public string jobid;
            public float credit;
        }
    }
}
