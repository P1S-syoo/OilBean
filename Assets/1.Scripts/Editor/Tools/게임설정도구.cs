using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Core;

namespace Game.Editor.Tools {
    // 게임 설정 도구 — 10개 카테고리 설정 .asset 생성 + 씬 컴포넌트의 config 필드를 타입별로 자동 배선
    // (옛 통합 설정 창의 씬 배선 패턴 계승 — 단일 통합 자산 대신 카테고리별 자산 주입)
    public class 게임설정도구 : EditorWindow {
        const string ConfigDir = "Assets/3.Data/Config";

        // 생성·배선 대상 10개 카테고리 타입
        static readonly Type[] CategoryTypes = {
            typeof(수면위설정),
            typeof(잠수설정),
            typeof(월드설정),
            typeof(오디오설정),
            typeof(위험설정),
            typeof(수집설정),
            typeof(제작설정),
            typeof(점수설정),
            typeof(연출설정),
            typeof(디버그설정),
        };

        [MenuItem("게임설정/설정 생성 및 배선")]
        public static void Open() {
            var win = GetWindow<게임설정도구>("게임 설정 도구");
            win.minSize = new Vector2(360f, 200f);
        }

        public static void ApplyWiring() {
            CreateAndWire();
        }

        void OnGUI() {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("게임 설정 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("10개 카테고리 설정 자산을 생성하고, 씬의 컴포넌트 config 필드를 타입에 맞는 자산으로 자동 배선합니다.", MessageType.Info);
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("설정 생성 및 배선", GUILayout.Height(32f))) {
                CreateAndWire();
            }
        }

        // 메뉴에서 직접 실행(창 없이) — 생성 + 배선 일괄
        static void CreateAndWire() {
            try {
                var assets = CreateConfigAssets(out int created);
                int wiredScene = WireSceneComponents(assets);
                int wiredAsset = WireScriptableObjectAssets(assets);   // RunData 등 SO 에셋의 config도 배선
                // 씬·에셋 저장 — 수동 Ctrl+S 누락으로 배선 유실 방지
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                Debug.Log($"[게임설정도구] 완료 — 자산 생성 {created}개 / 씬 배선 {wiredScene}개 / SO 배선 {wiredAsset}개");
            } catch (Exception e) {
                Debug.LogError($"[게임설정도구] 실행 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 프로젝트의 ScriptableObject 에셋(RunData 등) 중 config 필드가 카테고리 타입인 것 배선 — 씬 스캔이 못 닿는 SO 보강
        static int WireScriptableObjectAssets(Dictionary<Type, ScriptableObject> assets) {
            int wired = 0;
            try {
                var guids = AssetDatabase.FindAssets("t:ScriptableObject");
                foreach (var guid in guids) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (WireConfigField(so, assets)) {
                        wired++;
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[게임설정도구] SO 배선 실패: {e.Message}\n{e.StackTrace}");
            }
            return wired;
        }

        // 한 객체의 설정 타입 필드를 모두 찾아 해당 자산을 주입 — 여러 설정 참조를 가진 HUD까지 배선
        static bool WireConfigField(UnityEngine.Object obj, Dictionary<Type, ScriptableObject> assets) {
            if (obj == null) {
                return false;
            }
            var sso = new SerializedObject(obj);
            bool changed = false;
            var flags = System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;
            foreach (var field in obj.GetType().GetFields(flags)) {
                if (!assets.TryGetValue(field.FieldType, out var asset) || asset == null || asset == obj) {
                    continue;
                }
                var prop = sso.FindProperty(field.Name);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue == asset) {
                    continue;
                }
                prop.objectReferenceValue = asset;
                changed = true;
            }
            if (!changed) {
                return false;
            }
            sso.ApplyModifiedProperties();
            EditorUtility.SetDirty(obj);
            return true;
        }

        // 10개 카테고리 .asset 생성(없을 때만) — 타입→자산 매핑 반환
        static Dictionary<Type, ScriptableObject> CreateConfigAssets(out int created) {
            created = 0;
            var map = new Dictionary<Type, ScriptableObject>();
            try {
                // 폴더 보장(Assets/3.Data/Config)
                if (!AssetDatabase.IsValidFolder("Assets/3.Data")) {
                    AssetDatabase.CreateFolder("Assets", "3.Data");
                }
                if (!AssetDatabase.IsValidFolder(ConfigDir)) {
                    AssetDatabase.CreateFolder("Assets/3.Data", "Config");
                }
                foreach (var t in CategoryTypes) {
                    string path = $"{ConfigDir}/{t.Name}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (existing == null) {
                        existing = ScriptableObject.CreateInstance(t);
                        AssetDatabase.CreateAsset(existing, path);
                        created++;
                    }
                    map[t] = existing;
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            } catch (Exception e) {
                Debug.LogError($"[게임설정도구] 자산 생성 실패: {e.Message}");
            }
            return map;
        }

        // 씬의 모든 컴포넌트를 순회하며 config 필드 타입과 일치하는 카테고리 자산을 주입 — 배선 개수 반환
        static int WireSceneComponents(Dictionary<Type, ScriptableObject> assets) {
            int wired = 0;
            try {
                var comps = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in comps) {
                    if (WireConfigField(c, assets)) {
                        wired++;
                    }
                }
                if (wired > 0) {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }
            } catch (Exception e) {
                Debug.LogError($"[게임설정도구] 씬 배선 실패: {e.Message}\n{e.StackTrace}");
            }
            return wired;
        }
    }
}
