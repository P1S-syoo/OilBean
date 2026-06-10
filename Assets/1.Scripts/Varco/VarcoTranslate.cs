using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Varco {
    // Translate API 행동단위 래퍼 - 용어집 기반 텍스트 번역
    public static class VarcoTranslate {
        // 텍스트 번역 (언어코드: ko, en, ja, tw, cn, de, ru, es, pt, fr / provider: chat=채팅, content=콘텐츠, 실패 시 null)
        public static async Task<string> Translate(string sourceText, string targetLang, string sourceLang = "", string provider = "content") {
            var body = new TranslateRequest {
                TID = Guid.NewGuid().ToString(),
                svc = "varco-translation",
                provider = provider,
                source_lang = sourceLang,
                source_text = sourceText,
                target_lang = targetLang
            };
            string json = await VarcoApiClient.PostJsonAsync("/mt/chat-content/v1/translate", JsonUtility.ToJson(body));
            if (json == null) {
                return null;
            }
            try {
                TranslateResponse res = JsonUtility.FromJson<TranslateResponse>(json);
                return res.target_text;
            } catch (Exception e) {
                Debug.LogError($"번역 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        [Serializable] private class TranslateRequest { public string TID; public string svc; public string provider; public string source_lang; public string source_text; public string target_lang; }
        [Serializable] private class TranslateResponse { public string TID; public string svc; public string provider; public string source_lang; public string source_text; public string target_lang; public string target_text; }
    }
}
