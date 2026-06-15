using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public const float FontTitle   = 36f;   // 패널 타이틀
        public const float FontHeading = 26f;   // 섹션 헤딩
        public const float FontBody    = 19f;   // 본문·일반 값
        public const float FontCaption = 15f;   // 캡션·레이블

        // 한글 지원 폰트 — 에디터 빌더가 주입(NotoSansKR). null이면 TMP 기본(한글 □ 깨짐)
        public static TMP_FontAsset UIFont;

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

        // ── 라운드 코너 / 깊이 ──────────────────────────────────────
        // 에디터 빌더가 생성·주입하는 9슬라이스 라운드 스프라이트. null이면 직각 폴백(런타임 빌드는 씬에 베이크됨)
        public static Sprite RoundSprite;
        public static float RoundPixelsPerUnit = 1.2f;   // 작을수록 코너 반경 큼

        // ── 공통 헬퍼 ───────────────────────────────────────────────

        // 한글 폰트 주입 — 모든 UI 텍스트에 적용(미주입 시 TMP 기본=한글 깨짐)
        public static void ApplyFont(TMP_Text t) {
            if (UIFont != null && t != null) {
                t.font = UIFont;
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
            bgImg.color = BgBorder;
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

        // 헥스 문자열 → Color(알파 1)
        static Color HexColor(string hex) {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) {
                return c;
            }
            return Color.magenta;   // 파싱 실패 시 눈에 띄는 오류 색
        }

        // ── 스프라이트 헬퍼 (에디터 전용) ──────────────────────────────
#if UNITY_EDITOR
        // UI 아이콘/크롬 스프라이트 로드 — Assets/4.Art/UI/icons/{name}.png
        // 없으면 null 반환(Image.sprite = null → 흰 사각형 폴백)
        public static Sprite LoadUISprite(string name) {
            try {
                string path = $"Assets/4.Art/UI/icons/{name}.png";
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

        // 버튼 배경에 스프라이트 적용 — Image.type Sliced 시도, 보더 없으면 Simple 폴백
        // bgSpriteName이 null이면 기존 단색 유지
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
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 버튼 스프라이트 적용 오류({bgSpriteName}): {e.Message}");
            }
        }

        // 패널 배경에 스프라이트 적용 — Image.type Sliced/Simple 자동 선택
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
                img.color = tint ?? Color.white;
            } catch (System.Exception e) {
                Debug.LogError($"[UITheme] 패널 스프라이트 적용 오류({spriteName}): {e.Message}");
            }
        }
#endif
    }
}
