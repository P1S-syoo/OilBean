using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Surface;
using Game.World;

namespace Game.Editor.Surface {
    // 강 양안 폐허 스카이라인 산포 도구 — 빌딩 2줄(근경+원경) + 터널 섹션마다 다리, 스플라인 양 끝 너머까지 연장
    public static class SkylinePlacer {
        const float RiverHalfWidth = 18f;                  // LandmarkPlacer와 동일한 강폭 기준
        const float Spacing = 14f;                         // 빌딩 간 체인 거리(u)
        const float CoverExtra = 90f;                      // 스플라인 양 끝 너머 연장 — 시작 뒤·종점 너머 허전함 방지(안개 가시거리 커버)
        const float BackRowExtra = 18f;                    // 원경 뒷줄 추가 후퇴 거리
        const float SebitMin = 0.40f, SebitMax = 0.54f;    // 세빛섬(t=0.47 우안) 주변은 비움
        const string BridgePath = "Assets/VARCO3DImports/bridge_arch.glb";
        const float BridgeHeightM = 26f;                   // 한강대교 아치 수면 위 실측(m)
        const float SpanClearance = 5.5f;                  // 다리 밑면-수면 간격(u) — 잠수정(잠망경 ~3u) 통과 여유

        // 변주 소스 — (에셋 경로, 실물 높이 m). 새 빌딩은 여기에 추가
        static readonly (string path, float heightM)[] Variants = {
            ("Assets/VARCO3DImports/Ruins/ruin_block.glb", 22f),
            ("Assets/VARCO3DImports/Ruins/ruin_slab.glb", 42f),
        };

        [MenuItem("Tools/한강/폐허 스카이라인 배치")]
        public static void Place() {
            try {
                var rig = GameObject.Find("SurfaceRig");
                if (rig == null) {
                    Debug.LogError("[SkylinePlacer] SurfaceRig가 없습니다 — Tools/한강/수상 리그 생성 먼저 실행");
                    return;
                }
                var river = rig.GetComponentInChildren<SplineContainer>();
                if (river == null) {
                    Debug.LogError("[SkylinePlacer] River 스플라인을 못 찾았습니다");
                    return;
                }

                var old = rig.transform.Find("Skyline");
                if (old != null) {
                    Undo.DestroyObjectImmediate(old.gameObject);   // 재실행 시 교체
                }
                var root = new GameObject("Skyline");
                root.transform.SetParent(rig.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "폐허 스카이라인 배치");

                // 변주 프리팹 로드 — 없는 것은 건너뛰고 경고
                var prefabs = new GameObject[Variants.Length];
                int loaded = 0;
                for (int v = 0; v < Variants.Length; v++) {
                    prefabs[v] = AssetDatabase.LoadAssetAtPath<GameObject>(Variants[v].path);
                    if (prefabs[v] == null) {
                        Debug.LogWarning($"[SkylinePlacer] 모델 없음: {Variants[v].path} — roadview-to-3d로 생성 후 임포트");
                    } else {
                        loaded++;
                    }
                }
                if (loaded == 0) {
                    Debug.LogError("[SkylinePlacer] 사용 가능한 빌딩 모델이 없습니다");
                    return;
                }

                float len = river.CalculateLength();
                int steps = Mathf.Max(1, Mathf.FloorToInt((len + CoverExtra * 2f) / Spacing));
                int placed = 0;
                for (int i = 0; i <= steps; i++) {
                    float d = -CoverExtra + i * Spacing;
                    EvalRiver(river, len, d, out var pos, out var tangent);
                    float t01 = d / len;
                    for (int s = 0; s < 2; s++) {
                        int side = s == 0 ? -1 : 1;
                        bool sebitGap = side > 0 && t01 > SebitMin && t01 < SebitMax;
                        // 근경 줄 — 20% 틈(무너져 사라진 자리), 명소 자리 비움
                        int h = Hash(i * 2 + s, 91);
                        if (!sebitGap && h % 5 != 0) {
                            if (PlaceOne(root.transform, prefabs, pos, tangent, side,
                                RiverHalfWidth + 5f + (h >> 3) % 7, h, 1f)) {
                                placed++;
                            }
                        }
                        // 원경 뒷줄 — 근경 틈 사이로도 먼 빌딩이 보이게(더 크고 더 물러남)
                        int h2 = Hash(i * 2 + s, 173);
                        if (h2 % 7 != 0) {
                            if (PlaceOne(root.transform, prefabs, pos, tangent, side,
                                RiverHalfWidth + BackRowExtra + 6f + (h2 >> 3) % 9, h2, 1.25f)) {
                                placed++;
                            }
                        }
                    }
                }
                int bridges = PlaceSectionBridges(root.transform, river, len);
                EditorUtility.SetDirty(rig);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
                Debug.Log($"[SkylinePlacer] 폐허 스카이라인 배치 완료 — 빌딩 {placed}동(2줄) + 다리 {bridges}개 (연장 ±{CoverExtra}u)");
            } catch (Exception e) {
                Debug.LogError($"[SkylinePlacer] 배치 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 체인 거리 d(범위 밖 허용) → 강 중심선 위치·접선 — 스플라인 끝 너머는 끝 접선 방향 직선 연장
        static void EvalRiver(SplineContainer river, float len, float d, out Vector3 pos, out Vector3 tangent) {
            float t = Mathf.Clamp01(d / len);
            pos = (Vector3)river.EvaluatePosition(t);
            tangent = ((Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(t));
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
            pos += tangent * (d - t * len);
        }

        // 한 동 배치 — 실측 스케일 + 해시 변주(높이 ±20%·요 ±8°·기울임). heightMul=원경 확대 계수
        static bool PlaceOne(Transform parent, GameObject[] prefabs, Vector3 pos, Vector3 tangent, int side, float bankDist, int h, float heightMul) {
            int v = h % Variants.Length;
            if (prefabs[v] == null) {
                v = (v + 1) % Variants.Length;   // 빠진 변주는 다른 모델로 대체
                if (prefabs[v] == null) {
                    return false;
                }
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[v], parent);
            go.name = $"Ruin_{Mathf.RoundToInt(pos.x)}{(side < 0 ? "L" : "R")}{(heightMul > 1f ? "B" : "")}";

            Bounds b = CalcBounds(go);
            if (b.size.y <= 0.0001f) {
                UnityEngine.Object.DestroyImmediate(go);
                return false;
            }
            float varied = Variants[v].heightM * heightMul * (0.8f + (h % 41) * 0.01f);   // 높이 ±20% 변주
            // 침수: 하부를 수면 아래로 잠가 수중(사이드뷰)에서도 이어져 보이게 — 잠긴 만큼 키를 더해 수면 위 높이는 유지
            // 단 -z 강변(2.5D 카메라 쪽)은 잠긴 하부가 카메라 앞을 가려 침수 제외
            float submerge = side < 0 ? 5f + (h % 9) * 0.5f : 0f;
            float scale = (LandmarkScale.GameHeight(varied) + submerge) / b.size.y;
            // 파사드형 모델은 가로가 과대해질 수 있어 발자국 폭 상한(카메라 침범 방지)
            float footprintCap = 24f * heightMul;
            float widest = Mathf.Max(b.size.x, b.size.z);
            scale = Mathf.Min(scale, footprintCap / Mathf.Max(widest, 0.001f));
            go.transform.localScale = Vector3.one * scale;

            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 worldPos = pos + right * side * bankDist;

            // 바닥 스냅 — 수면 아래 submerge 깊이까지 잠김(침수 도시)
            float baseline = WorldGen.WaterY + 0.45f - submerge;
            go.transform.rotation = Quaternion.identity;
            Bounds sb = CalcBounds(go);
            worldPos.y = baseline - (sb.min.y - go.transform.position.y);
            go.transform.position = worldPos;

            // 정면은 강 쪽 + 요 변주, 일부 동은 기울임(붕괴 직전 무드)
            float yaw = ((h % 17) - 8f);
            var rot = Quaternion.LookRotation(-right * side) * Quaternion.Euler(0f, yaw, 0f);
            if (h % 3 == 0) {
                float lean = 2f + (h % 5);                                // 2~6° 기울임
                rot = Quaternion.AngleAxis(lean, tangent) * rot;
            }
            go.transform.rotation = rot;
            return true;
        }

        // 2.5D 터널 섹션(다리 밑 테마) 중심마다 강 가로지름 다리 — 수중 터널과 수상 다리가 같은 X에서 정렬
        static int PlaceSectionBridges(Transform parent, SplineContainer river, float len) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePath);
            if (prefab == null) {
                Debug.LogWarning($"[SkylinePlacer] 다리 모델 없음: {BridgePath} — roadview-to-3d로 생성 후 임포트");
                return 0;
            }
            EvalRiver(river, len, 0f, out var origin, out _);
            EvalRiver(river, len, -CoverExtra, out var startPos, out _);
            EvalRiver(river, len, len + CoverExtra, out var endPos, out _);
            float xMin = Mathf.Min(startPos.x, endPos.x);
            float xMax = Mathf.Max(startPos.x, endPos.x);

            int placed = 0;
            int giMin = Mathf.FloorToInt((xMin - WorldGen.OriginX) / WorldGen.SectionW);
            int giMax = Mathf.CeilToInt((xMax - WorldGen.OriginX) / WorldGen.SectionW);
            for (int gi = giMin; gi <= giMax; gi++) {
                float centerX = WorldGen.OriginX + (gi + 0.5f) * WorldGen.SectionW;
                if (centerX < xMin || centerX > xMax) {
                    continue;
                }
                if (WorldGen.SectionAt(Mathf.RoundToInt(centerX)) != SectionType.Tunnel) {
                    continue;
                }
                // 강은 대체로 +X 진행 — 체인 거리를 X 차이로 근사(굽이는 EvalRiver가 z를 보정)
                EvalRiver(river, len, centerX - origin.x, out var pos, out var tangent);
                if (PlaceBridge(parent, prefab, pos, tangent, gi)) {
                    placed++;
                }
            }
            return placed;
        }

        // 다리 한 스팬 — 긴 축을 강폭 방향으로, 밑면은 수면+통과 여유
        static bool PlaceBridge(Transform parent, GameObject prefab, Vector3 pos, Vector3 tangent, int gi) {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Bridge_{gi}";
            Bounds b = CalcBounds(go);
            if (b.size.y <= 0.0001f) {
                UnityEngine.Object.DestroyImmediate(go);
                return false;
            }
            float scale = LandmarkScale.GameHeight(BridgeHeightM) / b.size.y;
            go.transform.localScale = Vector3.one * scale;

            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            go.transform.rotation = (b.size.x >= b.size.z)
                ? Quaternion.LookRotation(tangent)
                : Quaternion.LookRotation(right);
            Bounds rb = CalcBounds(go);
            Vector3 spanPos = pos;
            spanPos.y = WorldGen.WaterY + 0.45f + SpanClearance - (rb.min.y - go.transform.position.y);
            go.transform.position = spanPos;
            return true;
        }

        // 좌표 결정적 해시 — 재실행해도 같은 배치
        static int Hash(int a, int b) {
            int h = a * 73856093 ^ b * 19349663;
            return (h % 1000 + 1000) % 1000;
        }

        // 모든 렌더러 합산 월드 바운즈
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
