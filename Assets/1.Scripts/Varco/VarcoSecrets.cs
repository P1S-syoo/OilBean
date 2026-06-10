using System;
using UnityEngine;

namespace Varco {
    // 환경변수(VARCO_OPENAPI_KEY)에서 API 키를 읽어 주입 - 키를 코드/에셋에 두지 않기 위한 장치
    public static class VarcoSecrets {
        public const string EnvVarName = "VARCO_OPENAPI_KEY";

        // 게임/에디터 시작 시 자동으로 키를 로드
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoad() {
            LoadApiKey();
        }

        // 프로세스 → 유저(레지스트리) 순으로 환경변수를 찾아 ApiKey 설정 (성공 여부 반환)
        public static bool LoadApiKey() {
            if (!string.IsNullOrEmpty(VarcoApiClient.ApiKey)) {
                return true;
            }
            try {
                string key = Environment.GetEnvironmentVariable(EnvVarName);
                if (string.IsNullOrEmpty(key)) {
                    // 에디터 실행 중 setx로 등록된 경우 프로세스 env에 없으므로 유저 스코프에서 재시도
                    key = Environment.GetEnvironmentVariable(EnvVarName, EnvironmentVariableTarget.User);
                }
                if (string.IsNullOrEmpty(key)) {
                    Debug.LogWarning($"환경변수 {EnvVarName}가 없습니다. VARCO API 호출 전 키를 설정하세요.");
                    return false;
                }
                VarcoApiClient.ApiKey = key.Trim();
                return true;
            } catch (Exception e) {
                Debug.LogError($"VARCO API 키 로드 실패: {e.Message}");
                return false;
            }
        }
    }
}
