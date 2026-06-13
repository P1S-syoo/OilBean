using NUnit.Framework;
using Game.Core;

namespace Game.Tests {
    // GameFsm 전환 규칙 스모크 테스트
    public class GameFsmTests {
        [Test]
        public void Starts_in_dock() {
            var fsm = new GameFsm();
            Assert.AreEqual(GameState.Dock, fsm.Current);
        }

        [Test]
        public void Dock_to_dive_allowed() {
            var fsm = new GameFsm();
            Assert.IsTrue(fsm.Change(GameState.Dive));
            Assert.AreEqual(GameState.Dive, fsm.Current);
        }

        [Test]
        public void Dive_back_to_dock_roundtrip() {
            var fsm = new GameFsm();
            fsm.Change(GameState.Dive);
            Assert.IsTrue(fsm.Change(GameState.Dock));
            Assert.AreEqual(GameState.Dock, fsm.Current);
        }

        [Test]
        public void Invalid_transition_blocked() {
            var fsm = new GameFsm();
            fsm.Change(GameState.Dive);
            // Dive에서 Research로 직접 전환은 불가
            Assert.IsFalse(fsm.Change(GameState.Research));
            Assert.AreEqual(GameState.Dive, fsm.Current);
        }

        [Test]
        public void Event_fires_on_change() {
            var fsm = new GameFsm();
            GameState seen = GameState.Clear;
            fsm.OnChanged += (from, to) => seen = to;
            fsm.Change(GameState.Dive);
            Assert.AreEqual(GameState.Dive, seen);
        }

        [Test]
        public void Surface_initial_state_supported() {
            // 수상 씬은 Surface로 시작, 기본 생성은 기존대로 Dock
            var fsm = new GameFsm(GameState.Surface);
            Assert.AreEqual(GameState.Surface, fsm.Current);
            Assert.AreEqual(GameState.Dock, new GameFsm().Current);
        }

        [Test]
        public void Surface_to_dock_allowed_dive_blocked() {
            var fsm = new GameFsm(GameState.Surface);
            // 수상에서 바로 수중 탐사는 불가 — 잠수(Dock)를 거쳐야 함
            Assert.IsFalse(fsm.Change(GameState.Dive));
            Assert.IsTrue(fsm.Change(GameState.Dock));
        }

        [Test]
        public void Clear_to_surface_allowed() {
            // 클리어 후 수면 복귀(W6) 경로
            var fsm = new GameFsm();
            fsm.Change(GameState.Dive);
            fsm.Change(GameState.Clear);
            Assert.IsTrue(fsm.Change(GameState.Surface));
        }
    }

    // RunData 통합 무게 회계/적재 로직 테스트
    public class RunDataTests {
        RunData Make() {
            return UnityEngine.ScriptableObject.CreateInstance<RunData>();   // maxWeight 기본 50
        }

        [Test]
        public void TryAdd_succeeds_until_capacity() {
            // maxWeight 값에 비의존 — 한계만큼 채우면 가득 차고 추가 적재 거부
            var run = Make();
            float cap = run.MaxWeight;
            Assert.IsTrue(run.TryAdd(ResourceKind.Scrap, cap), "한계만큼 적재는 성공");
            Assert.AreEqual(cap, run.Weight, 0.001f);
            Assert.IsFalse(run.HasRoom(0.01f), "한계 도달 후 여유 없음");
        }

        [Test]
        public void TryAdd_rejects_when_over_capacity() {
            var run = Make();
            float cap = run.MaxWeight;
            run.TryAdd(ResourceKind.Scrap, cap);   // 가득
            Assert.IsFalse(run.TryAdd(ResourceKind.Scrap, 1f), "초과 적재는 거부");
            Assert.AreEqual(cap, run.Weight, 0.001f, "거부 시 무게 변화 없음");
        }

        [Test]
        public void Counts_track_by_kind() {
            var run = Make();
            run.TryAdd(ResourceKind.Scrap, 5f);
            run.TryAdd(ResourceKind.Sample, 5f);
            run.TryAdd(ResourceKind.Sample, 5f);
            Assert.AreEqual(1, run.ScrapCount);
            Assert.AreEqual(2, run.SampleCount);
        }

        [Test]
        public void Reset_clears_all() {
            var run = Make();
            run.TryAdd(ResourceKind.Scrap, 20f);
            run.ResetRun();
            Assert.AreEqual(0f, run.Weight, 0.001f);
            Assert.AreEqual(0, run.ScrapCount);
        }

        [Test]
        public void Bank_steel_accumulates_and_consumes() {
            var run = Make();
            run.AddSteel(0, 16f);
            run.AddSteel(0, 8f);
            Assert.AreEqual(24f, run.GetSteel(0), 0.001f);
            Assert.IsFalse(run.TryConsumeSteel(0, 25f), "부족하면 거부");
            Assert.IsTrue(run.TryConsumeSteel(0, 24f));
            Assert.AreEqual(0f, run.GetSteel(0), 0.001f);
        }

        [Test]
        public void Bank_samples_track_by_level() {
            var run = Make();
            run.AddSampleAt(1);
            run.AddSampleAt(1);
            run.AddSampleAt(3);
            Assert.AreEqual(2, run.GetSampleCount(1));
            Assert.AreEqual(0, run.GetSampleCount(2));
            Assert.AreEqual(1, run.GetSampleCount(3));
            Assert.IsTrue(run.TryConsumeSampleAt(1));
            Assert.AreEqual(1, run.GetSampleCount(1));
            Assert.IsFalse(run.TryConsumeSampleAt(2), "없으면 거부");
        }

        [Test]
        public void Research_points_and_max_level() {
            var run = Make();
            run.AddResearchPoints(1);
            run.AddResearchPoints(3);
            run.AddResearchPoints(2);
            Assert.AreEqual(6, run.ResearchPoints);
            Assert.AreEqual(3, run.MaxAnalyzedLevel, "최고 분석 오염수준 갱신");
        }

        [Test]
        public void Buoy_stage_gates_depth() {
            var run = Make();
            Assert.AreEqual(15f, run.MaxDepth(), 0.001f, "기본 진입 수심");
            run.SetBuoyStage(1);
            Assert.AreEqual(35f, run.MaxDepth(), 0.001f);
            run.SetBuoyStage(2);
            Assert.AreEqual(50f, run.MaxDepth(), 0.001f);
            run.SetBuoyStage(99);
            Assert.AreEqual(3, run.BuoyStage, "단계 클램프");
        }

        [Test]
        public void Reset_clears_bank_and_research() {
            var run = Make();
            run.AddSteel(1, 10f);
            run.AddSampleAt(2);
            run.AddResearchPoints(3);
            run.SetBuoyStage(2);
            run.ResetRun();
            Assert.AreEqual(0f, run.GetSteel(1), 0.001f);
            Assert.AreEqual(0, run.GetSampleCount(2));
            Assert.AreEqual(0, run.ResearchPoints);
            Assert.AreEqual(0, run.MaxAnalyzedLevel);
            Assert.AreEqual(0, run.BuoyStage);
        }

        [Test]
        public void Unlock_is_idempotent() {
            var run = Make();
            run.Unlock("purifier");
            run.Unlock("purifier");
            Assert.IsTrue(run.IsUnlocked("purifier"));
        }

        [Test]
        public void Surface_target_records_and_resets() {
            // 잠수 시 다음 수상 목표 기록, 새 판이면 0으로
            var run = Make();
            run.SetSurfaceTarget(2);
            Assert.AreEqual(2, run.SurfaceTargetIdx);
            run.SetSurfaceTarget(-1);
            Assert.AreEqual(0, run.SurfaceTargetIdx, "음수는 0으로 방어");
            run.SetSurfaceTarget(3);
            run.ResetRun();
            Assert.AreEqual(0, run.SurfaceTargetIdx);
        }
    }
}
