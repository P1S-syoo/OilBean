using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Varco {
    // 음성 변환 모델 종류 (Varco: 일반, Acting: 연기체 감정 표현 최적화)
    public enum VarcoVcModel {
        Varco,
        Acting
    }

    // Voice Conversion API 행동단위 래퍼 - 음성을 다른 화자 목소리로 변환
    public static class VarcoVoiceConversion {
        // 음성을 프리셋 화자 목소리로 변환 (입력 최대 60초, 실패 시 null)
        public static async Task<AudioClip> Convert(AudioClip audio, string speakerUuid, VarcoVcModel model = VarcoVcModel.Varco) {
            // WAV 인코딩을 백그라운드에서 처리
            string b64 = await VarcoAudioUtil.ClipToWavBase64Async(audio);
            if (b64 == null) {
                return null;
            }
            var body = new ConvertRequest { audio = b64, audio_name = audio.name, speaker_uuid = speakerUuid };
            string json = await VarcoApiClient.PostJsonAsync($"{PathOf(model)}/voice-conversion", JsonUtility.ToJson(body));
            return ToClip(json);
        }

        // 커스텀 화자 음성을 함께 업로드해 그 목소리로 변환 (각 입력 최대 60초, 실패 시 null)
        public static async Task<AudioClip> ConvertCustom(AudioClip audio, AudioClip speakerAudio, VarcoVcModel model = VarcoVcModel.Varco) {
            // WAV 인코딩을 백그라운드에서 처리
            string audioB64 = await VarcoAudioUtil.ClipToWavBase64Async(audio);
            string speakerB64 = await VarcoAudioUtil.ClipToWavBase64Async(speakerAudio);
            if (audioB64 == null || speakerB64 == null) {
                return null;
            }
            var body = new ConvertCustomRequest {
                audio = audioB64,
                audio_name = audio.name,
                speaker_audio = speakerB64,
                speaker_name = speakerAudio.name
            };
            string json = await VarcoApiClient.PostJsonAsync($"{PathOf(model)}/voice-conversion-custom", JsonUtility.ToJson(body));
            return ToClip(json);
        }

        // 사용 가능한 화자(speaker_uuid) 목록을 원본 JSON으로 반환 (실패 시 null)
        public static Task<string> GetVoicesJson(VarcoVcModel model = VarcoVcModel.Varco) {
            return VarcoApiClient.GetAsync($"{PathOf(model)}/voices");
        }

        private static string PathOf(VarcoVcModel model) {
            return model == VarcoVcModel.Varco ? "/vc/varco/v1/api" : "/vc/acting/v1/api";
        }

        private static AudioClip ToClip(string json) {
            if (json == null) {
                return null;
            }
            try {
                AudioResponse res = JsonUtility.FromJson<AudioResponse>(json);
                if (res == null || string.IsNullOrEmpty(res.audio)) {
                    Debug.LogError("음성 변환 응답 파싱 실패: audio 필드가 없거나 비어 있습니다");
                    return null;
                }
                return VarcoAudioUtil.ClipFromWavBase64(res.audio, "varco_vc");
            } catch (Exception e) {
                Debug.LogError($"음성 변환 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        [Serializable] private class ConvertRequest { public string audio; public string audio_name; public string speaker_uuid; }
        [Serializable] private class ConvertCustomRequest { public string audio; public string audio_name; public string speaker_audio; public string speaker_name; }
        [Serializable] private class AudioResponse { public string audio; }
    }
}
