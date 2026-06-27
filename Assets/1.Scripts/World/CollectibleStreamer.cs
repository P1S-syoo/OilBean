using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.World {
    // 수집물 스트리머 — 2.5D 플레이어 X를 따라 셀 단위로 수심별 수집물(FBX)을 무한 스폰
    // ChunkStreamer/SkylineStreamer와 동일한 추적-셀 패턴. 수집되면 제거, 재진입 시 결정적 재생성
    public class CollectibleStreamer : MonoBehaviour {
        [SerializeField] Transform target;          // 2.5D 플레이어(잠수정)
        [SerializeField] ItemDataTable table;       // 수집물 마스터 데이터
        [SerializeField] float cellWidth;           // 셀 폭(u) — 기본값은 수집설정.수집물생성간격
        [SerializeField] int radiusCells;           // 좌우 로드 반경(셀) — 기본값은 수집설정.수집물로드반경
        [SerializeField] int slotsPerCell = 2;      // (호환) 기본 슬롯 — 하이브리드에서는 군집/개방 슬롯이 우선(config 비대상)
        [SerializeField] int density = 70;          // (호환) 기본 채움 확률(config 비대상)
        [SerializeField] float colliderRadius;      // 수집 트리거 월드 반경(m) — 기본값은 수집설정.트리거반경
        [SerializeField] float zSpread;             // 오염체 z축 분산 폭 — 기본값은 수집설정.수집물앞뒤흩어짐

        [Header("하이브리드 배치 — 난파 군집 + 개방 수역")]
        [SerializeField] float clusterInterval;        // 난파 군집 간격(u) — 기본값은 수집설정.군집간격
        [SerializeField] float clusterRadius;          // 군집 반경(u) — 기본값은 수집설정.군집반경
        [SerializeField] float bridgeAnchorX;          // 정화구역 중심 X — 기본값은 수집설정.정화구역중심X(BridgePlacer와 일치)
        [SerializeField] int clusterSlots;             // 군집 셀 슬롯 수 — 기본값은 수집설정.군집수집물칸수
        [SerializeField] int clusterDensity;           // 군집 채움 확률(%) — 기본값은 수집설정.군집생성확률
        [SerializeField] int openSlots;                // 개방 수역 셀 슬롯 수 — 기본값은 수집설정.일반수집물칸수
        [SerializeField] int openDensity;              // 개방 수역 채움 확률(%) — 기본값은 수집설정.일반생성확률
        [SerializeField] 수집설정 config;            // 수집 설정 — 연결 시 스폰/군집 수치 덮어씀(미연결 시 위 기본값 유지)

        public const float SpawnTopMargin = 0.5f;   // 수면 아래 여유(u) — 얕은 수집물도 수면 근처까지
        public const float SpawnBottomMargin = 1f;  // 바닥 위 여유(u)

        readonly Dictionary<int, List<GameObject>> cells = new();
        readonly List<ItemDef> candidates = new();   // PickByDepth 무할당 재사용
        readonly List<int> removeBuf = new();         // Reconcile 무할당 재사용
        int lastCenter = int.MinValue;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : 수집설정.기본;
                cellWidth = cfg.수집물생성간격;
                radiusCells = cfg.수집물로드반경;
                colliderRadius = cfg.트리거반경;
                zSpread = cfg.수집물앞뒤흩어짐;
                clusterInterval = cfg.군집간격;
                clusterRadius = cfg.군집반경;
                clusterSlots = cfg.군집수집물칸수;
                clusterDensity = cfg.군집생성확률;
                openSlots = cfg.일반수집물칸수;
                openDensity = cfg.일반생성확률;
                bridgeAnchorX = cfg.정화구역중심X;
            } catch (Exception e) {
                Debug.LogError($"[CollectibleStreamer] config 적용 실패: {e.Message}");
            }
        }

        void LateUpdate() {
            if (target == null || table == null || table.items == null) {
                return;
            }
            int center = Mathf.FloorToInt(target.position.x / cellWidth);
            if (center == lastCenter) {
                return;
            }
            lastCenter = center;
            Reconcile(center);
        }

        // 범위 밖 셀 해제 + 범위 안 빈 셀 채움
        void Reconcile(int center) {
            try {
                removeBuf.Clear();
                foreach (var kv in cells) {
                    if (Mathf.Abs(kv.Key - center) > radiusCells) {
                        foreach (var go in kv.Value) {
                            if (go != null) {
                                Destroy(go);
                            }
                        }
                        removeBuf.Add(kv.Key);
                    }
                }
                foreach (var k in removeBuf) {
                    cells.Remove(k);
                }
                for (int i = center - radiusCells; i <= center + radiusCells; i++) {
                    if (!cells.ContainsKey(i)) {
                        var list = new List<GameObject>();
                        FillCell(i, list);
                        cells[i] = list;
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[CollectibleStreamer] Reconcile 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // 셀 1칸 채움 — 난파 군집(밀집·수직 더미·희귀 센터피스) vs 개방 수역(희소). 깊이대 = 수심 biome
        void FillCell(int cell, List<GameObject> outList) {
            float cellMidX = (cell + 0.5f) * cellWidth;
            float anchor = NearestAnchor(cellMidX);
            bool inCluster = Mathf.Abs(cellMidX - anchor) < clusterRadius;
            int slots = inCluster ? clusterSlots : openSlots;
            int dens = inCluster ? clusterDensity : openDensity;

            for (int s = 0; s < slots; s++) {
                int h = Hash(cell, s);
                if (h % 100 >= dens) {
                    continue;   // 채움 확률 미달 슬롯
                }
                PlaceSlot(cell, s, h, slots, inCluster, outList);
            }

            // 군집 중심 셀 — 앵커를 '포함하는' 셀에만 1회(앵커가 셀 경계면 인접 두 셀 중복 방지)
            if (inCluster && Mathf.FloorToInt(anchor / cellWidth) == cell) {
                PlaceCenterpiece(anchor, outList);
            }
        }

        // 슬롯 1개 배치 — 해시 비트대역으로 x분산·깊이를 독립 결정해 수집물 1개 스폰
        void PlaceSlot(int cell, int s, int h, int slots, bool inCluster, List<GameObject> outList) {
            float x = cell * cellWidth + HashX(h) * cellWidth;
            // 군집은 슬롯별로 다른 깊이대에 쌓아 '수직 잔해 더미' 형성, 개방은 균등 랜덤
            float depthT = inCluster
                ? (s + HashClusterDepth(h)) / Mathf.Max(1, slots)
                : HashDepth(h);
            float yWorld = Mathf.Lerp(DepthMap.SurfaceY - SpawnTopMargin, DepthMap.SeabedY + SpawnBottomMargin, depthT);
            float excelY = WorldToExcel(yWorld);
            var def = PickByDepth(excelY, h);
            if (def == null || def.prefab == null) {
                return;
            }
            var go = Spawn(def, new Vector3(x, FloorClamp(x, yWorld) + 0.5f, 0f));
            if (go != null) {
                outList.Add(go);
            }
        }

        // 군집 중심 희귀 센터피스 1개 배치(앵커 위치, 깊은 대역)
        void PlaceCenterpiece(float anchor, List<GameObject> outList) {
            float deepY = Mathf.Lerp(DepthMap.SeabedY + SpawnBottomMargin, DepthMap.SurfaceY - SpawnTopMargin, 0.18f);
            var rare = PickRarest(WorldToExcel(deepY));
            if (rare == null || rare.prefab == null) {
                return;
            }
            var go = Spawn(rare, new Vector3(anchor, FloorClamp(anchor, deepY) + 0.5f, 0f));
            if (go != null) {
                outList.Add(go);
            }
        }

        // 게임 월드 y → 엑셀 수심 좌표
        static float WorldToExcel(float yWorld) {
            float unitPerM = (DepthMap.SurfaceY - DepthMap.SeabedY) / DepthMap.MaxDepthM;
            return (yWorld - DepthMap.SurfaceY) / unitPerM;
        }

        // 31비트 해시 비트대역 분리(독립 난수, 각 0~1) — x분산[0..10] / 깊이[10..20] / 아이템선택은 PickByDepth가 [20..30] 사용
        static float HashX(int h) => (h & 0x3FF) / 1024f;            // 하위 10비트 — 셀 내 x 분산
        static float HashDepth(int h) => ((h >> 10) & 0x3FF) / 1024f; // 중위 10비트 — 개방 수역 깊이
        static float HashClusterDepth(int h) => ((h >> 10) & 0xFF) / 255f; // 중위 8비트 — 군집 슬롯 내 미세 깊이

        // 가장 가까운 군집 앵커 X(다리 기준 주기 격자)
        float NearestAnchor(float x) {
            return Mathf.Round((x - bridgeAnchorX) / clusterInterval) * clusterInterval + bridgeAnchorX;
        }

        // 바닥에 안 묻히게 — 솔리드면 위로 올림. 비솔리드면 원본 float 깊이 유지(정수 양자화 방지)
        float FloorClamp(float x, float yWorld) {
            int xi = Mathf.RoundToInt(x);
            int yi = Mathf.FloorToInt(yWorld);
            int guard = 0;
            while (WorldGen.IsSolid(1, xi, yi) && yi < WorldGen.WaterY && guard++ < 40) {
                yi++;
            }
            return Mathf.Max(yWorld, yi);   // 안 올렸으면 yWorld(연속), 올렸으면 정리된 정수 높이
        }

        // 해당 수심에서 가장 희귀한 아이템(오염수준>등급 우선) — 센터피스용
        ItemDef PickRarest(float excelY) {
            ItemDef best = null;
            int bestRank = -1;
            foreach (var it in table.items) {
                if (it == null || it.prefab == null) {
                    continue;
                }
                if (excelY < it.minSpawnY || excelY > it.maxSpawnY) {
                    continue;
                }
                int rank = it.pollutionLevel * 10 + it.grade;
                if (rank > bestRank) {
                    bestRank = rank;
                    best = it;
                }
            }
            return best;
        }

        // 해당 수심(excelY)에 출현 가능한 아이템 중 결정적 1개
        ItemDef PickByDepth(float excelY, int h) {
            candidates.Clear();
            foreach (var it in table.items) {
                if (it == null || it.prefab == null) {
                    continue;
                }
                if (excelY >= it.minSpawnY && excelY <= it.maxSpawnY) {
                    candidates.Add(it);
                }
            }
            if (candidates.Count == 0) {
                return null;
            }
            return candidates[((h >> 20) & 0x3FF) % candidates.Count];
        }

        GameObject Spawn(ItemDef def, Vector3 pos) {
            try {
                // z축 분산 — 시각적 입체감(2D 물리/수집은 xy만 사용해 영향 없음)
                pos.z = ((Hash(Mathf.RoundToInt(pos.x * 11f), def.id.Length + 3) % 200) / 200f - 0.5f) * zSpread;
                float yaw = Hash(Mathf.RoundToInt(pos.x), def.id.Length) % 360;
                var go = Instantiate(def.prefab, pos, Quaternion.Euler(0f, yaw, 0f), transform);
                go.name = "Pickup_" + def.id;
                // 사이즈 정규화 — prefab 임포트 스케일에 추가 배율을 곱함(덮어쓰면 임포트 보정이 날아감)
                float scale = def.spawnScale > 0.0001f ? def.spawnScale : 1f;
                go.transform.localScale *= scale;
                // 수집 트리거(2D) — 최종 월드 스케일로 보정해 트리거 반경 일정
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = colliderRadius / Mathf.Max(go.transform.lossyScale.x, 0.0001f);
                var pk = go.AddComponent<Game.Items.Pickup>();
                pk.SetDef(def);
                return go;
            } catch (Exception e) {
                Debug.LogError($"[CollectibleStreamer] {def?.id} 스폰 실패: {e.Message}");
                return null;
            }
        }

        // 스폰 가능 엑셀 수심 범위 — excelMin(깊음) ~ excelMax(얕음, 0에 가까움). 도달성 검증·테스트용
        public static void SpawnExcelRange(out float excelMin, out float excelMax) {
            float unitPerM = (DepthMap.SurfaceY - DepthMap.SeabedY) / DepthMap.MaxDepthM;
            excelMax = (DepthMap.SurfaceY - SpawnTopMargin - DepthMap.SurfaceY) / unitPerM;
            excelMin = (DepthMap.SeabedY + SpawnBottomMargin - DepthMap.SurfaceY) / unitPerM;
        }

        // 좌표 결정적 해시 — 어느 시점에 스트리밍돼도 같은 배치(31비트 양수)
        static int Hash(int a, int b) {
            unchecked {
                int h = a * 73856093 ^ b * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
