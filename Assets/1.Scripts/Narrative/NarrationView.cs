using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using Game.UI;

namespace Game.Narrative {
    // 화면 하단 대사 팝업 — 런타임에 UI를 직접 생성, 타자기 효과로 순차 출력
    public class NarrationView : MonoBehaviour {

        // ── 설정 ────────────────────────────────────────────────────
        [SerializeField] float charDelay = 0.04f;   // 글자 간 딜레이(초)
        [SerializeField] float fadeTime   = 0.35f;   // 패널 페이드 시간
        [SerializeField] float panelH     = 120f;    // 대사창 높이

        // ── 런타임 UI 레퍼런스 ──────────────────────────────────────
        Canvas        canvas;
        CanvasGroup   canvasGroup;
        TMP_Text      bodyText;
        TMP_Text      hintText;   // "▼ 다음/스킵" 안내

        // ── 상태 ────────────────────────────────────────────────────
        string[]       lines;
        int            lineIdx;
        Action         onDone;
        Coroutine      typingCoroutine;
        bool           typingDone;   // 현재 줄 타자기 완료 여부

        public bool IsPlaying { get; private set; }

        // ── 초기화 ──────────────────────────────────────────────────

        void Awake() {
            try {
                BuildUI();
            } catch (Exception e) {
                Debug.LogError($"[NarrationView] UI 생성 실패: {e.Message}");
            }
        }

        // 런타임 UI 빌드 — 씬에 전용 Canvas + 패널을 직접 생성
        void BuildUI() {
            // Canvas
            var canvasGo = new GameObject("NarrationCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;   // 다른 UI 위에 표시
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // CanvasGroup — 페이드 인/아웃용
            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // 하단 배경 패널
            var panelGo = UITheme.MakePanel("NarrationPanel", canvasGo.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 0f), new Vector2(-24f, panelH),
                new Color(UITheme.BgPanel.r, UITheme.BgPanel.g, UITheme.BgPanel.b, 0.88f));

            // 클릭 투명 버튼 — 전체 패널 영역 클릭으로 진행
            var btnGo = new GameObject("ClickArea");
            btnGo.transform.SetParent(panelGo.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = Vector2.zero;
            btnRt.anchorMax = Vector2.one;
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = Color.clear;
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(OnAdvance);

            // 본문 텍스트
            bodyText = UITheme.MakeText("BodyText", panelGo.transform,
                "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.Left);
            var bodyRt = bodyText.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(UITheme.SpaceLG, UITheme.SpaceLG);
            bodyRt.offsetMax = new Vector2(-UITheme.SpaceLG, -UITheme.SpaceMD);
            bodyText.enableWordWrapping = true;
            bodyText.overflowMode = TextOverflowModes.Truncate;

            // "▼ 다음/스킵" 힌트 텍스트 (우하단)
            hintText = UITheme.MakeText("HintText", panelGo.transform,
                "▼ 클릭 또는 Space/Enter — 다음/스킵",
                UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.Right);
            var hintRt = hintText.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.offsetMin = new Vector2(UITheme.SpaceMD, UITheme.SpaceXS);
            hintRt.offsetMax = new Vector2(-UITheme.SpaceMD, UITheme.SpaceMD);
        }

        // ── 공개 API ────────────────────────────────────────────────

        // 대사 배열을 순차 재생하고 완료 시 onDone 콜백 호출
        public void Play(string[] newLines, Action onDone) {
            if (newLines == null || newLines.Length == 0) {
                onDone?.Invoke();
                return;
            }
            try {
                this.lines  = newLines;
                this.onDone = onDone;
                lineIdx     = 0;
                IsPlaying   = true;
                ShowPanel(() => ShowLine(lineIdx));
            } catch (Exception e) {
                Debug.LogError($"[NarrationView] 재생 시작 실패: {e.Message}");
            }
        }

        // 타자기 진행 중이면 즉시 완성, 완성 상태면 다음 줄로 진행
        public void Skip() {
            try {
                if (!IsPlaying) {
                    return;
                }
                OnAdvance();
            } catch (Exception e) {
                Debug.LogError($"[NarrationView] 스킵 처리 실패: {e.Message}");
            }
        }

        // ── 내부 흐름 ────────────────────────────────────────────────

        void Update() {
            if (!IsPlaying) {
                return;
            }
            // Space 또는 Enter로 진행
            try {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null &&
                    (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) {
                    OnAdvance();
                }
            } catch (Exception e) {
                Debug.LogError($"[NarrationView] 입력 처리 실패: {e.Message}");
            }
        }

        // 클릭 또는 키 — 타자기 중이면 완성, 완성이면 다음 줄
        void OnAdvance() {
            if (!IsPlaying) {
                return;
            }
            if (!typingDone) {
                // 타자기 즉시 완성
                CompleteTyping();
            } else {
                // 다음 줄 또는 종료
                lineIdx++;
                if (lineIdx < lines.Length) {
                    ShowLine(lineIdx);
                } else {
                    Finish();
                }
            }
        }

        // 지정 줄 타자기 시작
        void ShowLine(int idx) {
            if (typingCoroutine != null) {
                StopCoroutine(typingCoroutine);
            }
            typingDone = false;
            bodyText.text = "";
            typingCoroutine = StartCoroutine(TypeLine(lines[idx]));
        }

        // 타자기 코루틴 — unscaled 시간으로 시간 정지 무관
        IEnumerator TypeLine(string line) {
            for (int i = 0; i <= line.Length; i++) {
                bodyText.text = line.Substring(0, i);
                yield return new WaitForSecondsRealtime(charDelay);
            }
            typingDone = true;
            typingCoroutine = null;
        }

        // 현재 줄 타자기를 즉시 완성
        void CompleteTyping() {
            if (typingCoroutine != null) {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            if (lines != null && lineIdx < lines.Length) {
                bodyText.text = lines[lineIdx];
            }
            typingDone = true;
        }

        // 모든 줄 완료 — 패널 페이드 아웃 후 콜백
        void Finish() {
            IsPlaying = false;
            HidePanel(() => {
                onDone?.Invoke();
                onDone = null;
            });
        }

        // 패널 페이드 인
        void ShowPanel(Action onComplete = null) {
            canvasGroup.blocksRaycasts = true;
            DOTween.To(
                () => canvasGroup.alpha,
                a  => canvasGroup.alpha = a,
                1f, fadeTime
            ).SetUpdate(true).SetTarget(canvasGroup).OnComplete(() => onComplete?.Invoke());
        }

        // 패널 페이드 아웃
        void HidePanel(Action onComplete = null) {
            canvasGroup.blocksRaycasts = false;
            DOTween.To(
                () => canvasGroup.alpha,
                a  => canvasGroup.alpha = a,
                0f, fadeTime
            ).SetUpdate(true).SetTarget(canvasGroup).OnComplete(() => onComplete?.Invoke());
        }

        void OnDestroy() {
            DOTween.Kill(canvasGroup);
        }
    }
}
