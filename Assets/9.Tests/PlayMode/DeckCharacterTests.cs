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
        public IEnumerator Walks_at_human_speed() {
            // 1초간 전진 입력 → 보행 속도(1.6m/s ±5%)로 이동해야
            var dc = Make();
            yield return null;
            for (int i = 0; i < 50; i++) {
                dc.Step(new Vector2(0f, 1f), 0.02f);
            }
            Assert.AreEqual(1.6f, dc.transform.localPosition.z, 0.08f, "1초 이동 거리 = 보행 속도(1.6m/s)");
        }

        [UnityTest]
        public IEnumerator Climbs_step_ledge() {
            // 낮은 바닥(y=0)에서 전진하다 0.4m 턱을 만나면 멈추지 않고 올라서야(경사·단차 보정)
            var dc = Make();
            SetField(dc, "deckHalf", new Vector2(10f, 10f));
            var floorGo = new GameObject("Floor");
            floorGo.transform.SetParent(sub.transform, false);
            var floor = floorGo.AddComponent<BoxCollider>();
            floor.size = new Vector3(20f, 0.5f, 4f);          // z<2 바닥(윗면 y=0.25)
            floorGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            var ledgeGo = new GameObject("Ledge");
            ledgeGo.transform.SetParent(sub.transform, false);
            var ledge = ledgeGo.AddComponent<BoxCollider>();
            ledge.size = new Vector3(20f, 0.9f, 6f);          // 바닥에 인접(z 2..8), 윗면 y=0.65 → 바닥(0.25) 대비 +0.4 단차
            ledgeGo.transform.localPosition = new Vector3(0f, 0.2f, 5f);
            SetField(dc, "deckColliders", new Collider[] { floor, ledge });
            SetField(dc, "cam", sub.transform);   // 이동 기준 +z 고정(Camera.main 의존 제거)
            dc.enabled = false;                    // 컴포넌트 Update의 중복 idle-Step 차단 — 수동 Step만
            dc.transform.localPosition = new Vector3(0f, 0.13f, -1f);
            yield return null;
            Physics.SyncTransforms();              // 동기 Step 루프 전 박스 콜라이더를 물리 씬에 반영(스위트 순서 무관)
            for (int i = 0; i < 200; i++) {
                dc.Step(new Vector2(0f, 1f), 0.02f);          // 턱을 향해 전진
            }
            Assert.Greater(dc.transform.localPosition.z, 3f, "턱에 막히지 않고 넘어가 전진해야");
            Assert.Greater(dc.transform.localPosition.y, 0.5f, "턱 윗면(≈0.58)으로 올라서야");
        }

        [UnityTest]
        public IEnumerator Walk_limited_to_hull_surface() {
            // 선체 콜라이더 배선 시 레이캐스트 명중 면 위로만 이동 — 콜라이더 밖(물 위 허공)으로 못 나감
            var dc = Make();
            SetField(dc, "deckHalf", new Vector2(10f, 10f));   // 거친 클램프는 느슨하게 — 콜라이더가 한계
            var deckGo = new GameObject("Hull");
            deckGo.transform.SetParent(sub.transform, false);
            var box = deckGo.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 0.5f, 6f);
            SetField(dc, "deckColliders", new Collider[] { box });
            SetField(dc, "cam", sub.transform);   // 이동 기준 +x/+z 고정
            dc.enabled = false;                    // 수동 Step만
            yield return null;
            Physics.SyncTransforms();              // 동기 Step 루프 전 콜라이더 물리 씬 반영
            for (int i = 0; i < 600; i++) {
                dc.Step(new Vector2(1f, 0f), 0.02f);   // 우측으로 계속 밀어붙임
            }
            Assert.LessOrEqual(dc.transform.localPosition.x, 2.05f, "콜라이더 가장자리(x=2)에서 멈춰야");
            Assert.AreEqual(0.13f, dc.transform.localPosition.y, 0.04f, "발이 콜라이더 윗면에 스냅(footSink 0.12 묻힘)");
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
