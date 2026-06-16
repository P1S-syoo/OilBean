using System.Collections.Generic;
using UnityEngine;

namespace Game.World {
    // 청크 스트리밍 — 플레이어 주변 청크만 풀에서 생성/반납(보이는 범위+α만 렌더). 대규모 맵 메모리 상수 유지
    public class ChunkStreamer : MonoBehaviour {
        [SerializeField] Transform target;          // 플레이어(잠수정)
        [SerializeField] int chunkW = 16;           // 청크 가로(블록)
        [SerializeField] int chunkH = 16;           // 청크 세로(블록)
        [SerializeField] int radiusX = 2;           // 가로 로드 반경(청크) — 화면+α
        [SerializeField] int radiusY = 2;           // 세로 로드 반경(청크)
        [SerializeField] float blockScale = 0.95f;  // 블록 틈(격자감)
        [SerializeField] float playfieldDepth = 4f; // Playfield 블록 Z 두께 — 솔리드 덩어리(앞뒤 빈틈 제거)
        [SerializeField] Mesh blockMesh;            // 2-서브메시 VoxelBlock(윗면/옆면). 없으면 기본 큐브
        [SerializeField] Material terrainTriMat;    // 지형 triplanar(월드UV 연속 텍스처) — 타일 반복 제거
        [SerializeField] Material bgTriMat;         // 배경 triplanar(흐림). 없으면 terrainTriMat
        [SerializeField] Material[] terrainTops;    // (구) 윗면 변형 — triMat 없을 때 폴백
        [SerializeField] Material[] terrainSides;   // (구) 옆면 변형 — triMat 없을 때 폴백
        [SerializeField] Material bgTop;            // 배경 윗면(흐림). 없으면 terrainTops[0]
        [SerializeField] Material bgSide;           // 배경 옆면(흐림). 없으면 terrainSide
        [SerializeField] Material[] materials;      // 강변(Shore) 전용 팔레트 [layer*12 + role*3 + variant]

        BlockPool pool;
        Mesh cubeMesh;
        Material[][] terrainPairs;                  // [variant] = [top, side] 캐시(무할당)
        Material[] bgPair;
        Material[] triPair, bgTriPair;              // triplanar 페어 캐시
        Material[][] shorePairs;
        readonly Dictionary<Vector2Int, List<GameObject>> loaded = new();
        readonly List<WorldGen.Spec> specBuf = new(3);
        readonly List<Vector2Int> removeBuf = new();   // Reconcile 무할당 재사용
        Vector2Int lastChunk = new(int.MinValue, int.MinValue);

        void Awake() {
            try {
                // VoxelBlock 메시(2-서브메시) 우선, 없으면 기본 큐브
                if (blockMesh != null) {
                    cubeMesh = blockMesh;
                } else {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                    DestroyImmediate(tmp);
                }
                pool = new BlockPool(transform, cubeMesh);
                // variant별 [top, side] 페어 캐시 — variant는 0~2 고정 3종이므로 배열도 3 고정
                const int VariantCount = 3;
                terrainPairs = new Material[VariantCount][];
                bool hasSides = terrainSides != null && terrainSides.Length > 0;
                for (int i = 0; i < VariantCount; i++) {
                    // terrainTops 부족 시 순환(마지막 항목 재사용)해 인덱스 어긋남 방지
                    var t = (terrainTops != null && terrainTops.Length > 0) ? terrainTops[i % terrainTops.Length] : null;
                    var s = hasSides ? terrainSides[i % terrainSides.Length] : null;
                    terrainPairs[i] = new[] { t, s };
                }
                var bt = bgTop != null ? bgTop : terrainPairs[0][0];
                var fallSide = hasSides ? terrainSides[0] : null;
                bgPair = new[] { bt, bgSide != null ? bgSide : fallSide };
                // triplanar 페어(양 서브메시 동일 — 월드 노멀로 윗면/옆면 판정)
                if (terrainTriMat != null) {
                    triPair = new[] { terrainTriMat, terrainTriMat };
                    var bgm = bgTriMat != null ? bgTriMat : terrainTriMat;
                    bgTriPair = new[] { bgm, bgm };
                }
                if (materials != null) {
                    shorePairs = new Material[materials.Length][];
                    for (int i = 0; i < materials.Length; i++) {
                        if (materials[i] != null) shorePairs[i] = new[] { materials[i], materials[i] };
                    }
                }
            } catch (System.Exception e) {
                Debug.LogError($"[ChunkStreamer] Awake 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        void LateUpdate() {
            if (target == null || pool == null) {
                return;
            }
            var pc = ToChunk(target.position);
            if (pc == lastChunk) {
                return;   // 같은 청크면 갱신 불필요
            }
            lastChunk = pc;
            Reconcile(pc);
        }

        Vector2Int ToChunk(Vector3 p) {
            return new Vector2Int(Mathf.FloorToInt(p.x / chunkW), Mathf.FloorToInt(p.y / chunkH));
        }

        // 원하는 청크 집합과 현재 로드 집합을 비교해 로드/언로드
        void Reconcile(Vector2Int center) {
            // 1) 범위 밖 청크 언로드
            removeBuf.Clear();
            foreach (var kv in loaded) {
                var c = kv.Key;
                if (Mathf.Abs(c.x - center.x) > radiusX || Mathf.Abs(c.y - center.y) > radiusY) {
                    foreach (var go in kv.Value) {
                        pool.Return(go);
                    }
                    removeBuf.Add(c);
                }
            }
            foreach (var c in removeBuf) {
                loaded.Remove(c);
            }
            // 2) 범위 안 새 청크 로드 — X는 무한 순환 맵이라 clamp 없음(생성은 WorldGen이 좌표로 결정)
            for (int cx = center.x - radiusX; cx <= center.x + radiusX; cx++) {
                for (int cy = center.y - radiusY; cy <= center.y + radiusY; cy++) {
                    var c = new Vector2Int(cx, cy);
                    if (!loaded.ContainsKey(c)) {
                        loaded[c] = LoadChunk(c);
                    }
                }
            }
        }

        // 청크 1개 생성(WorldGen 샘플링 → 풀에서 블록 대여)
        List<GameObject> LoadChunk(Vector2Int c) {
            var list = new List<GameObject>();
            int x0 = c.x * chunkW, y0 = c.y * chunkH;
            for (int x = x0; x < x0 + chunkW; x++) {
                for (int y = y0; y < y0 + chunkH; y++) {
                    specBuf.Clear();
                    WorldGen.Column(x, y, specBuf);
                    foreach (var s in specBuf) {
                        var go = pool.Rent();
                        // Playfield는 Z로 두껍게 → 솔리드 덩어리(앞뒤 빈틈 제거). 나머지 레이어는 얇게
                        float z = WorldGen.LayerZ[s.layer];
                        var scale = Vector3.one * blockScale;
                        if (s.layer == 1) {
                            scale.z = playfieldDepth;
                            z = playfieldDepth * 0.5f - 0.5f;   // 앞면을 ~-0.5(카메라 쪽)에 맞춤
                        }
                        go.transform.localPosition = new Vector3(x, y, z);
                        go.transform.localScale = scale;
                        go.GetComponent<BlockTile>().Set(s.role, s.variant);
                        // per-face 머티리얼: 지형=윗면/옆면 분리, 배경=흐림, 강변=팔레트색(양 서브메시 동일)
                        Material[] mats;
                        if (s.layer == 3) {
                            int idx = 3 * 12 + (int)s.role * 3 + s.variant;
                            mats = (shorePairs != null && idx < shorePairs.Length && shorePairs[idx] != null) ? shorePairs[idx] : terrainPairs[0];
                        } else if (s.layer == 0) {
                            mats = (bgTriPair != null) ? bgTriPair : bgPair;          // 배경
                        } else {
                            mats = (triPair != null) ? triPair : terrainPairs[s.variant % 3];   // 지형 triplanar(variant 0~2 고정 3종)
                        }
                        if (mats != null) {
                            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
                        }
                        // 충돌은 Playfield(Z0) 노출 표면만 — 2D 콜라이더는 Z 무시라 배경/돌출에 켜면 오충돌
                        var col = go.GetComponent<BoxCollider2D>();
                        if (col != null) {
                            col.enabled = (s.layer == 1) && WorldGen.Exposed(x, y);
                            // blockScale 축소 보정: 콜라이더 크기를 월드 기준 1×1로 유지해 블록 사이 틈 없앰
                            float invScale = 1f / blockScale;
                            col.size = new Vector2(invScale, invScale);
                        }
                        list.Add(go);
                    }
                }
            }
            return list;
        }

        public int LoadedChunks => loaded.Count;
    }
}
