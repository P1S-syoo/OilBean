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
        [SerializeField] float charDelay;            // 글자 간 딜레이(초) — 기본값은 연출설정.글자속도
        [SerializeField] float fadeTime;             // 패널 페이드 시간 — 기본값은 연출설정.페이드시간
        [SerializeField] Game.Core.연출설정 config; // 연출 설정 — 미연결 시 SO 기본값(연출설정.기본) 사용
        [SerializeField] float panelH     = 168f;    // 대사창 높이(위계 상향)
        [SerializeField] float panelBottom = 96f;    // 하단 HUD와 안 겹치게 바닥에서 띄우는 여백

        // ── 런타임 UI 레퍼런스 ──────────────────────────────────────
        Canvas        canvas;
        CanvasGroup   canvasGroup;
        GameObject    panelGo;    // 대사 패널(RiseIn 대상)
        Image         backdrop;   // 전체 화면 딤 — 뒤 UI와 시각 분리 + 클릭 차단
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

        // 폰트 바인더(DefaultExecutionOrder -1000)의 Awake가 먼저 끝난 뒤 UI를 빌드하도록 Start에서 생성
        void Start() {
            try {
                ApplyConfig();
                // Play가 레이스로 먼저 빌드했을 수 있음 — 중복 캔버스 생성 방지
                if (canvasGroup == null) {
                    BuildUI();
                }
            } catch (Exception e) {
                Debug.LogError($"[NarrationView] UI 생성 실패: {e.Message}");
            }
        }

        // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
        void ApplyConfig() {
            var cfg = config != null ? config : Game.Core.연출설정.기본;
            charDelay = cfg.글자속도;
            fadeTime = cfg.페이드시간;
        }

        // 런타임 UI 빌드 — 씬에 전용 Canvas + 패널을 직접 생성
        void BuildUI() {
            // Canvas
            var canvasGo = new GameObject("NarrationCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;   // 다른 UI 위에 표시
            // 메인 캔버스와 동일 스케일 — 비-1080p 해상도에서 대사창 크기 정합
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // CanvasGroup — 페이드 인/아웃용
            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // 전체 화면 딤 백드롭 — 뒤 UI를 어둡게 덮어 "겹침"이 아닌 "포커스된 다이얼로그"로 분리
            var dimGo = UITheme.MakeStretchPanel("Backdrop", canvasGo.transform, 0, 0, 0, 0,
                new Color(UITheme.BgDeep.r, UITheme.BgDeep.g, UITheme.BgDeep.b, 0.72f));
            backdrop = dimGo.GetComponent<Image>();
            backdrop.raycastTarget = true;   // 뒤 UI 클릭 차단
            // 백드롭 클릭으로도 진행
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(OnAdvance);

            // 하단 대사 패널 — 글래스 표면, 하단 HUD 위(panelBottom) 안전영역에 배치
            panelGo = UITheme.MakeGlassPanel("NarrationPanel", canvasGo.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, panelBottom), new Vector2(-24f, panelBottom + panelH));
            // 글래스는 반투명 — 가독성 위해 불투명 표면을 한 겹 더 깔아 대비 확보
            var solidGo = UITheme.MakePanel("PanelSolid", panelGo.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(UITheme.BgModal.r, UITheme.BgModal.g, UITheme.BgModal.b, 0.96f));
            solidGo.transform.SetAsFirstSibling();   // 테두리·하이라이트 아래로
            solidGo.GetComponent<Image>().raycastTarget = false;
            // 상단 아쿠아 액센트 바 — 다이얼로그 강조
            UITheme.MakeAccentBar("TopAccent", panelGo.transform, 1f, 3f, UITheme.SpaceMD, UITheme.Accent);

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

            // 본문 텍스트 — 위계 상향(FontHeading)으로 다이얼로그 가독성 강화
            bodyText = UITheme.MakeText("BodyText", panelGo.transform,
                "", UITheme.FontHeading, UITheme.TextPrimary,
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
                ApplyConfig();
                // Start 실행 순서 레이스 방어 — 컨트롤러 Start가 뷰 Start보다 먼저면 UI 미생성 상태
                // 모든 Awake(폰트 바인더 -1000 포함) 이후 호출되므로 여기서 빌드해도 폰트 주입 순서 안전
                if (canvasGroup == null) {
                    BuildUI();
                }
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

        // 패널 페이드 인 — 캔버스 딤은 페이드, 패널은 수면 부상(RiseIn) 모션
        void ShowPanel(Action onComplete = null) {
            canvasGroup.blocksRaycasts = true;
            if (panelGo != null) {
                UITheme.RiseIn(panelGo, 24f, fadeTime);
            }
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
            // RiseIn이 panelGo의 RectTransform·CanvasGroup에 건 트윈 정리(죽은 타깃 경고 방지)
            if (panelGo != null) {
                var prt = panelGo.GetComponent<RectTransform>();
                if (prt != null) {
                    DOTween.Kill(prt);
                }
                var pcg = panelGo.GetComponent<CanvasGroup>();
                if (pcg != null) {
                    DOTween.Kill(pcg);
                }
            }
        }
    }
}
