using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;
using Game.Items;
using Game.Minigame;

namespace Game.UI {
    // 제작 패널 확장 표시 — 자원 수치 / 레시피 선택 / 상세 패널 / 제작 실행
    // 기존 CraftPanel.cs 와 동일 GameObject 에 부착
    public class CraftPanelExtUI : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] Crafting crafting;
        [SerializeField] Research research;
        [SerializeField] Collector collector;

        // 자원 표시 텍스트
        [SerializeField] TMP_Text steel0Text;   // 일반 강재 kg
        [SerializeField] TMP_Text steel1Text;   // 합금 강재 kg
        [SerializeField] TMP_Text steel2Text;   // 특수 강재 kg
        [SerializeField] TMP_Text agentText;    // 해금 약품 목록

        // 상세 패널
        [SerializeField] TMP_Text detailName;     // 선택 레시피 이름
        [SerializeField] TMP_Text detailReq;      // 재료 요구사항
        [SerializeField] TMP_Text detailStatus;   // 제작 가능 여부 메시지
        [SerializeField] Button   craftExecBtn;   // 제작 실행 버튼

        // 레시피 버튼 배열 (빌더가 배선)
        [SerializeField] Button[] recipeBtns;
        [SerializeField] string[] recipeIds;
        [SerializeField] string[] recipeLabels;
        [SerializeField] string[] recipeReqs;
        [SerializeField] CraftMinigame minigame;   // 슬롯 배치 퍼즐(미연결 시 즉시 제작 폴백)

        int selectedIdx = -1;   // 현재 선택된 레시피 인덱스(-1=없음)

        // 직전 캐시
        float lastS0 = -1f, lastS1 = -1f, lastS2 = -1f;

        bool fallbackWarned;   // 폴백 경고 1회만 출력

        void Start() {
            // minigame 미배선 시 자가탐색(NarrationController/ScoreHud 패턴)
            if (minigame == null) {
                minigame = FindFirstObjectByType<CraftMinigame>();
            }
        }

        void OnEnable() {
            // 레시피 버튼 클릭 등록
            RegisterRecipeButtons();
            if (crafting != null) {
                crafting.OnBuoyCrafted += OnChanged;
                crafting.OnUpgraded    += OnChanged;
            }
            if (research != null) {
                research.OnUnlocked += OnChanged;
            }
            if (collector != null) {
                collector.OnCollect += OnCollected;
            }
            if (craftExecBtn != null) {
                craftExecBtn.onClick.AddListener(OnCraftExec);
            }
            selectedIdx = -1;
            Refresh();
            // 런타임 패널 등장 모션(수면 부상) — 에디터 베이크 버그 회피 위해 플레이 중에만
            if (Application.isPlaying) {
                UITheme.RiseIn(gameObject);
            }
        }

        void OnDisable() {
            // 패널이 닫히면(상태 전환) 열린 퍼즐도 취소 — 팝업·콜백 잔류 방지
            if (minigame != null && minigame.IsOpen) {
                minigame.Cancel();
            }
            KillRiseInTweens();
            Unsubscribe();
        }

        // RiseIn이 건 RectTransform·CanvasGroup 트윈 정리(죽은 타깃 경고/위치 드리프트 방지)
        void KillRiseInTweens() {
            var rt = GetComponent<RectTransform>();
            if (rt != null) {
                DG.Tweening.DOTween.Kill(rt);
            }
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) {
                DG.Tweening.DOTween.Kill(cg);
            }
        }

        void OnDestroy() {
            Unsubscribe();
        }

        // 이벤트 해제(중복 안전)
        void Unsubscribe() {
            if (crafting != null) {
                crafting.OnBuoyCrafted -= OnChanged;
                crafting.OnUpgraded    -= OnChanged;
            }
            if (research != null) {
                research.OnUnlocked -= OnChanged;
            }
            if (collector != null) {
                collector.OnCollect -= OnCollected;
            }
            if (craftExecBtn != null) {
                craftExecBtn.onClick.RemoveListener(OnCraftExec);
            }
            UnregisterRecipeButtons();
        }

        // 레시피 버튼 클릭 이벤트 등록
        void RegisterRecipeButtons() {
            if (recipeBtns == null) {
                return;
            }
            for (int i = 0; i < recipeBtns.Length; i++) {
                if (recipeBtns[i] == null) {
                    continue;
                }
                int idx = i;   // 클로저 캡처용 로컬 복사
                recipeBtns[i].onClick.AddListener(() => OnRecipeSelected(idx));
            }
        }

        // 레시피 버튼 클릭 이벤트 해제
        void UnregisterRecipeButtons() {
            if (recipeBtns == null) {
                return;
            }
            foreach (var btn in recipeBtns) {
                if (btn != null) {
                    btn.onClick.RemoveAllListeners();
                }
            }
        }

        void OnChanged()                      => Refresh();
        void OnCollected(ResourceKind kind)   => Refresh();

        void LateUpdate() {
            // 자원 수치 변경 시에만 갱신
            if (run == null) {
                return;
            }
            float s0 = run.GetSteel(0), s1 = run.GetSteel(1), s2 = run.GetSteel(2);
            if (Mathf.Approximately(s0, lastS0)
                && Mathf.Approximately(s1, lastS1)
                && Mathf.Approximately(s2, lastS2)) {
                return;
            }
            lastS0 = s0; lastS1 = s1; lastS2 = s2;
            Refresh();
        }

        // 전체 갱신
        void Refresh() {
            UpdateResourceTexts();
            UpdateRecipeButtons();
            UpdateDetailPanel();
        }

        // 좌측 자원 수치 텍스트
        void UpdateResourceTexts() {
            if (run == null) {
                return;
            }
            SetSteelText(steel0Text, run.GetSteel(0));
            SetSteelText(steel1Text, run.GetSteel(1));
            SetSteelText(steel2Text, run.GetSteel(2));

            if (agentText != null) {
                agentText.text = BuildAgentList();
                agentText.color = UITheme.TextSecondary;
            }
        }

        // 강재 수치 텍스트 색상 포함 갱신
        void SetSteelText(TMP_Text txt, float kg) {
            if (txt == null) {
                return;
            }
            txt.text = $"{kg:0.#} kg";
            txt.color = kg <= 0f ? UITheme.TextDisabled : UITheme.TextPrimary;
        }

        // 해금 약품 목록 문자열
        string BuildAgentList() {
            if (run == null) {
                return "없음";
            }
            string result = "";
            if (run.IsUnlocked("agent_mild"))   { result += "약품Ⅰ\n"; }
            if (run.IsUnlocked("agent_mid"))    { result += "약품Ⅱ\n"; }
            if (run.IsUnlocked("agent_strong")) { result += "약품Ⅲ"; }
            return string.IsNullOrWhiteSpace(result) ? "없음" : result.TrimEnd('\n');
        }

        // 레시피 버튼 활성 + 선택 강조
        void UpdateRecipeButtons() {
            if (recipeBtns == null || recipeIds == null) {
                return;
            }
            for (int i = 0; i < recipeBtns.Length; i++) {
                if (recipeBtns[i] == null || i >= recipeIds.Length) {
                    continue;
                }
                bool canCraft = crafting != null && crafting.CanCraft(recipeIds[i]);
                recipeBtns[i].interactable = canCraft;

                // 선택 상태 강조: 배경 색 변경
                var img = recipeBtns[i].GetComponent<Image>();
                if (img != null) {
                    if (i == selectedIdx) {
                        img.color = new Color(UITheme.Accent.r * 0.3f,
                                              UITheme.Accent.g * 0.3f,
                                              UITheme.Accent.b * 0.3f, 1f);
                    } else {
                        img.color = canCraft ? UITheme.BgBorder
                                              : new Color(0.08f, 0.16f, 0.18f, 1f);
                    }
                }
            }
        }

        // 레시피 선택 → 상세 패널 갱신
        void OnRecipeSelected(int idx) {
            selectedIdx = idx;
            UpdateRecipeButtons();
            UpdateDetailPanel();
        }

        // 우측 상세 패널
        void UpdateDetailPanel() {
            if (selectedIdx < 0 || recipeIds == null || selectedIdx >= recipeIds.Length) {
                // 미선택 상태
                if (detailName != null) {
                    detailName.text = "항목을 선택하세요";
                    detailName.color = UITheme.TextSecondary;
                }
                if (detailReq != null) {
                    detailReq.text = "";
                }
                if (detailStatus != null) {
                    detailStatus.text = "";
                }
                if (craftExecBtn != null) {
                    craftExecBtn.interactable = false;
                }
                return;
            }

            string id    = recipeIds[selectedIdx];
            string label = selectedIdx < recipeLabels?.Length ? recipeLabels[selectedIdx] : id;
            string req   = selectedIdx < recipeReqs?.Length   ? recipeReqs[selectedIdx]   : "";
            bool canCraft = crafting != null && crafting.CanCraft(id);

            if (detailName != null) {
                detailName.text  = label;
                detailName.color = canCraft ? UITheme.Accent : UITheme.TextSecondary;
            }
            if (detailReq != null) {
                detailReq.text = req;
            }
            if (detailStatus != null) {
                detailStatus.text  = BuildCraftStatus(id, canCraft);
                detailStatus.color = canCraft ? UITheme.ColSuccess : UITheme.ColWarn;
            }
            if (craftExecBtn != null) {
                craftExecBtn.interactable = canCraft;
            }
        }

        // 제작 가능 여부 상세 메시지
        string BuildCraftStatus(string id, bool canCraft) {
            if (canCraft) {
                return "제작 가능";
            }
            if (run == null) {
                return "데이터 없음";
            }
            // 약품 미해금 체크
            if (id == "buoy_1" && !run.IsUnlocked("agent_mild"))   { return "약품Ⅰ 미해금"; }
            if (id == "buoy_2" && !run.IsUnlocked("agent_mid"))    { return "약품Ⅱ 미해금"; }
            if (id == "buoy_3" && !run.IsUnlocked("agent_strong")) { return "약품Ⅲ 미해금"; }
            // 부유체 순차 체크
            if (id == "buoy_2" && run.BuoyStage < 1) { return "부유체Ⅰ 먼저 제작 필요"; }
            if (id == "buoy_3" && run.BuoyStage < 2) { return "부유체Ⅱ 먼저 제작 필요"; }
            // 내압 중복
            if (id == "gear_hull" && run.HullArmor) { return "이미 장착됨"; }
            // 강재 부족 — 등급별 필요량 하드코딩(Recipe는 internal struct라 직접 접근 불가)
            return "강재 부족";
        }

        // 제작 실행 버튼
        void OnCraftExec() {
            try {
                if (crafting == null || selectedIdx < 0 || recipeIds == null
                    || selectedIdx >= recipeIds.Length) {
                    return;
                }
                string id = recipeIds[selectedIdx];
                if (!crafting.CanCraft(id)) {
                    Debug.LogWarning($"[CraftPanelExtUI] 제작 불가(재료/조건 미충족): {id}");
                    return;
                }
                // 퍼즐 게이트 — 성공 시에만 제작. 미연결이면 즉시 제작(폴백)
                if (minigame != null) {
                    if (minigame.IsOpen) {
                        return;   // 이미 퍼즐 진행 중 — 연타로 우회 방지
                    }
                    string label = selectedIdx < recipeLabels?.Length ? recipeLabels[selectedIdx] : id;
                    minigame.Open(label, SlotCountFor(id), () => DoCraft(id));
                } else {
                    // 자가탐색도 실패한 실제 폴백 — 경고 1회 출력
                    if (!fallbackWarned) {
                        Debug.LogWarning("[CraftPanelExtUI] minigame 미배선 — 즉시 제작 폴백");
                        fallbackWarned = true;
                    }
                    DoCraft(id);
                }
            } catch (System.Exception e) {
                Debug.LogError($"[CraftPanelExtUI] 제작 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // 실제 제작 실행(퍼즐 성공 또는 폴백에서 호출) — 성공 시 선택 해제 + 갱신
        void DoCraft(string id) {
            bool ok = crafting != null && crafting.Craft(id);
            if (ok) {
                selectedIdx = -1;
                Refresh();
                Debug.Log($"[CraftPanelExtUI] 제작 완료: {id}");
            } else {
                Debug.LogWarning($"[CraftPanelExtUI] 제작 실패: {id}");
            }
        }

        // 레시피별 퍼즐 슬롯 수 — 부유체는 3, 업그레이드/장비는 2
        int SlotCountFor(string id) {
            return (!string.IsNullOrEmpty(id) && id.StartsWith("buoy")) ? 3 : 2;
        }
    }
}
