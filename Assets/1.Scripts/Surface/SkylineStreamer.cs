using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Game.World;

namespace Game.Surface {
    // 폐허 스카이라인 스트리머 — 메인 카메라 X를 따라 빌딩(2줄)·침수 기둥·터널 섹션 다리를 셀 단위로 무한 생성
    // 수상(궤도 카메라)·수중(사이드 카메라) 모두 카메라를 따라가므로 양 페이즈에서 끊김 없음
    public class SkylineStreamer : MonoBehaviour {
        [SerializeField] SplineContainer river;        // 강 중심선 — 범위 밖은 끝 접선으로 직선 연장
        [SerializeField] GameObject[] buildingPrefabs; // 변주 모델(roadview→VARCO GLB)
        [SerializeField] float[] buildingHeightsM;     // 변주별 실물 높이(m) — prefabs와 인덱스 쌍
        [SerializeField] GameObject bridgePrefab;      // 터널 섹션 다리 스팬
        [SerializeField] Material pillarMat;           // 침수 기둥(잠긴 콘크리트 톤)
        [SerializeField] float radius = 130f;          // 카메라 좌우 스트리밍 범위(u) — 안개 가시거리(밀도 0.011) 기준
        [SerializeField] Vector3[] landmarkZones;      // 랜드마크 점유 구간(x=중심X, y=반경, z=강변 side, 0=양안) — LandmarkPlacer가 배선

        public const float RiverHalfWidth = 18f;       // LandmarkPlacer와 동일한 강폭 기준
        public const float Spacing = 11f;              // 셀(빌딩 슬롯) 간격(u)
        const float BackRowExtra = 18f;                // 원경 뒷줄 추가 후퇴
        const float BridgeClear = 13f;                 // 다리 양끝 강변의 빌딩 제외 반경(u) — 스팬이 빌딩을 뚫지 않게
        const float BridgeHeightM = 26f;               // 한강대교 아치 수면 위 실측(m)
        const float SpanClearance = 5.5f;              // 다리 밑면-수면 간격(u) — 잠수정 통과 여유
        const float BridgeFirstX = 90f;                // 다리 기준 X — 구 터널 섹션 중심 위치 유지(섹션 시스템 제거 후 고정 간격)
        const float BridgeInterval = 180f;             // 다리 반복 간격(u)
        public const float CamBankMaxZ = -22f;         // 카메라(-z) 강변 z 한계 — 사이드뷰 카메라(z-20) 뒤 2u 여유(LandmarkPlacer 공용)
        public const float FarBankMinZ = 6f;           // 원경(+z) 강변 z 한계 — 2.5D Background 블록(z4) 뒤 2u 여유(LandmarkPlacer 공용)

        readonly Dictionary<int, List<GameObject>> cells = new();
        readonly List<int> removeBuf = new();   // Reconcile 무할당 재사용
        readonly Queue<int> pending = new();   // 스폰 대기 셀 — 프레임당 1셀만 채워 히치 분산(잠수 텔레포트 폭주 흡수)
        float riverLen;
        Vector3 riverOrigin;
        int lastCenter = int.MinValue;
        Camera cam;                            // Camera.main 캐시 — 단일 메인 카메라(Brain) 구조라 1회면 충분

        void Awake() {
            try {
                ClearChildren();   // 에디터 정적 프리뷰 제거 — 런타임은 스트리밍이 전담
                CacheRiver();
            } catch (Exception e) {
                Debug.LogError($"[SkylineStreamer] 초기화 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        void LateUpdate() {
            if (cam == null) {
                cam = Camera.main;
            }
            if (cam == null || river == null || riverLen <= 0f) {
                return;
            }
            int center = Mathf.FloorToInt((cam.transform.position.x - riverOrigin.x) / Spacing);
            if (center != lastCenter) {
                lastCenter = center;
                Reconcile(center);
            }
            // 대기 셀을 프레임당 1개만 채움 — 셀당 GLB 다수 Instantiate로 인한 히치 방지
            while (pending.Count > 0) {
                int i = pending.Dequeue();
                if (cells.TryGetValue(i, out var list) && list.Count == 0) {
                    FillCell(i, list);
                    break;
                }
            }
        }

        void CacheRiver() {
            if (river == null) {
                Debug.LogError("[SkylineStreamer] 강 스플라인 미연결 — 인스펙터에서 할당하세요.");
                return;
            }
            riverLen = river.CalculateLength();
            riverOrigin = (Vector3)river.EvaluatePosition(0f);
        }

        // 범위 밖 셀 해제 + 범위 안 빈 셀 생성
        void Reconcile(int center) {
            int span = Mathf.CeilToInt(radius / Spacing);
            removeBuf.Clear();
            foreach (var kv in cells) {
                if (Mathf.Abs(kv.Key - center) > span) {
                    foreach (var go in kv.Value) {
                        SafeDestroy(go);
                    }
                    removeBuf.Add(kv.Key);
                }
            }
            foreach (var key in removeBuf) {
                cells.Remove(key);
            }
            for (int i = center - span; i <= center + span; i++) {
                if (!cells.ContainsKey(i)) {
                    cells[i] = new List<GameObject>();   // 자리만 확보 — 채움은 큐가 프레임 분산
                    pending.Enqueue(i);
                }
            }
        }

        // 셀 1칸 채움 — 좌우 강변 × (근경 + 원경) + 이 셀이 소유한 다리(랜드마크·다리 구간은 빌딩 제외)
        void FillCell(int i, List<GameObject> list) {
            float d = i * Spacing;
            EvalRiver(d, out var pos, out var tangent);
            for (int s = 0; s < 2; s++) {
                int side = s == 0 ? -1 : 1;
                // 랜드마크 점유 구간·다리 접안부는 빌딩을 비움 — 구조물끼리 관통 방지
                if (InLandmarkZone(pos.x, side) || NearBridge(pos.x)) {
                    continue;
                }
                int h = Hash(i * 2 + s, 91);
                if (h % 7 != 0) {
                    PlaceOne(pos, tangent, side, RiverHalfWidth + 5f + (h >> 3) % 7, h, 1f, list);
                }
                int h2 = Hash(i * 2 + s, 173);
                if (h2 % 9 != 0) {
                    PlaceOne(pos, tangent, side, RiverHalfWidth + BackRowExtra + 6f + (h2 >> 3) % 9, h2, 1.25f, list);
                }
            }
            // 다리 소유: 다리 X(기준+간격 반복)의 소유 셀 인덱스(정수 규칙)가 이 셀이면 배치 — float 경계 중복/누락 없음
            float cellMidX = riverOrigin.x + d + Spacing * 0.5f;
            int kNear = Mathf.RoundToInt((cellMidX - BridgeFirstX) / BridgeInterval);
            for (int k = kNear - 1; k <= kNear + 1; k++) {
                float centerX = BridgeFirstX + k * BridgeInterval;
                int owner = Mathf.FloorToInt((centerX - riverOrigin.x) / Spacing);
                if (owner == i && !InLandmarkZone(centerX, 0)) {
                    EvalRiver(centerX - riverOrigin.x, out var bpos, out var btan);
                    PlaceBridge(bpos, btan, k, list);
                }
            }
        }

        // 랜드마크 점유 구간 검사 — side 0이면 강변 무관(다리용), 그 외엔 같은 강변의 구간만
        bool InLandmarkZone(float x, int side) {
            if (landmarkZones == null) {
                return false;
            }
            foreach (var z in landmarkZones) {
                bool sideMatch = side == 0 || z.z == 0f || Mathf.RoundToInt(z.z) == side;
                if (sideMatch && Mathf.Abs(x - z.x) < z.y) {
                    return true;
                }
            }
            return false;
        }

        // 다리 접안부 근접 검사 — 다리 X 격자(BridgeFirstX + k*Interval) 주변
        static bool NearBridge(float x) {
            float k = Mathf.Round((x - BridgeFirstX) / BridgeInterval);
            return Mathf.Abs(x - (BridgeFirstX + k * BridgeInterval)) < BridgeClear;
        }

        // 체인 거리 d(범위 밖 허용) → 강 중심선 위치·접선 — 끝 너머는 X축 직선 연장
        void EvalRiver(float d, out Vector3 pos, out Vector3 tangent) {
            float t = Mathf.Clamp01(d / riverLen);
            pos = (Vector3)river.EvaluatePosition(t);
            tangent = ((Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(t));
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
            if (d < 0f || d > riverLen) {
                // 끝 접선의 z 성분을 버리고 X로만 연장 — 멀어질수록 강변이 사이드뷰 카메라 통로로 표류하는 문제 방지
                tangent = tangent.x >= 0f ? Vector3.right : Vector3.left;
            }
            pos += tangent * (d - t * riverLen);
        }

        // 한 동 배치 — 실측 스케일 + 해시 변주(높이·요·기울임). +z 강변은 침수+기둥(사이드뷰 연속감)
        GameObject PlaceOne(Vector3 pos, Vector3 tangent, int side, float bankDist, int h, float heightMul, List<GameObject> outList) {
            if (buildingPrefabs == null || buildingPrefabs.Length == 0) {
                return null;
            }
            int v = h % buildingPrefabs.Length;
            for (int tryV = 0; tryV < buildingPrefabs.Length && buildingPrefabs[v] == null; tryV++) {
                v = (v + 1) % buildingPrefabs.Length;   // 빠진 변주는 다음 모델로
            }
            if (buildingPrefabs[v] == null) {
                return null;
            }
            var go = Spawn(buildingPrefabs[v]);
            go.name = $"Ruin_{Mathf.RoundToInt(pos.x)}{(side < 0 ? "L" : "R")}{(heightMul > 1f ? "B" : "")}";

            Bounds b = CalcBounds(go);
            if (b.size.y <= 0.0001f) {
                SafeDestroy(go);
                return null;
            }
            float heightM = (buildingHeightsM != null && v < buildingHeightsM.Length) ? buildingHeightsM[v] : 25f;
            float varied = heightM * heightMul * (0.8f + (h % 41) * 0.01f);
            // 침수: -z 강변(2.5D 카메라 쪽)은 잠긴 하부가 시야를 가려 제외
            float submerge = side < 0 ? 5f + (h % 9) * 0.5f : 0f;
            float scale = (LandmarkScale.GameHeight(varied) + submerge) / b.size.y;
            float footprintCap = 24f * heightMul;
            float widest = Mathf.Max(b.size.x, b.size.z);
            scale = Mathf.Min(scale, footprintCap / Mathf.Max(widest, 0.001f));
            go.transform.localScale = Vector3.one * scale;

            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 worldPos = pos + right * side * bankDist;
            float baseline = WorldGen.WaterY + 0.45f - submerge;
            go.transform.rotation = Quaternion.identity;
            Bounds sb = CalcBounds(go);
            worldPos.y = baseline - (sb.min.y - go.transform.position.y);
            go.transform.position = worldPos;

            float yaw = ((h % 17) - 8f);
            var rot = Quaternion.LookRotation(-right * side) * Quaternion.Euler(0f, yaw, 0f);
            if (h % 3 == 0) {
                rot = Quaternion.AngleAxis(2f + (h % 5), tangent) * rot;   // 2~6° 기울임(붕괴 직전 무드)
            }
            go.transform.rotation = rot;
            ClampCorridor(go, side);
            outList.Add(go);
            if (submerge > 0f) {
                AddPillar(go, outList);   // 침수 동은 강바닥 아래까지 질량 연장
            }
            return go;
        }

        // 강 굽이/연장으로 빌딩이 2.5D 사이드뷰 통로(카메라 z-20 ~ 플레이필드)를 침범하면 z 바깥으로 밀어냄
        void ClampCorridor(GameObject go, int side) {
            Bounds b = CalcBounds(go);
            if (side > 0 && b.max.z > CamBankMaxZ) {
                go.transform.position += Vector3.forward * (CamBankMaxZ - b.max.z);
            } else if (side < 0 && b.min.z < FarBankMinZ) {
                go.transform.position += Vector3.forward * (FarBankMinZ - b.min.z);
            }
        }

        // 침수 하부 기둥 — 빌딩 발자국 크기의 어두운 실루엣 박스를 강바닥 아래까지
        void AddPillar(GameObject building, List<GameObject> outList) {
            Bounds b = CalcBounds(building);
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = building.name + "_Pillar";
            pillar.transform.SetParent(transform, false);
            float top = b.min.y + 1.5f;                  // 모델 하단과 겹쳐 이음새 가림
            float bottom = WorldGen.MinY - 2f;
            pillar.transform.position = new Vector3(b.center.x, (top + bottom) * 0.5f, b.center.z);
            pillar.transform.localScale = new Vector3(b.size.x * 0.85f, top - bottom, b.size.z * 0.85f);
            pillar.transform.rotation = Quaternion.Euler(0f, building.transform.eulerAngles.y, 0f);
            SafeDestroy(pillar.GetComponent<Collider>());
            if (pillarMat != null) {
                pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
            }
            outList.Add(pillar);
        }

        // 다리 한 스팬 — 긴 축을 강폭 방향으로, 밑면은 수면+통과 여유
        void PlaceBridge(Vector3 pos, Vector3 tangent, int gi, List<GameObject> outList) {
            if (bridgePrefab == null) {
                return;
            }
            var go = Spawn(bridgePrefab);
            go.name = $"Bridge_{gi}";
            Bounds b = CalcBounds(go);
            if (b.size.y <= 0.0001f) {
                SafeDestroy(go);
                return;
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
            outList.Add(go);
        }

        // 에디터 정적 프리뷰 — 메뉴(Tools/한강/폐허 스카이라인 배치)가 호출. 런타임 Awake가 다시 지움
        public void RebuildPreview(float coverExtra) {
            ClearChildren();
            CacheRiver();
            if (riverLen <= 0f) {
                return;
            }
            coverExtra = Mathf.Max(0f, coverExtra);
            var sink = new List<GameObject>();
            int iMin = Mathf.FloorToInt(-coverExtra / Spacing);
            int iMax = Mathf.CeilToInt((riverLen + coverExtra) / Spacing);
            for (int i = iMin; i <= iMax; i++) {
                FillCell(i, sink);
            }
            Debug.Log($"[SkylineStreamer] 정적 프리뷰 {sink.Count}개 생성 (셀 {iMin}~{iMax}) — 플레이 시 스트리밍으로 대체");
        }

        void ClearChildren() {
            for (int i = transform.childCount - 1; i >= 0; i--) {
                SafeDestroy(transform.GetChild(i).gameObject);
            }
            cells.Clear();
            pending.Clear();
            lastCenter = int.MinValue;
        }

        // 프리팹 인스턴스 — 에디터 프리뷰는 프리팹 링크 유지(씬 파일 경량), 런타임은 일반 Instantiate
        GameObject Spawn(GameObject prefab) {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
            }
#endif
            return Instantiate(prefab, transform);
        }

        static void SafeDestroy(UnityEngine.Object o) {
            if (o == null) {
                return;
            }
            if (Application.isPlaying) {
                Destroy(o);
            } else {
                DestroyImmediate(o);
            }
        }

        // 좌표 결정적 해시 — 어느 시점에 스트리밍돼도 같은 배치
        static int Hash(int a, int b) {
            int h = a * 73856093 ^ b * 19349663;
            return (h % 1000 + 1000) % 1000;
        }

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
