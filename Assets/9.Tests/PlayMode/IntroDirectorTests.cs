using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Surface;

namespace Game.Tests {
    // IntroDirector 입력 잠금/해제·종료 인계 PlayMode 테스트
    public class IntroDirectorTests {
        GameObject root;

        [TearDown]
        public void Cleanup() {
            if (root != null) {
                Object.Destroy(root);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        [UnityTest]
        public IEnumerator Missing_refs_skip_cutscene_and_keep_control() {
            root = new GameObject("Root");
            var deckGo = new GameObject("Deck");
            deckGo.transform.SetParent(root.transform, false);
            var deck = deckGo.AddComponent<DeckCharacter>();

            var dirGo = new GameObject("Intro");
            dirGo.transform.SetParent(root.transform, false);
            dirGo.SetActive(false);
            var director = dirGo.AddComponent<IntroDirector>();
            SetField(director, "deck", deck);   // introCam/focus 미연결
            dirGo.SetActive(true);
            yield return null;
            Assert.IsTrue(deck.enabled, "참조 없으면 컷신 생략하고 즉시 조작 가능");
            Assert.IsFalse(director.Running);
        }

        [UnityTest]
        public IEnumerator Locks_control_during_dolly_then_releases() {
            root = new GameObject("Root");
            var focus = new GameObject("Focus");
            focus.transform.SetParent(root.transform, false);
            var deckGo = new GameObject("Deck");
            deckGo.transform.SetParent(root.transform, false);
            var deck = deckGo.AddComponent<DeckCharacter>();
            var camGo = new GameObject("IntroCam");
            camGo.transform.SetParent(root.transform, false);
            var introCam = camGo.AddComponent<CinemachineCamera>();

            var dirGo = new GameObject("Intro");
            dirGo.transform.SetParent(root.transform, false);
            dirGo.SetActive(false);
            var director = dirGo.AddComponent<IntroDirector>();
            SetField(director, "introCam", introCam);
            SetField(director, "focus", focus.transform);
            SetField(director, "deck", deck);
            SetField(director, "duration", 0.2f);
            dirGo.SetActive(true);
            yield return null;
            Assert.IsFalse(deck.enabled, "컷신 중 조작 잠금");
            Assert.IsTrue(director.Running);
            // 돌리 종료까지 대기
            for (int i = 0; i < 120 && director.Running; i++) {
                yield return null;
            }
            Assert.IsTrue(deck.enabled, "컷신 종료 후 조작 해제");
            Assert.IsFalse(introCam.gameObject.activeSelf, "종료 시 인트로 카메라 비활성");
        }
    }
}
