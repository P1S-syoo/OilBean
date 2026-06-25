using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Game.UI {
    // 제작/업그레이드 완료 피드백 팝업 — 반투명 오버레이 + 글래스 카드 + 완료 메시지 + 확인 버튼
    // 기획의 파란 네모 팝업을 패널 톤(teal/glass)으로 재해석. 런타임에 자가 생성(에디터 빌더 불필요)
    public class CraftCompletePopup : MonoBehaviour {
        GameObject overlay;     // 화면 전체 딤(클릭 차단)
        GameObject card;        // 중앙 글래스 카드
        TMP_Text titleTxt;      // "제작 완료"
        TMP_Text nameTxt;       // 결과물 이름
        TMP_Text descTxt;       // 효과 설명
        CanvasGroup overlayCg;  // 오버레이 페이드용

        bool built;

        // 부모 Canvas/패널 아래에 1회 생성 — 부모를 꽉 채우는 오버레이 + 황금비 카드
        void Build(Transform parent) {
            if (built) {
                return;
            }
            try {
                // 오버레이 — 부모 전체를 덮는 반투명 딤(클릭 차단)
                overlay = UITheme.MakeStretchPanel("CompleteOverlay", parent,
                    color: new Color(0.02f, 0.05f, 0.07f, 0.72f));
                overlayCg = overlay.AddComponent<CanvasGroup>();

                // 중앙 글래스 카드(황금비 가로 480)
                Vector2 cardSize = UITheme.GoldenSize(480f, widthIsLong: true);
                card = UITheme.MakeGlassPanel("CompleteCard", overlay.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    -cardSize * 0.5f, cardSize * 0.5f);
                UITheme.AddShadow(card, 0.6f, 0f, -6f);

                // 상단 액센트 언더라인(타이틀 강조)
                UITheme.MakeAccentBar("CardAccent", card.transform, 1f, 3f, UITheme.SpaceLG, UITheme.Accent);

                // 타이틀 — "제작 완료"(성공색)
                titleTxt = UITheme.MakeText("CompleteTitle", card.transform,
                    "제작 완료", UITheme.FontHeading, UITheme.ColSuccess, TextAlignmentOptions.Center);
                titleTxt.fontStyle = UITheme.HeadingStyle;
                var tRt = titleTxt.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0f, 0.66f);
                tRt.anchorMax = new Vector2(1f, 0.86f);
                tRt.offsetMin = new Vector2(UITheme.SpaceLG, 0f);
                tRt.offsetMax = new Vector2(-UITheme.SpaceLG, 0f);

                // 결과물 이름(액센트, 크게)
                nameTxt = UITheme.MakeText("CompleteName", card.transform,
                    "", UITheme.FontTitle, UITheme.Accent, TextAlignmentOptions.Center);
                nameTxt.fontStyle = FontStyles.Bold;
                var nRt = nameTxt.GetComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0f, 0.44f);
                nRt.anchorMax = new Vector2(1f, 0.66f);
                nRt.offsetMin = new Vector2(UITheme.SpaceLG, 0f);
                nRt.offsetMax = new Vector2(-UITheme.SpaceLG, 0f);

                // 효과 설명(보조 텍스트)
                descTxt = UITheme.MakeText("CompleteDesc", card.transform,
                    "", UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center);
                descTxt.enableWordWrapping = true;
                var dRt = descTxt.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0f, 0.26f);
                dRt.anchorMax = new Vector2(1f, 0.44f);
                dRt.offsetMin = new Vector2(UITheme.SpaceLG, 0f);
                dRt.offsetMax = new Vector2(-UITheme.SpaceLG, 0f);

                // 확인 버튼 — 하단 중앙, 황금비 폭
                float okH = 52f;
                float okW = Mathf.Round(okH * UITheme.Golden * UITheme.Golden * UITheme.Golden);   // φ³≈220
                var okBtn = UITheme.MakeButton("CompleteOk", card.transform,
                    "확인", UITheme.FontBody,
                    new Vector2(okW, okH), Vector2.zero,
                    UITheme.Accent, UITheme.BgDeep);
                var okRt = okBtn.GetComponent<RectTransform>();
                okRt.anchorMin = new Vector2(0.5f, 0f);
                okRt.anchorMax = new Vector2(0.5f, 0f);
                okRt.pivot = new Vector2(0.5f, 0f);
                okRt.sizeDelta = new Vector2(okW, okH);
                okRt.anchoredPosition = new Vector2(0f, UITheme.SpaceLG);
                UITheme.AddScaleFeedback(okBtn);
                okBtn.onClick.AddListener(Hide);

                overlay.SetActive(false);
                built = true;
            } catch (System.Exception e) {
                Debug.LogError($"[CraftCompletePopup] 생성 오류: {e.Message}");
            }
        }

        // 완료 팝업 표시 — 부모 전달(최초 1회 생성), 결과물 이름/설명 갱신 후 등장
        public void Show(Transform parent, string resultName, string desc) {
            try {
                Build(parent);
                if (overlay == null) {
                    return;
                }
                if (nameTxt != null) {
                    nameTxt.text = resultName;
                }
                if (descTxt != null) {
                    descTxt.text = desc;
                }
                overlay.transform.SetAsLastSibling();   // 항상 최상단
                overlay.SetActive(true);
                // 오버레이 페이드 + 카드 부상 등장
                if (overlayCg != null) {
                    DOTween.Kill(overlayCg);
                    overlayCg.alpha = 0f;
                    DOTween.To(() => overlayCg.alpha, a => overlayCg.alpha = a, 1f, 0.18f)
                        .SetUpdate(true).SetTarget(overlayCg);
                }
                if (card != null) {
                    UITheme.RiseIn(card, 24f, 0.26f);
                }
            } catch (System.Exception e) {
                Debug.LogError($"[CraftCompletePopup] 표시 오류: {e.Message}");
            }
        }

        // 확인 → 닫기(페이드 아웃 후 비활성)
        public void Hide() {
            try {
                if (overlay == null) {
                    return;
                }
                if (overlayCg != null) {
                    DOTween.Kill(overlayCg);
                    DOTween.To(() => overlayCg.alpha, a => overlayCg.alpha = a, 0f, 0.16f)
                        .SetUpdate(true).SetTarget(overlayCg)
                        .OnComplete(() => { if (overlay != null) overlay.SetActive(false); });
                } else {
                    overlay.SetActive(false);
                }
            } catch (System.Exception e) {
                Debug.LogError($"[CraftCompletePopup] 닫기 오류: {e.Message}");
            }
        }

        void OnDisable() {
            // 트윈 정리(죽은 타깃 경고 방지)
            if (overlayCg != null) {
                DOTween.Kill(overlayCg);
            }
            if (card != null) {
                DOTween.Kill(card.GetComponent<RectTransform>());
                var cg = card.GetComponent<CanvasGroup>();
                if (cg != null) {
                    DOTween.Kill(cg);
                }
            }
        }
    }
}
