using NUnit.Framework;
using UnityEditor;
using Game.Core;
using Game.World;

namespace Game.Tests {
    // 수집물 스폰 도달성 — 모든 아이템의 출현 수심대가 스트리머 스폰 범위와 교집합이 있어야(scrap_Can 회귀 방지)
    public class CollectibleSpawnTests {
        const string TablePath = "Assets/3.Data/ItemDataTable.asset";

        [Test]
        public void Every_item_is_spawnable() {
            var table = AssetDatabase.LoadAssetAtPath<ItemDataTable>(TablePath);
            Assert.IsNotNull(table, $"테이블 없음: {TablePath} — '아이템 테이블 생성' 먼저");
            CollectibleStreamer.SpawnExcelRange(out float excelMin, out float excelMax);
            foreach (var it in table.items) {
                Assert.IsNotNull(it, "빈 아이템 정의");
                // 교집합 조건: 아이템 [minSpawnY, maxSpawnY] ∩ 스폰 [excelMin, excelMax] ≠ ∅
                bool reachable = it.maxSpawnY >= excelMin && it.minSpawnY <= excelMax;
                Assert.IsTrue(reachable,
                    $"{it.id}({it.minSpawnY}~{it.maxSpawnY})가 스폰 범위({excelMin:F1}~{excelMax:F1}) 밖 — 영영 안 나옴");
            }
        }
    }
}
