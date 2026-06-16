using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.World {
    // 깊이 연동 오염원 스트리머 — 잠수정 X를 따라 셀 단위로 Hazard 프리팹을 무한 스폰
    // 수심이 깊을수록 스폰 밀도가 높아짐(surfaceDensity → deepDensity, Lerp). CollectibleStreamer 추적-셀 패턴 차용
    public class HazardStreamer : MonoBehaviour {
        [SerializeField] Transform target;              // 2.5D 잠수정
        [SerializeField] GameObject hazardPrefab;       // 클론할 오염원 프리팹
        [SerializeField] float cellWidth = 8f;          // 셀 폭(u)
        [SerializeField] int radiusCells = 5;           // 좌우 로드 반경(셀)
        [SerializeField] float surfaceDensity = 8f;     // 수면 근처 셀 스폰 확률(%)
        [SerializeField] float deepDensity = 55f;       // 해저 근처 셀 스폰 확률(%)
        [SerializeField] int maxPerCell = 2;            // 셀당 최대 슬롯 수

        const float SpawnTopMargin = 0.5f;      // 수면 아래 여유(u)
        const float SpawnBottomMargin = 1f;     // 바닥 위 여유(u)

        readonly Dictionary<int, List<GameObject>> cells = new();
        int lastCenter = int.MinValue;

        GameBootstrap bootstrap;
        bool spawning = true;       // 현재 스폰 허용 상태(Dive에서만 true). bootstrap 미발견 시 기존 상시 동작
        bool targetWarned;          // target 미배선 경고 1회만 출력

        void Start() {
            // AmbientParticleGate 패턴 — GameBootstrap 자가탐색 후 상태 구독
            try {
                bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null) {
                    bootstrap.OnStateChanged += OnState;
                    spawning = (bootstrap.State == GameState.Dive);
                } else {
                    Debug.LogWarning("[HazardStreamer] GameBootstrap 미발견 — 상태 무시하고 기존 동작 유지");
                }
                // target 미배선 시 자가탐색(Camera.main → 플레이어 태그)
                if (target == null) {
                    if (Camera.main != null) {
                        target = Camera.main.transform;
                    } else {
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null) {
                            target = player.transform;
                        }
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[HazardStreamer] 초기화 실패: {e.Message}");
            }
        }

        void OnDestroy() {
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= OnState;
            }
        }

        // 상태 전환 — Dive 진입 시 스폰 허용, 이탈 시 스폰 셀 전체 정리
        void OnState(GameState from, GameState to) {
            spawning = (to == GameState.Dive);
            if (!spawning) {
                ClearAll();
            }
        }

        // 스폰된 셀 전체 Destroy + 캐시 비움 — 재진입 시 Reconcile이 다시 채움
        void ClearAll() {
            try {
                foreach (var kv in cells) {
                    foreach (var go in kv.Value) {
                        if (go != null) {
                            Destroy(go);
                        }
                    }
                }
                cells.Clear();
                lastCenter = int.MinValue;
            } catch (Exception e) {
                Debug.LogError($"[HazardStreamer] ClearAll 오류: {e.Message}");
            }
        }

        void LateUpdate() {
            // Dive 상태에서만 스폰(bootstrap 미발견 시 spawning=true로 기존 동작 유지)
            if (!spawning) {
                return;
            }
            if (hazardPrefab == null) {
                return;
            }
            if (target == null) {
                // 자가탐색 실패 시 스폰 정지 — 경고 1회만(매 프레임 도배 금지)
                if (!targetWarned) {
                    Debug.LogWarning("[HazardStreamer] target 미배선 — 스폰 정지");
                    targetWarned = true;
                }
                return;
            }
            // CollectibleStreamer와 동일한 추적-셀 방식 — X 셀이 바뀔 때만 Reconcile
            int center = Mathf.FloorToInt(target.position.x / cellWidth);
            if (center == lastCenter) {
                return;
            }
            lastCenter = center;
            Reconcile(center);
        }

        // 범위 밖 셀 Destroy + 범위 안 빈 셀 FillCell
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
                Debug.LogError($"[HazardStreamer] Reconcile 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // 셀 1칸 채움 — 슬롯별 Hash 결정적 확률 판정 + 깊이 정규화 depthT로 밀도 산출
        void FillCell(int cell, List<GameObject> outList) {
            try {
                for (int s = 0; s < maxPerCell; s++) {
                    // 슬롯 결정적 깊이 위치 — 슬롯별 비트대역 분리(CollectibleStreamer Hash 패턴 동일)
                    int h = Hash(cell, s);
                    float depthT = (s + ((h >> 10) & 0xFF) / 255f) / Mathf.Max(1, maxPerCell);
                    float yWorld = Mathf.Lerp(
                        DepthMap.SurfaceY - SpawnTopMargin,
                        DepthMap.SeabedY + SpawnBottomMargin,
                        depthT
                    );
                    // 셀 대표 수심(depthT)으로 스폰 확률 산출 — 깊을수록 밀도 증가
                    float density = DensityAt(depthT, surfaceDensity, deepDensity);
                    // 균등 0~100 판정(상위 비트대역 18~30 — x/depthT 대역과 분리). 모듈러 편향 제거
                    float roll = ((h >> 18) & 0x1FFF) / 8191f * 100f;
                    if (roll >= density) {
                        continue;
                    }
                    // X 분산 — 셀 내부에서 Hash 기반 오프셋
                    float x = cell * cellWidth + ((h & 0x3FF) / 1024f) * cellWidth;
                    float spawnY = FloorClamp(x, yWorld) + SpawnBottomMargin;
                    var pos = new Vector3(x, spawnY, 0f);
                    var go = SpawnHazard(cell, s, pos);
                    if (go != null) {
                        outList.Add(go);
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[HazardStreamer] FillCell({cell}) 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // 깊이 정규화 값(0=수면, 1=해저)으로 스폰 확률(%) 계산 — 테스트 가능 순수 함수
        public static float DensityAt(float depthT, float surfaceDensity, float deepDensity) {
            return Mathf.Lerp(surfaceDensity, deepDensity, Mathf.Clamp01(depthT));
        }

        // Hazard 프리팹 인스턴스화 — Hazard 컴포넌트 자체 patrol 사용, 추가 설정 불필요
        GameObject SpawnHazard(int cell, int slot, Vector3 pos) {
            try {
                var go = Instantiate(hazardPrefab, pos, Quaternion.identity, transform);
                go.name = $"Hazard_{cell}_{slot}";
                return go;
            } catch (Exception e) {
                Debug.LogError($"[HazardStreamer] Hazard_{cell}_{slot} 스폰 실패: {e.Message}");
                return null;
            }
        }

        // 바닥 솔리드면 위로 보정 — CollectibleStreamer FloorClamp 동일 패턴
        float FloorClamp(float x, float yWorld) {
            int xi = Mathf.RoundToInt(x);
            int yi = Mathf.FloorToInt(yWorld);
            int guard = 0;
            while (WorldGen.IsSolid(1, xi, yi) && yi < WorldGen.WaterY && guard++ < 40) {
                yi++;
            }
            // 솔리드 보정 없으면 연속값 유지, 있으면 정수 경계
            return Mathf.Max(yWorld, yi);
        }

        // 결정적 31비트 해시 — 재진입 시 동일 배치 보장(CollectibleStreamer 동일 구현)
        static int Hash(int a, int b) {
            unchecked {
                int h = a * 73856093 ^ b * 19349663;
                h = (h ^ (h >> 13)) * 1274126177;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
