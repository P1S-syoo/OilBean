using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Player;
using Game.Items;
using Game.World;

namespace Game.UI {
    // HUD 확장 표시 — 배터리 % 텍스트 / 적재 게이지 / 수심 / 경고 배너 / 수집 토스트
    // 기존 Hud.cs 가 처리하지 않는 시각 요소를 보완
    public class HudExtUI : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] Battery battery;
        [SerializeField] Collector collector;

        [SerializeField] TMP_Text batteryPct;    // "100%" 퍼센트 텍스트
        [SerializeField] Image cargoFill;         // 적재 게이지 fillAmount
        [SerializeField] TMP_Text depthText;      // "수심 Xm" 텍스트
        [SerializeField] GameObject warnBanner;   // 상단 경고 배너 루트
        [SerializeField] GameObject toastRoot;    // 수집 토스트 루트
        [SerializeField] TMP_Text toastText;      // 토스트 메시지

        // 직전 캐시(변경 시에만 갱신)
        float lastRatio = -1f, lastW = -1f, lastMax = -1f;
        float toastTimer = 0f;
        const float ToastDuration = 2.0f;   // 토스트 표시 초

        void OnEnable() {
            if (collector != null) {
                collector.OnCollect += OnCollect;
            }
        }

        void OnDisable() {
            Unsubscribe();
        }

        void OnDestroy() {
            Unsubscribe();
        }

        // 이벤트 해제(중복 해제 안전)
        void Unsubscribe() {
            if (collector != null) {
                collector.OnCollect -= OnCollect;
            }
        }

        void LateUpdate() {
            UpdateBatteryPct();
            UpdateCargoGauge();
            UpdateDepth();
            UpdateWarnBanner();
            TickToast();
        }

        // 배터리 퍼센트 텍스트 갱신
        void UpdateBatteryPct() {
            if (battery == null || batteryPct == null) {
                return;
            }
            float ratio = battery.Ratio;
            if (Mathf.Approximately(ratio, lastRatio)) {
                return;
            }
            lastRatio = ratio;
            int pct = Mathf.RoundToInt(ratio * 100f);
            batteryPct.text = $"{pct}%";
            // 저배터리 시 색상 경고
            batteryPct.color = ratio <= 0.2f ? UITheme.ColDanger
                             : ratio <= 0.4f ? UITheme.ColWarn
                             : UITheme.TextPrimary;
        }

        // 적재 게이지 fillAmount 갱신
        void UpdateCargoGauge() {
            if (run == null || cargoFill == null) {
                return;
            }
            float w = run.Weight, max = run.MaxWeight;
            if (Mathf.Approximately(w, lastW) && Mathf.Approximately(max, lastMax)) {
                return;
            }
            lastW = w; lastMax = max;
            float ratio = max > 0f ? Mathf.Clamp01(w / max) : 0f;
            cargoFill.fillAmount = ratio;
            // 적재 80% 초과 시 경고 색
            cargoFill.color = ratio >= 1f   ? UITheme.ColDanger
                            : ratio >= 0.8f ? UITheme.ColWarn
                            : UITheme.ColSuccess;
        }

        // 수심 텍스트 — 플레이어 Transform Y를 DepthMap 변환
        void UpdateDepth() {
            if (depthText == null) {
                return;
            }
            // 플레이어 오브젝트는 HudExtUI 와 같은 씬에 존재 — GameObject.Find 대신 Transform 캐시
            float depth = DepthMap.WorldToDepth(GetPlayerY());
            depthText.text = $"수심 {Mathf.Max(0f, depth):0}m";
            depthText.color = depth > 40f ? UITheme.ColDanger
                            : depth > 25f ? UITheme.ColWarn
                            : UITheme.ColInfo;
        }

        // 경고 배너 — 배터리 방전/저배터리/적재 한계 시 표시
        void UpdateWarnBanner() {
            if (warnBanner == null) {
                return;
            }
            bool battDead = battery != null && battery.Ratio <= 0f;
            bool battLow  = battery != null && battery.Ratio > 0f && battery.Ratio <= 0.2f;
            bool cargoFull = run != null && !run.HasRoom(5f);
            bool show = battDead || battLow || cargoFull;
            if (warnBanner.activeSelf != show) {
                warnBanner.SetActive(show);
            }
        }

        // 수집 이벤트 → 토스트 표시
        void OnCollect(ResourceKind kind) {
            if (toastRoot == null || toastText == null) {
                return;
            }
            string msg = kind == ResourceKind.Scrap ? "고철 수집" : "샘플 수집";
            toastText.text = msg;
            toastText.color = kind == ResourceKind.Scrap ? UITheme.ColWarn : UITheme.ColSuccess;
            toastRoot.SetActive(true);
            toastTimer = ToastDuration;
        }

        // 토스트 타이머 소멸
        void TickToast() {
            if (toastTimer <= 0f || toastRoot == null) {
                return;
            }
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f) {
                toastRoot.SetActive(false);
                toastTimer = 0f;
            }
        }

        // 플레이어 월드Y 획득 — 태그 "Player" 오브젝트 캐시
        Transform playerTr;
        float GetPlayerY() {
            if (playerTr == null) {
                var go = GameObject.FindWithTag("Player");
                if (go != null) {
                    playerTr = go.transform;
                }
            }
            return playerTr != null ? playerTr.position.y : DepthMap.SurfaceY;
        }
    }
}
