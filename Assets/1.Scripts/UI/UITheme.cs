using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI {
    // 디자인 시스템 상수 — 오염된 한강 디스토피아 수중 탐사 무드
    // 팔레트: 딥 청록 배경 / 패널 / 청록 액센트 / 시맨틱 4색
    // 60/30/10 법칙: 배경 60%, 패널 30%, 액센트 10%
    public static class UITheme {

        // ── 팔레트 ──────────────────────────────────────────────────
        // 배경(60%) — 거의 검정에 가까운 심해 청록
        public static readonly Color BgDeep    = HexColor("#0D1F24");
        // 패널(30%) — 배경보다 약간 밝은 패널 표면
        public static readonly Color BgPanel   = HexColor("#142A30");
        // 패널 테두리 / 구분선
        public static readonly Color BgBorder  = HexColor("#1E3A42");
        // 헤더 강조 배경 (패널 상단 타이틀 바)
        public static readonly Color BgHeader  = HexColor("#0A1820");

        // 액센트(10%) — 현재 선택·주요 행동·활성 상태에만 사용
        public static readonly Color Accent     = HexColor("#2EC4B6");
        public static readonly Color AccentDim  = HexColor("#1A7A72");   // 비활성 액센트

        // 시맨틱 색상
        public static readonly Color ColSuccess = HexColor("#5FBF6A");   // 성공·완료·해금
        public static readonly Color ColWarn    = HexColor("#E8A13C");   // 경고·부족
        public static readonly Color ColDanger  = HexColor("#D9534F");   // 위험·방전·강제 복귀
        public static readonly Color ColInfo    = HexColor("#4AB8D0");   // 정보·수심 밴드

        // 텍스트
        public static readonly Color TextPrimary   = HexColor("#E6F0F0");   // 주요 텍스트
        public static readonly Color TextSecondary = HexColor("#9FB4B4");   // 보조·레이블
        public static readonly Color TextDisabled  = HexColor("#4A6A6A");   // 비활성

        // 수심 밴드 색상 (Common→Uncommon→Rare 심도)
        public static readonly Color DepthCommon   = HexColor("#4AB8D0");
        public static readonly Color DepthUncommon = HexColor("#9B59B6");
        public static readonly Color DepthRare     = HexColor("#D9534F");

        // ── 타이포그래피 스케일 ──────────────────────────────────────
        public const float FontTitle   = 28f;   // 패널 타이틀
        public const float FontHeading = 20f;   // 섹션 헤딩
        public const float FontBody    = 14f;   // 본문·일반 값
        public const float FontCaption = 11f;   // 캡션·레이블

        // ── 스페이싱 리듬 (8px 그리드) ──────────────────────────────
        public const float SpaceXS  = 4f;
        public const float SpaceSM  = 8f;
        public const float SpaceMD  = 16f;
        public const float SpaceLG  = 24f;
        public const float SpaceXL  = 32f;
        public const float SpaceXXL = 48f;

        // 게이지 기본 높이
        public const float GaugeHeight = 10f;
        public const float GaugeThin   = 6f;

        // ── 공통 헬퍼 ───────────────────────────────────────────────

        // 패널(단색 Image) 생성 — 지정 부모 아래 RectTransform 배치
        public static GameObject MakePanel(string name, Transform parent,
                Vector2 anchorMin, Vector2 anchorMax,
                Vector2 offsetMin, Vector2 offsetMax,
                Color? color = null) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color ?? BgPanel;
            return go;
        }

        // 앵커 스트레치(전체 채우기) 패널
        public static GameObject MakeStretchPanel(string name, Transform parent,
                float padL = 0, float padR = 0, float padT = 0, float padB = 0,
                Color? color = null) {
            return MakePanel(name, parent,
                Vector2.zero, Vector2.one,
                new Vector2(padL, padB), new Vector2(-padR, -padT),
                color);
        }

        // TMP_Text 생성 — 지정 부모 아래, 앵커/피벗 기본값(좌상)
        public static TMP_Text MakeText(string name, Transform parent,
                string content, float fontSize,
                Color? color = null,
                TextAlignmentOptions align = TextAlignmentOptions.Left) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color ?? TextPrimary;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        // 고정 크기 TMP_Text — anchoredPosition 기준 배치
        public static TMP_Text MakeFixedText(string name, Transform parent,
                string content, float fontSize,
                Vector2 size, Vector2 anchoredPos,
                Color? color = null,
                TextAlignmentOptions align = TextAlignmentOptions.Left) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color ?? TextPrimary;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        // 버튼 생성 — 단색 배경 + 레이블 텍스트
        public static Button MakeButton(string name, Transform parent,
                string label, float fontSize,
                Vector2 size, Vector2 anchoredPos,
                Color? bgColor = null, Color? textColor = null) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.color = bgColor ?? Accent;
            var btn = go.AddComponent<Button>();

            // 컬러 블록 — 비활성 시 dimmed
            var cb = btn.colors;
            cb.normalColor      = bgColor ?? Accent;
            cb.highlightedColor = Color.Lerp(bgColor ?? Accent, Color.white, 0.15f);
            cb.pressedColor     = Color.Lerp(bgColor ?? Accent, Color.black, 0.2f);
            cb.disabledColor    = new Color(0.2f, 0.3f, 0.3f, 0.6f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
            btn.targetGraphic = img;

            // 레이블
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(SpaceSM, SpaceXS);
            lrt.offsetMax = new Vector2(-SpaceSM, -SpaceXS);
            var txt = lbl.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.color = textColor ?? TextPrimary;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            txt.enableWordWrapping = false;
            return btn;
        }

        // 게이지(수평 fillAmount Image) 생성 — 배경 + 채움 막대 반환
        // outFill: 채움 Image(fillAmount 조작용)
        public static GameObject MakeGauge(string name, Transform parent,
                Vector2 size, Vector2 anchoredPos,
                Color fillColor,
                out Image outFill) {
            // 배경 트랙
            var bg = new GameObject(name + "_BG");
            bg.transform.SetParent(parent, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = size;
            bgRt.anchoredPosition = anchoredPos;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BgBorder;

            // 채움 막대(자식)
            var fill = new GameObject(name + "_Fill");
            fill.transform.SetParent(bg.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            outFill = fillImg;
            return bg;
        }

        // 앵커 기반 게이지(스트레치) — HUD의 수평 레이아웃 게이지용
        public static Image MakeStretchGauge(string name, Transform parent,
                Vector2 anchorMin, Vector2 anchorMax,
                Vector2 offsetMin, Vector2 offsetMax,
                Color fillColor) {
            // 배경 트랙
            var bg = MakePanel(name + "_BG", parent, anchorMin, anchorMax, offsetMin, offsetMax, BgBorder);

            // 채움 막대
            var fill = new GameObject(name + "_Fill");
            fill.transform.SetParent(bg.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            return fillImg;
        }

        // 구분선(수평 1px)
        public static GameObject MakeDivider(string name, Transform parent, float padH = 0f) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(padH, -0.5f);
            rt.offsetMax = new Vector2(-padH, 0.5f);
            var img = go.AddComponent<Image>();
            img.color = BgBorder;
            return go;
        }

        // 헥스 문자열 → Color(알파 1)
        static Color HexColor(string hex) {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) {
                return c;
            }
            return Color.magenta;   // 파싱 실패 시 눈에 띄는 오류 색
        }
    }
}
