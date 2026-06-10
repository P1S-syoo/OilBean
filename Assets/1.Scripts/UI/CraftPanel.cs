using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;
using Game.Items;

namespace Game.UI {
    // 제작 패널(uGUI) — 정화 부유체 제작 / 무게 업그레이드 버튼 + 상태 텍스트
    public class CraftPanel : MonoBehaviour {
        [SerializeField] Crafting crafting;
        [SerializeField] Research research;     // 레시피 해금 시 제작 가능 갱신용
        [SerializeField] RunData run;
        [SerializeField] Collector collector;   // 고철 수집 시 갱신용
        [SerializeField] Button buoyBtn;
        [SerializeField] Button upgradeBtn;
        [SerializeField] TMP_Text statusText;

        void OnEnable() {
            if (buoyBtn != null) {
                buoyBtn.onClick.AddListener(OnBuoy);
            }
            if (upgradeBtn != null) {
                upgradeBtn.onClick.AddListener(OnUpgrade);
            }
            if (crafting != null) {
                crafting.OnBuoyCrafted += OnChanged;
                crafting.OnUpgraded += OnChanged;
            }
            if (research != null) {
                research.OnUnlocked += OnChanged;   // 해금 → 부유체 버튼 활성 갱신
            }
            if (collector != null) {
                collector.OnCollect += OnCollected;   // 고철 수집 → 갱신
            }
            Refresh();
        }

        void OnDisable() {
            if (buoyBtn != null) {
                buoyBtn.onClick.RemoveListener(OnBuoy);
            }
            if (upgradeBtn != null) {
                upgradeBtn.onClick.RemoveListener(OnUpgrade);
            }
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

        void OnBuoy() {
            if (crafting != null) {
                crafting.CraftBuoy();
            }
            Refresh();
        }

        void OnUpgrade() {
            if (crafting != null) {
                crafting.UpgradeWeight();
            }
            Refresh();
        }

        // 제작/해금/수집 이벤트 → 실시간 갱신
        void OnChanged() => Refresh();
        void OnCollected(ResourceKind kind) => Refresh();

        // 버튼 활성/상태 갱신(이벤트성)
        void Refresh() {
            if (buoyBtn != null && crafting != null) {
                buoyBtn.interactable = crafting.CanCraftBuoy;
            }
            if (upgradeBtn != null && crafting != null) {
                upgradeBtn.interactable = crafting.CanUpgrade;
            }
            if (statusText != null && run != null) {
                string buoy = run.BuoyReady ? "부유체 준비됨" : "부유체 미제작";
                statusText.text = $"고철 {run.ScrapCount}  한계 {run.MaxWeight:0}kg  {buoy}";
            }
        }
    }
}
