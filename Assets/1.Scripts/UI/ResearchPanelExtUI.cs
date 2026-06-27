using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;
using Game.Items;

namespace Game.UI {
    // 연구 패널 확장 표시 — 샘플 수량 3등급 / 다음 약품 텍스트 / 약품 카드 상태 배지
    // 기존 ResearchPanel.cs 와 동일 GameObject 에 부착
    public class ResearchPanelExtUI : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] Research research;
        [SerializeField] ResearchPanel panel;

        [SerializeField] TMP_Text smp1Text;   // 오염수준 1 샘플 개수
        [SerializeField] TMP_Text smp2Text;   // 오염수준 2 샘플 개수
        [SerializeField] TMP_Text smp3Text;   // 오염수준 3 샘플 개수
        [SerializeField] TMP_Text nextText;   // "다음: 약품X — Npt" 텍스트
        [SerializeField] Image   progFill;    // 분석 진행 게이지

        // 약품 해금 카드 루트(각 카드 안의 Badge TMP_Text 를 찾아 갱신)
        [SerializeField] GameObject card1Root;   // agent_mild
        [SerializeField] GameObject card2Root;   // agent_mid
        [SerializeField] GameObject card3Root;   // agent_strong

        // 직전 캐시
        int lastS1 = -1, lastS2 = -1, lastS3 = -1, lastPts = -1, lastSelected = -1;
        bool lastDone = false;
        Button[] sampleButtons;

        void OnEnable() {
            if (panel == null) {
                panel = GetComponent<ResearchPanel>();
            }
            EnsureSampleButtons();
            if (research != null) {
                research.OnUnlocked += OnChanged;
                research.OnAnalyzed += OnChanged;
            }
            Refresh();
        }

        void OnDisable() {
            Unsubscribe();
        }

        void OnDestroy() {
            Unsubscribe();
        }

        // 이벤트 해제(중복 안전)
        void Unsubscribe() {
            if (research != null) {
                research.OnUnlocked -= OnChanged;
                research.OnAnalyzed -= OnChanged;
            }
        }

        void OnChanged() => Refresh();

        void LateUpdate() {
            // 값 변경 시에만 갱신
            if (run == null || research == null) {
                return;
            }
            int s1 = run.GetSampleCount(1), s2 = run.GetSampleCount(2), s3 = run.GetSampleCount(3);
            int pts = research.Points;
            bool done = research.Done;
            int selected = panel != null ? panel.SelectedSampleLevel : 0;
            if (s1 == lastS1 && s2 == lastS2 && s3 == lastS3
                && pts == lastPts && done == lastDone && selected == lastSelected) {
                return;
            }
            lastS1 = s1; lastS2 = s2; lastS3 = s3;
            lastPts = pts; lastDone = done; lastSelected = selected;
            Refresh();
        }

        // 전체 갱신
        void Refresh() {
            UpdateSampleCounts();
            UpdateProgressGauge();
            UpdateNextText();
            UpdateAgentCards();
        }

        // 샘플 개수 텍스트
        void UpdateSampleCounts() {
            if (run == null) {
                return;
            }
            int selected = panel != null ? panel.SelectedSampleLevel : 0;
            if (smp1Text != null) {
                int n = run.GetSampleCount(1);
                SetSampleText(smp1Text, 1, n, selected);
            }
            if (smp2Text != null) {
                int n = run.GetSampleCount(2);
                SetSampleText(smp2Text, 2, n, selected);
            }
            if (smp3Text != null) {
                int n = run.GetSampleCount(3);
                SetSampleText(smp3Text, 3, n, selected);
            }
        }

        // 샘플 수량 텍스트 — 선택 행만 짧은 화살표로 강조해 잘림 방지
        void SetSampleText(TMP_Text text, int level, int count, int selected) {
            bool hasSample = count > 0;
            bool isSelected = hasSample && selected == level;
            text.text = isSelected ? $"▶ {count}" : $"{count}";
            text.color = !hasSample ? UITheme.TextDisabled : isSelected ? UITheme.Accent : UITheme.TextPrimary;
        }

        // 기존 텍스트 행 위에 투명 버튼을 덮어 클릭 선택 지원(빌더 재생성 불필요)
        void EnsureSampleButtons() {
            if (sampleButtons != null || smp1Text == null || smp2Text == null || smp3Text == null) {
                return;
            }
            sampleButtons = new[] {
                BuildSampleButton(smp1Text, 1),
                BuildSampleButton(smp2Text, 2),
                BuildSampleButton(smp3Text, 3)
            };
        }

        Button BuildSampleButton(TMP_Text countText, int level) {
            var parent = countText.transform.parent;
            var go = new GameObject($"SampleSelect_Lv{level}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var src = countText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, src.anchorMin.y);
            rt.anchorMax = new Vector2(1f, src.anchorMax.y);
            rt.offsetMin = new Vector2(UITheme.SpaceSM, 0f);
            rt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);
            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SelectLevel(level));
            go.transform.SetAsLastSibling();   // 투명 클릭 영역은 텍스트 위에 있어야 이벤트를 받음
            return btn;
        }

        void SelectLevel(int level) {
            if (run == null || run.GetSampleCount(level) <= 0 || panel == null) {
                return;
            }
            panel.SelectSampleLevel(level);
            Refresh();
        }

        // 분석 진행 게이지
        void UpdateProgressGauge() {
            if (progFill == null || research == null) {
                return;
            }
            progFill.fillAmount = research.Progress;
            // 완료 시 성공 색, 진행 중 액센트
            progFill.color = research.Done ? UITheme.ColSuccess : UITheme.Accent;
        }

        // "다음 약품까지 Npt" 텍스트
        void UpdateNextText() {
            if (nextText == null || research == null) {
                return;
            }
            if (research.Done) {
                nextText.text = "모든 정화제 해금 완료";
                nextText.color = UITheme.ColSuccess;
                return;
            }
            nextText.color = UITheme.TextSecondary;
            int pts = research.Points;
            if (run != null && !run.IsUnlocked("agent_mild")) {
                nextText.text = $"다음: 약품Ⅰ — {pts}/3pt";
            } else if (run != null && !run.IsUnlocked("agent_mid")) {
                nextText.text = $"다음: 약품Ⅱ — {pts}/8pt";
            } else if (run != null && !run.IsUnlocked("agent_strong")) {
                nextText.text = $"다음: 약품Ⅲ — {pts}/15pt (고농도 샘플 필요)";
            }
        }

        // 약품 카드 상태 배지 — 해금됨 / 진행 중 / 잠금
        void UpdateAgentCards() {
            UpdateCard(card1Root, "agent_mild",   "약품Ⅰ");
            UpdateCard(card2Root, "agent_mid",    "약품Ⅱ");
            UpdateCard(card3Root, "agent_strong", "약품Ⅲ");
        }

        // 카드 1종 상태 배지 텍스트 + 카드 배경 색조 갱신
        void UpdateCard(GameObject cardRoot, string agentId, string label) {
            if (cardRoot == null || run == null) {
                return;
            }
            bool unlocked = run.IsUnlocked(agentId);

            // Badge 텍스트 검색(빌더가 "Badge"로 명명)
            var badge = cardRoot.transform.Find("Badge")?.GetComponent<TMP_Text>();
            if (badge != null) {
                badge.text = unlocked ? "해금됨" : "잠금";
                badge.color = unlocked ? UITheme.ColSuccess : UITheme.TextDisabled;
            }

            // 카드 배경 Image 색조 — 해금 시 약간 밝게
            var bg = cardRoot.GetComponent<Image>();
            if (bg != null) {
                bg.color = unlocked
                    ? new Color(UITheme.BgPanel.r + 0.04f, UITheme.BgPanel.g + 0.06f, UITheme.BgPanel.b + 0.04f, 1f)
                    : UITheme.BgPanel;
            }
        }
    }
}
