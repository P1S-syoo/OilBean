using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Player;

namespace Game.Tests {
    // PlayerMove 이동/관성 PlayMode 스모크 테스트(입력은 SetMoveInput로 주입)
    public class PlayerMoveTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) {
                Object.Destroy(go);
            }
        }

        PlayerMove MakeMover() {
            go = new GameObject("Sub");
            return go.AddComponent<PlayerMove>();   // RequireComponent로 Rigidbody2D 자동 추가
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
