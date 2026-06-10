using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Varco.Editor {
    // VARCO API 실호출 스모크 테스트 - 메뉴에서 실행, 결과는 콘솔 로그로 확인
    public static class VarcoApiSmokeTest {
        private const string OutputDir = "Assets/9.Tests/Varco";

        // 저비용 호출(번역 + 화자 목록 + 짧은 TTS)을 한 번에 검증
        [MenuItem("Varco/Smoke Test/Run All (translate + voices + tts)")]
        private static async void RunAll() {
            if (!VarcoSecrets.LoadApiKey()) {
                return;
            }
            Debug.Log("[VarcoSmoke] 시작 - 번역");
            string translated = await VarcoTranslate.Translate("Hello, adventurer!", "ko", "en");
            Debug.Log($"[VarcoSmoke] 번역 결과: {translated ?? "실패"}");

            Debug.Log("[VarcoSmoke] TTS 화자 목록 조회");
            string voices = await VarcoTts.GetVoicesJson(VarcoTtsTier.Lite);
            Debug.Log($"[VarcoSmoke] 화자 목록(앞 500자): {(voices == null ? "실패" : voices.Substring(0, Math.Min(500, voices.Length)))}");

            string speakerUuid = ExtractFirstUuid(voices);
            if (speakerUuid == null) {
                Debug.LogError("[VarcoSmoke] 화자 uuid를 찾지 못해 TTS 합성을 건너뜁니다");
                return;
            }
            Debug.Log($"[VarcoSmoke] TTS 합성 (화자: {speakerUuid})");
            AudioClip clip = await VarcoTts.Synthesize("바르코 연동 테스트입니다.", speakerUuid);
            if (clip == null) {
                Debug.LogError("[VarcoSmoke] TTS 합성 실패");
                return;
            }
            SaveClip(clip, "tts_test.wav");
            Debug.Log($"[VarcoSmoke] TTS 성공 - 길이 {clip.length:F2}초, {clip.frequency}Hz, {clip.channels}ch → {OutputDir}/tts_test.wav");
        }

        // 효과음 생성 테스트 (크레딧 소모가 있어 별도 메뉴로 분리)
        [MenuItem("Varco/Smoke Test/Text2Sound (크레딧 소모 주의)")]
        private static async void TestText2Sound() {
            if (!VarcoSecrets.LoadApiKey()) {
                return;
            }
            Debug.Log("[VarcoSmoke] 효과음 생성 요청 (sword slash)");
            AudioClip[] clips = await VarcoSound.TextToSound("metallic sword slash", 1);
            if (clips == null || clips.Length == 0 || clips[0] == null) {
                Debug.LogError("[VarcoSmoke] 효과음 생성 실패");
                return;
            }
            SaveClip(clips[0], "text2sound_test.wav");
            Debug.Log($"[VarcoSmoke] 효과음 생성 성공 - 길이 {clips[0].length:F2}초 → {OutputDir}/text2sound_test.wav");
        }

        // 화자 목록 JSON에서 첫 uuid 추출 (스키마 미문서화 대응으로 정규식 사용)
        private static string ExtractFirstUuid(string json) {
            if (string.IsNullOrEmpty(json)) {
                return null;
            }
            var m = System.Text.RegularExpressions.Regex.Match(json, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value : null;
        }

        // 결과 오디오를 프로젝트 에셋으로 저장해 에디터에서 바로 들어볼 수 있게 함
        private static void SaveClip(AudioClip clip, string fileName) {
            try {
                byte[] wav = VarcoAudioUtil.ClipToWavBytes(clip);
                if (wav == null) {
                    return;
                }
                Directory.CreateDirectory(OutputDir);
                File.WriteAllBytes(Path.Combine(OutputDir, fileName), wav);
                AssetDatabase.Refresh();
            } catch (Exception e) {
                Debug.LogError($"테스트 오디오 저장 실패: {e.Message}");
            }
        }
    }
}
