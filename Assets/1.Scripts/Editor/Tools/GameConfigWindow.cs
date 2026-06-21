using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Core;

namespace Game.Editor.Tools {
    // 통합 설정 도구 — GameConfig 자산을 피쳐별 섹션으로 한 창에서 편집하고 씬 컴포넌트에 일괄 배선
    public class GameConfigWindow : EditorWindow {
        const string ConfigPath = "Assets/3.Data/GameConfig.asset";

        GameConfig config;
        SerializedObject so;
        Vector2 scroll;

        // 섹션 폴드아웃 상태 — 헤더 텍스트 기준
        readonly Dictionary<string, bool> folds = new();

        [MenuItem("Tools/한강/통합 설정")]
        public static void Open() {
            var win = GetWindow<GameConfigWindow>("통합 설정");
            win.minSize = new Vector2(420f, 480f);
            win.Reload();
        }

        void OnEnable() {
            Reload();
        }

        // 자산 로드(없으면 null 유지 — 생성 버튼 노출)
        void Reload() {
            try {
                config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
                so = config != null ? new SerializedObject(config) : null;
            } catch (Exception e) {
                Debug.LogError($"[GameConfigWindow] 자산 로드 실패: {e.Message}");
            }
        }

        void OnGUI() {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("한강 통합 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            // 런타임 1회 적용(Awake/스폰 주입) 안내 — 플레이 중 변경은 재시작 후 반영
            EditorGUILayout.HelpBox("플레이 중 변경은 재시작 후 반영됩니다 (값은 Awake에서 1회 적용).", MessageType.Info);
            EditorGUILayout.Space(4f);

            if (config == null) {
                EditorGUILayout.HelpBox($"GameConfig 자산이 없습니다.\n경로: {ConfigPath}", MessageType.Info);
                if (GUILayout.Button("GameConfig 자산 생성", GUILayout.Height(28f))) {
                    CreateConfig();
                }
                return;
            }

            so.Update();

            using (var scope = new EditorGUILayout.ScrollViewScope(scroll)) {
                scroll = scope.scrollPosition;
                DrawGroupedFields();
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("씬 컴포넌트에 config 일괄 배선", GUILayout.Height(26f))) {
                    WireSceneComponents();
                }
                if (GUILayout.Button("저장", GUILayout.Width(80f), GUILayout.Height(26f))) {
                    Save();
                }
            }

            // 변경 즉시 반영(저장은 버튼 또는 SaveAssets로 디스크 기록)
            if (so.ApplyModifiedProperties()) {
                EditorUtility.SetDirty(config);
            }
        }

        // [Header]를 폴드아웃 섹션으로 환산해 모든 직렬화 필드를 그룹별로 그림
        void DrawGroupedFields() {
            var it = so.GetIterator();
            bool enterChildren = true;
            string currentGroup = "기타";
            bool currentOpen = true;
            while (it.NextVisible(enterChildren)) {
                enterChildren = false;
                if (it.propertyPath == "m_Script") {
                    continue;
                }
                // 필드의 [Header] 속성으로 섹션 분리 — 리플렉션으로 헤더 텍스트 추출
                string header = HeaderOf(it.name);
                if (header != null) {
                    currentGroup = header;
                    if (!folds.TryGetValue(currentGroup, out currentOpen)) {
                        currentOpen = true;
                        folds[currentGroup] = true;
                    }
                    EditorGUILayout.Space(2f);
                    folds[currentGroup] = EditorGUILayout.Foldout(folds[currentGroup], currentGroup, true, EditorStyles.foldoutHeader);
                    currentOpen = folds[currentGroup];
                }
                if (!currentOpen) {
                    continue;
                }
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(it, true);
                EditorGUI.indentLevel--;
            }
        }

        // 필드명에 붙은 [Header] 속성 텍스트 조회(없으면 null)
        static string HeaderOf(string fieldName) {
            try {
                var f = typeof(GameConfig).GetField(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f == null) {
                    return null;
                }
                var attrs = f.GetCustomAttributes(typeof(HeaderAttribute), false);
                return attrs.Length > 0 ? ((HeaderAttribute)attrs[0]).header : null;
            } catch (Exception e) {
                Debug.LogError($"[GameConfigWindow] 헤더 조회 실패({fieldName}): {e.Message}");
                return null;
            }
        }

        // GameConfig 자산 생성(3.Data 폴더에)
        void CreateConfig() {
            try {
                if (!AssetDatabase.IsValidFolder("Assets/3.Data")) {
                    AssetDatabase.CreateFolder("Assets", "3.Data");
                }
                var asset = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(asset, ConfigPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Reload();
                Debug.Log($"[GameConfigWindow] GameConfig 생성: {ConfigPath}");
            } catch (Exception e) {
                Debug.LogError($"[GameConfigWindow] 자산 생성 실패: {e.Message}");
            }
        }

        // 디스크 저장
        void Save() {
            try {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log("[GameConfigWindow] 저장 완료");
            } catch (Exception e) {
                Debug.LogError($"[GameConfigWindow] 저장 실패: {e.Message}");
            }
        }

        // 메뉴에서 직접 씬 배선(창을 열지 않고도 실행) — 자산 자동 로드 후 위임
        [MenuItem("Tools/한강/통합 설정 — 씬 일괄 배선")]
        public static void WireSceneComponentsMenu() {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (cfg == null) {
                Debug.LogWarning($"[GameConfigWindow] config 미생성 — 먼저 {ConfigPath} 생성 필요");
                return;
            }
            WireSceneComponents(cfg);
        }

        // 인스턴스 진입점 — 현재 로드된 config로 배선
        void WireSceneComponents() {
            if (config == null) {
                Debug.LogWarning("[GameConfigWindow] config 미생성 — 먼저 자산을 생성하세요.");
                return;
            }
            WireSceneComponents(config);
        }

        // 씬의 대상 컴포넌트들에 config 참조를 SerializedObject로 주입 — GameConfig 타입 config 필드를 가진 것만
        static void WireSceneComponents(GameConfig config) {
            try {
                int wired = 0;
                var comps = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in comps) {
                    if (c == null) {
                        continue;
                    }
                    // 필드 타입이 GameConfig인지 리플렉션으로 확인(동명의 다른 타입 config 필드 오염 방지)
                    var field = c.GetType().GetField("config",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field == null || field.FieldType != typeof(GameConfig)) {
                        continue;
                    }
                    var cso = new SerializedObject(c);
                    var prop = cso.FindProperty("config");
                    if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) {
                        continue;
                    }
                    if (prop.objectReferenceValue == config) {
                        continue;
                    }
                    prop.objectReferenceValue = config;
                    cso.ApplyModifiedProperties();
                    EditorUtility.SetDirty(c);
                    wired++;
                }
                if (wired > 0) {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }
                Debug.Log($"[GameConfigWindow] 씬 배선 완료 — {wired}개 컴포넌트에 config 주입");
            } catch (Exception e) {
                Debug.LogError($"[GameConfigWindow] 씬 배선 실패: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
