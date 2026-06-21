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
        [SerializeField] float cellWidth = 6f;      // 셀 폭(u)
        [SerializeField] int radiusCells = 6;       // 좌우 로드 반경(셀) — 화면+α
        [SerializeField] int slotsPerCell = 2;      // (호환) 기본 슬롯 — 하이브리드에서는 군집/개방 슬롯이 우선
        [SerializeField] int density = 70;          // (호환) 기본 채움 확률
        [SerializeField] float colliderRadius = 1f; // 수집 트리거 월드 반경(m)

        [Header("하이브리드 배치 — 난파 군집 + 개방 수역")]
        [SerializeField] float clusterInterval = 60f;  // 난파 군집 간격(u) — 다리 앵커 기준 주기
        [SerializeField] float clusterRadius = 13f;    // 군집 반경(u) — 이 안은 밀집, 밖은 희소
        [SerializeField] float bridgeAnchorX = 38f;    // 양화대교 잔해 X — 군집 격자의 기준점(BridgePlacer와 일치)
        [SerializeField] int clusterSlots = 5;         // 군집 셀 슬롯 수(수직 잔해 더미)
        [SerializeField] int clusterDensity = 90;      // 군집 채움 확률(%)
        [SerializeField] int openSlots = 1;            // 개방 수역 셀 슬롯 수
        [SerializeField] int openDensity = 26;         // 개방 수역 채움 확률(%)
        [SerializeField] GameConfig config;            // 통합 설정 — 연결 시 군집/개방 밀도 덮어씀(미연결 시 위 기본값 유지)

        public const float SpawnTopMargin = 0.5f;   // 수면 아래 여유(u) — 얕은 수집물도 수면 근처까지
        public const float SpawnBottomMargin = 1f;  // 바닥 위 여유(u)

        readonly Dictionary<int, List<GameObject>> cells = new();
        readonly List<ItemDef> candidates = new();   // PickByDepth 무할당 재사용
        readonly List<int> removeBuf = new();         // Reconcile 무할당 재사용
        int lastCenter = int.MinValue;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 기본값 유지
                if (config != null) {
                    clusterDensity = config.collectibleClusterDensity;
                    openDensity = config.collectibleOpenDensity;
                }
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
            float unitPerM = (DepthMap.SurfaceY - DepthMap.SeabedY) / DepthMap.MaxDepthM;
            float cellMidX = (cell + 0.5f) * cellWidth;
            float anchor = NearestAnchor(cellMidX);
            float dist = Mathf.Abs(cellMidX - anchor);
            bool inCluster = dist < clusterRadius;
            int slots = inCluster ? clusterSlots : openSlots;
            int dens = inCluster ? clusterDensity : openDensity;

            for (int s = 0; s < slots; s++) {
                int h = Hash(cell, s);
                if (h % 100 >= dens) {
                    continue;
                }
                // 31비트 해시 비트대역 분리(독립 난수) — x분산 / 깊이 / 아이템선택
                float x = cell * cellWidth + ((h & 0x3FF) / 1024f) * cellWidth;
                // 군집은 슬롯별로 다른 깊이대에 쌓아 '수직 잔해 더미' 형성, 개방은 균등 랜덤
                float depthT = inCluster
                    ? (s + ((h >> 10) & 0xFF) / 255f) / Mathf.Max(1, slots)
                    : ((h >> 10) & 0x3FF) / 1024f;
                float yWorld = Mathf.Lerp(DepthMap.SurfaceY - SpawnTopMargin, DepthMap.SeabedY + SpawnBottomMargin, depthT);
                float excelY = (yWorld - DepthMap.SurfaceY) / unitPerM;   // 게임y → 엑셀 수심
                var def = PickByDepth(excelY, h);
                if (def == null || def.prefab == null) {
                    continue;
                }
                var go = Spawn(def, new Vector3(x, FloorClamp(x, yWorld) + 0.5f, 0f));
                if (go != null) {
                    outList.Add(go);
                }
            }

            // 군집 중심 셀 — 앵커를 '포함하는' 셀에만 1회(앵커가 셀 경계면 인접 두 셀 중복 방지)
            if (inCluster && Mathf.FloorToInt(anchor / cellWidth) == cell) {
                float deepY = Mathf.Lerp(DepthMap.SeabedY + SpawnBottomMargin, DepthMap.SurfaceY - SpawnTopMargin, 0.18f);
                float deepExcel = (deepY - DepthMap.SurfaceY) / unitPerM;
                var rare = PickRarest(deepExcel);
                if (rare != null && rare.prefab != null) {
                    var go = Spawn(rare, new Vector3(anchor, FloorClamp(anchor, deepY) + 0.5f, 0f));
                    if (go != null) {
                        outList.Add(go);
                    }
                }
            }
        }

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
