using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Game.Craft;

namespace Game.Tests {
    // Crafting 제작/업그레이드 + RunData 고철 소비·무게 업그레이드 EditMode 테스트
    public class CraftingTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.DestroyImmediate(go);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        RunData MakeRun() {
            return ScriptableObject.CreateInstance<RunData>();   // maxWeight 50
        }

        Crafting MakeCrafting(RunData run) {
            go = new GameObject("Crafting");
            var c = go.AddComponent<Crafting>();
            SetField(c, "run", run);
            SetField(c, "recipeId", "purifier");
            SetField(c, "buoyScrap", 3);
            SetField(c, "upgradeScrap", 2);
            SetField(c, "scrapUnitWeight", 8f);
            SetField(c, "weightBonus", 20f);
            return c;
        }

        static void AddScrap(RunData run, int n) {
            for (int i = 0; i < n; i++) run.TryAdd(ResourceKind.Scrap, 8f);
        }

        [Test]
        public void ConsumeScrap_reduces_count_and_weight() {
            var run = MakeRun();
            AddScrap(run, 3);                 // 24kg
            Assert.IsTrue(run.TryConsumeScrap(2, 8f));
            Assert.AreEqual(1, run.ScrapCount);
            Assert.AreEqual(8f, run.Weight, 0.001f);
        }

        [Test]
        public void ConsumeScrap_fails_when_insufficient() {
            var run = MakeRun();
            AddScrap(run, 1);
            Assert.IsFalse(run.TryConsumeScrap(3, 8f));
            Assert.AreEqual(1, run.ScrapCount);
        }

        [Test]
        public void CraftBuoy_needs_recipe_and_scrap() {
            var run = MakeRun();
            AddScrap(run, 3);
            var c = MakeCrafting(run);
            Assert.IsFalse(c.CanCraftBuoy, "레시피 해금 전엔 제작 불가");
            Assert.IsFalse(c.CraftBuoy());
            run.Unlock("purifier");
            Assert.IsTrue(c.CanCraftBuoy);
            Assert.IsTrue(c.CraftBuoy());
            Assert.IsTrue(run.BuoyReady);
            Assert.AreEqual(0, run.ScrapCount, "고철 3개 소비");
        }

        [Test]
        public void CraftBuoy_only_once() {
            var run = MakeRun();
            AddScrap(run, 6);
            run.Unlock("purifier");
            var c = MakeCrafting(run);
            Assert.IsTrue(c.CraftBuoy());
            Assert.IsFalse(c.CanCraftBuoy, "이미 제작됨 → 재제작 불가");
            Assert.IsFalse(c.CraftBuoy());
        }

        [Test]
        public void UpgradeWeight_raises_cap() {
            var run = MakeRun();
            AddScrap(run, 2);
            var c = MakeCrafting(run);
            float before = run.MaxWeight;
            Assert.IsTrue(c.UpgradeWeight());
            Assert.AreEqual(before + 20f, run.MaxWeight, 0.001f);
            Assert.AreEqual(0, run.ScrapCount);
        }

        [Test]
        public void Reset_clears_buoy_and_restores_cap() {
            var run = MakeRun();
            AddScrap(run, 2);
            var c = MakeCrafting(run);
            c.UpgradeWeight();               // 한계 70
            run.SetBuoyReady(true);
            run.ResetRun();
            Assert.IsFalse(run.BuoyReady);
            Assert.AreEqual(50f, run.MaxWeight, 0.001f, "리셋 시 기준 한계 복원");
        }
    }
}
