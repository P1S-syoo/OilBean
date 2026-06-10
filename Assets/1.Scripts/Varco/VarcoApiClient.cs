using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Varco {
    // VARCO OpenAPI 공통 클라이언트 - 인증 헤더와 요청/응답 처리를 담당
    public static class VarcoApiClient {
        public const string DefaultBaseUrl = "https://openapi.ai.nc.com";

        // 플랫폼에서 발급받은 OPENAPI_KEY를 게임 시작 시 1회 설정한다 (코드에 하드코딩 금지)
        public static string ApiKey = "";
        public static string BaseUrl = DefaultBaseUrl;
        public static int TimeoutSeconds = 180;

        // JSON 바디 POST 후 JSON 문자열 반환 (실패 시 null)
        public static async Task<string> PostJsonAsync(string path, string jsonBody) {
            try {
                using (UnityWebRequest req = new UnityWebRequest(BaseUrl + path, UnityWebRequest.kHttpVerbPOST)) {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    return await SendForTextAsync(req, path);
                }
            } catch (Exception e) {
                Debug.LogError($"VARCO 요청 중 예외 ({path}): {e.Message}");
                return null;
            }
        }

        // multipart/form-data POST 후 바이너리(이미지 등) 반환 (실패 시 null)
        public static async Task<byte[]> PostMultipartAsync(string path, List<IMultipartFormSection> form) {
            try {
                using (UnityWebRequest req = UnityWebRequest.Post(BaseUrl + path, form)) {
                    return await SendForBytesAsync(req, path);
                }
            } catch (Exception e) {
                Debug.LogError($"VARCO 요청 중 예외 ({path}): {e.Message}");
                return null;
            }
        }

        // multipart/form-data POST 후 JSON 문자열 반환 (실패 시 null)
        public static async Task<string> PostMultipartForJsonAsync(string path, List<IMultipartFormSection> form) {
            byte[] bytes = await PostMultipartAsync(path, form);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }

        // GET 후 JSON 문자열 반환 (실패 시 null)
        public static async Task<string> GetAsync(string path) {
            try {
                using (UnityWebRequest req = UnityWebRequest.Get(BaseUrl + path)) {
                    return await SendForTextAsync(req, path);
                }
            } catch (Exception e) {
                Debug.LogError($"VARCO 요청 중 예외 ({path}): {e.Message}");
                return null;
            }
        }

        // 절대 URL에서 바이너리 다운로드 (3D 모델 GLB 등, 실패 시 null)
        public static async Task<byte[]> DownloadAsync(string absoluteUrl) {
            try {
                using (UnityWebRequest req = UnityWebRequest.Get(absoluteUrl)) {
                    req.timeout = TimeoutSeconds;
                    await AwaitRequest(req);
                    if (req.result != UnityWebRequest.Result.Success) {
                        Debug.LogError($"VARCO 파일 다운로드 실패 ({absoluteUrl}): {req.responseCode} {req.error}");
                        return null;
                    }
                    return req.downloadHandler.data;
                }
            } catch (Exception e) {
                Debug.LogError($"VARCO 다운로드 중 예외: {e.Message}");
                return null;
            }
        }

        private static async Task<string> SendForTextAsync(UnityWebRequest req, string path) {
            byte[] bytes = await SendCoreAsync(req, path);
            return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
        }

        private static Task<byte[]> SendForBytesAsync(UnityWebRequest req, string path) {
            return SendCoreAsync(req, path);
        }

        private static async Task<byte[]> SendCoreAsync(UnityWebRequest req, string path) {
            if (string.IsNullOrEmpty(ApiKey)) {
                Debug.LogError("VARCO ApiKey가 설정되지 않았습니다. VarcoApiClient.ApiKey를 먼저 설정하세요.");
                return null;
            }
            req.SetRequestHeader("OPENAPI_KEY", ApiKey);
            req.timeout = TimeoutSeconds;
            await AwaitRequest(req);
            if (req.result != UnityWebRequest.Result.Success) {
                string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                Debug.LogError($"VARCO API 호출 실패 ({path}): HTTP {req.responseCode} {req.error} {body}");
                return null;
            }
            return req.downloadHandler.data;
        }

        // UnityWebRequest를 await 가능하게 변환 (RunContinuationsAsynchronously로 메인스레드 재개 보장)
        private static Task AwaitRequest(UnityWebRequest req) {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            req.SendWebRequest().completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }

    // JsonUtility 보조 - 최상위 배열 응답 파싱용
    public static class VarcoJson {
        // JsonUtility가 최상위 배열을 못 읽으므로 래퍼 객체로 감싸 파싱
        public static T[] FromJsonArray<T>(string json) {
            try {
                string wrapped = "{\"items\":" + json + "}";
                return JsonUtility.FromJson<Wrapper<T>>(wrapped).items;
            } catch (Exception e) {
                Debug.LogError($"VARCO 배열 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        [Serializable]
        private class Wrapper<T> {
            public T[] items;
        }
    }
}
