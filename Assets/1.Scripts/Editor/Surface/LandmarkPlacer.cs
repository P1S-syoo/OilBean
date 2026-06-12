using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Surface;

namespace Game.Editor.Surface {
    // 강 스플라인 기준 실물 명소 배치 도구 — 임시 하늘 블록을 대체하는 3D 스카이라인
    public static class LandmarkPlacer {
        const float RiverHalfWidth = 18f;    // 스플라인 중심선에서 강변까지 기본 거리(스플라인 z폭 ±6 포함)
        const float WaterDraft = 0.15f;      // 수상 구조물 흘수(수면에 살짝 잠김)
        const float SizeMul = 2f;            // 랜드마크 전용 크기 배율 — 다리·빌딩 대비 위계 강조(가로·세로 2배)

        // 명소 정의 — 스플라인 정규화 거리(t), 강변 측(side: -1 좌안/+1 우안), 추가 오프셋, 실물 높이
        [Serializable]
        class LandmarkDef {
            public string name;
            public string assetPath;
            public float realHeightM;    // 실물 높이(m) — LandmarkScale로 게임 높이 환산
            public float t;              // 스플라인 정규화 거리 0~1
            public int side;             // -1=좌안(-z쪽), +1=우안(+z쪽)
            public float bankOffset;     // 강변에서 바깥쪽 추가 거리(수상 구조물은 음수로 강 안쪽 허용)
            public float yawOffset;      // 모델 정면축 보정(도) — 시각 확인 후 조정
            public bool floatOnWater;    // true=수면에 띄움, false=강변(수면 기준) 배치
        }

        // 배치 목록 — 새 명소는 여기에 추가 (roadview-to-3d 스킬로 GLB 생성 후 등록)
        static readonly LandmarkDef[] Defs = {
            new LandmarkDef {
                name = "Sebitseom",
                assetPath = "Assets/VARCO3DImports/sebit_lowpoly.glb",
                realHeightM = 20f,           // 세빛섬 가빛섬 실물 약 20m(4층)
                t = 0.47f,                   // 첫 정화 목표(0.5) 직전 — 도착 시 정면에 보임
                side = 1,                    // 실제 세빛섬은 강남측(우안)
                bankOffset = 1f,             // 수상 구조물 — 강 안쪽이되 궤도 카메라(반경 8) 침범 밖

                yawOffset = 0f,
                floatOnWater = true,
            },
        };

        [MenuItem("Tools/한강/명소 배치")]
        public static void Place() {
            try {
                var rig = GameObject.Find("SurfaceRig");
                if (rig == null) {
                    Debug.LogError("[LandmarkPlacer] SurfaceRig가 없습니다 — Tools/한강/수상 리그 생성 먼저 실행");
                    return;
                }
                var river = rig.GetComponentInChildren<SplineContainer>();
                if (river == null) {
                    Debug.LogError("[LandmarkPlacer] River 스플라인을 못 찾았습니다");
                    return;
                }

                var old = rig.transform.Find("Landmarks");
                if (old != null) {
                    Undo.DestroyObjectImmediate(old.gameObject);   // 재실행 시 교체
                }
                var root = new GameObject("Landmarks");
                root.transform.SetParent(rig.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "명소 배치");

                int placed = 0;
                foreach (var def in Defs) {
                    if (PlaceOne(root.transform, river, def)) {
                        placed++;
                    }
                }
                EditorUtility.SetDirty(rig);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
                Debug.Log($"[LandmarkPlacer] 명소 배치 완료 — {placed}/{Defs.Length}개 (스케일 표준: K={LandmarkScale.Calibration}, p={LandmarkScale.Exponent}, cap={LandmarkScale.MaxHeight})");
            } catch (Exception e) {
                Debug.LogError($"[LandmarkPlacer] 배치 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        static bool PlaceOne(Transform parent, SplineContainer river, LandmarkDef def) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.assetPath);
            if (prefab == null) {
                Debug.LogError($"[LandmarkPlacer] 모델 없음: {def.assetPath} — roadview-to-3d 스킬로 생성 후 임포트하세요");
                return false;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = def.name;

            // 원본 바운즈 → 실물 높이 기반 균일 스케일 (모델 피벗 위치와 무관하게 바닥 스냅)
            Bounds bounds = CalcBounds(go);
            if (bounds.size.y <= 0.0001f) {
                Debug.LogError($"[LandmarkPlacer] {def.name} 렌더러 바운즈가 비어 있음");
                UnityEngine.Object.DestroyImmediate(go);
                return false;
            }
            float gameHeight = LandmarkScale.GameHeight(def.realHeightM) * SizeMul;
            float scale = gameHeight / bounds.size.y;
            go.transform.localScale = Vector3.one * scale;

            // 스플라인 t 지점의 접선·법선으로 강변 방향 산출
            Vector3 pos = (Vector3)river.EvaluatePosition(def.t);
            Vector3 tangent = ((Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(def.t));
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            // 수평 위치: 중심선 → 강변 측으로 (강폭 절반 + 오프셋)
            Vector3 worldPos = pos + right * def.side * (RiverHalfWidth + def.bankOffset);

            // 수직 위치: 스케일 적용 후 바닥(bounds.min.y)을 수면/강변 기준선에 스냅
            float waterTop = Game.World.WorldGen.WaterY + 0.45f;   // 수면 평면 높이와 일치
            float baseline = def.floatOnWater ? waterTop - WaterDraft : waterTop;
            float bottomOffset = (bounds.min.y - go.transform.position.y) * scale;
            worldPos.y = baseline - bottomOffset;
            go.transform.position = worldPos;

            // 정면이 강(스플라인)을 향하도록 회전 + 모델축 보정
            go.transform.rotation = Quaternion.LookRotation(-right * def.side) * Quaternion.Euler(0f, def.yawOffset, 0f);

            // 2.5D 사이드뷰 통로 보호 — 빌딩과 동일 기준(z -22~+6) 침범 시 가까운 바깥쪽으로 밀어냄
            Bounds placedBounds = CalcBounds(go);
            if (placedBounds.max.z > SkylineStreamer.CamBankMaxZ && placedBounds.min.z < SkylineStreamer.FarBankMinZ) {
                bool camSide = placedBounds.center.z < (SkylineStreamer.CamBankMaxZ + SkylineStreamer.FarBankMinZ) * 0.5f;
                float push = camSide ? SkylineStreamer.CamBankMaxZ - placedBounds.max.z : SkylineStreamer.FarBankMinZ - placedBounds.min.z;
                go.transform.position += Vector3.forward * push;
            }

            Debug.Log($"[LandmarkPlacer] {def.name}: 실물 {def.realHeightM}m → {gameHeight:F1}u (scale {scale:F2}), t={def.t}, side={(def.side < 0 ? "좌안" : "우안")}");
            return true;
        }

        // 모든 렌더러를 합친 월드 바운즈 (스케일 1 상태에서 호출)
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
