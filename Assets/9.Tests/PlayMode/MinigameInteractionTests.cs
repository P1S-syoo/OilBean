using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Game.Minigame;

namespace Game.Tests {
    // 미니게임 드래그 상호작용 자동 구동·검증 PlayMode 테스트
    // 시드 메서드는 실제 연결/도킹 판정 코드 경로를 타고, onSolved 발화까지 검증한다
    public class MinigameInteractionTests {
        GameObject canvasGo;
        GameObject eventSysGo;
        GameObject popupGo;

        [SetUp]
        public void Setup() {
            // EventSystem만 생성 — 입력 모듈 없이 EventSystem.current 등록
            // StandaloneInputModule은 레거시 UnityEngine.Input을 폴링해 Input System 전용 프로젝트에서 예외 발생
            // 시드 기반 테스트는 입력 모듈 폴링이 불필요(직접 메서드 호출 경로)
            eventSysGo = new GameObject("EventSystem", typeof(EventSystem));
            // 미니게임 UI가 기댈 캔버스(ScreenSpaceOverlay — worldCamera 불필요)
            canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(1080f, 1920f);
        }

        [TearDown]
        public void Cleanup() {
            if (popupGo != null) Object.Destroy(popupGo);
            if (canvasGo != null) Object.Destroy(canvasGo);
            if (eventSysGo != null) Object.Destroy(eventSysGo);
        }

        // 팝업 RectTransform을 캔버스 자식으로 만들어 적당한 크기를 준다
        RectTransform MakePopup(string name) {
            popupGo = new GameObject(name, typeof(RectTransform));
            popupGo.transform.SetParent(canvasGo.transform, false);
            var rt = popupGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 720f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        // ── ResearchMinigame ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator Research_SimulateConnectAll_solves_via_real_connect_path() {
            MakePopup("ResearchPopup");
            var game = popupGo.AddComponent<ResearchMinigame>();

            bool solved = false;
            game.Open(2, () => solved = true);   // 노드 5개(3+2)
            yield return null;                   // Awake/Open 1프레임

            int total = game.NodeCount;
            Assert.Greater(total, 0, "Open 후 노드가 생성되어야 함");

            // 실제 ConnectNode 경로로 전 노드 연결 — _visitedCount가 코드로 증가
            bool allConnected = game.SimulateConnectAll();
            Assert.IsTrue(allConnected, "모든 노드가 실제 연결 판정으로 방문됨");
            Assert.AreEqual(total, game.VisitedCount, "방문 카운트 == 노드 수");

            // OnPuzzleSolved DOTween 시퀀스는 SetUpdate(true) — unscaled 실시간 기준
            // PunchScale(0.4s) + ColorFlash(0.5s) + Interval(0.15s) + CloseAnim(0.18s) ≈ 1.3s 이상 필요
            // yield return null 루프는 프레임 수 기반이라 부족할 수 있으므로 WaitForSecondsRealtime 사용
            yield return new WaitForSecondsRealtime(3f);
            Assert.IsTrue(solved, "전 노드 연결 후 onSolved 콜백 1회 발화");
        }

        [UnityTest]
        public IEnumerator Research_partial_connect_does_not_solve() {
            MakePopup("ResearchPopup");
            var game = popupGo.AddComponent<ResearchMinigame>();

            bool solved = false;
            game.Open(1, () => solved = true);
            yield return null;

            // 시뮬 호출 없이 일부만 검증 — onSolved는 발화하지 않아야 함
            Assert.AreEqual(0, game.VisitedCount, "연결 전에는 방문 0");
            Assert.IsFalse(solved, "연결 미완료 시 onSolved 미발화");
            yield return null;
        }

        // ── CraftMinigame ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Craft_SimulatePlaceCorrect_fills_all_slots_via_real_dock_path() {
            MakePopup("CraftPopup");
            var game = popupGo.AddComponent<CraftMinigame>();

            bool solved = false;
            game.Open("테스트 레시피", 3, () => solved = true);   // 슬롯 3개 + decoy 1
            yield return null;
            yield return null;   // 등장 트윈/레이아웃 안정화

            // 실제 OnTokenEndDrag(타입 일치 검사 + DockToSlot) 경로로 정답 토큰 배치
            bool filled = game.SimulatePlaceCorrect();
            Assert.IsTrue(filled, "모든 슬롯이 실제 도킹 판정으로 채워짐");
            Assert.IsTrue(game.AllFilledForTest, "AllSlotsFilled 판정 통과");

            // 실제 OnConfirm 경로 실행 → 지연 후 ClosePopup(true) → onSolved
            Assert.IsTrue(game.SimulateConfirm(), "확정 판정 통과");

            // OnConfirm → DelayedCall(0.4s) → ClosePopup → DOTween(0.2s) → onSolved — 실시간 대기
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(solved, "전 슬롯 정답 배치 + 확인 후 onSolved 콜백 1회 발화");
        }

        [UnityTest]
        public IEnumerator Craft_confirm_blocked_when_slots_empty() {
            MakePopup("CraftPopup");
            var game = popupGo.AddComponent<CraftMinigame>();

            bool solved = false;
            game.Open("테스트 레시피", 2, () => solved = true);
            yield return null;

            // 배치 전 확정 시도 — 거부되고 onSolved 미발화
            Assert.IsFalse(game.AllFilledForTest, "배치 전엔 미충전");
            Assert.IsFalse(game.SimulateConfirm(), "미충전 시 확정 거부");
            Assert.IsFalse(solved, "확정 거부 시 onSolved 미발화");
            yield return null;
        }
    }
}
