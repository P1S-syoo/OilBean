using UnityEngine;
using UnityEditor;
using Game.Core;
using Game.Items;
using Game.World;
using Game.Narrative;

namespace Game.EditorTools {
    // E5(내레이션)·E6(깊이 해저드 스트리머) 씬 배선 — 단일 해저드 프리팹 생성 + 스트리머/내레이션 GO 배선
    public static class WorldNarrationBuilder {
        const string HAZARD_PREFAB = "Assets/3.Prefabs/Main3D/HazardUnit.prefab";
        const string WARN_SPRITE = "Assets/4.Art/UI/icons/icon_warning.png";

        [MenuItem("Tools/한강/World/E5·E6 배선")]
        public static void Build() {
            try {
                var prefab = EnsureHazardPrefab();
                BuildHazardStreamer(prefab);
                BuildNarration();
                Debug.Log("[WorldNarrationBuilder] E5·E6 배선 완료");
            } catch (System.Exception e) {
                Debug.LogError($"[WorldNarrationBuilder] 배선 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 스트리머가 클론할 단일 오염원 프리팹 생성(없으면) — Hazard + 트리거 콜라이더 + 경고 스프라이트
        static GameObject EnsureHazardPrefab() {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HAZARD_PREFAB);
            if (existing != null) {
                return existing;
            }
            var go = new GameObject("HazardUnit");
            try {
                var sr = go.AddComponent<SpriteRenderer>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WARN_SPRITE);
                if (sprite != null) {
                    sr.sprite = sprite;
                }
                sr.color = new Color(0.85f, 0.33f, 0.31f, 0.92f);   // 위험 적색
                go.transform.localScale = Vector3.one * 0.9f;
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.55f;
                var hz = go.AddComponent<Hazard>();
                // 좌우 순환 patrol(private 직렬화 필드) — SerializedObject로 주입
                var so = new SerializedObject(hz);
                var pts = so.FindProperty("points");
                if (pts != null) {
                    pts.arraySize = 2;
                    pts.GetArrayElementAtIndex(0).vector2Value = new Vector2(-1.6f, 0f);
                    pts.GetArrayElementAtIndex(1).vector2Value = new Vector2(1.6f, 0f);
                }
                var spd = so.FindProperty("speed");
                if (spd != null) {
                    spd.floatValue = 1.4f;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, HAZARD_PREFAB);
                Debug.Log("[WorldNarrationBuilder] HazardUnit 프리팹 생성");
                return prefab;
            } finally {
                Object.DestroyImmediate(go);
            }
        }

        // 깊이 해저드 스트리머 GO 생성 + target(Sub)/hazardPrefab 배선
        static void BuildHazardStreamer(GameObject prefab) {
            var existing = GameObject.Find("HazardStreamer");
            GameObject go = existing != null ? existing : new GameObject("HazardStreamer");
            if (existing == null) {
                Undo.RegisterCreatedObjectUndo(go, "HazardStreamer");
                var gameplay = GameObject.Find("Gameplay");
                if (gameplay != null) {
                    go.transform.SetParent(gameplay.transform, false);
                }
            }
            var hs = go.GetComponent<HazardStreamer>() ?? Undo.AddComponent<HazardStreamer>(go);
            var so = new SerializedObject(hs);
            var sub = GameObject.Find("Sub");
            if (sub != null) {
                AssignRef(so, "target", sub.transform);
            }
            AssignRef(so, "hazardPrefab", prefab);
        }

        // 내레이션 GO 생성(NarrationView + Controller). 컨트롤러는 bootstrap 자가탐색
        static void BuildNarration() {
            var existing = GameObject.Find("Narration");
            GameObject go = existing != null ? existing : new GameObject("Narration");
            if (existing == null) {
                Undo.RegisterCreatedObjectUndo(go, "Narration");
            }
            var view = go.GetComponent<NarrationView>() ?? Undo.AddComponent<NarrationView>(go);
            var ctrl = go.GetComponent<NarrationController>() ?? Undo.AddComponent<NarrationController>(go);
            var so = new SerializedObject(ctrl);
            AssignRef(so, "view", view);
            var game = GameObject.Find("Game");
            if (game != null) {
                var gb = game.GetComponent<GameBootstrap>();
                if (gb != null) {
                    AssignRef(so, "bootstrap", gb);
                }
            }
        }

        // 직렬화 참조 즉시 적용
        static void AssignRef(SerializedObject so, string propName, Object value) {
            var prop = so.FindProperty(propName);
            if (prop != null) {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            } else {
                Debug.LogWarning($"[WorldNarrationBuilder] 프로퍼티 '{propName}' 미발견");
            }
        }
    }
}
