using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Surface;

namespace Game.Editor.Surface {
    // 양화대교 파괴 모델(NCAI)을 강을 가로지르는 붕괴 스팬 랜드마크로 배치 — 수면 위 실루엣 + 수중 잔해밭 앵커
    public static class BridgePlacer {
        const string BridgePath = "Assets/NCAI_Asset/bridge/YangHwaBrindge_destruction.fbx";
        const float SpanT = 0.32f;        // 배치 지점(강 시작부 근처 — 초반 잠수에서 만나는 센터피스)
        const float TargetSpan = 34f;     // 가로 스팬(강폭 절반 넘게 가로지르는 붕괴 잔해 — 강 전체를 덮지 않음)
        const float SinkBelowDeck = 0.6f; // 상판을 수면 위로 띄우는 높이(나머지 구조는 수중 잔해)
        const float TiltDeg = 11f;        // 붕괴 기울기 — 무너져 한쪽으로 기운 잔해

        // [MenuItem("Tools/한강/양화대교 잔해 배치")]
        public static void Place() {
            try {
                var rig = GameObject.Find("SurfaceRig");
                if (rig == null) {
                    Debug.LogError("[BridgePlacer] SurfaceRig가 없습니다 — Tools/한강/수상 리그 생성 먼저 실행");
                    return;
                }
                var river = rig.GetComponentInChildren<SplineContainer>();
                if (river == null) {
                    Debug.LogError("[BridgePlacer] River 스플라인을 못 찾았습니다");
                    return;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePath);
                if (prefab == null) {
                    Debug.LogError($"[BridgePlacer] 모델 없음: {BridgePath}");
                    return;
                }

                // 호스트: Landmarks 아래(없으면 생성). 기존 다리만 교체(명소는 보존)
                var landmarks = rig.transform.Find("Landmarks");
                if (landmarks == null) {
                    var lm = new GameObject("Landmarks");
                    lm.transform.SetParent(rig.transform, false);
                    Undo.RegisterCreatedObjectUndo(lm, "Landmarks");
                    landmarks = lm.transform;
                }
                var old = landmarks.Find("Bridge_Yanghwa");
                if (old != null) {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, landmarks);
                go.name = "Bridge_Yanghwa";

                // 가장 긴 수평축을 TargetSpan으로 균일 스케일
                Bounds b = CalcBounds(go);
                float longest = Mathf.Max(b.size.x, b.size.z);
                if (longest < 0.0001f) {
                    Debug.LogError("[BridgePlacer] 다리 렌더러 바운즈가 비어 있음");
                    UnityEngine.Object.DestroyImmediate(go);
                    return;
                }
                float scale = TargetSpan / longest;
                go.transform.localScale = Vector3.one * scale;
                b = CalcBounds(go);

                // 스플라인 지점 + 가로(right=강 횡단) 방향
                Vector3 pos = (Vector3)river.EvaluatePosition(SpanT);
                Vector3 tan = (Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(SpanT);
                tan.y = 0f;
                tan = tan.sqrMagnitude > 0.0001f ? tan.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, tan).normalized;

                // 모델의 긴 축(현재 월드 X 또는 Z)을 right로 정렬하는 yaw
                bool spanIsX = b.size.x >= b.size.z;
                float baseYaw = Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg;   // +Z 기준
                float yaw = baseYaw + (spanIsX ? 90f : 0f);
                go.transform.rotation = Quaternion.Euler(TiltDeg, yaw, 0f);

                // 위치: 강 중심. 상판(윗면)을 수면 위 SinkBelowDeck만큼만 — 나머지 구조는 수중에 잠김
                b = CalcBounds(go);
                float waterTop = Game.World.WorldGen.WaterY + 0.45f;
                float topOffset = b.max.y - go.transform.position.y;
                pos.y = (waterTop + SinkBelowDeck) - topOffset;
                go.transform.position = pos;

                // 점유 구간을 스카이라인 스트리머에 추가(빌딩·VARCO 다리가 이 X를 비움) — 기존 zone 보존
                b = CalcBounds(go);
                float zoneRadius = Mathf.Max(b.extents.x, b.extents.z) + 10f;
                AppendZone(rig, new Vector3(b.center.x, zoneRadius, 0f));

                EditorUtility.SetDirty(rig);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
                Debug.Log($"[BridgePlacer] 양화대교 잔해 배치 — span {TargetSpan}u (scale {scale:F2}), x={b.center.x:F0}±{zoneRadius:F0}, 상판 y={b.max.y:F1}");
            } catch (Exception e) {
                Debug.LogError($"[BridgePlacer] 배치 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 기존 landmarkZones 끝에 한 칸 추가(중복 X는 갱신) — 명소 배치 zone을 덮어쓰지 않음
        static void AppendZone(GameObject rig, Vector3 zone) {
            var streamer = rig.GetComponentInChildren<SkylineStreamer>(true);
            if (streamer == null) {
                Debug.LogWarning("[BridgePlacer] SkylineStreamer 없음 — 스카이라인 배치 후 다시 실행하면 점유 반영");
                return;
            }
            var so = new SerializedObject(streamer);
            var prop = so.FindProperty("landmarkZones");
            int found = -1;
            for (int i = 0; i < prop.arraySize; i++) {
                var v = prop.GetArrayElementAtIndex(i).vector3Value;
                if (Mathf.Abs(v.x - zone.x) < 5f) {
                    found = i;
                    break;
                }
            }
            if (found < 0) {
                found = prop.arraySize;
                prop.arraySize = found + 1;
            }
            prop.GetArrayElementAtIndex(found).vector3Value = zone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 모든 렌더러 합친 월드 바운즈
        static Bounds CalcBounds(GameObject go) {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) {
                return new Bounds(go.transform.position, Vector3.zero);
            }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }
    }
}
