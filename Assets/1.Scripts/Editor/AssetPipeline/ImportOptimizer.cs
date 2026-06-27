using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor {
    // 에셋 임포트 경량화 + 빌드 씬 교정 유틸 — 멱등(여러 번 실행해도 안전)
    public static class ImportOptimizer {
        private const string AudioBgmDir = "Assets/5.Audio/BGM";
        private const string AudioVoiceDir = "Assets/5.Audio/Voice";
        private const string MainScenePath = "Assets/2.Scenes/Main3D.unity";

        // 크런치 압축 품질(0~100) — 불투명 지형 albedo 전용
        private const int CrunchQuality = 75;

        // 크런치 대상은 불투명 Riverbed albedo만 — 노멀맵(_N)/SkyGradient/Backgrounds/UI 제외(알파 아티팩트 방지)
        private static readonly string[] CrunchTextures = new string[] {
            "Assets/4.Art/Textures/RiverbedSide.png",
            "Assets/4.Art/Textures/RiverbedSide1.png",
            "Assets/4.Art/Textures/RiverbedSide2.png",
            "Assets/4.Art/Textures/RiverbedTop.png",
            "Assets/4.Art/Textures/RiverbedTop1.png",
            "Assets/4.Art/Textures/RiverbedTop2.png",
            "Assets/4.Art/Textures/Riverbed/Main_Riverbed_albedo.png"
        };

        // 캐릭터 텍스처 maxSize 하향 대상(2048 -> 1024)
        private static readonly string[] CharTextures = new string[] {
            "Assets/4.Art/Characters/CharTextures/diffuse.png",
            "Assets/4.Art/Characters/CharTextures/normal.png"
        };

        // [MenuItem("Tools/한강/최적화/임포트 설정 적용")]
        public static void ApplyImportSettings() {
            var log = new List<string>();
            ApplyAudioSettings(log);
            ApplyTextureCrunch(log);
            ApplyCharTextureMaxSize(log);
            AssetDatabase.Refresh();
            Debug.Log($"[ImportOptimizer] 임포트 설정 적용 완료 ({log.Count}건)\n" + string.Join("\n", log));
        }

        // [MenuItem("Tools/한강/최적화/빌드 씬 교정")]
        public static void FixBuildScenes() {
            try {
                if (!File.Exists(MainScenePath)) {
                    Debug.LogError($"[ImportOptimizer] 메인 씬을 찾을 수 없음: {MainScenePath}");
                    return;
                }
                // Main3D만 enabled로 빌드 씬 목록 재설정(스텁 씬 제거)
                EditorBuildSettings.scenes = new EditorBuildSettingsScene[] {
                    new EditorBuildSettingsScene(MainScenePath, true)
                };
                Debug.Log($"[ImportOptimizer] 빌드 씬 교정 완료 -> {MainScenePath} (enabled)");
            } catch (Exception e) {
                Debug.LogError($"[ImportOptimizer] 빌드 씬 교정 실패: {e.Message}");
            }
        }

        // [MenuItem("Tools/한강/최적화/전체 적용 (임포트+빌드씬)")]
        public static void ApplyAll() {
            ApplyImportSettings();
            FixBuildScenes();
        }

        // BGM은 스트리밍, Voice는 압축 인메모리(Vorbis) — 백그라운드 로드
        private static void ApplyAudioSettings(List<string> log) {
            ApplyAudioInDir(AudioBgmDir, AudioClipLoadType.Streaming, log);
            ApplyAudioInDir(AudioVoiceDir, AudioClipLoadType.CompressedInMemory, log);
        }

        private static void ApplyAudioInDir(string dir, AudioClipLoadType loadType, List<string> log) {
            try {
                if (!Directory.Exists(dir)) {
                    log.Add($"SKIP(폴더 없음): {dir}");
                    return;
                }
                string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { dir });
                foreach (string guid in guids) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) {
                        continue;
                    }
                    AudioImporterSampleSettings s = importer.defaultSampleSettings;
                    // 이미 동일하면 재임포트 생략(멱등)
                    bool needsChange = s.loadType != loadType || !importer.loadInBackground;
                    if (!needsChange) {
                        log.Add($"AUDIO(변경없음): {path}");
                        continue;
                    }
                    s.loadType = loadType;
                    importer.defaultSampleSettings = s;
                    importer.loadInBackground = true;
                    importer.SaveAndReimport();
                    log.Add($"AUDIO[{loadType}]: {path}");
                }
            } catch (Exception e) {
                Debug.LogError($"[ImportOptimizer] 오디오 설정 적용 실패({dir}): {e.Message}");
            }
        }

        // 불투명 Riverbed albedo에만 크런치 압축 적용(노멀맵/Sky/Backgrounds/UI는 목록에서 제외됨)
        private static void ApplyTextureCrunch(List<string> log) {
            foreach (string path in CrunchTextures) {
                try {
                    if (!File.Exists(path)) {
                        log.Add($"SKIP(파일 없음): {path}");
                        continue;
                    }
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) {
                        log.Add($"SKIP(텍스처 아님): {path}");
                        continue;
                    }
                    bool needsChange = !importer.crunchedCompression || importer.compressionQuality != CrunchQuality;
                    if (!needsChange) {
                        log.Add($"TEX(변경없음): {path}");
                        continue;
                    }
                    importer.crunchedCompression = true;
                    importer.compressionQuality = CrunchQuality;
                    importer.SaveAndReimport();
                    log.Add($"TEX[crunch q{CrunchQuality}]: {path}");
                } catch (Exception e) {
                    Debug.LogError($"[ImportOptimizer] 텍스처 크런치 적용 실패({path}): {e.Message}");
                }
            }
        }

        // 캐릭터 텍스처 maxSize 1024로 하향
        private static void ApplyCharTextureMaxSize(List<string> log) {
            foreach (string path in CharTextures) {
                try {
                    if (!File.Exists(path)) {
                        log.Add($"SKIP(파일 없음): {path}");
                        continue;
                    }
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) {
                        log.Add($"SKIP(텍스처 아님): {path}");
                        continue;
                    }
                    if (importer.maxTextureSize <= 1024) {
                        log.Add($"CHARTEX(변경없음): {path}");
                        continue;
                    }
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                    log.Add($"CHARTEX[max1024]: {path}");
                } catch (Exception e) {
                    Debug.LogError($"[ImportOptimizer] 캐릭터 텍스처 maxSize 적용 실패({path}): {e.Message}");
                }
            }
        }
    }
}
