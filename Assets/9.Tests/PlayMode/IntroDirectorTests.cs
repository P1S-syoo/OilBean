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
            Assert.IsFalse(deck.ControlLocked, "참조 없으면 컷신 생략하고 즉시 조작 가능");
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
            Assert.IsTrue(deck.ControlLocked, "컷신 중 조작 잠금(접지 스냅은 유지)");
            Assert.IsTrue(deck.enabled, "컷신 중에도 컴포넌트는 살아서 접지 스냅 동작");
            Assert.IsTrue(director.Running);
            // 돌리 종료까지 대기
            for (int i = 0; i < 120 && director.Running; i++) {
                yield return null;
            }
            Assert.IsFalse(deck.ControlLocked, "컷신 종료 후 조작 해제");
            Assert.IsFalse(introCam.gameObject.activeSelf, "종료 시 인트로 카메라 비활성");
        }

        [UnityTest]
        public IEnumerator Sailing_waits_until_cutscene_ends() {
            // 컷신 중 목표 도달 경합 방지 — 항해는 컷신 종료 후 출발
            root = new GameObject("Root");
            var focus = new GameObject("Focus");
            focus.transform.SetParent(root.transform, false);
            var navGo = new GameObject("Sub");
            navGo.transform.SetParent(root.transform, false);
            // river 미연결 에러 로그는 예상된 것 — enabled 토글만 검증
            LogAssert.Expect(LogType.Error, "[SubNavigator] 강 스플라인 미연결 — 인스펙터에서 할당하세요.");
            var nav = navGo.AddComponent<Game.Surface.SubNavigator>();
            var camGo = new GameObject("IntroCam");
            camGo.transform.SetParent(root.transform, false);
            var introCam = camGo.AddComponent<CinemachineCamera>();

            var dirGo = new GameObject("Intro");
            dirGo.transform.SetParent(root.transform, false);
            dirGo.SetActive(false);
            var director = dirGo.AddComponent<IntroDirector>();
            SetField(director, "introCam", introCam);
            SetField(director, "focus", focus.transform);
            SetField(director, "nav", nav);
            SetField(director, "duration", 0.2f);
            dirGo.SetActive(true);
            yield return null;
            Assert.IsFalse(nav.enabled, "컷신 중 항해 보류");
            for (int i = 0; i < 120 && director.Running; i++) {
                yield return null;
            }
            Assert.IsTrue(nav.enabled, "컷신 종료 후 항해 출발");
        }
    }
}
