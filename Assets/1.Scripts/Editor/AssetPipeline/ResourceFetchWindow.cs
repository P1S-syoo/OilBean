using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor {
    // 리소스 조달 창 — 클린 카탈로그를 보고 라이선스 확인 후 다운로드
    public class ResourceFetchWindow : EditorWindow {
        List<AssetEntry> items;
        Vector2 scroll;
        string fsQuery = "underwater ambience";   // Freesound 검색어
        int fsCount = 3;

        // [MenuItem("Game/리소스 조달 파이프라인")]
        static void Open() {
            GetWindow<ResourceFetchWindow>("리소스 조달").Show();
        }

        void OnEnable() {
            items = AssetCatalog.Default();
        }

        void OnGUI() {
            EditorGUILayout.HelpBox("CC0/OFL 등 라이선스 클린 에셋만 조달합니다. CC-BY-NC/SA·GPL은 자동 차단됩니다.", MessageType.Info);
            if (items == null) {
                items = AssetCatalog.Default();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var e in items) {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(e.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{e.license} · {e.author}");
                EditorGUILayout.LabelField(e.savePath, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                bool blocked = AssetCatalog.IsBlocked(e.license);
                GUI.enabled = !blocked;
                if (GUILayout.Button(blocked ? "차단됨" : "다운로드", GUILayout.Width(80), GUILayout.Height(42))) {
                    AssetFetcher.Fetch(e);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Freesound 섹션 — 토큰은 이 머신(EditorPrefs)에만 저장(코드/레포 비노출)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Freesound (CC0 사운드)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("API 토큰은 이 PC(EditorPrefs)에만 저장됩니다. freesound.org/apiv2/apply 에서 발급한 Api key를 붙여넣으세요.", MessageType.Info);
            string tok = FreesoundFetcher.Token;
            string newTok = EditorGUILayout.PasswordField("API 토큰", tok);
            if (newTok != tok) {
                FreesoundFetcher.Token = newTok;
            }
            fsQuery = EditorGUILayout.TextField("검색어", fsQuery);
            fsCount = EditorGUILayout.IntSlider("개수", fsCount, 1, 10);
            GUI.enabled = !string.IsNullOrEmpty(FreesoundFetcher.Token);
            if (GUILayout.Button("CC0 검색 + 다운로드", GUILayout.Height(28))) {
                int n = FreesoundFetcher.SearchAndFetch(fsQuery, fsCount);
                ShowNotification(new GUIContent($"Freesound {n}개 다운로드"));
            }
            GUI.enabled = true;
        }
    }
}
