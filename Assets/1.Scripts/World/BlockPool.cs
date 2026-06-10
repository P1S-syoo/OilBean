using System.Collections.Generic;
using UnityEngine;

namespace Game.World {
    // 블록 GameObject 풀 — 청크 로드/언로드 시 큐브를 재사용(인스턴스/파괴 GC 회피)
    public class BlockPool {
        readonly Stack<GameObject> idle = new();
        // 이중 반납 방어용 멤버십 추적 — Stack 자체는 중복 허용이라 별도 Set으로 관리
        readonly HashSet<GameObject> idleSet = new();
        readonly Transform root;
        readonly Mesh cubeMesh;

        public BlockPool(Transform root, Mesh cubeMesh) {
            this.root = root;
            this.cubeMesh = cubeMesh;
        }

        // 풀에서 1개 대여(없으면 생성)
        public GameObject Rent() {
            GameObject go;
            if (idle.Count > 0) {
                go = idle.Pop();
                idleSet.Remove(go);
                go.SetActive(true);
            } else {
                go = new GameObject("Block", typeof(MeshFilter), typeof(MeshRenderer), typeof(BlockTile), typeof(BoxCollider2D));
                go.GetComponent<MeshFilter>().sharedMesh = cubeMesh;
                go.GetComponent<BoxCollider2D>().enabled = false;   // 기본 끔(Playfield 노출면만 켬)
                go.transform.SetParent(root, false);
            }
            return go;
        }

        // 사용 끝난 블록 반납(비활성) — 이중 반납 시 경고 후 무시
        public void Return(GameObject go) {
            if (idleSet.Contains(go)) {
                Debug.LogWarning($"[BlockPool] 이중 반납 감지 — 무시: {go.name}");
                return;
            }
            go.SetActive(false);
            idle.Push(go);
            idleSet.Add(go);
        }

        public int IdleCount => idle.Count;
    }
}
