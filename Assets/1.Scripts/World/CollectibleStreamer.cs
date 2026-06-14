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
        [SerializeField] int slotsPerCell = 2;      // 셀당 최대 수집물
        [SerializeField] int density = 70;          // 슬롯 채움 확률(%)
        [SerializeField] float pickupScale = 1.3f;  // 수집물 표시 스케일
        [SerializeField] float colliderRadius = 0.9f; // 수집 트리거 반경

        readonly Dictionary<int, List<GameObject>> cells = new();
        readonly List<ItemDef> candidates = new();   // PickByDepth 무할당 재사용
        int lastCenter = int.MinValue;

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
                var remove = new List<int>();
                foreach (var kv in cells) {
                    if (Mathf.Abs(kv.Key - center) > radiusCells) {
                        foreach (var go in kv.Value) {
                            if (go != null) {
                                Destroy(go);
                            }
                        }
                        remove.Add(kv.Key);
                    }
                }
                foreach (var k in remove) {
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

        // 셀 1칸 채움 — 슬롯마다 수심 랜덤 → 그 수심 출현 가능 아이템 결정적 선택
        void FillCell(int cell, List<GameObject> outList) {
            float unitPerM = (DepthMap.SurfaceY - DepthMap.SeabedY) / DepthMap.MaxDepthM;
            for (int s = 0; s < slotsPerCell; s++) {
                int h = Hash(cell, s);
                if (h % 100 >= density) {
                    continue;
                }
                float x = cell * cellWidth + ((h % 1000) / 1000f) * cellWidth;
                // 수중 깊이 — 수면 바로 아래 ~ 바닥 위
                float yWorld = Mathf.Lerp(DepthMap.SurfaceY - 1.5f, DepthMap.SeabedY + 1f, ((h >> 10) % 1000) / 1000f);
                float excelY = (yWorld - DepthMap.SurfaceY) / unitPerM;   // 게임y → 엑셀 수심
                var def = PickByDepth(excelY, h);
                if (def == null || def.prefab == null) {
                    continue;
                }
                // 바닥에 안 묻히게 — IsSolid면 위로 올림
                int xi = Mathf.RoundToInt(x);
                int yi = Mathf.RoundToInt(yWorld);
                int guard = 0;
                while (WorldGen.IsSolid(1, xi, yi) && yi < WorldGen.WaterY && guard++ < 40) {
                    yi++;
                }
                var go = Spawn(def, new Vector3(x, yi + 0.5f, 0f));
                if (go != null) {
                    outList.Add(go);
                }
            }
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
            return candidates[(h >> 18) % candidates.Count];
        }

        GameObject Spawn(ItemDef def, Vector3 pos) {
            try {
                float yaw = Hash(Mathf.RoundToInt(pos.x), def.id.Length) % 360;
                var go = Instantiate(def.prefab, pos, Quaternion.Euler(0f, yaw, 0f), transform);
                go.name = "Pickup_" + def.id;
                go.transform.localScale = Vector3.one * pickupScale;
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = colliderRadius;
                var pk = go.AddComponent<Game.Items.Pickup>();
                pk.SetDef(def);
                return go;
            } catch (Exception e) {
                Debug.LogError($"[CollectibleStreamer] {def?.id} 스폰 실패: {e.Message}");
                return null;
            }
        }

        // 좌표 결정적 해시 — 어느 시점에 스트리밍돼도 같은 배치
        static int Hash(int a, int b) {
            int h = a * 73856093 ^ b * 19349663;
            return (h % 100000 + 100000) % 100000;
        }
    }
}
