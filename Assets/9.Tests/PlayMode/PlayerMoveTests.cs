using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Player;
using Game.Core;
using Game.World;

namespace Game.Tests {
    // PlayerMove 이동/관성 PlayMode 스모크 테스트(입력은 SetMoveInput로 주입)
    public class PlayerMoveTests {
        GameObject go;
        RunData run;

        [TearDown]
        public void Cleanup() {
            if (go != null) {
                Object.Destroy(go);
            }
            if (run != null) {
                Object.Destroy(run);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        PlayerMove MakeMover() {
            go = new GameObject("Sub");
            return go.AddComponent<PlayerMove>();   // RequireComponent로 Rigidbody2D 자동 추가
        }

        [UnityTest]
        public IEnumerator DepthGate_blocks_below_buoy_limit() {
            var mv = MakeMover();
            run = ScriptableObject.CreateInstance<RunData>();   // BuoyStage 0 → MaxDepth 15m
            SetField(mv, "run", run);
            mv.SetDepthGate(true);
            yield return new WaitForFixedUpdate();
            float limit0 = DepthMap.DepthToWorld(15f);          // ≈18.9
            go.transform.position = new Vector3(0f, limit0 - 5f, 0f);   // 한계 아래로 강제
            for (int i = 0; i < 5; i++) {
                yield return new WaitForFixedUpdate();
            }
            Assert.GreaterOrEqual(go.transform.position.y, limit0 - 0.2f, "기본 단계: 한계 수심 위로 클램프");
            // 부유체 2단계 → 50m 해제 → 더 깊이 가능
            run.SetBuoyStage(2);
            float limit2 = DepthMap.DepthToWorld(50f);          // ≈7
            go.transform.position = new Vector3(0f, limit2 + 3f, 0f);
            for (int i = 0; i < 5; i++) {
                yield return new WaitForFixedUpdate();
            }
            Assert.GreaterOrEqual(go.transform.position.y, limit2 - 0.2f, "2단계: 더 깊은 한계까지 허용");
            Assert.Less(go.transform.position.y, limit0, "2단계 한계는 기본보다 깊음");
        }

        [UnityTest]
        public IEnumerator DepthGate_off_does_not_clamp() {
            var mv = MakeMover();
            run = ScriptableObject.CreateInstance<RunData>();
            SetField(mv, "run", run);
            mv.SetDepthGate(false);   // 게이트 꺼짐(거점/연출)
            yield return new WaitForFixedUpdate();
            go.transform.position = new Vector3(0f, 0f, 0f);   // 게임 한계 한참 아래여도
            for (int i = 0; i < 5; i++) {
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(0f, go.transform.position.y, 0.1f, "게이트 꺼지면 클램프 안 함");
        }

        [UnityTest]
        public IEnumerator Moves_right_on_input() {
            var mv = MakeMover();
            yield return new WaitForFixedUpdate();
            mv.SetMoveInput(Vector2.right);
            for (int i = 0; i < 20; i++) {
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(mv.Velocity.x, 0.1f, "오른쪽 입력 시 +x로 이동해야 함");
        }

        [UnityTest]
        public IEnumerator Decelerates_after_release() {
            var mv = MakeMover();
            yield return new WaitForFixedUpdate();
            mv.SetMoveInput(Vector2.right);
            for (int i = 0; i < 20; i++) {
                yield return new WaitForFixedUpdate();
            }
            float moving = mv.Velocity.magnitude;
            mv.SetMoveInput(Vector2.zero);
            for (int i = 0; i < 30; i++) {
                yield return new WaitForFixedUpdate();
            }
            Assert.Less(mv.Velocity.magnitude, moving, "입력 해제 후 관성으로 감속해야 함");
        }

        [UnityTest]
        public IEnumerator Speed_is_capped() {
            var mv = MakeMover();
            yield return new WaitForFixedUpdate();
            mv.SetMoveInput(Vector2.one);   // 대각선 최대 입력
            for (int i = 0; i < 120; i++) {
                yield return new WaitForFixedUpdate();
            }
            Assert.LessOrEqual(mv.Velocity.magnitude, 6.5f, "최고 속도 상한(maxSpeed≈6) 근처로 제한돼야 함");
        }
    }
}
