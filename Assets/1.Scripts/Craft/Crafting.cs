using System;
using UnityEngine;
using Game.Core;

namespace Game.Craft {
    // 제작 — 슬롯 퍼즐 대신 "재료+레시피 충족 시 버튼 제작"(기조 단순화)
    public class Crafting : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] string recipeId = "purifier";   // 부유체 제작에 필요한 해금 레시피
        [SerializeField] int buoyScrap = 3;               // 정화 부유체 제작 고철 수
        [SerializeField] int upgradeScrap = 2;            // 무게 업그레이드 고철 수
        [SerializeField] float scrapUnitWeight = 8f;      // 고철 1개 무게(차감용)
        [SerializeField] float weightBonus = 20f;         // 업그레이드 시 한계 증가량

        // 제작/업그레이드 완료 — 사운드/UI가 구독
        public event Action OnBuoyCrafted;
        public event Action OnUpgraded;

        // 부유체 제작 가능 여부(레시피 해금 + 고철 충족 + 아직 미제작)
        public bool CanCraftBuoy => run != null && !run.BuoyReady && run.IsUnlocked(recipeId) && run.ScrapCount >= buoyScrap;
        public bool CanUpgrade => run != null && run.ScrapCount >= upgradeScrap;

        // 정화 부유체 제작
        public bool CraftBuoy() {
            try {
                if (!CanCraftBuoy) {
                    return false;
                }
                if (!run.TryConsumeScrap(buoyScrap, scrapUnitWeight)) {
                    return false;   // 소비 실패 시 제작 안 함(단일 진실 원천)
                }
                run.SetBuoyReady(true);
                OnBuoyCrafted?.Invoke();
                Debug.Log("[Crafting] 정화 부유체 제작 완료");
                return true;
            } catch (Exception e) {
                Debug.LogError($"[Crafting] CraftBuoy 오류: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // 탐사 기계 업그레이드(무게 한계↑)
        public bool UpgradeWeight() {
            try {
                if (!CanUpgrade) {
                    return false;
                }
                if (!run.TryConsumeScrap(upgradeScrap, scrapUnitWeight)) {
                    return false;
                }
                run.AddMaxWeight(weightBonus);
                OnUpgraded?.Invoke();
                Debug.Log($"[Crafting] 무게 한계 +{weightBonus} → {run.MaxWeight}");
                return true;
            } catch (Exception e) {
                Debug.LogError($"[Crafting] UpgradeWeight 오류: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
    }
}
