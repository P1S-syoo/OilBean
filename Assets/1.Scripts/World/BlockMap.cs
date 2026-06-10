using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.World {
    // 블록 맵 — 자식 BlockTile을 전부 수집해 {pos,role,variant} JSON으로 기록(VARCO 모델 생성 입력)
    public class BlockMap : MonoBehaviour {
        // 직렬화용 블록 1개 레코드(VARCO가 역할/변형별 모델을 이 좌표에 배치)
        [Serializable]
        public struct BlockRecord {
            public float x, y, z;
            public string role;
            public int variant;
        }

        // 직렬화 루트 — role/variant별 카운트 요약 포함
        [Serializable]
        public class BlockData {
            public float gridSize = 1f;
            public int total;
            public List<BlockRecord> blocks = new();
        }

        [SerializeField] float gridSize = 1f;   // 블록 그리드 단위(메타)

        // 자식 BlockTile 전부 수집
        public BlockData Collect() {
            var data = new BlockData { gridSize = gridSize };
            try {
                var tiles = GetComponentsInChildren<BlockTile>();
                foreach (var t in tiles) {
                    var p = t.WorldPos;
                    data.blocks.Add(new BlockRecord {
                        x = p.x, y = p.y, z = p.z,
                        role = t.Role.ToString(),
                        variant = t.Variant
                    });
                }
                data.total = data.blocks.Count;
            } catch (Exception e) {
                Debug.LogError($"[BlockMap] 수집 오류: {e.Message}\n{e.StackTrace}");
            }
            return data;
        }

        public string ToJson() => JsonUtility.ToJson(Collect(), true);

        // JSON 파일로 내보내기(에디터 우클릭 또는 코드 호출) — VARCO 입력용
        [ContextMenu("BlockMap JSON 내보내기")]
        public void ExportJson() {
            try {
                var path = Path.Combine(Application.dataPath, "6.Data/blockmap.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, ToJson());
                Debug.Log($"[BlockMap] 내보냄: {path} ({Collect().total} 블록)");
            } catch (Exception e) {
                Debug.LogError($"[BlockMap] 내보내기 오류: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
