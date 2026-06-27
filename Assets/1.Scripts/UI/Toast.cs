using UnityEngine;
using TMPro;
using DG.Tweening;
using Game.Core;
using Game.Items;
using Game.Surface;

namespace Game.UI {
    // Notice/Toast 공용 표시기 — sticky면 상단 notice처럼 유지, 아니면 잠깐 표시 후 사라짐
    [RequireComponent(typeof(CanvasGroup))]
    public class Toast : MonoBehaviour {
        [SerializeField] TMP_Text label;
        [SerializeField] float showTime = 2f;   // 표시 유지(초)
        [SerializeField] float fade = 0.3f;
        [SerializeField] 연출설정 config;       // 연출 설정 — 연결 시 토스트 시간 적용
        [SerializeField] bool sticky = false;   // notice 모드 — 한 번 뜨면 계속 유지
        [SerializeField] bool compact = false;  // 긴 시스템 문구를 짧은 안내문으로 압축
        [SerializeField] bool rotateTips = false; // notice 모드에서 5초마다 조작 팁 순환
        [SerializeField] float tipInterval = 5f;
        [SerializeField] GameBootstrap game;
        [SerializeField] SurfaceBootstrap surface;
        [SerializeField] Collector collector;

        CanvasGroup cg;
        float nextTipTime;
        float overrideUntil;
        string overrideText = "";
        int tipIndex;
        bool firstCollectDone;
        float nextResolveTime;

        void Awake() {
            cg = GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            ApplyConfig();
        }

        void Start() {
            ResolveRefs();
            if (sticky && rotateTips) {
                cg.alpha = 1f;
                SetText(NextNotice());
                nextTipTime = Time.unscaledTime + tipInterval;
            }
        }

        void Update() {
            if (!sticky || !rotateTips || label == null) {
                return;
            }
            TickNotice();
        }

        // 토스트 연출 설정 적용 — 미연결 시 SO 기본값 사용
        void ApplyConfig() {
            var cfg = config != null ? config : 연출설정.기본;
            showTime = cfg.토스트표시시간;
            fade = cfg.토스트페이드시간;
            tipInterval = cfg.노티스팁간격;
        }

        // 메시지 1회 표시(페이드 인 → 유지 → 페이드 아웃)
        public void Show(string msg) {
            try {
                string text = compact ? CompactMessage(msg) : msg;
                DOTween.Kill(cg);
                cg.alpha = sticky ? 1f : 0f;
                if (sticky) {
                    SetOverride(text, 3f);
                    return;
                }
                SetText(text);
                // CanvasGroup.DOFade는 UI 모듈 → 본체 DOTween.To로 alpha 트윈
                var seq = DOTween.Sequence().SetUpdate(true);   // timeScale 무시
                seq.Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, fade));
                seq.AppendInterval(showTime);
                seq.Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, fade));
            } catch (System.Exception e) {
                Debug.LogError($"[Toast] 표시 오류: {e.Message}");
            }
        }

        void ResolveRefs() {
            if (Time.unscaledTime < nextResolveTime) {
                return;
            }
            nextResolveTime = Time.unscaledTime + 1f;
            if (game == null) {
                game = FindFirstObjectByType<GameBootstrap>();
            }
            if (surface == null) {
                surface = FindFirstObjectByType<SurfaceBootstrap>();
            }
            if (collector == null) {
                collector = FindFirstObjectByType<Collector>();
            }
        }

        void TickNotice() {
            if (Time.unscaledTime < overrideUntil) {
                SetText(overrideText);
                return;
            }
            string forced = ForcedNotice();
            if (!string.IsNullOrEmpty(forced)) {
                SetText(forced);
                nextTipTime = Time.unscaledTime + tipInterval;
                return;
            }
            if (Time.unscaledTime >= nextTipTime) {
                SetText(NextNotice());
                nextTipTime = Time.unscaledTime + tipInterval;
            }
        }

        void SetOverride(string text, float seconds) {
            SetText(text);
            overrideText = text;
            overrideUntil = Time.unscaledTime + seconds;
        }

        void SetText(string text) {
            if (label != null) {
                label.text = text;
            }
        }

        string ForcedNotice() {
            ResolveRefs();
            if (game == null) {
                return "";
            }
            if (surface != null && game.State == GameState.Surface && surface.DiveReady && !surface.ConsoleShown) {
                return "[E]키를 눌러 정화선 거점을 여세요.";
            }
            if (game.State == GameState.Dive && !firstCollectDone && collector != null && collector.HasInteractable) {
                return "[E]키를 눌러 재료를 수집하세요.";
            }
            return "";
        }

        string NextNotice() {
            ResolveRefs();
            string[] tips = TipsForState(game != null ? game.State : GameState.Dock);
            string text = tips[tipIndex % tips.Length];
            tipIndex++;
            return text;
        }

        string[] TipsForState(GameState state) {
            if (state == GameState.Dive) {
                return DiveTips;
            }
            if (state == GameState.Research || state == GameState.Craft || state == GameState.Dock) {
                return HubTips;
            }
            return SurfaceTips;
        }

        static readonly string[] SurfaceTips = {
            "[E]키는 정화구역 도착 후 거점 패널을 열 수 있어요.",
            "[마우스]로 시점을 돌려 발판과 모니터를 볼 수 있어요.",
            "[1][2]키는 정화구역 도착 후 연구와 제작을 열 수 있어요."
        };

        static readonly string[] DiveTips = {
            "[WASD]키로 잠수정을 움직일 수 있어요.",
            "[E]키로 가까운 재료를 수집할 수 있어요.",
            "[R]키로 정화선에 복귀할 수 있어요.",
            "[이동]키로 경고 방향의 위험체를 피할 수 있어요.",
            "[수심] 제한선 아래로는 더 내려갈 수 없어요."
        };

        static readonly string[] HubTips = {
            "[1]키로 연구 패널을 열 수 있어요.",
            "[2]키로 장비 제작 패널을 열 수 있어요.",
            "[잠수] 버튼으로 정화구역에 잠수할 수 있어요."
        };

        // notice는 좁고 계속 떠 있으므로 의미만 남긴 짧은 문구로 정리
        string CompactMessage(string msg) {
            if (string.IsNullOrWhiteSpace(msg)) {
                return "";
            }
            if (msg.Contains("고철") || msg.Contains("샘플")) {
                firstCollectDone = true;
                return msg;
            }
            if (msg.Contains("정화 지점 도착")) {
                return "[E]키를 눌러 정화선 거점을 여세요.";
            }
            if (msg.Contains("정화선 거점")) {
                return "정화 지점으로 이동";
            }
            if (msg.Contains("거점 복귀")) {
                return "거점 복귀 · 정비 가능";
            }
            if (msg.Contains("배터리")) {
                return "배터리 부족 · 거점 복귀";
            }
            if (msg.Contains("정화 부유체 완성")) {
                return "부유체 완성 · 잠수 후 설치";
            }
            if (msg.Contains("스테이지 클리어") || msg.Contains("맑아졌습니다")) {
                return "구역 정화 완료";
            }
            if (msg.Contains("오염원 충돌")) {
                return "충돌 · 배터리 손실";
            }
            if (msg.Contains("분석 완료")) {
                return "분석 완료";
            }
            if (msg.Contains("업그레이드")) {
                return "업그레이드 완료";
            }
            if (msg.Length <= 24) {
                return msg;
            }
            int cut = msg.IndexOfAny(new[] { '—', '-', '\n', ':' });
            if (cut > 0 && cut <= 24) {
                return msg.Substring(0, cut).Trim();
            }
            return msg.Substring(0, 22).TrimEnd() + "…";
        }
    }
}
