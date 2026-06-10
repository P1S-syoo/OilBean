using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Varco {
    // Image to 3D API 행동단위 래퍼 - 비동기 작업 (요청 -> 폴링 -> GLB 다운로드)
    public static class VarcoImageTo3D {
        // 3D 생성 요청 후 requestId 반환 (image는 PNG만 지원, faceNum 1000~300000, 실패 시 null)
        public static async Task<string> Request(byte[] pngBytes, string targetFaceType = "tri", int targetFaceNum = 300000, bool generateTexture = true, int seed = -1) {
            var form = new List<IMultipartFormSection> {
                new MultipartFormFileSection("image", pngBytes, "input.png", "image/png"),
                new MultipartFormDataSection("target_face_type", targetFaceType),
                new MultipartFormDataSection("target_face_num", targetFaceNum.ToString()),
                new MultipartFormDataSection("generate_texture", generateTexture ? "true" : "false"),
                new MultipartFormDataSection("seed", seed.ToString())
            };
            string json = await VarcoApiClient.PostMultipartForJsonAsync("/3d/varco/v1/image-to-3d", form);
            if (json == null) {
                return null;
            }
            try {
                AcceptResponse res = JsonUtility.FromJson<AcceptResponse>(json);
                return res.requestId;
            } catch (Exception e) {
                Debug.LogError($"3D 생성 요청 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        // 작업 상태 1회 조회 - status(processing/succeeded/failed)와 model_url 반환 (실패 시 null)
        public static async Task<ResultResponse> GetResult(string requestId) {
            string json = await VarcoApiClient.GetAsync($"/inference/result/{requestId}");
            if (json == null) {
                return null;
            }
            try {
                return JsonUtility.FromJson<ResultResponse>(json);
            } catch (Exception e) {
                Debug.LogError($"3D 상태 조회 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        // 완료까지 폴링 후 GLB 다운로드 URL 반환 (model_url 유효기간 7일, 실패/타임아웃 시 null)
        public static async Task<string> WaitForModelUrl(string requestId, float pollIntervalSec = 5f, float timeoutSec = 600f) {
            float elapsed = 0f;
            while (elapsed < timeoutSec) {
                ResultResponse res = await GetResult(requestId);
                if (res == null) {
                    return null;
                }
                if (res.status == "succeeded") {
                    return res.model_url;
                }
                if (res.status == "failed") {
                    Debug.LogError($"3D 생성 작업 실패 (requestId: {requestId})");
                    return null;
                }
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSec));
                elapsed += pollIntervalSec;
            }
            Debug.LogError($"3D 생성 폴링 타임아웃 ({timeoutSec}초, requestId: {requestId})");
            return null;
        }

        // GLB 파일 다운로드 - 런타임 로드는 glTFast 등 별도 임포터 필요 (실패 시 null)
        public static Task<byte[]> DownloadModel(string modelUrl) {
            return VarcoApiClient.DownloadAsync(modelUrl);
        }

        [Serializable] private class AcceptResponse { public string requestId; public string requestTime; public string message; }

        [Serializable]
        public class ResultResponse {
            public string status;
            public string model_url;
        }
    }
}
