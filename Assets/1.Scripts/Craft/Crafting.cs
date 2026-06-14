using System;
using UnityEngine;
using Game.Core;
using Game.Player;

namespace Game.Craft {
    // 제작 — 엑셀 ItemRecipeTable: 정화 부유체 3단계(약품+강재등급별 kg) + 업그레이드 3종
    public class Crafting : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] Battery battery;   // 배터리 셀 업그레이드 대상(선택)

        enum Effect { Buoy, MaxWeight, BatteryCap, Hull }

        // 레시피 1종 — 약품 게이트(빈 문자열=불필요) + 강재 등급/소비량 + 효과
        struct Recipe {
            public string id;
            public string agent;     // 필요 해금 약품 id(없으면 "")
            public int grade;        // 강재 등급 0~2
            public float kg;         // 소비 강재 kg
            public Effect effect;
            public float amount;     // Buoy=단계 / MaxWeight=증가 / BatteryCap=용량 / Hull=1
        }

        static readonly Recipe[] Recipes = {
            new Recipe { id = "buoy_1", agent = "agent_mild",   grade = 0, kg = 24f, effect = Effect.Buoy, amount = 1 },
            new Recipe { id = "buoy_2", agent = "agent_mid",    grade = 1, kg = 30f, effect = Effect.Buoy, amount = 2 },
            new Recipe { id = "buoy_3", agent = "agent_strong", grade = 2, kg = 40f, effect = Effect.Buoy, amount = 3 },
            new Recipe { id = "up_cargo",   agent = "", grade = 0, kg = 16f, effect = Effect.MaxWeight,  amount = 20f },
            new Recipe { id = "up_battery", agent = "", grade = 1, kg = 18f, effect = Effect.BatteryCap, amount = 33f },  // ≈탐사시간 +10s
            new Recipe { id = "gear_hull",  agent = "", grade = 2, kg = 30f, effect = Effect.Hull,       amount = 1 },
        };

        public event Action OnBuoyCrafted;
        public event Action OnUpgraded;

        // 특정 레시피 제작 가능 여부 — 약품 해금 + 강재 충족 + 부유체 순차/내압 중복 방지
        public bool CanCraft(string id) {
            if (run == null) {
                return false;
            }
            if (!TryFind(id, out var r)) {
                return false;
            }
            if (!string.IsNullOrEmpty(r.agent) && !run.IsUnlocked(r.agent)) {
                return false;
            }
            if (run.GetSteel(r.grade) < r.kg) {
                return false;
            }
            if (r.effect == Effect.Buoy && run.BuoyStage != (int)r.amount - 1) {
                return false;   // 직전 단계에서만 다음 부유체 제작(순차)
            }
            if (r.effect == Effect.Hull && run.HullArmor) {
                return false;   // 내압 이미 장착
            }
            return true;
        }

        // 레시피 제작 — 강재 소비 후 효과 적용
        public bool Craft(string id) {
            try {
                if (!CanCraft(id) || !TryFind(id, out var r)) {
                    return false;
                }
                if (!run.TryConsumeSteel(r.grade, r.kg)) {
                    return false;   // 소비 실패 시 효과 미적용(단일 진실 원천)
                }
                ApplyEffect(r);
                return true;
            } catch (Exception e) {
                Debug.LogError($"[Crafting] Craft({id}) 오류: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        void ApplyEffect(Recipe r) {
            switch (r.effect) {
                case Effect.Buoy:
                    run.SetBuoyStage((int)r.amount);
                    run.SetBuoyReady(true);
                    OnBuoyCrafted?.Invoke();
                    Debug.Log($"[Crafting] 정화 부유체 {r.amount}단계 제작");
                    break;
                case Effect.MaxWeight:
                    run.AddMaxWeight(r.amount);
                    OnUpgraded?.Invoke();
                    Debug.Log($"[Crafting] 적재 한계 +{r.amount} → {run.MaxWeight}");
                    break;
                case Effect.BatteryCap:
                    if (battery != null) {
                        battery.AddCapacity(r.amount);
                    }
                    OnUpgraded?.Invoke();
                    Debug.Log("[Crafting] 배터리 용량 증가");
                    break;
                case Effect.Hull:
                    run.SetHullArmor(true);
                    OnUpgraded?.Invoke();
                    Debug.Log("[Crafting] 내압 프레임 장착");
                    break;
            }
        }

        static bool TryFind(string id, out Recipe r) {
            foreach (var x in Recipes) {
                if (x.id == id) {
                    r = x;
                    return true;
                }
            }
            r = default;
            return false;
        }

        // ── 기존 호환 API (CraftPanel/GameBootstrap) ──
        // 다음 단계 부유체 id — 현재 BuoyStage+1 (3단계 초과면 null)
        string NextBuoyId() {
            int next = (run != null ? run.BuoyStage : 0) + 1;
            return next <= 3 ? $"buoy_{next}" : null;
        }

        public bool CanCraftBuoy {
            get {
                string id = NextBuoyId();
                return id != null && CanCraft(id);
            }
        }

        public bool CraftBuoy() {
            string id = NextBuoyId();
            return id != null && Craft(id);
        }

        public bool CanUpgrade => CanCraft("up_cargo");
        public bool UpgradeWeight() => Craft("up_cargo");
    }
}
