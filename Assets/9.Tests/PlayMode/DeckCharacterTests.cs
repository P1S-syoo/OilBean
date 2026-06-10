using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Surface;

namespace Game.Tests {
    // DeckCharacter 덱 이동/경계 클램프 PlayMode 테스트 — Step 직접 호출로 입력 주입
    public class DeckCharacterTests {
        GameObject sub;

        [TearDown]
        public void Cleanup() {
            if (sub != null) {
                Object.Destroy(sub);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        DeckCharacter Make() {
            sub = new GameObject("Sub");
            var go = new GameObject("Player");
            go.transform.SetParent(sub.transform, false);
            var dc = go.AddComponent<DeckCharacter>();
            SetField(dc, "deckHalf", new Vector2(1.8f, 4.6f));
            return dc;
        }

        [UnityTest]
        public IEnumerator Moves_with_input() {
            var dc = Make();
            yield return null;
            for (int i = 0; i < 30; i++) {
                dc.Step(new Vector2(0f, 1f), 0.02f);   // 전진 입력
            }
            Assert.Greater(dc.transform.localPosition.z, 0.5f, "전진 입력으로 덱 위를 이동해야");
        }

        [UnityTest]
        public IEnumerator Clamped_inside_deck() {
            var dc = Make();
            yield return null;
            for (int i = 0; i < 600; i++) {
                dc.Step(new Vector2(1f, 0f), 0.02f);   // 우측으로 계속 밀어붙임
            }
            Assert.AreEqual(1.8f, dc.transform.localPosition.x, 0.01f, "덱 경계에서 멈춰야(물에 못 떨어짐)");
            for (int i = 0; i < 600; i++) {
                dc.Step(new Vector2(0f, -1f), 0.02f);   // 후방으로 계속
            }
            Assert.AreEqual(-4.6f, dc.transform.localPosition.z, 0.01f, "후방 경계 클램프");
        }

        [UnityTest]
        public IEnumerator Moving_deck_does_not_slide_character() {
            var dc = Make();
            yield return null;
            var local = dc.transform.localPosition;
            // 잠수정이 이동·회전해도 입력 없으면 로컬 위치 불변(부모화로 미끄러짐 0)
            sub.transform.position += new Vector3(10f, 0f, 5f);
            sub.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            yield return null;
            Assert.AreEqual(local.x, dc.transform.localPosition.x, 0.001f);
            Assert.AreEqual(local.z, dc.transform.localPosition.z, 0.001f);
        }
    }
}
