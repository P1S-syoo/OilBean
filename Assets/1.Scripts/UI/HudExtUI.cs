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
        [SerializeField] TMP_Text cargoText;      // "X / Y kg" 적재 수치 전용
        [SerializeField] TMP_Text depthText;      // "수심 Xm" 텍스트
        [SerializeField] RectTransform depthMarker;   // 수심 밴드 위 현재 수심 마커
        [SerializeField] TMP_Text missionText;    // 좌하 임무 안내(부유체 단계 기반)
        [SerializeField] GameObject warnBanner;   // 상단 경고 배너 루트
        [SerializeField] GameObject toastRoot;    // 수집 토스트 루트
        [SerializeField] TMP_Text toastText;      // 토스트 메시지
        [SerializeField] GameObject interactPrompt;   // "E 정제" 상호작용 프롬프트(범위 진입 시)

        // 직전 캐시(변경 시에만 갱신)
        float lastRatio = -1f, lastW = -1f, lastMax = -1f;
        int lastBuoyStage = -1;   // 임무 텍스트 갱신용
        float toastTimer = 0f;
        const float ToastDuration = 2.0f;   // 토스트 표시 초

        void OnEnable() {
            if (collector != null) {
                collector.OnCollect += OnCollect;
                collector.OnInteractableChanged += OnInteractable;
            }
            if (interactPrompt != null) {
                interactPrompt.SetActive(false);
            }
        }

        // 상호작용 가능 — E 프롬프트 토글
        void OnInteractable(bool can) {
            if (interactPrompt != null) {
                interactPrompt.SetActive(can);
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
                collector.OnInteractableChanged -= OnInteractable;
            }
        }

        void LateUpdate() {
            UpdateBatteryPct();
            UpdateCargoGauge();
            UpdateDepth();
            UpdateMission();
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
            UITheme.TweenFill(cargoFill, ratio);
            // 적재 80% 초과 시 경고 색
            cargoFill.color = ratio >= 1f   ? UITheme.ColDanger
                            : ratio >= 0.8f ? UITheme.ColWarn
                            : UITheme.ColSuccess;
            // 적재 수치 전용 텍스트 — "X / Y kg" (Hud.infoText 긴 문자열 오버플로 분리)
            if (cargoText != null) {
                cargoText.text = $"{w:0} / {max:0} kg";
            }
        }

        // 수심 텍스트 + 밴드 마커 — 플레이어 Transform Y를 DepthMap 변환
        void UpdateDepth() {
            // 플레이어 오브젝트는 HudExtUI 와 같은 씬에 존재 — GameObject.Find 대신 Transform 캐시
            float depth = Mathf.Max(0f, DepthMap.WorldToDepth(GetPlayerY()));
            if (depthText != null) {
                depthText.text = $"수심 {depth:0}m";
                depthText.color = depth > 40f ? UITheme.ColDanger
                                : depth > 25f ? UITheme.ColWarn
                                : UITheme.ColInfo;
            }
            UpdateDepthMarker(depth);
        }

        // 수심 밴드 마커 위치 — 현재 수심 비율로 밴드 위를 오르내림
        // 밴드 로컬 y: 1.0=얕음(수면), 0.0=깊음(바닥). ratio 0→상단, 1→하단
        void UpdateDepthMarker(float depth) {
            if (depthMarker == null) {
                return;
            }
            float ratio = Mathf.Clamp01(depth / DepthMap.MaxDepthM);
            float y = 1f - ratio;
            var min = depthMarker.anchorMin;
            var max = depthMarker.anchorMax;
            min.y = y;
            max.y = y;
            depthMarker.anchorMin = min;
            depthMarker.anchorMax = max;
        }

        // 좌하 임무 안내 — 부유체 단계 기반 현재 목표 표시
        void UpdateMission() {
            if (missionText == null || run == null) {
                return;
            }
            int stage = run.BuoyStage;
            if (stage == lastBuoyStage) {
                return;
            }
            lastBuoyStage = stage;
            missionText.text = stage switch {
                0 => "정화 약품 연구",
                1 => "부유체 제작",
                2 => "부유체 설치",
                _ => "3단계 정화 진행",
            };
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

        // 플레이어 월드Y 획득 — 배터리(플레이어 잠수정에 부착)의 transform 사용(태그 의존 제거)
        Transform playerTr;
        float GetPlayerY() {
            if (playerTr == null) {
                if (battery != null) {
                    playerTr = battery.transform;   // 배터리=잠수정 루트 → 수심 기준
                } else {
                    var go = GameObject.FindWithTag("Player");
                    if (go != null) {
                        playerTr = go.transform;
                    }
                }
            }
            return playerTr != null ? playerTr.position.y : DepthMap.SurfaceY;
        }
    }
}
