using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core {
    // 수집물 1종 정의 — 엑셀 ItemDataTable의 한 행
    [Serializable]
    public class ItemDef {
        public string id;              // scrap_Can, sample_oil_clump 등
        public ResourceKind kind;      // Scrap / Sample
        public string displayName;     // 표시명(한글)
        public int grade;              // 0~2 — 고철=강재등급(일반/합금/특수), 샘플=희귀도(Common/Uncommon/Rare)
        public float weight;           // 적재·소비 무게(kg)
        public int minSpawnY;          // 출현 최소 Y(깊이) — 음수일수록 깊음
        public int maxSpawnY;          // 출현 최대 Y
        public int pollutionLevel;     // 오염수준 0~3 — 샘플 분석 시 분석포인트로 사용
        public GameObject prefab;      // FBX 모델(배치·스폰용)
        public float spawnScale = 1f;  // 스폰 스케일 — 에디터에서 prefab 바운즈 측정해 목표 크기로 정규화(런타임 stale 회피)
    }

    // 수집물 마스터 데이터 — 고철 11종 + 샘플 10종. 스폰·수집·연구·제작이 공통 참조
    [CreateAssetMenu(fileName = "ItemDataTable", menuName = "Game/ItemDataTable")]
    public class ItemDataTable : ScriptableObject {
        public ItemDef[] items;

        Dictionary<string, ItemDef> byId;   // id 조회 캐시(최초 1회 구축)

        public ItemDef Find(string id) {
            if (byId == null) {
                byId = new Dictionary<string, ItemDef>();
                if (items != null) {
                    foreach (var it in items) {
                        if (it != null && !string.IsNullOrEmpty(it.id)) {
                            byId[it.id] = it;
                        }
                    }
                }
            }
            return id != null && byId.TryGetValue(id, out var v) ? v : null;
        }

        // 강재 등급 한글명(고철) — grade 0~2
        public static string SteelName(int grade) {
            switch (grade) {
                case 0: return "일반 강재";
                case 1: return "합금 강재";
                default: return "특수 강재";
            }
        }

        // 희귀도 한글명(샘플) — grade 0~2
        public static string RarityName(int grade) {
            switch (grade) {
                case 0: return "Common";
                case 1: return "Uncommon";
                default: return "Rare";
            }
        }
    }
}
