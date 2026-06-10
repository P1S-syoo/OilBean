using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Varco {
    // TTS 품질 등급 (lite: 저렴/빠름, standard: 고품질)
    public enum VarcoTtsTier {
        Lite,
        Standard
    }

    // Voice TTS API 행동단위 래퍼 - 텍스트를 음성으로 합성
    public static class VarcoTts {
        // 텍스트를 음성으로 합성 (voice는 화자 speaker_uuid, text 최대 1200바이트/SSML 가능, 실패 시 null)
        public static async Task<AudioClip> Synthesize(
            string text,
            string speakerUuid,
            VarcoTtsTier tier = VarcoTtsTier.Lite,
            string language = "korean",
            float speed = 1f,
            float pitch = 1f,
            int nFmSteps = 8,
            int seed = -1) {
            var body = new SynthesizeRequest {
                text = text,
                voice = speakerUuid,
                language = language,
                properties = new VoiceProperties { speed = speed, pitch = pitch },
                n_fm_steps = nFmSteps,
                seed = seed,
                return_metadata = false,
                media_type = "wav"
            };
            string json = await VarcoApiClient.PostJsonAsync($"{PathOf(tier)}/synthesize", JsonUtility.ToJson(body));
            if (json == null) {
                return null;
            }
            try {
                SynthesizeResponse res = JsonUtility.FromJson<SynthesizeResponse>(json);
                if (res == null || string.IsNullOrEmpty(res.audio)) {
                    Debug.LogError("TTS 응답 파싱 실패: audio 필드가 없거나 비어 있습니다");
                    return null;
                }
                return VarcoAudioUtil.ClipFromWavBase64(res.audio, "varco_tts");
            } catch (Exception e) {
                Debug.LogError($"TTS 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        // 사용 가능한 화자(speaker_uuid) 목록을 원본 JSON으로 반환 (실패 시 null)
        public static Task<string> GetVoicesJson(VarcoTtsTier tier = VarcoTtsTier.Lite) {
            return VarcoApiClient.GetAsync($"{PathOf(tier)}/voices/varco");
        }

        private static string PathOf(VarcoTtsTier tier) {
            return tier == VarcoTtsTier.Lite ? "/tts/lite/v1/api" : "/tts/standard/v1/api";
        }

        [Serializable] private class SynthesizeRequest { public string text; public string voice; public string language; public VoiceProperties properties; public int n_fm_steps; public int seed; public bool return_metadata; public string media_type; }
        [Serializable] private class VoiceProperties { public float speed; public float pitch; }
        [Serializable] private class SynthesizeResponse { public string audio; public string media_type; }
    }
}
