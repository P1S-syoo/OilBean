using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Varco {
    // Sound API 행동단위 래퍼 - 효과음 생성/변형/후처리
    public static class VarcoSound {
        // 텍스트 프롬프트로 효과음 생성 (numSample 1~3, version v1/v2, 실패 시 null)
        public static async Task<AudioClip[]> TextToSound(string prompt, int numSample = 1, string version = "v1") {
            var body = new Text2SoundRequest { version = version, prompt = prompt, num_sample = numSample };
            string json = await VarcoApiClient.PostJsonAsync("/sound/varco/v1/api/text2sound", JsonUtility.ToJson(body));
            return ToClips(json, "text2sound");
        }

        // 원본 오디오의 변형 버전 생성 (numSample 1~5, strength 0~3, include로 변환 구간 지정, 실패 시 null)
        public static async Task<AudioClip[]> Variation(AudioClip source, int numSample = 1, float strength = 1f, float includeBegin = 0f, float includeEnd = 0f) {
            string b64 = VarcoAudioUtil.ClipToWavBase64(source);
            if (b64 == null) {
                return null;
            }
            var body = new VariationRequest {
                source = b64,
                num_sample = numSample,
                strength = strength,
                include = new TimeRange { begin = includeBegin, end = includeEnd }
            };
            string json = await VarcoApiClient.PostJsonAsync("/sound/varco/v1/api/variation", JsonUtility.ToJson(body));
            return ToClips(json, "variation");
        }

        // 모노 오디오를 스테레오로 변환 (실패 시 null)
        public static Task<AudioClip> MonoToStereo(AudioClip source) {
            return SingleSourceCall("/sound/varco/v1/api/mono2stereo", source, "mono2stereo");
        }

        // 끊김 없는 루프 사운드 생성 (preserve로 보존 구간 지정, 실패 시 null)
        public static async Task<AudioClip> Looping(AudioClip source, float preserveBegin = 0f, float preserveEnd = 0f) {
            string b64 = VarcoAudioUtil.ClipToWavBase64(source);
            if (b64 == null) {
                return null;
            }
            var body = new LoopingRequest { source = b64, preserve = new TimeRange { begin = preserveBegin, end = preserveEnd } };
            string json = await VarcoApiClient.PostJsonAsync("/sound/varco/v1/api/looping", JsonUtility.ToJson(body));
            return ToClip(json, "looping");
        }

        // 소스를 참조 오디오의 스타일로 변환 (ratio 0~2, 1.2 이상이면 참조 음색과 멀어질 수 있음, 실패 시 null)
        public static async Task<AudioClip> Conversion(AudioClip source, AudioClip reference, float ratio = 1f, bool enhance = false) {
            string srcB64 = VarcoAudioUtil.ClipToWavBase64(source);
            string refB64 = VarcoAudioUtil.ClipToWavBase64(reference);
            if (srcB64 == null || refB64 == null) {
                return null;
            }
            var body = new ConversionRequest { source = srcB64, reference = refB64, ratio = ratio, enhance = enhance };
            string json = await VarcoApiClient.PostJsonAsync("/sound/varco/v1/api/conversion", JsonUtility.ToJson(body));
            return ToClip(json, "conversion");
        }

        // 오디오 노이즈 제거 (44100Hz 16bit WAV로 반환, 실패 시 null)
        public static Task<AudioClip> Enhance(AudioClip source) {
            return SingleSourceCall("/sound/varco/v1/api/enhance", source, "enhance");
        }

        // source 하나만 보내는 공통 호출 처리
        private static async Task<AudioClip> SingleSourceCall(string path, AudioClip source, string name) {
            string b64 = VarcoAudioUtil.ClipToWavBase64(source);
            if (b64 == null) {
                return null;
            }
            var body = new SourceOnlyRequest { source = b64 };
            string json = await VarcoApiClient.PostJsonAsync(path, JsonUtility.ToJson(body));
            return ToClip(json, name);
        }

        // 단일 {audio} 응답을 AudioClip으로 변환
        private static AudioClip ToClip(string json, string name) {
            if (json == null) {
                return null;
            }
            try {
                AudioResponse res = JsonUtility.FromJson<AudioResponse>(json);
                if (res == null || string.IsNullOrEmpty(res.audio)) {
                    Debug.LogError($"사운드 응답 파싱 실패 ({name}): audio 필드가 없거나 비어 있습니다");
                    return null;
                }
                return VarcoAudioUtil.ClipFromWavBase64(res.audio, $"varco_{name}");
            } catch (Exception e) {
                Debug.LogError($"사운드 응답 파싱 실패 ({name}): {e.Message}");
                return null;
            }
        }

        // [{audio}] 배열 응답을 AudioClip 배열로 변환
        private static AudioClip[] ToClips(string json, string name) {
            if (json == null) {
                return null;
            }
            AudioResponse[] items = VarcoJson.FromJsonArray<AudioResponse>(json);
            if (items == null) {
                return null;
            }
            AudioClip[] clips = new AudioClip[items.Length];
            for (int i = 0; i < items.Length; i++) {
                try {
                    if (string.IsNullOrEmpty(items[i].audio)) {
                        Debug.LogError($"사운드 배열 응답 파싱 실패 ({name}[{i}]): audio 필드가 없거나 비어 있습니다");
                        clips[i] = null;
                    } else {
                        clips[i] = VarcoAudioUtil.ClipFromWavBase64(items[i].audio, $"varco_{name}_{i}");
                    }
                } catch (Exception e) {
                    Debug.LogError($"사운드 배열 응답 파싱 실패 ({name}[{i}]): {e.Message}");
                    clips[i] = null;
                }
            }
            return clips;
        }

        [Serializable] private class Text2SoundRequest { public string version; public string prompt; public int num_sample; }
        [Serializable] private class VariationRequest { public string source; public int num_sample; public float strength; public TimeRange include; }
        [Serializable] private class LoopingRequest { public string source; public TimeRange preserve; }
        [Serializable] private class ConversionRequest { public string source; public string reference; public float ratio; public bool enhance; }
        [Serializable] private class SourceOnlyRequest { public string source; }
        [Serializable] private class TimeRange { public float begin; public float end; }
        [Serializable] private class AudioResponse { public string audio; }
    }
}
