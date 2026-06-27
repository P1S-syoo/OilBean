using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Surface;

namespace Game.Editor.Surface {
    // 수상 부유 잔해를 실물 FBX로 교체 — 강 따라 희소 산포 + 양화대교 잔해 주변 밀집 뗏목(가벼운 품목만 표류)
    public static class FloatingDebrisPlacer {
        const float RiverHalfWidth = 18f;
        const float BridgeX = 38f;        // 다리 잔해 X(BridgePlacer와 일치) — 표류 뗏목 중심
        const int TotalCount = 48;
        const int RaftCount = 20;         // 다리 주변 밀집 개수
        const float Draft = 0.25f;        // 수면 아래 살짝 잠김

        // 표류 가능한 가벼운 품목 — (FBX, 최장축 목표 m). 무거운 H빔·닻·맨홀은 가라앉으므로 제외(수중에서만 사용)
        static readonly (string path, float sizeM)[] Light = {
            ("Assets/NCAI_Asset/scrap/scrap_Can.fbx", 0.4f),
            ("Assets/NCAI_Asset/scrap/scrap_Cone.fbx", 0.8f),
            ("Assets/NCAI_Asset/scrap/scrap_Bottle.fbx", 0.4f),   // 없으면 자동 스킵
            ("Assets/NCAI_Asset/scrap/scrap_Cart.fbx", 1.4f),
            ("Assets/NCAI_Asset/scrap/scrap_Bicycle.fbx", 1.7f),
            ("Assets/NCAI_Asset/scrap/scrap_Wire.fbx", 1.0f),
            ("Assets/NCAI_Asset/sample/sample_chemicalbarrel.fbx", 1.2f),
            ("Assets/NCAI_Asset/sample/sample_oil.fbx", 1.0f),
            ("Assets/NCAI_Asset/sample/sample_detergent.fbx", 0.5f),
            ("Assets/NCAI_Asset/sample/sample_microplastic.fbx", 0.7f),
            ("Assets/NCAI_Asset/sample/sample_algae.fbx", 0.9f),
        };

        // [MenuItem("Tools/한강/부유 잔해 배치")]
        public static void Place() {
            try {
                var rig = GameObject.Find("SurfaceRig");
                if (rig == null) {
                    Debug.LogError("[FloatingDebrisPlacer] SurfaceRig가 없습니다 — Tools/한강/수상 리그 생성 먼저 실행");
                    return;
                }
                var river = rig.GetComponentInChildren<SplineContainer>();
                if (river == null) {
                    Debug.LogError("[FloatingDebrisPlacer] River 스플라인을 못 찾았습니다");
                    return;
                }

                // 프리팹 로드(없는 항목 제외)
                var prefabs = new System.Collections.Generic.List<(GameObject go, float sizeM)>();
                foreach (var l in Light) {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(l.path);
                    if (p != null) {
                        prefabs.Add((p, l.sizeM));
                    }
                }
                if (prefabs.Count == 0) {
                    Debug.LogError("[FloatingDebrisPlacer] 표류 가능 FBX를 하나도 못 찾음");
                    return;
                }

                // 기존 절차 잔해(SurfaceRigBuilder 생성) 또는 이전 실물 잔해 교체
                var env = rig.transform.Find("Environment");
                Transform host = env != null ? env : rig.transform;
                var old = host.Find("FloatingDebris");
                if (old != null) {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
                var root = new GameObject("FloatingDebris");
                root.transform.SetParent(host, false);
                Undo.RegisterCreatedObjectUndo(root, "부유 잔해 배치");

                float waterY = Game.World.WorldGen.WaterY + 0.45f;
                float riverLen = river.Spline.GetLength();
                int placed = 0;
                for (int i = 0; i < TotalCount; i++) {
                    int h = Hash(i, 53);
                    bool raft = i < RaftCount;

                    // 위치 — 뗏목은 다리 X 주변, 나머지는 스플라인 전체 t 분산
                    Vector3 pos, right;
                    if (raft) {
                        // 다리 주변 ±13u, 강폭 안에서 밀집
                        EvalNearX(river, BridgeX + ((h & 0xFF) / 255f - 0.5f) * 26f, riverLen, out pos, out right);
                    } else {
                        float t = ((h >> 8) & 0x3FF) / 1024f;
                        pos = (Vector3)river.EvaluatePosition(t);
                        var tan = (Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(t);
                        tan.y = 0f;
                        tan = tan.sqrMagnitude > 0.0001f ? tan.normalized : Vector3.forward;
                        right = Vector3.Cross(Vector3.up, tan).normalized;
                    }
                    float lateral = ((h >> 18) & 0x3FF) / 1024f * 2f - 1f;   // -1~1
                    float spread = raft ? 0.7f : 0.95f;
                    pos += right * (lateral * RiverHalfWidth * spread);
                    pos.y = waterY - Draft;

                    var pick = prefabs[((h >> 4) % prefabs.Count + prefabs.Count) % prefabs.Count];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pick.go, root.transform);
                    go.name = $"Float_{i:00}_{pick.go.name}";
                    go.transform.position = pos;

                    // 사이즈 — 최장축을 목표 m로(품목별 현실 크기)
                    float scale = MeasureScale(go, pick.sizeM);
                    go.transform.localScale *= scale;
                    // 무작위 yaw + 약간의 기울임(표류물 무드)
                    go.transform.rotation = Quaternion.Euler((h % 7) - 3f, h % 360, (h % 11) - 5f);

                    // 콜라이더 제거(장식)
                    foreach (var c in go.GetComponentsInChildren<Collider>()) {
                        UnityEngine.Object.DestroyImmediate(c);
                    }
                    // 파도 흔들림 — 개체별 변주
                    var bob = go.AddComponent<FloatBob>();
                    var bso = new SerializedObject(bob);
                    bso.FindProperty("amplitude").floatValue = 0.06f + (h % 5) * 0.02f;
                    bso.FindProperty("period").floatValue = 2.2f + (h % 7) * 0.35f;
                    bso.FindProperty("rollDegrees").floatValue = 4f + (h % 9);
                    bso.FindProperty("phase").floatValue = (h % 100) / 100f;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    placed++;
                }

                EditorUtility.SetDirty(rig);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
                Debug.Log($"[FloatingDebrisPlacer] 부유 잔해 {placed}개 배치 — 다리 뗏목 {RaftCount} + 강 분산 {TotalCount - RaftCount}, 품목 {prefabs.Count}종");
            } catch (Exception e) {
                Debug.LogError($"[FloatingDebrisPlacer] 배치 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 목표 월드 X에 가장 가까운 스플라인 지점 근사 — 샘플링으로 t 탐색
        static void EvalNearX(SplineContainer river, float targetX, float riverLen, out Vector3 pos, out Vector3 right) {
            float bestT = 0f, bestD = float.MaxValue;
            for (int k = 0; k <= 40; k++) {
                float t = k / 40f;
                float px = ((Vector3)river.EvaluatePosition(t)).x;
                float d = Mathf.Abs(px - targetX);
                if (d < bestD) {
                    bestD = d;
                    bestT = t;
                }
            }
            pos = (Vector3)river.EvaluatePosition(bestT);
            var tan = (Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(bestT);
            tan.y = 0f;
            tan = tan.sqrMagnitude > 0.0001f ? tan.normalized : Vector3.forward;
            right = Vector3.Cross(Vector3.up, tan).normalized;
        }

        // 최장축을 목표 m로 맞추는 스케일
        static float MeasureScale(GameObject go, float targetM) {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) {
                return 1f;
            }
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) {
                b.Encapsulate(rs[i].bounds);
            }
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            return longest > 0.0001f ? targetM / longest : 1f;
        }

        static int Hash(int a, int b) {
            unchecked {
                int h = a * 73856093 ^ b * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
