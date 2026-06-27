using System;
using UnityEditor;
using UnityEngine;
using Game.Core;
using Game.World;

namespace Game.Editor.Data {
    // 수집물 스트리머 씬 배선 — CollectibleStreamer GO 생성 + 2.5D 플레이어 target + ItemDataTable 연결
    public static class CollectibleStreamerWirer {
        const string TablePath = "Assets/3.Data/ItemDataTable.asset";

        // [MenuItem("Tools/한강/데이터/수집물 스트리머 배선")]
        public static void Wire() {
            try {
                var existing = UnityEngine.Object.FindFirstObjectByType<CollectibleStreamer>();
                var streamer = existing;
                if (streamer == null) {
                    var go = new GameObject("CollectibleStreamer");
                    Undo.RegisterCreatedObjectUndo(go, "수집물 스트리머");
                    streamer = go.AddComponent<CollectibleStreamer>();
                }
                var player = UnityEngine.Object.FindFirstObjectByType<Game.Player.PlayerMove>();
                var table = AssetDatabase.LoadAssetAtPath<ItemDataTable>(TablePath);
                if (player == null) {
                    Debug.LogWarning("[CollectibleStreamerWirer] 2.5D 플레이어(PlayerMove)를 못 찾음 — target 수동 연결 필요");
                }
                if (table == null) {
                    Debug.LogError($"[CollectibleStreamerWirer] 테이블 없음: {TablePath} — 먼저 '아이템 테이블 생성' 실행");
                    return;
                }
                var so = new SerializedObject(streamer);
                so.FindProperty("target").objectReferenceValue = player != null ? player.transform : null;
                so.FindProperty("table").objectReferenceValue = table;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(streamer);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(streamer.gameObject.scene);
                Debug.Log($"[CollectibleStreamerWirer] 배선 완료 — target={(player != null ? player.name : "없음")}, table={table.items?.Length ?? 0}종");
            } catch (Exception e) {
                Debug.LogError($"[CollectibleStreamerWirer] 배선 실패: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
