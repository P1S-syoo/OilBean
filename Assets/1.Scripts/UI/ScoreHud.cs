using System;
using UnityEngine;
using TMPro;
using DG.Tweening;
using Game.Score;

namespace Game.UI {
    // 점수·콤보 HUD — ScoreSystem 이벤트 구독 후 텍스트 갱신 + DOTween punch 연출
    public class ScoreHud : MonoBehaviour {
        [SerializeField] TMP_Text scoreText;   // 점수 표시 텍스트
        [SerializeField] TMP_Text comboText;   // 콤보 표시 텍스트

        ScoreSystem scoreSystem;

        void Start() {
            try {
                scoreSystem = FindFirstObjectByType<ScoreSystem>();
                if (scoreSystem != null) {
                    scoreSystem.OnScoreChanged += HandleScoreChanged;
                    scoreSystem.OnComboReset += HandleComboReset;
                    scoreSystem.OnGrade += HandleGrade;
                } else {
                    Debug.LogWarning("[ScoreHud] ScoreSystem 미발견 — HUD 이벤트를 구독할 수 없습니다.");
                }
                // 초기 텍스트 세팅
                if (scoreText != null) {
                    scoreText.text = "0";
                }
                if (comboText != null) {
                    comboText.gameObject.SetActive(false);
                    comboText.text = string.Empty;
                }
            } catch (Exception e) {
                Debug.LogError($"[ScoreHud] 초기화 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        void OnDestroy() {
            if (scoreSystem != null) {
                scoreSystem.OnScoreChanged -= HandleScoreChanged;
                scoreSystem.OnComboReset -= HandleComboReset;
                scoreSystem.OnGrade -= HandleGrade;
            }
        }

        // 점수·콤보 변경 — 텍스트 갱신 후 punch scale 연출
        void HandleScoreChanged(int score, int combo) {
            try {
                if (scoreText != null) {
                    scoreText.text = $"{score:N0}";
                    scoreText.transform.DOKill();
                    scoreText.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f).SetUpdate(true);
                }
                if (comboText != null) {
                    if (combo > 1) {
                        comboText.gameObject.SetActive(true);
                        comboText.text = $"x{combo}";
                        comboText.transform.DOKill();
                        comboText.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f).SetUpdate(true);
                    } else {
                        comboText.gameObject.SetActive(false);
                        comboText.text = string.Empty;
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[ScoreHud] 점수 갱신 실패: {e.Message}");
            }
        }

        // 콤보 끊김 — 콤보 텍스트 페이드 후 비활성
        void HandleComboReset() {
            try {
                if (comboText == null) {
                    return;
                }
                DOTween.Kill(comboText);
                // TMP_Text.alpha(텍스트 알파)를 직접 보간 — DOFade(머티리얼) 모듈 의존 회피
                DOTween.To(() => comboText.alpha, a => comboText.alpha = a, 0f, 0.3f)
                    .SetUpdate(true)
                    .SetTarget(comboText)
                    .OnComplete(() => {
                        comboText.gameObject.SetActive(false);
                        comboText.text = string.Empty;
                        comboText.alpha = 1f;   // 다음 콤보 표시 대비 알파 복원
                    });
            } catch (Exception e) {
                Debug.LogError($"[ScoreHud] 콤보 리셋 연출 실패: {e.Message}");
            }
        }

        // 클리어 등급 확정 — 별도 패널 담당이므로 로그만 출력
        void HandleGrade(string grade, int score, int best) {
            Debug.Log($"[ScoreHud] 클리어 등급: {grade} | 점수: {score:N0} | 베스트: {best:N0}");
        }
    }
}
