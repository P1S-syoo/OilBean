using System;
using UnityEngine;

namespace Game.UI {
    // 오염체 수집 스타포스 미니게임 — 좌우 왕복 커서를 멈춰 타겟존에 명중시키면 수집 성공
    // 입력(E)은 Collector가 단일 소유 — 시작은 StartGame, 멈춤 판정은 Judge를 호출
    public class CollectMinigame : MonoBehaviour {
        [SerializeField] GameObject panel;       // 미니게임 루트(표시 토글)
        [SerializeField] RectTransform track;    // 커서 왕복 기준 폭
        [SerializeField] RectTransform cursor;   // 좌우 왕복 마커
        [SerializeField] RectTransform target;   // 성공 구간 하이라이트
        // 오염 농도 등급(Lv1/2/3 = 인덱스 0/1/2)별 난이도 — 깊을수록 빠르고 좁다
        [SerializeField] float[] speeds = { 0.9f, 1.3f, 1.8f };          // 왕복 속도(/s)
        [SerializeField] float[] targetHalfs = { 0.16f, 0.11f, 0.07f };  // 타겟 반폭(0~1)

        bool active;
        float t, dir = 1f, speed, targetCenter, targetHalf;
        Action<bool> onResult;

        public bool Active => active;

        void Awake() {
            if (panel != null) {
                panel.SetActive(false);
            }
        }

        // 미니게임 시작 — level(0~2 오염농도), 결과 콜백(명중=true)
        public void StartGame(int level, Action<bool> result) {
            try {
                int lv = Mathf.Clamp(level, 0, Mathf.Min(speeds.Length, targetHalfs.Length) - 1);
                speed = speeds[lv];
                targetHalf = targetHalfs[lv];
                targetCenter = UnityEngine.Random.Range(0.2f, 0.8f);
                onResult = result;
                t = 0f;
                dir = 1f;
                active = true;
                if (panel != null) {
                    panel.SetActive(true);
                }
                LayoutTarget();
                UpdateCursor();
            } catch (Exception e) {
                Debug.LogError($"[CollectMinigame] 시작 실패: {e.Message}");
                active = false;
                result?.Invoke(false);
            }
        }

        // 타겟존 위치/폭을 트랙 기준으로 배치
        void LayoutTarget() {
            if (target == null || track == null) {
                return;
            }
            float w = track.rect.width;
            target.anchorMin = new Vector2(0f, 0.5f);
            target.anchorMax = new Vector2(0f, 0.5f);
            target.sizeDelta = new Vector2(w * targetHalf * 2f, target.sizeDelta.y);
            target.anchoredPosition = new Vector2(w * targetCenter, 0f);
        }

        void Update() {
            if (!active) {
                return;
            }
            // 커서 0~1 왕복(양끝 반사) — UI라 unscaled 시간 사용
            t += dir * speed * Time.unscaledDeltaTime;
            if (t >= 1f) {
                t = 1f;
                dir = -1f;
            } else if (t <= 0f) {
                t = 0f;
                dir = 1f;
            }
            UpdateCursor();
        }

        // 커서를 트랙 폭에 비례해 배치
        void UpdateCursor() {
            if (cursor == null || track == null) {
                return;
            }
            float w = track.rect.width;
            cursor.anchorMin = new Vector2(0f, 0.5f);
            cursor.anchorMax = new Vector2(0f, 0.5f);
            cursor.anchoredPosition = new Vector2(w * t, 0f);
        }

        // 멈춤 판정 — Collector의 E 입력이 호출. 커서가 타겟 반폭 내면 명중
        public void Judge() {
            if (!active) {
                return;
            }
            bool hit = Mathf.Abs(t - targetCenter) <= targetHalf;
            active = false;
            if (panel != null) {
                panel.SetActive(false);
            }
            var cb = onResult;
            onResult = null;
            cb?.Invoke(hit);
        }

        // 외부 취소 — 상태 이탈/범위 벗어남 등(수집 없이 종료)
        public void Cancel() {
            if (!active) {
                return;
            }
            active = false;
            if (panel != null) {
                panel.SetActive(false);
            }
            onResult = null;
        }
    }
}
