using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Player;

namespace Game.UI {
    // 탐사 HUD(uGUI) — 적재 무게/개수 + 배터리 + 복귀 경고. 변경 시에만 갱신(무할당)
    public class Hud : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] Battery battery;
        [SerializeField] TMP_Text infoText;   // 무게/개수
        [SerializeField] Image batteryFill;   // 배터리 막대(fillAmount)
        [SerializeField] TMP_Text warnText;   // 복귀 경고
        [SerializeField] Image purifyFill;    // 정화 설치 진행 게이지
        [SerializeField] float lowRatio = 0.2f;   // 저배터리 경고 임계값
        [SerializeField] float minPickupWeight = 5f;   // 가장 가벼운 픽업도 못 실으면 적재 한계로 판정

        // 직전 표시값 캐시(매 프레임 재생성 방지)
        float lastW = -1f, lastMax = -1f, lastRatio = -1f, lastPurify = -1f;
        int lastScrap = -1, lastSample = -1;

        void LateUpdate() {
            UpdateInfo();
            UpdateBattery();
            UpdatePurify();
            UpdateWarn();
        }

        // 정화 설치 진행 게이지(설치 중 0→1)
        void UpdatePurify() {
            if (run == null || purifyFill == null) {
                return;
            }
            if (!Mathf.Approximately(run.Purify, lastPurify)) {
                lastPurify = run.Purify;
                // 설치 진행은 홀드 중 매 프레임 연속 변화 → 트윈(Kill+재시작) 스래싱으로 막대가 안 차므로 직접 설정
                UITheme.SetGaugeFill(purifyFill, run.Purify);   // 9-slice 채움(anchorMax.x)
            }
        }

        // 무게/개수 — 값이 바뀐 프레임에만 문자열 생성
        void UpdateInfo() {
            if (run == null || infoText == null) {
                return;
            }
            if (Mathf.Approximately(run.Weight, lastW) && Mathf.Approximately(run.MaxWeight, lastMax) && run.ScrapCount == lastScrap && run.SampleCount == lastSample) {
                return;
            }
            lastW = run.Weight; lastMax = run.MaxWeight; lastScrap = run.ScrapCount; lastSample = run.SampleCount;
            infoText.text = $"무게 {run.Weight:0}/{run.MaxWeight:0}kg  고철 {run.ScrapCount}  샘플 {run.SampleCount}";
        }

        void UpdateBattery() {
            if (battery == null || batteryFill == null) {
                return;
            }
            if (!Mathf.Approximately(battery.Ratio, lastRatio)) {
                lastRatio = battery.Ratio;
                // 배터리는 탐사 중 매 프레임 연속 감소 → 트윈(Kill+재시작) 스래싱으로 막대가 1.0 근처에 고정됨.
                // 연속값은 직접 설정해야 매끄럽게 줄어든다(불연속 점프인 적재 게이지만 트윈 사용).
                UITheme.SetGaugeFill(batteryFill, battery.Ratio);   // 9-slice 채움(anchorMax.x)
            }
        }

        // 적재 한계 / 방전 / 저배터리 경고
        void UpdateWarn() {
            if (warnText == null) {
                return;
            }
            bool full = run != null && !run.HasRoom(minPickupWeight);
            bool dead = battery != null && battery.Ratio <= 0f;
            bool low = battery != null && battery.Ratio > 0f && battery.Ratio <= lowRatio;
            warnText.enabled = full || dead || low;
            if (!warnText.enabled) {
                return;
            }
            warnText.text = full ? "적재 한계 — R키로 복귀"
                          : dead ? "배터리 방전 — 정화선 복귀"
                                 : "배터리 부족 — 복귀 권장";
        }
    }
}
