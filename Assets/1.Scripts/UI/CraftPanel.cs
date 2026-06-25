using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;
using Game.Items;

namespace Game.UI {
    // 제작 패널 상태 표시(uGUI) — 강재/부유체 요약 텍스트만 담당.
    // 레시피 버튼 클릭·제작 실행·활성 제어는 CraftPanelExtUI가 단독 소유(이중 등록/interactable 충돌 방지)
    public class CraftPanel : MonoBehaviour {
        [SerializeField] Crafting crafting;
        [SerializeField] Research research;     // 해금 시 상태 텍스트 갱신용
        [SerializeField] RunData run;
        [SerializeField] Collector collector;   // 고철 수집 시 갱신용
        [SerializeField] Button buoyBtn;        // (호환) 빌더 배선 유지 — 제어는 ExtUI가 담당
        [SerializeField] Button upgradeBtn;     // (호환) 빌더 배선 유지 — 제어는 ExtUI가 담당
        [SerializeField] TMP_Text statusText;

        void OnEnable() {
            // 버튼 onClick·interactable은 건드리지 않음 — CraftPanelExtUI 단독 소유
            if (crafting != null) {
                crafting.OnBuoyCrafted += OnChanged;
                crafting.OnUpgraded += OnChanged;
            }
            if (research != null) {
                research.OnUnlocked += OnChanged;
            }
            if (collector != null) {
                collector.OnCollect += OnCollected;
            }
            Refresh();
        }

        void OnDisable() {
            Unsubscribe();
        }

        void OnDestroy() {
            Unsubscribe();
        }

        // 이벤트 해제 — OnDisable과 OnDestroy 양쪽에서 호출(중복 해제 안전)
        void Unsubscribe() {
            if (crafting != null) {
                crafting.OnBuoyCrafted -= OnChanged;
                crafting.OnUpgraded -= OnChanged;
            }
            if (research != null) {
                research.OnUnlocked -= OnChanged;
            }
            if (collector != null) {
                collector.OnCollect -= OnCollected;
            }
        }

        // 제작/해금/수집 이벤트 → 상태 텍스트 갱신
        void OnChanged() => Refresh();
        void OnCollected(ResourceKind kind) => Refresh();

        // 강재/부유체 요약 텍스트(이벤트성)
        void Refresh() {
            if (statusText != null && run != null) {
                string buoy = run.BuoyStage > 0 ? $"부유체 {run.BuoyStage}단계" : "부유체 미제작";
                statusText.text = $"강재 일{run.GetSteel(0):0}/합{run.GetSteel(1):0}/특{run.GetSteel(2):0}kg  한계 {run.MaxWeight:0}  {buoy}";
            }
        }
    }
}
