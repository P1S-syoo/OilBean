using System;
using UnityEditor;
using UnityEngine;
using Game.Audio;

namespace Game.Editor.Audio {
    // GameAudio 씬 배선 도구 — 오브젝트 생성 + 클립/참조 일괄 할당
    public static class AudioWirer {
        const string ClipRoot = "Assets/5.Audio";

        [MenuItem("Tools/한강/오디오 배선")]
        public static void Wire() {
            try {
                var go = GameObject.Find("GameAudio");
                if (go == null) {
                    go = new GameObject("GameAudio");
                    Undo.RegisterCreatedObjectUndo(go, "오디오 배선");
                }
                var audio = go.GetComponent<GameAudio>();
                if (audio == null) {
                    audio = go.AddComponent<GameAudio>();
                }
                // 씬에 AudioListener가 하나도 없으면 소리가 안 들림 — 2D 사운드라 위치 무관하게 여기 부착
                if (UnityEngine.Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) == null) {
                    go.AddComponent<AudioListener>();
                    Debug.Log("[AudioWirer] AudioListener 없음 — GameAudio에 추가");
                }

                var so = new SerializedObject(audio);
                int missing = 0;
                // 씬 참조 — 비활성 포함 검색(부팅 순서에 따라 꺼져 있을 수 있음)
                missing += SetRef<Game.Core.GameBootstrap>(so, "bootstrap");
                missing += SetRef<Game.Items.Collector>(so, "collector");
                missing += SetRef<Game.Player.Battery>(so, "battery");
                missing += SetRef<Game.Player.HazardDetector>(so, "hazard");
                missing += SetRef<Game.Stage.PurifyInstaller>(so, "purify");
                missing += SetRef<Game.Craft.Crafting>(so, "crafting");
                missing += SetRef<Game.Craft.Research>(so, "research");
                // 베이크된 클립 — 5.Audio 폴더의 고정 경로 규약
                missing += SetClip(so, "bgmDive", "BGM/bgm_dive");
                missing += SetClip(so, "bgmSurface", "BGM/bgm_surface");
                missing += SetClip(so, "sfxCollect", "SFX/sfx_collect");
                missing += SetClip(so, "sfxInvFull", "SFX/sfx_inv_full");
                missing += SetClip(so, "sfxHazardHit", "SFX/sfx_hazard_hit");
                missing += SetClip(so, "sfxPurifyDone", "SFX/sfx_purify_done");
                missing += SetClip(so, "sfxStageClear", "SFX/sfx_stage_clear");
                missing += SetClip(so, "sfxToast", "SFX/sfx_toast");
                missing += SetClip(so, "sfxBatteryLow", "SFX/sfx_battery_low");
                missing += SetClip(so, "sfxDive", "SFX/sfx_dive");
                missing += SetClip(so, "sfxCraftDenied", "SFX/sfx_craft_denied");
                missing += SetClip(so, "sfxCraftDone", "SFX/sfx_craft_done");
                missing += SetClip(so, "sfxMgHit", "SFX/sfx_mg_hit");
                missing += SetClip(so, "sfxMgMiss", "SFX/sfx_mg_miss");
                missing += SetClip(so, "sfxNodePass", "SFX/sfx_node_pass");
                missing += SetClip(so, "sfxPuzzleDone", "SFX/sfx_puzzle_done");
                missing += SetClip(so, "sfxSlotPlace", "SFX/sfx_slot_place");
                missing += SetClip(so, "sfxHazardWarn", "SFX/sfx_hazard_warn");
                missing += SetClip(so, "sfxUiClick", "SFX/sfx_ui_click");
                missing += SetClip(so, "voiceIntro", "Voice/voice_intro");
                missing += SetClip(so, "voiceDive", "Voice/voice_dive");
                missing += SetClip(so, "voiceInvFull", "Voice/voice_inv_full");
                missing += SetClip(so, "voiceBattery", "Voice/voice_battery");
                missing += SetClip(so, "voiceHazard", "Voice/voice_hazard");
                missing += SetClip(so, "voiceBuoy", "Voice/voice_buoy");
                missing += SetClip(so, "voicePurify", "Voice/voice_purify");
                missing += SetClip(so, "voiceClear", "Voice/voice_clear");
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(go);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
                Debug.Log($"[AudioWirer] 오디오 배선 완료 — 누락 {missing}건 (씬 저장 필요)");
            } catch (Exception e) {
                Debug.LogError($"[AudioWirer] 배선 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 씬 컴포넌트 참조 할당 — 없으면 경고 후 1 반환
        static int SetRef<T>(SerializedObject so, string field) where T : UnityEngine.Object {
            var target = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            so.FindProperty(field).objectReferenceValue = target;
            if (target == null) {
                Debug.LogWarning($"[AudioWirer] 씬에서 {typeof(T).Name} 미발견 — {field} 비연결");
                return 1;
            }
            return 0;
        }

        // 오디오 클립 할당 — 경로 규약: Assets/5.Audio/<폴더>/<이름>.wav
        static int SetClip(SerializedObject so, string field, string relPath) {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipRoot}/{relPath}.wav");
            so.FindProperty(field).objectReferenceValue = clip;
            if (clip == null) {
                Debug.LogWarning($"[AudioWirer] 클립 미발견: {relPath}.wav — {field} 비연결");
                return 1;
            }
            return 0;
        }
    }
}
