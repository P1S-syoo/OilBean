using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI {
    // 디자인 시스템 상수 — 오염된 한강 디스토피아 수중 탐사 무드
    // 팔레트: 딥 네이비 배경 / 미드 틸 패널 / 발광 아쿠아 액센트 / 시맨틱 4색
    // 60/30/10 법칙: 배경 60%, 패널 30%, 액센트 10%. 깊이는 그림자가 아닌 "밝기(Elevation)"로 표현.
    public static class UITheme {

        // ── 팔레트 (60/30/10) ───────────────────────────────────────
        // 배경(60%) — 딥 네이비 (Elevation 0)
        public static readonly Color BgDeep    = HexColor("#08111C");
        // 패널(30%) — 미드 틸, 배경보다 밝게 (Elevation 1). BgDeep↔BgPanel 대비 상향(레이어감 확보)
        public static readonly Color BgPanel   = HexColor("#0F2030");
        // 모달·중첩 표면 (Elevation 2)
        public static readonly Color BgModal   = HexColor("#162A40");
        // 패널 테두리 / 구분선
        public static readonly Color BgBorder  = HexColor("#1E3650");
        // 헤더 강조 배경 (패널 상단 타이틀 바)
        public static readonly Color BgHeader  = HexColor("#0A1622");

        // 액센트(10%) — 발광 아쿠아. 현재 선택·주요 행동(CTA)·활성 상태에만 사용
        public static readonly Color Accent     = HexColor("#00E5CC");
        public static readonly Color AccentDim  = HexColor("#0A6E63");   // 비활성 액센트

        // 시맨틱 색상
        public static readonly Color ColSuccess = HexColor("#7AFF8C");   // 성공·완료·해금
        public static readonly Color ColWarn    = HexColor("#FFB347");   // 경고·부족
        public static readonly Color ColDanger  = HexColor("#FF4D4D");   // 위험·방전·강제 복귀
        public static readonly Color ColInfo    = HexColor("#00E5CC");   // 정보·산소·수심 밴드

        // 텍스트
        public static readonly Color TextPrimary   = HexColor("#E8F1F5");   // 주요 텍스트
        public static readonly Color TextSecondary = HexColor("#93AAB8");   // 보조·레이블
        public static readonly Color TextDisabled  = HexColor("#4A6072");   // 비활성

        // 수심 밴드 색상 (Common→Uncommon→Rare 심도) — 보라 제거, 심해 청록 계조로 통일
        public static readonly Color DepthCommon   = HexColor("#4AB8D0");
        public static readonly Color DepthUncommon = HexColor("#2A6E8C");   // 심해 청록(구 #9B59B6 보라 교체)
        public static readonly Color DepthRare     = HexColor("#163E5C");   // 더 깊은 청록

        // ── Elevation (다크테마 깊이 = 밝기 단계) ────────────────────
        public static readonly Color El0 = HexColor("#08111C");   // 배경
        public static readonly Color El1 = HexColor("#0F2030");   // 패널
        public static readonly Color El2 = HexColor("#162A40");   // 모달
        public static readonly Color El3 = HexColor("#1E3650");   // 툴팁·최상단

        // 글래스 패널 표면색(반투명) + 아쿠아 테두리/상단 하이라이트
        public static readonly Color GlassFill      = new Color(0f, 0.118f, 0.196f, 0.60f);  // rgba(0,30,50,0.6)
        public static readonly Color GlassBorder    = new Color(0f, 0.898f, 0.8f, 0.18f);    // 아쿠아 0.18 알파
        public static readonly Color GlassHighlight = new Color(0f, 0.898f, 0.8f, 0.12f);    // 상단 inset 하이라이트

        // ── 타이포그래피 스케일 (1920×1080 기준, 전반 상향) ──────────
        public const float FontDisplay = 56f;   // 디스플레이(스테이지 클리어 등 대형 타이틀)
        public const float FontTitle   = 40f;   // 패널 타이틀
        public const float FontHeading = 28f;   // 섹션 헤딩·레이블
        public const float FontBody    = 22f;   // 본문·일반 값
        public const float FontCaption = 17f;   // 캡션·단위
        public const float FontMetric  = 40f;   // 수치 강조(자원·점수) — Bold 의도

        // 수치 강조 기본 스타일 토큰
        public const FontStyles MetricStyle = FontStyles.Bold;     // 수치는 Bold
        public const FontStyles HeadingStyle = FontStyles.Bold;    // 헤딩 Bold

        // 한글 지원 폰트 — 빌더(에디터)·UIThemeFontBinder(런타임)가 주입. null이면 ResolveFont가 TMP 기본 폴백
        public static TMP_FontAsset UIFont;

        // ── 스페이싱 리듬 (8pt 그리드) ──────────────────────────────
        public const float SpaceXS  = 4f;
        public const float SpaceSM  = 8f;
        public const float SpaceMD  = 16f;
        public const float SpaceLG  = 24f;
        public const float SpaceXL  = 32f;
        public const float SpaceXXL = 48f;

        // 패널 기본 안쪽 여백(8pt 그리드)
        public const float PanelPadding = 16f;

        // 황금비 상수
        public const float Golden = 1.618f;

        // 게이지 기본 높이 — 가시성 위해 상향
        public const float GaugeHeight = 14f;
        public const float GaugeThin   = 8f;

        // ── 라운드 코너 / 깊이 ──────────────────────────────────────
        // 에디터 빌더가 생성·주입하는 9슬라이스 라운드 스프라이트. null이면 직각 폴백(런타임 빌드는 씬에 베이크됨)
        public static Sprite RoundSprite;
        public static float RoundPixelsPerUnit = 1.2f;   // 작을수록 코너 반경 큼

        // ── 공통 헬퍼 ───────────────────────────────────────────────

        // 폰트 해석 — UIFont 우선, 없으면 TMP 기본 폰트로 폴백(런타임 바인더 누락 시 안전망)
        public static TMP_FontAsset ResolveFont() {
            if (UIFont != null) {
                return UIFont;
            }
            // 바인더 미배치/빌더 미실행 상황 — 적어도 TMP 기본/폴백 폰트라도 타게 한다
            if (TMP_Settings.defaultFontAsset != null) {
                return TMP_Settings.defaultFontAsset;
            }
            return null;
        }

        // 한글 폰트 주입 — 모든 UI 텍스트에 적용. UIFont 미주입이면 TMP 기본 폴백
        public static void ApplyFont(TMP_Text t) {
            if (t == null) {
                return;
            }
            var f = ResolveFont();
            if (f != null) {
                t.font = f;
            }
        }

        // 라운드 스프라이트가 주입돼 있으면 9슬라이스로 적용(직각→둥근 모서리)
        public static void ApplyRound(Image img, float pxPerUnit = 0f) {
            if (RoundSprite == null || img == null) {
                return;
            }
            img.sprite = RoundSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pxPerUnit > 0f ? pxPerUnit : RoundPixelsPerUnit;
        }

        // 패널·카드·버튼에 부드러운 드롭섀도(깊이감) — 라운드 스프라이트와 함께 쓰면 카드처럼 떠 보임
        public static Shadow AddShadow(GameObject go, float alpha = 0.45f, float dx = 0f, float dy = -3f) {
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, alpha);
            sh.effectDistance = new Vector2(dx, dy);
            return sh;
        }

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
            ApplyRound(img);
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
            ApplyFont(t);
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
            ApplyFont(t);
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
            ApplyRound(img);
            var btn = go.AddComponent<Button>();

            // 컬러 블록 — hover/press 피드백 강화(인지 가능하게 Lerp 30%+, fade 0.12)
            var cb = btn.colors;
            cb.normalColor      = bgColor ?? Accent;
            cb.highlightedColor = Color.Lerp(bgColor ?? Accent, Color.white, 0.30f);
            cb.pressedColor     = Color.Lerp(bgColor ?? Accent, Color.black, 0.30f);
            cb.disabledColor    = new Color(0.18f, 0.26f, 0.30f, 0.6f);
            cb.fadeDuration     = 0.12f;
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
            ApplyFont(txt);
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
            bgImg.color = BgHeader;   // 더 어두운 트랙으로 채움 막대 대비↑
            ApplyRound(bgImg, 3f);   // 게이지 트랙도 살짝 둥글게

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
            // 배경 트랙 — 더 어두운 색으로 채움 막대 대비↑
            var bg = MakePanel(name + "_BG", parent, anchorMin, anchorMax, offsetMin, offsetMax, BgHeader);

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

        // 구분선(수평) — 부모 정중앙(anchorY 0.5)에 배치. 색·두께 선택
        public static GameObject MakeDivider(string name, Transform parent, float padH = 0f,
                Color? color = null, float thickness = 1f) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(padH, -thickness * 0.5f);
            rt.offsetMax = new Vector2(-padH, thickness * 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color ?? BgBorder;
            return go;
        }

        // 수평 액센트 바 — 부모의 특정 모서리(상/하)에 붙는 두꺼운 선(헤더 언더라인 등)
        public static GameObject MakeAccentBar(string name, Transform parent,
                float anchorY, float thickness, float padH, Color color) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.offsetMin = new Vector2(padH, -thickness * 0.5f);
            rt.offsetMax = new Vector2(-padH, thickness * 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        // ── 황금비 레이아웃 헬퍼 ────────────────────────────────────
        // 황금비 크기 계산 — 긴 변 1.618 : 짧은 변 1
        // widthIsLong=true: width가 긴 변 → height = width / 1.618
        // widthIsLong=false: height가 긴 변 → width = height / 1.618
        public static Vector2 GoldenSize(float longSide, bool widthIsLong) {
            float shortSide = longSide / Golden;
            return widthIsLong ? new Vector2(longSide, shortSide)
                               : new Vector2(shortSide, longSide);
        }

        // 패널 기본 패딩 RectOffset(8pt 그리드) — LayoutGroup 패딩에 사용
        public static RectOffset PanelPad(float pad = PanelPadding) {
            int p = Mathf.RoundToInt(pad);
            return new RectOffset(p, p, p, p);
        }

        // ── 게이지 트윈 ──────────────────────────────────────────────
        // fillAmount를 0.3s OutCubic으로 트윈. 일시정지(timeScale=0) 영향 없게 SetUpdate(true).
        // 같은 Image 대상 중복 트윈은 SetTarget으로 교체(Kill 후 재생).
        public static Tween TweenFill(Image fill, float target, float dur = 0.3f) {
            if (fill == null) {
                return null;
            }
            try {
                // DOTween.dll 코어 To 사용(UI 모듈 DOFillAmount 미포함 — 코드베이스 표준 패턴)
                DOTween.Kill(fill);   // 이전 트윈 중단(중복 방지)
                float clamped = Mathf.Clamp01(target);
                return DOTween.To(() => fill.fillAmount, v => fill.fillAmount = v, clamped, dur)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetTarget(fill);
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 게이지 트윈 오류: {e.Message}");
                fill.fillAmount = Mathf.Clamp01(target);   // 폴백: 즉시 적용
                return null;
            }
        }

        // ── 수면 부상 등장 모션 ─────────────────────────────────────
        // translateY +20 → 0 + 페이드 인. ease-out 250ms. CanvasGroup 없으면 추가.
        public static void RiseIn(GameObject go, float rise = 20f, float dur = 0.25f) {
            if (go == null) {
                return;
            }
            try {
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) {
                    return;
                }
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) {
                    cg = go.AddComponent<CanvasGroup>();
                }
                // 이전 트윈을 먼저 죽여 안정 좌표를 읽는다(빠른 재오픈 시 위치 드리프트 방지)
                DOTween.Kill(rt);
                DOTween.Kill(cg);
                Vector2 endPos = rt.anchoredPosition;
                rt.anchoredPosition = endPos + new Vector2(0f, rise);
                cg.alpha = 0f;
                // DOTween.dll 코어 To 사용(UI 모듈 DOAnchorPos/DOFade 미포함 — 코드베이스 표준 패턴)
                DOTween.To(() => rt.anchoredPosition, p => rt.anchoredPosition = p, endPos, dur)
                    .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(rt);
                DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, dur)
                    .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(cg);
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 수면 부상 모션 오류: {e.Message}");
            }
        }

        // ── 버튼 hover/press 스케일 피드백 ──────────────────────────
        // hover scale 1.04 / press scale 0.96, ease-out. 이벤트 트리거로 부착.
        public static void AddScaleFeedback(Button btn, float hover = 1.04f, float press = 0.96f) {
            if (btn == null) {
                return;
            }
            try {
                var fb = btn.gameObject.AddComponent<UIThemeButtonFeedback>();
                fb.Setup(btn, hover, press);
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 버튼 스케일 피드백 부착 오류: {e.Message}");
            }
        }

        // ── 글래스 패널 ──────────────────────────────────────────────
        // 반투명 패널 + 아쿠아 1px 테두리 + 상단 미세 하이라이트(글래스모피즘 근사)
        public static GameObject MakeGlassPanel(string name, Transform parent,
                Vector2 anchorMin, Vector2 anchorMax,
                Vector2 offsetMin, Vector2 offsetMax) {
            // 본체(반투명 표면)
            var go = MakePanel(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, GlassFill);

            // 아쿠아 1px 테두리(Outline 컴포넌트로 근사)
            var ol = go.AddComponent<Outline>();
            ol.effectColor = GlassBorder;
            ol.effectDistance = new Vector2(1f, 1f);

            // 상단 미세 하이라이트(상단 모서리 얇은 바)
            var hi = MakeAccentBar(name + "_Highlight", go.transform, 1f, 2f, SpaceSM, GlassHighlight);
            var hiImg = hi.GetComponent<Image>();
            if (hiImg != null) {
                hiImg.raycastTarget = false;   // 장식은 레이캐스트 제외
            }
            return go;
        }

        // ── LayoutGroup 부착 헬퍼 ───────────────────────────────────
        // VerticalLayoutGroup + ContentSizeFitter 기본 패딩/스페이싱 주입(반복 목록용)
        public static VerticalLayoutGroup AddVerticalLayout(GameObject go,
                float spacing = SpaceSM, float pad = PanelPadding,
                bool childExpandWidth = true, bool fitVertical = true) {
            if (go == null) {
                return null;
            }
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = PanelPad(pad);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = childExpandWidth;
            vlg.childForceExpandHeight = false;
            if (fitVertical) {
                var csf = go.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return vlg;
        }

        // 헥스 문자열 → Color(알파 1)
        static Color HexColor(string hex) {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) {
                return c;
            }
            return Color.magenta;   // 파싱 실패 시 눈에 띄는 오류 색
        }

        // ── 스프라이트 헬퍼 (에디터 전용) ──────────────────────────────
#if UNITY_EDITOR
        // 크롬 스프라이트 이름 → Sci-Fi UI 팩 스프라이트 매핑(scifi 폴더). 매핑에 없으면 icons 폴더 폴백.
        // 청록 컷코너 글래스 패널/버튼으로 평평한 단색 UI를 교체. 아이콘(icon_*)은 매핑 제외 → icons 유지.
        static string ResolveSpritePath(string name) {
            switch (name) {
                case "panel_bg":      return "Assets/4.Art/UI/scifi/window_transparent.png";   // 메인 글래스 패널
                case "header_bar":    return "Assets/4.Art/UI/scifi/window_transparent1.png";  // 헤더 바(대체 패널)
                case "button_accent": return "Assets/4.Art/UI/scifi/button_active.png";         // 주행동 CTA 기본
                case "button_dark":   return "Assets/4.Art/UI/scifi/button_active.png";         // 보조 버튼(톤 통일)
                case "__scifi_button_active": return "Assets/4.Art/UI/scifi/button_active.png";  // SpriteSwap hover 상태
                case "__scifi_button_pushed": return "Assets/4.Art/UI/scifi/button_pushed.png";  // SpriteSwap press 상태
                default:              return $"Assets/4.Art/UI/icons/{name}.png";                // 그 외 아이콘은 icons
            }
        }

        // UI 아이콘/크롬 스프라이트 로드 — 크롬은 scifi, 아이콘은 icons(ResolveSpritePath)
        // 없으면 null 반환(Image.sprite = null → 흰 사각형 폴백)
        public static Sprite LoadUISprite(string name) {
            try {
                string path = ResolveSpritePath(name);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) {
                    Debug.LogWarning($"[UITheme] 스프라이트 없음: {path}");
                }
                return sp;
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 스프라이트 로드 오류({name}): {e.Message}");
                return null;
            }
        }

        // 아이콘 Image 오브젝트 생성 — 부모 좌상단 기준 고정 크기
        // tint: 흰색이면 스프라이트 원본 색 유지
        public static Image MakeIconImage(string name, Transform parent,
                string spriteName, float size,
                Vector2 anchoredPos, Vector2 anchor,
                Color? tint = null) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.sprite = LoadUISprite(spriteName);
            img.color = tint ?? Color.white;
            img.preserveAspect = true;
            return img;
        }

        // 아이콘 + 스트레치 게이지 복합 블록 생성
        // 아이콘을 게이지 블록 좌상단(SpaceSM, -SpaceSM)에 붙이고, 게이지를 아이콘 오른쪽으로 밀어 정렬
        // iconSprite: 아이콘 스프라이트 이름, iconSize: 아이콘 px
        // 반환값: 채움 Image(fillAmount 조작용) — 기존 MakeStretchGauge와 동일 시그니처 호환
        public static Image MakeIconGauge(string blockName, Transform blockParent,
                string iconSprite, float iconSize,
                Vector2 anchorMin, Vector2 anchorMax,
                Vector2 offsetMin, Vector2 offsetMax,
                Color fillColor) {
            // 스트레치 게이지 생성 (아이콘 너비만큼 좌측 밀기)
            float shift = iconSize + SpaceSM;
            var adjustedOffMin = new Vector2(offsetMin.x + shift, offsetMin.y);
            var fillImg = MakeStretchGauge(blockName, blockParent,
                anchorMin, anchorMax, adjustedOffMin, offsetMax, fillColor);

            // 아이콘 — 게이지 BG의 부모(blockParent)에 직접 붙임, 게이지와 같은 앵커 기준 좌측
            var iconGo = new GameObject(blockName + "_Icon");
            iconGo.transform.SetParent(blockParent, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = anchorMin;
            iconRt.anchorMax = new Vector2(anchorMin.x, anchorMax.y);
            iconRt.offsetMin = new Vector2(offsetMin.x, offsetMin.y);
            iconRt.offsetMax = new Vector2(offsetMin.x + iconSize, offsetMin.y + iconSize);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = LoadUISprite(iconSprite);
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            return fillImg;
        }

        // 버튼 배경에 Sci-Fi 스프라이트 적용 + SpriteSwap 상태 배선(hover/press)
        // 기본=button_active, highlighted=button_active(글로우), pressed=button_pushed. 무틴트(흰색)로 원본 청록 유지.
        // bgSpriteName이 null이면 기존 단색 유지. 시그니처는 호출부 보존을 위해 그대로 둔다.
        public static void ApplySpriteButton(Button btn, string bgSpriteName) {
            if (btn == null || string.IsNullOrEmpty(bgSpriteName)) {
                return;
            }
            try {
                var sp = LoadUISprite(bgSpriteName);
                if (sp == null) {
                    return;
                }
                var img = btn.GetComponent<Image>();
                if (img == null) {
                    return;
                }
                img.sprite = sp;
                // 9슬라이스 보더가 실제로 설정돼 있으면 Sliced, 없으면 Simple 폴백
                bool hasBorder = sp.border.sqrMagnitude > 0.01f;
                img.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;   // 틴트 제거 — 스프라이트 원본 색 사용

                // hover=button_active(글로우), press=button_pushed 상태 스프라이트로 SpriteSwap 전환 배선
                var spActive = LoadUISprite("__scifi_button_active");   // 직접 경로 로드용 내부 키
                var spPushed = LoadUISprite("__scifi_button_pushed");
                if (spActive != null && spPushed != null) {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    var ss = btn.spriteState;
                    ss.highlightedSprite = spActive;
                    ss.pressedSprite     = spPushed;
                    ss.selectedSprite    = spActive;
                    ss.disabledSprite    = sp;
                    btn.spriteState = ss;
                    btn.targetGraphic = img;
                }
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 버튼 스프라이트 적용 오류({bgSpriteName}): {e.Message}");
            }
        }

        // scifi 폴더 크롬 스프라이트 여부 — 기존 단색 오버레이 tint(알파 0.04/0.08)를 1.0으로 보정하기 위한 판별
        static bool IsScifiChromeSprite(string name) {
            return name == "panel_bg" || name == "header_bar";
        }

        // 패널 배경에 스프라이트 적용 — Image.type Sliced/Simple 자동 선택
        // scifi 크롬 스프라이트(panel_bg/header_bar)는 tint 알파를 강제 1.0으로 보정한다.
        // 호출부가 단색 오버레이 의도로 넘긴 alpha 0.04/0.08이 scifi 스프라이트에 곱해지면 사실상 투명해지기 때문.
        public static void ApplyPanelSprite(GameObject panel, string spriteName, Color? tint = null) {
            if (panel == null || string.IsNullOrEmpty(spriteName)) {
                return;
            }
            try {
                var sp = LoadUISprite(spriteName);
                if (sp == null) {
                    return;
                }
                var img = panel.GetComponent<Image>();
                if (img == null) {
                    img = panel.AddComponent<Image>();
                }
                img.sprite = sp;
                bool hasBorder = sp.border.sqrMagnitude > 0.01f;
                img.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
                // scifi 크롬은 무틴트(흰색)로 원본 청록 글래스를 유지. 단색 폴백은 호출부 tint 그대로.
                Color finalTint = tint ?? Color.white;
                if (IsScifiChromeSprite(spriteName)) {
                    finalTint = new Color(finalTint.r, finalTint.g, finalTint.b, 1f);
                }
                img.color = finalTint;
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 패널 스프라이트 적용 오류({spriteName}): {e.Message}");
            }
        }
#endif
    }
}
