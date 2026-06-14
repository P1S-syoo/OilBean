using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Game.Craft;

namespace Game.Tests {
    // Crafting 제작(부유체 3단계·업그레이드 3종, 강재 등급별 kg 소비) EditMode 테스트
    public class CraftingTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) {
                Object.DestroyImmediate(go);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        RunData MakeRun() {
            return ScriptableObject.CreateInstance<RunData>();
        }

        Crafting MakeCrafting(RunData run) {
            go = new GameObject("Crafting");
            var c = go.AddComponent<Crafting>();
            SetField(c, "run", run);
            return c;
        }

        [Test]
        public void Buoy1_needs_agent_and_steel() {
            var run = MakeRun();
            run.AddSteel(0, 24f);   // 일반강재 24kg
            var c = MakeCrafting(run);
            Assert.IsFalse(c.CanCraft("buoy_1"), "약품 미해금이면 제작 불가");
            run.Unlock("agent_mild");
            Assert.IsTrue(c.CanCraft("buoy_1"));
            Assert.IsTrue(c.Craft("buoy_1"));
            Assert.AreEqual(1, run.BuoyStage, "부유체 1단계");
            Assert.AreEqual(0f, run.GetSteel(0), 0.001f, "강재 24kg 소비");
        }

        [Test]
        public void Buoy_must_be_sequential() {
            var run = MakeRun();
            run.AddSteel(1, 30f);
            run.Unlock("agent_mid");
            var c = MakeCrafting(run);
            Assert.IsFalse(c.CanCraft("buoy_2"), "1단계 없이 2단계 불가(순차)");
        }

        [Test]
        public void Insufficient_steel_blocks_craft() {
            var run = MakeRun();
            run.AddSteel(0, 20f);   // 24 미만
            run.Unlock("agent_mild");
            var c = MakeCrafting(run);
            Assert.IsFalse(c.CanCraft("buoy_1"));
            Assert.IsFalse(c.Craft("buoy_1"));
        }

        [Test]
        public void Upgrade_cargo_raises_cap_and_consumes_steel() {
            var run = MakeRun();
            run.AddSteel(0, 16f);
            var c = MakeCrafting(run);
            float before = run.MaxWeight;
            Assert.IsTrue(c.Craft("up_cargo"));
            Assert.AreEqual(before + 20f, run.MaxWeight, 0.001f);
            Assert.AreEqual(0f, run.GetSteel(0), 0.001f);
        }

        [Test]
        public void Hull_armor_once() {
            var run = MakeRun();
            run.AddSteel(2, 60f);
            var c = MakeCrafting(run);
            Assert.IsTrue(c.Craft("gear_hull"));
            Assert.IsTrue(run.HullArmor);
            Assert.IsFalse(c.CanCraft("gear_hull"), "이미 장착이면 재제작 불가");
            Assert.IsTrue(run.ConsumeHullArmor(), "충돌 1회 흡수");
            Assert.IsFalse(run.HullArmor, "흡수 후 소진");
        }

        [Test]
        public void Legacy_api_buoy_and_upgrade() {
            // CraftPanel 호환 — CraftBuoy()=다음 단계, UpgradeWeight()=up_cargo
            var run = MakeRun();
            run.AddSteel(0, 24f);
            run.Unlock("agent_mild");
            var c = MakeCrafting(run);
            Assert.IsTrue(c.CanCraftBuoy);
            Assert.IsTrue(c.CraftBuoy());
            Assert.AreEqual(1, run.BuoyStage);
            run.AddSteel(0, 16f);
            Assert.IsTrue(c.CanUpgrade);
            Assert.IsTrue(c.UpgradeWeight());
        }

        [Test]
        public void Reset_clears_buoy_hull_and_cap() {
            var run = MakeRun();
            run.AddSteel(0, 16f);
            var c = MakeCrafting(run);
            c.Craft("up_cargo");
            float upgraded = run.MaxWeight;
            run.SetBuoyStage(2);
            run.SetHullArmor(true);
            run.ResetRun();
            Assert.AreEqual(0, run.BuoyStage);
            Assert.IsFalse(run.HullArmor);
            Assert.Less(run.MaxWeight, upgraded, "리셋 시 기준 한계 복원");
        }
    }
}
