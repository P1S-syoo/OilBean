// HudUIBuilder.cs — 에디터 메뉴에서 Canvas + 4종 패널을 코드로 생성
// [Tools/한강/UI/HUD·패널 생성] 한 번 실행으로 완성
// 생성 패널: DockConsole(거점), HudPanel(인게임 HUD), ResearchPanel, CraftPanel
// 패널은 기본 비활성 — GameState 전환에 따라 호출자가 활성 제어
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.UI;
using Game.Core;
using Game.Player;
using Game.Items;
using Game.Craft;

namespace Game.Editor.UI {
    // UI 4종 + Canvas를 씬에 코드로 생성하고 컴포넌트 참조를 자동 배선하는 에디터 도구
    public static class HudUIBuilder {

        const string CANVAS_NAME = "GameCanvas";
        const string ROUND_SPRITE_PATH = "Assets/4.Art/UI/RoundedRect.png";
        const string KOREAN_FONT_PATH = "Assets/4.Art/Fonts/IyagiGGC SDF.asset";
        const string CURSOR_TEXTURE_PATH = "Assets/4.Art/Textures/UI/cursor_holo.png";

        // ── HUD 비겹침 레이아웃 상수(8pt 그리드) — 그리드밖 절대수치(-136f 등) 제거 ──
        const float HUD_SAFE   = UITheme.SpaceXL;    // 화면 가장자리 안전여백 32
        const float HUD_BLOCK_W = 320f;              // 코너 HUD 블록 너비
        const float HUD_BLOCK_H = 96f;               // 코너 HUD 블록 높이
        const float DEPTH_BAND_W = 48f;              // 우측 수심 밴드 너비 — 현재 수심 발광 마커 포함

        // 한글 지원 TMP 폰트 로드 — 미발견 시 null(기본 폰트, 한글 깨짐 경고)
        static TMP_FontAsset LoadKoreanFont() {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KOREAN_FONT_PATH);
            if (f == null) {
                Debug.LogWarning($"[HudUIBuilder] 한글 폰트 없음: {KOREAN_FONT_PATH} — 한글이 깨질 수 있음");
            }
            return f;
        }

        // ── 진입점 ────────────────────────────────────────────────────────────
        // [MenuItem("Tools/한강/UI/HUD·패널 생성")]
        public static void Build() {
            try {
                // 라운드 코너 스프라이트 + 한글 폰트 주입 — 전 패널 공통(한글 □ 깨짐 방지)
                UITheme.RoundSprite = EnsureRoundedSprite();
                UITheme.UIFont = LoadKoreanFont();
                // 기존 Canvas 재활용 또는 신규 생성
                var canvasGo = GetOrCreateCanvas();
                var canvasTr = canvasGo.transform;

                // 씬에서 런타임 컴포넌트 수집 (없으면 null — 경고만)
                var run      = FindAsset<RunData>("RunData");
                var battery  = FindComponent<Battery>();
                var collector = FindComponent<Collector>();
                var research  = FindComponent<Research>();
                var crafting  = FindComponent<Crafting>();
                var bootstrap = FindComponent<GameBootstrap>();
                var diveConfig = FindAsset<잠수설정>("잠수설정");
                var collectConfig = FindAsset<수집설정>("수집설정");

                // ── 패널 4종 생성 ──────────────────────────────────────────────
                BuildDockConsole(canvasTr, run, research, crafting, bootstrap);
                BuildHudPanel(canvasTr, run, battery, collector, diveConfig, collectConfig);
                BuildResearchPanel(canvasTr, run, research, collector);
                BuildCraftPanel(canvasTr, run, research, crafting, collector);
                BuildMinigamePanel(canvasTr, collector);
                BuildCustomCursor(canvasGo);

                // ── 패널 라우터 배선 (GameState별 토글) ──
                WireRouter(canvasGo, bootstrap);

                // ── 런타임 한글 폰트 바인더 배선(□ 깨짐 방지 핵심) ──
                WireFontBinder();

                // 씬 더티 표시
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                Debug.Log("[HudUIBuilder] UI 4종 생성 완료 — GameCanvas 아래를 확인하세요.");
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 생성 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        static void BuildCustomCursor(GameObject canvasGo) {
            var cursor = canvasGo.GetComponent<CustomCursorFx>() ?? canvasGo.AddComponent<CustomCursorFx>();
            var so = new SerializedObject(cursor);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CURSOR_TEXTURE_PATH);
            AssignRef(so, "cursorTexture", tex);
        }

        // ── Canvas 획득/생성 ──────────────────────────────────────────────────
        static GameObject GetOrCreateCanvas() {
            var existing = GameObject.Find(CANVAS_NAME);
            if (existing != null) {
                var existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null) {
                    existingCanvas.overrideSorting = false;
                    existingCanvas.sortingOrder = 10;
                }
                return existing;
            }
            var go = new GameObject(CANVAS_NAME);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 1. 거점 콘솔 (DockConsole) — GameState.Dock 시 표시
        // 레이아웃: 상단 자원바 / 중앙 행동 버튼 3종 / 우측 목표 카드
        // ═══════════════════════════════════════════════════════════════════════
        static void BuildDockConsole(Transform canvas,
                RunData run, Research research, Crafting crafting, GameBootstrap bootstrap) {
            // 기존 패널 제거 후 재생성
            DestroyExisting(canvas, "DockConsole");

            // 루트 패널 — 화면을 꽉 채우지 않는 황금비 중앙 허브(1400×865). 3D 수면 위에 떠 보이게 어두운 톤 유지
            var root = MakeGoldenPanel("DockConsole", canvas, 1400f);
            root.GetComponent<Image>().color = new Color(0.05f, 0.12f, 0.14f, 0.96f);
            UITheme.AddShadow(root, 0.6f, 0f, -6f);
            root.AddComponent<Game.UI.PopupPanel>();   // 연구/제작 패널과 동일한 스케일+페이드 등장감
            root.SetActive(false);   // 기본 비활성

            // ── 상단 자원 바 (높이 72) ──────────────────────────────────────
            var topBar = UITheme.MakePanel("TopBar", root.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -72f), new Vector2(0f, 0f),
                UITheme.BgHeader);

            // 타이틀 — ■ 기호 제거, 좌측 4px 세로 아쿠아 액센트바로 대체
            MakeVAccentBar("TitleAccent", topBar.transform, UITheme.SpaceLG, 28f);
            var titleTxt = UITheme.MakeText("Title", topBar.transform,
                "정화선 거점", UITheme.FontTitle, UITheme.TextPrimary,
                TextAlignmentOptions.Left);
            titleTxt.fontStyle = UITheme.HeadingStyle;
            var titleRt = titleTxt.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.3f, 1f);
            titleRt.offsetMin = new Vector2(UITheme.SpaceLG + 16f, 0f);
            titleRt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);

            // 자원 텍스트 (강재 / 샘플 / 적재 / 정화제) — 우측은 닫기 버튼 공간만큼 좁힘
            var resTxt = MakeStretchText("ResourceBar", topBar.transform,
                "강재: — / 샘플: — / 적재: — / 정화제: —",
                UITheme.FontBody, UITheme.TextSecondary,
                new Vector2(0.3f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(-UITheme.SpaceLG - 48f, 0f));

            // 우상단 닫기 버튼 — 수상 콘솔 접기. HubCloseButton 대신 DockConsoleUI.OnClose가 처리
            var closeBtn = UITheme.MakeButton("CloseBtn", topBar.transform, "X", UITheme.FontHeading,
                new Vector2(40f, 40f), Vector2.zero, UITheme.BgBorder, UITheme.TextPrimary);
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.sizeDelta = new Vector2(40f, 40f);
            closeRt.anchoredPosition = new Vector2(-UITheme.SpaceMD - 20f, 0f);
            UITheme.AddScaleFeedback(closeBtn);

            // ── 중앙 영역 ────────────────────────────────────────────────────
            var center = UITheme.MakePanel("Center", root.transform,
                new Vector2(0f, 0f), new Vector2(0.68f, 1f),
                new Vector2(UITheme.SpaceLG, UITheme.SpaceLG),
                new Vector2(-UITheme.SpaceSM, -72f - UITheme.SpaceSM),
                UITheme.BgPanel);
            UITheme.AddShadow(center, 0.4f, 0f, -3f);

            // 주행동: 탐사 시작 (대형 CTA) — 황금비 기반 비율(폭 = 높이 × φ³ ≈ 4.236)로 중앙 상단 고정
            // 가로 스트레치 바 대신 비례 잡힌 CTA로(요구: 내부 버튼도 황금비)
            float diveH = 96f;
            float diveW = Mathf.Round(diveH * UITheme.Golden * UITheme.Golden * UITheme.Golden);   // φ³ ≈ 407
            var diveBtn = UITheme.MakeButton("DiveBtn", center.transform,
                "탐사 시작 — 수중 잠수", UITheme.FontHeading,
                new Vector2(diveW, diveH), Vector2.zero,
                UITheme.Accent, UITheme.BgDeep);
            // sci-fi 버튼 스프라이트(button_active)는 어둡고 중앙이 반투명 → 어두운 패널 위에서 버튼·라벨이 사라짐.
            // 불투명 솔리드 액센트 배경 유지(밝은 청록 CTA + 어두운 라벨 = 고대비)로 '잠수 개시' 텍스트 가독성 확보
            // 중앙 상단 고정(앵커 0.5,1 / 피벗 0.5,1) — 패널 폭이 줄어도 중앙 정렬 유지
            var diveRt = diveBtn.GetComponent<RectTransform>();
            diveRt.anchorMin = new Vector2(0.5f, 1f);
            diveRt.anchorMax = new Vector2(0.5f, 1f);
            diveRt.pivot = new Vector2(0.5f, 1f);
            diveRt.sizeDelta = new Vector2(diveW, diveH);
            diveRt.anchoredPosition = new Vector2(0f, -UITheme.SpaceMD);
            // 잠수 아이콘 — 버튼 레이블 왼쪽에 붙이고 레이블은 아이콘 너머로 인셋(첫 글자 잘림 방지)
            AddButtonIcon(diveBtn, "icon_dive", 32f, UITheme.SpaceLG + 16f);
            UITheme.AddShadow(diveBtn.gameObject, 0.5f, 0f, -3f);   // 주행동 버튼 강조 깊이
            UITheme.AddScaleFeedback(diveBtn, 1.06f, 0.95f);        // 주행동 — 더 큰 스케일 강조

            // 보조 버튼 행 (연구 / 제작 / 정화 설치) — CTA 바로 아래(diveH+상하 여백)에 배치
            float subBtnY = -UITheme.SpaceMD - diveH - UITheme.SpaceMD;
            Button researchBtn = MakeSubButton("ResearchBtn",  center.transform, "연구",     subBtnY, 0f);
            Button craftBtn    = MakeSubButton("CraftBtn",     center.transform, "제작",     subBtnY, 1f);
            Button purifyBtn   = MakeSubButton("PurifyBtn",    center.transform, "정화 설치", subBtnY, 2f);
            // 보조 버튼 — 솔리드 불투명 배경 유지(스프라이트 미적용). 어두운 패널 위에서도 버튼 형태가 또렷
            AddSubBtnIcon(researchBtn, "icon_research");
            AddSubBtnIcon(craftBtn,    "icon_craft");
            AddSubBtnIcon(purifyBtn,   "icon_purify");
            // 보조 버튼 스케일 피드백
            UITheme.AddScaleFeedback(researchBtn);
            UITheme.AddScaleFeedback(craftBtn);
            UITheme.AddScaleFeedback(purifyBtn);

            // ── 중앙 하단: 탐사 브리핑 — 버튼 아래 빈 공간을 안내 정보로 채움 ──
            // 브리핑 상단 = CTA(-16-96) + 보조버튼행(16+64) + 여백 → 약 -208(타 버튼과 비겹침)
            var brief = UITheme.MakePanel("Brief", center.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceMD, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, -208f),
                UITheme.BgHeader);
            UITheme.MakeAccentBar("BriefBar", brief.transform, 1f, 2f, 0f, UITheme.AccentDim);

            var briefTitle = UITheme.MakeText("BriefTitle", brief.transform,
                "탐사 브리핑", UITheme.FontCaption, UITheme.Accent, TextAlignmentOptions.Left);
            var briefTitleRt = briefTitle.GetComponent<RectTransform>();
            briefTitleRt.anchorMin = new Vector2(0f, 1f);
            briefTitleRt.anchorMax = new Vector2(1f, 1f);
            briefTitleRt.offsetMin = new Vector2(UITheme.SpaceMD, -UITheme.SpaceMD - 18f);
            briefTitleRt.offsetMax = new Vector2(-UITheme.SpaceMD, -UITheme.SpaceMD);
            briefTitle.fontStyle = FontStyles.Bold;

            var briefBody = MakeStretchText("BriefBody", brief.transform,
                "· 수중에서 고철과 오염 샘플을 수집해 거점으로 가져옵니다.\n" +
                "· 배터리 방전·적재 한계·오염원 충돌 전에 복귀하세요.\n" +
                "· 연구로 정화제를 해금하고, 제작으로 정화 부유체를 만듭니다.\n" +
                "· 부유체 단계가 오를수록 더 깊은 수심까지 탐사할 수 있습니다.",
                UITheme.FontBody, UITheme.TextSecondary,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceMD, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, -UITheme.SpaceMD - 28f),
                TextAlignmentOptions.TopLeft);
            briefBody.enableWordWrapping = true;
            briefBody.overflowMode = TextOverflowModes.Ellipsis;

            // ── 우측: 현재 목표 카드 ─────────────────────────────────────────
            var goalCard = UITheme.MakePanel("GoalCard", root.transform,
                new Vector2(0.68f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, UITheme.SpaceLG),
                new Vector2(-UITheme.SpaceLG, -72f - UITheme.SpaceSM),
                UITheme.BgPanel);
            UITheme.AddShadow(goalCard, 0.4f, 0f, -3f);

            var goalTitle = UITheme.MakeText("GoalTitle", goalCard.transform,
                "현재 목표", UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Left);
            var goalTitleRt = goalTitle.GetComponent<RectTransform>();
            goalTitleRt.anchorMin = new Vector2(0f, 1f);
            goalTitleRt.anchorMax = new Vector2(1f, 1f);
            goalTitleRt.offsetMin = new Vector2(UITheme.SpaceMD, -UITheme.SpaceMD - UITheme.FontHeading - 4f);
            goalTitleRt.offsetMax = new Vector2(-UITheme.SpaceMD, -UITheme.SpaceMD);

            // 제목 아래 구분선 — 카드 상단에 배치(정중앙 배치 버그 회피)
            UITheme.MakeAccentBar("GoalDiv", goalCard.transform, 0.86f, 1f, UITheme.SpaceMD, UITheme.BgBorder);

            var goalBody = MakeStretchText("GoalBody", goalCard.transform,
                "수심 15m까지 탐사 가능\n샘플을 수집하고 복귀하여\n연구를 진행하세요.",
                UITheme.FontBody, UITheme.TextPrimary,
                new Vector2(0f, 0f), new Vector2(1f, 0.84f),
                new Vector2(UITheme.SpaceMD, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, -UITheme.SpaceMD),
                TextAlignmentOptions.TopLeft);
            goalBody.enableWordWrapping = true;
            goalBody.overflowMode = TextOverflowModes.Overflow;

            // DockConsoleUI 컴포넌트 부착 — 런타임 참조 배선
            var dockUi = root.AddComponent<DockConsoleUI>();
            // SerializedObject로 private 필드에 배선
            var so = new SerializedObject(dockUi);
            AssignRef(so, "run",        run);
            AssignRef(so, "research",   research);
            AssignRef(so, "crafting",   crafting);
            AssignRef(so, "bootstrap",  bootstrap);
            AssignRef(so, "resourceBarText", resTxt);
            AssignRef(so, "goalBodyText",    goalBody);
            AssignRef(so, "diveBtn",         diveBtn);
            AssignRef(so, "researchBtn",     researchBtn);
            AssignRef(so, "craftBtn",        craftBtn);
            AssignRef(so, "purifyBtn",       purifyBtn);
            AssignRef(so, "closeBtn",        closeBtn);
            AssignRef(so, "surface",         FindComponent<Game.Surface.SurfaceBootstrap>());
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Build DockConsole UI");
            Debug.Log("[HudUIBuilder] DockConsole 패널 생성");
        }

        // 거점 보조 버튼 3분할 (0/1/2 순서)
        static Button MakeSubButton(string name, Transform parent, string label, float topOffset, float idx) {
            var btn = UITheme.MakeButton(name, parent, label, UITheme.FontBody,
                Vector2.zero, Vector2.zero,
                UITheme.BgBorder, UITheme.TextPrimary);
            UITheme.AddShadow(btn.gameObject, 0.35f, 0f, -2f);
            var rt = btn.GetComponent<RectTransform>();
            float w = 1f / 3f;
            const float subBtnH = 64f;   // 높이 상향(40→64) — 1/3폭 대비 비율을 황금비에 가깝게
            rt.anchorMin = new Vector2(w * idx, 1f);
            rt.anchorMax = new Vector2(w * (idx + 1f), 1f);
            rt.offsetMin = new Vector2(idx == 0 ? UITheme.SpaceMD : UITheme.SpaceXS, topOffset - subBtnH);
            rt.offsetMax = new Vector2(idx == 2 ? -UITheme.SpaceMD : -UITheme.SpaceXS, topOffset);
            return btn;
        }

        // 보조 버튼에 아이콘 이미지 추가 — 버튼 레이블 좌측 중앙 고정(20px)
        static void AddSubBtnIcon(Button btn, string spriteName) {
            AddButtonIcon(btn, spriteName, 20f, UITheme.SpaceMD + 10f);
        }

        // 버튼 좌측 아이콘 + 가운데 레이블 겹침 방지 — 레이블 좌측 여백을 아이콘 우측 너머로 인셋(첫 글자 잘림 해소)
        static void AddButtonIcon(Button btn, string spriteName, float size, float xPos) {
            if (btn == null) {
                return;
            }
            UITheme.MakeIconImage(spriteName + "_Icon", btn.transform, spriteName, size,
                new Vector2(xPos, 0f), new Vector2(0f, 0.5f), Color.white);
            var label = btn.transform.Find("Label") as RectTransform;
            if (label != null) {
                float clear = xPos + size * 0.5f + UITheme.SpaceSM;   // 아이콘 우측 끝 + 여백
                label.offsetMin = new Vector2(clear, label.offsetMin.y);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. 인게임 HUD — GameState.Dive 시 표시
        // 비겹침 4모서리 배치: 좌상 배터리 / 우상 수심 / 좌하 임무 / 우하 적재
        // 좌측 수심 밴드(세로 중앙) / 상단 배너·경고아이콘·하단 토스트는 코너 블록과 수직 분리
        // ═══════════════════════════════════════════════════════════════════════
        static void BuildHudPanel(Transform canvas,
                RunData run, Battery battery, Collector collector,
                잠수설정 diveConfig, 수집설정 collectConfig) {
            DestroyExisting(canvas, "HudPanel");

            // 루트 — 완전 투명(개별 자식만 색)
            var root = UITheme.MakeStretchPanel("HudPanel", canvas, color: Color.clear);
            root.SetActive(false);

            // ── 좌하: 배터리 블록(글래스) — 임무와 위치 스왑(corner 2) ─────────────
            var batBlock = MakeCornerBlock("BatteryBlock", root.transform, corner: 2);
            UITheme.MakeIconImage("BatIcon", batBlock.transform, "icon_battery", 30f,
                new Vector2(UITheme.SpaceMD + 15f, -UITheme.SpaceSM - 15f),
                new Vector2(0f, 1f), Color.white);
            var batLabel = UITheme.MakeText("BatLabel", batBlock.transform,
                "배터리", UITheme.FontCaption, UITheme.TextSecondary);
            PinTopLeft(batLabel.GetComponent<RectTransform>(), UITheme.SpaceMD + 38f, UITheme.SpaceSM, 100f, 20f);
            // 수치 강조 — FontMetric(40) Bold, 우상단 큰 % 표시
            var batPct = UITheme.MakeText("BatPct", batBlock.transform,
                "100%", UITheme.FontMetric, UITheme.ColSuccess, TextAlignmentOptions.Right);
            batPct.fontStyle = UITheme.MetricStyle;
            PinTopRight(batPct.GetComponent<RectTransform>(), UITheme.SpaceMD, UITheme.SpaceXS, 130f, 44f);
            // 게이지 — 블록 하단, 아이콘 너비만큼 좌측 오프셋
            Image batFill = UITheme.MakeStretchGauge("BatGauge", batBlock.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceMD + 38f, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, UITheme.SpaceMD + UITheme.GaugeHeight),
                UITheme.ColSuccess);

            // 수심 수치는 우측 수심 밴드 상단의 미터패널(DepthMeter)로 이동 — 게이지 옆에 일원화

            // ── 우하: 적재 블록(글래스) — 게이지 + kg 수치 ─────────────────
            var cargoBlock = MakeCornerBlock("CargoBlock", root.transform, corner: 3);
            UITheme.MakeIconImage("CargoIcon", cargoBlock.transform, "icon_cargo", 30f,
                new Vector2(UITheme.SpaceMD + 15f, -UITheme.SpaceSM - 15f),
                new Vector2(0f, 1f), Color.white);
            var cargoLabel = UITheme.MakeText("CargoLabel", cargoBlock.transform,
                "적재", UITheme.FontCaption, UITheme.TextSecondary);
            PinTopLeft(cargoLabel.GetComponent<RectTransform>(), UITheme.SpaceMD + 38f, UITheme.SpaceSM, 100f, 20f);
            var cargoTxt = UITheme.MakeText("CargoTxt", cargoBlock.transform,
                "0 / 70 kg", UITheme.FontHeading, UITheme.TextPrimary, TextAlignmentOptions.Right);
            cargoTxt.fontStyle = UITheme.HeadingStyle;
            PinTopRight(cargoTxt.GetComponent<RectTransform>(), UITheme.SpaceMD, UITheme.SpaceSM, 160f, 30f);
            Image cargoFill = UITheme.MakeStretchGauge("CargoGauge", cargoBlock.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceMD + 38f, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, UITheme.SpaceMD + UITheme.GaugeHeight),
                UITheme.ColWarn);

            // ── 좌상: 정화도 / 임무 안내 블록(글래스) — 배터리와 위치 스왑(corner 0) ──
            var missionBlock = MakeCornerBlock("MissionBlock", root.transform, corner: 0);
            UITheme.MakeIconImage("MissionIcon", missionBlock.transform, "icon_purify", 26f,
                new Vector2(UITheme.SpaceMD + 13f, -UITheme.SpaceSM - 13f),
                new Vector2(0f, 1f), UITheme.Accent);
            var missionLabel = UITheme.MakeText("MissionLabel", missionBlock.transform,
                "임무", UITheme.FontCaption, UITheme.TextSecondary);
            PinTopLeft(missionLabel.GetComponent<RectTransform>(), UITheme.SpaceMD + 36f, UITheme.SpaceSM, 100f, 20f);
            var missionTxt = MakeStretchText("MissionTxt", missionBlock.transform,
                "샘플 수집 후 복귀", UITheme.FontBody, UITheme.TextPrimary,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceMD, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, UITheme.SpaceMD + 26f),
                TextAlignmentOptions.Left);

            // ── 우상: 수심 HUD — 다른 코너 HUD와 같은 글래스 디자인 ─────────────
            var depthShell = MakeCornerBlock("DepthHud", root.transform, corner: 1);
            UITheme.MakeIconImage("DepthHudIcon", depthShell.transform, "icon_depth", 24f,
                new Vector2(UITheme.SpaceMD + 12f, -UITheme.SpaceMD - 12f),
                new Vector2(0f, 1f), UITheme.ColInfo);
            var depthTitle = UITheme.MakeText("DepthTitle", depthShell.transform,
                "수심", UITheme.FontCaption, UITheme.TextSecondary);
            depthTitle.fontStyle = FontStyles.Bold;
            PinTopLeft(depthTitle.GetComponent<RectTransform>(), UITheme.SpaceMD + 34f, UITheme.SpaceSM, 80f, 22f);
            var depthTxt = UITheme.MakeText("DepthTxt", depthShell.transform,
                "0m", UITheme.FontHeading, UITheme.ColInfo, TextAlignmentOptions.Right);
            depthTxt.fontStyle = UITheme.HeadingStyle;
            PinTopRight(depthTxt.GetComponent<RectTransform>(), UITheme.SpaceMD, UITheme.SpaceXS, 150f, 34f);

            var depthBand = UITheme.MakeGlassPanel("DepthBand", root.transform,
                new Vector2(1f, 0.16f), new Vector2(1f, 0.82f),
                new Vector2(-HUD_SAFE - DEPTH_BAND_W, 0f),
                new Vector2(-HUD_SAFE, 0f));
            UITheme.AddShadow(depthBand, 0.4f, 0f, -2f);

            // 얕음(상) / 중간(중) / 깊음(하) 3구간 — 불투명도 상향(가독성)
            BuildDepthSegment("DepthCommon",   depthBand.transform, 0.67f, 1f,    UITheme.DepthCommon);
            BuildDepthSegment("DepthUncommon", depthBand.transform, 0.33f, 0.67f, UITheme.DepthUncommon);
            BuildDepthSegment("DepthRare",     depthBand.transform, 0f,    0.33f, UITheme.DepthRare);

            // 수심 밴드 한글 레이블(우측)
            BuildDepthLabel("LblCommon",   depthBand.transform, 0.67f, 1f,    "얕음", UITheme.DepthCommon);
            BuildDepthLabel("LblUncommon", depthBand.transform, 0.33f, 0.67f, "중간", UITheme.TextPrimary);
            BuildDepthLabel("LblRare",     depthBand.transform, 0f,    0.33f, "깊음", UITheme.TextPrimary);

            // 현재 수심 인디케이터 — 밴드 위를 오르내리는 아쿠아 마커(런타임에서 위치 갱신)
            var depthMarker = new GameObject("DepthMarker");
            depthMarker.transform.SetParent(depthBand.transform, false);
            var markerRt = depthMarker.AddComponent<RectTransform>();
            markerRt.anchorMin = new Vector2(0f, 1f);
            markerRt.anchorMax = new Vector2(1f, 1f);
            markerRt.offsetMin = new Vector2(-12f, -9f);
            markerRt.offsetMax = new Vector2(12f, 9f);
            BuildDepthMarkerGlow(depthMarker.transform);

            // ── 상단 경고 배너 — 코너 블록 아래(y -144~-200)로 내려 비겹침 ──
            var warnBanner = UITheme.MakePanel("WarnBanner", root.transform,
                new Vector2(0.3f, 1f), new Vector2(0.7f, 1f),
                new Vector2(0f, -HUD_SAFE - HUD_BLOCK_H - UITheme.SpaceMD - 56f),
                new Vector2(0f, -HUD_SAFE - HUD_BLOCK_H - UITheme.SpaceMD),
                UITheme.ColDanger);
            warnBanner.SetActive(false);   // 평상시 숨김

            UITheme.MakeIconImage("WarnBannerIcon", warnBanner.transform, "icon_warning", 28f,
                new Vector2(UITheme.SpaceMD + 14f, 0f),
                new Vector2(0f, 0.5f), Color.white);
            var warnTxt = UITheme.MakeText("WarnText", warnBanner.transform,
                "배터리 방전 — 정화선 복귀", UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            warnTxt.fontStyle = UITheme.HeadingStyle;

            // ── 독립 경고 아이콘 — 화면 중앙 상단(코너·배너와 분리), 기본 비활성 ──
            var warnIconGo = new GameObject("WarnIcon");
            warnIconGo.transform.SetParent(root.transform, false);
            var warnIconRt = warnIconGo.AddComponent<RectTransform>();
            warnIconRt.anchorMin = new Vector2(0.5f, 1f);
            warnIconRt.anchorMax = new Vector2(0.5f, 1f);
            warnIconRt.sizeDelta = new Vector2(48f, 48f);
            warnIconRt.anchoredPosition = new Vector2(0f, -HUD_SAFE - 24f);
            var warnIconImg = warnIconGo.AddComponent<Image>();
            warnIconImg.sprite = UITheme.LoadUISprite("icon_warning");
            warnIconImg.color = UITheme.ColDanger;
            warnIconImg.preserveAspect = true;
            warnIconImg.raycastTarget = false;   // 장식 — 레이캐스트 제외
            warnIconGo.SetActive(false);

            // ── 하단 중앙: 수집 토스트 — 우하 적재블록 위(y 160~224)로 비겹침 ──
            var toastGo = UITheme.MakeGlassPanel("CollectToast", root.transform,
                new Vector2(0.3f, 0f), new Vector2(0.7f, 0f),
                new Vector2(0f, HUD_SAFE + HUD_BLOCK_H + UITheme.SpaceXL),
                new Vector2(0f, HUD_SAFE + HUD_BLOCK_H + UITheme.SpaceXL + 52f));
            var toastTxt = UITheme.MakeText("ToastTxt", toastGo.transform,
                "", UITheme.FontBody, UITheme.Accent, TextAlignmentOptions.Center);
            toastTxt.fontStyle = FontStyles.Bold;
            toastGo.SetActive(false);

            // ── 상호작용 프롬프트 — 하단 중앙 "[E] 오염체 정제"(범위 진입 시) ──
            var promptGo = UITheme.MakeGlassPanel("InteractPrompt", root.transform,
                new Vector2(0.38f, 0f), new Vector2(0.62f, 0f),
                new Vector2(0f, HUD_SAFE + UITheme.SpaceSM),
                new Vector2(0f, HUD_SAFE + UITheme.SpaceSM + 48f));
            var promptTxt = UITheme.MakeText("PromptTxt", promptGo.transform,
                "[E] 오염체 정제", UITheme.FontBody, UITheme.Accent, TextAlignmentOptions.Center);
            promptTxt.fontStyle = FontStyles.Bold;
            promptGo.SetActive(false);

            // Hud 컴포넌트 배선 (기존 Hud.cs)
            var hudComp = root.AddComponent<Hud>();
            var hudSo = new SerializedObject(hudComp);
            AssignRef(hudSo, "run",         run);
            AssignRef(hudSo, "battery",     battery);
            // infoText는 배선하지 않음 — 적재 수치는 HudExtUI.cargoText가 전용으로 갱신(160px 오버플로 분리)
            AssignRef(hudSo, "batteryFill", batFill);
            AssignRef(hudSo, "warnText",    warnTxt);
            AssignRef(hudSo, "diveConfig", diveConfig);
            AssignRef(hudSo, "collectConfig", collectConfig);
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // HudExtUI — 배터리 % / 수심 / 경고 배너 표시 확장
            var hudExt = root.AddComponent<HudExtUI>();
            var extSo = new SerializedObject(hudExt);
            AssignRef(extSo, "run",          run);
            AssignRef(extSo, "battery",      battery);
            AssignRef(extSo, "collector",    collector);
            AssignRef(extSo, "batteryPct",   batPct);
            AssignRef(extSo, "cargoFill",    cargoFill);
            AssignRef(extSo, "cargoText",    cargoTxt);
            AssignRef(extSo, "depthText",    depthTxt);
            AssignRef(extSo, "depthMarker",  markerRt);
            AssignRef(extSo, "missionText",  missionTxt);
            AssignRef(extSo, "warnBanner",   warnBanner);
            AssignRef(extSo, "toastRoot",    toastGo);
            AssignRef(extSo, "toastText",    toastTxt);
            AssignRef(extSo, "interactPrompt", promptGo);
            AssignRef(extSo, "config", diveConfig);
            AssignRef(extSo, "collectConfig", collectConfig);
            extSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Build HUD Panel UI");
            Debug.Log("[HudUIBuilder] HudPanel 생성");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 3. 연구 패널 — GameState.Research 시 표시
        // 레이아웃: 보유 샘플(3등급) / 분석 진행 게이지 / 약품 해금 카드 3종
        // ═══════════════════════════════════════════════════════════════════════
        static void BuildResearchPanel(Transform canvas,
                RunData run, Research research, Collector collector) {
            DestroyExisting(canvas, "ResearchPanel");

            // 황금비 패널(1.618:1) — 화면 중앙, 안전영역 내 고정크기
            var root = MakeGoldenPanel("ResearchPanel", canvas, 1200f);
            // 패널 배경 크롬 스프라이트 적용 — 원본 색이 없으면 단색 폴백
            UITheme.ApplyPanelSprite(root, "panel_bg", new Color(1f, 1f, 1f, 0.04f));
            UITheme.AddShadow(root, 0.6f, 0f, -6f);
            root.AddComponent<Game.UI.PopupPanel>();   // 팝업 스케일+페이드 연출(CanvasGroup 자동)
            root.SetActive(false);

            // 헤더 — header_bar 스프라이트는 BuildPanelHeader 내부에서 적용
            BuildPanelHeader(root.transform, "연구실 — 샘플 분석");

            // 컨텐츠 영역
            var content = UITheme.MakePanel("Content", root.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLG, UITheme.SpaceLG),
                new Vector2(-UITheme.SpaceLG, -56f),
                Color.clear);

            // ── 좌측: 보유 샘플 + 분석 버튼 (40%)──────────────────────────
            var leftCol = UITheme.MakePanel("LeftCol", content.transform,
                new Vector2(0f, 0f), new Vector2(0.4f, 1f),
                new Vector2(0f, 0f), new Vector2(-UITheme.SpaceSM, 0f),
                UITheme.BgPanel);
            ApplyColumnGlass(leftCol);

            // 섹션 타이틀
            MakeColTitle(leftCol.transform, "보유 샘플");

            // 샘플 수량 — 3레벨(오염수준 1/2/3)
            var smp1Txt = MakeSampleRow(leftCol.transform, "Lv.1  오염 저농도", UITheme.DepthCommon,   0.78f);
            var smp2Txt = MakeSampleRow(leftCol.transform, "Lv.2  오염 중농도", UITheme.DepthUncommon, 0.65f);
            var smp3Txt = MakeSampleRow(leftCol.transform, "Lv.3  오염 고농도", UITheme.DepthRare,     0.52f);

            UITheme.MakeDivider("Div", leftCol.transform, UITheme.SpaceMD);

            // 분석 포인트 게이지
            MakeColTitle(leftCol.transform, "누적 분석포인트", 0.39f);
            Image progFill;
            UITheme.MakeGauge("AnalyzeGauge", leftCol.transform,
                new Vector2(-UITheme.SpaceMD * 2f, UITheme.GaugeHeight + 2f),
                new Vector2(0f, 0.27f),
                UITheme.Accent, out progFill);

            // 다음 약품까지 텍스트
            var nextTxt = UITheme.MakeText("NextTxt", leftCol.transform,
                "다음: 약품Ⅰ — 0/3pt", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            nextTxt.fontStyle = FontStyles.Bold;
            nextTxt.overflowMode = TextOverflowModes.Overflow;
            PinAbsCenter(nextTxt.GetComponent<RectTransform>(), 0.19f, 34f, 20f);

            // 분석 버튼 — 황금비 기반 비율(폭 = 높이 × φ³ ≈ 4.236)로 컬럼 하단 중앙 고정
            // 가로 스트레치 바 대신 비례 잡힌 CTA로(요구: 분석 버튼도 황금비)
            float anaH = 68f;
            float anaW = Mathf.Round(anaH * UITheme.Golden * UITheme.Golden * UITheme.Golden);   // ≈ 237
            Button analyzeBtn = UITheme.MakeButton("AnalyzeBtn", leftCol.transform,
                "샘플 분석", UITheme.FontHeading,
                new Vector2(anaW, anaH), Vector2.zero,
                UITheme.Accent, UITheme.BgDeep);
            // sci-fi 버튼 스프라이트(어둡고 중앙 반투명)는 라벨을 가림 → 불투명 솔리드 액센트 배경 유지(가독성 확보)
            AddButtonIcon(analyzeBtn, "icon_research", 20f, UITheme.SpaceMD + 10f);
            UITheme.AddScaleFeedback(analyzeBtn);
            // 하단 중앙 고정(앵커 0.5,0 / 피벗 0.5,0)
            var anaRt = analyzeBtn.GetComponent<RectTransform>();
            anaRt.anchorMin = new Vector2(0.5f, 0f);
            anaRt.anchorMax = new Vector2(0.5f, 0f);
            anaRt.pivot = new Vector2(0.5f, 0f);
            anaRt.sizeDelta = new Vector2(anaW, anaH);
            anaRt.anchoredPosition = new Vector2(0f, UITheme.SpaceMD);

            // ── 우측: 약품 해금 카드 3종 (60%) ─────────────────────────────
            var rightCol = UITheme.MakePanel("RightCol", content.transform,
                new Vector2(0.4f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(0f, 0f),
                Color.clear);

            MakeColTitle(rightCol.transform, "정화제 해금");

            float cardH = 0.27f;
            var card1 = BuildAgentCard(rightCol.transform, "정화제 Ⅰ", "agent_mild",   UITheme.ColInfo,    0.71f, cardH,
                "얕은 수심 오염 정화 · 부유체 Ⅰ 제작 재료", "필요 분석 3pt");
            var card2 = BuildAgentCard(rightCol.transform, "정화제 Ⅱ", "agent_mid",    UITheme.Accent,     0.40f, cardH,
                "중간 수심 오염 정화 · 부유체 Ⅱ 제작 재료", "필요 분석 8pt · 정화제 Ⅰ 선행");
            var card3 = BuildAgentCard(rightCol.transform, "정화제 Ⅲ", "agent_strong", UITheme.ColDanger,  0.09f, cardH,
                "심해 고농도 오염 정화 · 부유체 Ⅲ 제작 재료", "필요 분석 15pt · 고농도 샘플 분석");

            // ResearchPanel 컴포넌트 배선 (기존 ResearchPanel.cs)
            var resComp = root.AddComponent<ResearchPanel>();
            var resSo = new SerializedObject(resComp);
            AssignRef(resSo, "research",     research);
            AssignRef(resSo, "run",          run);
            AssignRef(resSo, "collector",    collector);
            AssignRef(resSo, "analyzeBtn",   analyzeBtn);
            AssignRef(resSo, "progressFill", progFill);
            AssignRef(resSo, "statusText",   nextTxt);
            resSo.ApplyModifiedPropertiesWithoutUndo();

            // ResearchPanelExtUI — 샘플 수량/약품 카드 갱신
            var resExt = root.AddComponent<ResearchPanelExtUI>();
            var resExtSo = new SerializedObject(resExt);
            AssignRef(resExtSo, "run",        run);
            AssignRef(resExtSo, "research",   research);
            AssignRef(resExtSo, "panel",      resComp);
            AssignRef(resExtSo, "smp1Text",   smp1Txt);
            AssignRef(resExtSo, "smp2Text",   smp2Txt);
            AssignRef(resExtSo, "smp3Text",   smp3Txt);
            AssignRef(resExtSo, "nextText",   nextTxt);
            AssignRef(resExtSo, "progFill",   progFill);
            AssignRef(resExtSo, "card1Root",  card1);
            AssignRef(resExtSo, "card2Root",  card2);
            AssignRef(resExtSo, "card3Root",  card3);
            resExtSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Build Research Panel UI");
            Debug.Log("[HudUIBuilder] ResearchPanel 생성");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4. 제작 패널 — GameState.Craft 시 표시
        // 레이아웃: 보유 자원 / 레시피 목록 / 선택 상세 + 제작 버튼
        // ═══════════════════════════════════════════════════════════════════════
        static void BuildCraftPanel(Transform canvas,
                RunData run, Research research, Crafting crafting, Collector collector) {
            DestroyExisting(canvas, "CraftPanel");

            // 황금비 패널(1.618:1) — 화면 중앙, 안전영역 내 고정크기
            var root = MakeGoldenPanel("CraftPanel", canvas, 1300f);
            // 패널 배경 크롬 스프라이트 적용
            UITheme.ApplyPanelSprite(root, "panel_bg", new Color(1f, 1f, 1f, 0.04f));
            UITheme.AddShadow(root, 0.6f, 0f, -6f);
            root.AddComponent<Game.UI.PopupPanel>();   // 팝업 스케일+페이드 연출(CanvasGroup 자동)
            root.SetActive(false);

            // 헤더 — header_bar 스프라이트는 BuildPanelHeader 내부에서 적용
            BuildPanelHeader(root.transform, "제작소 — 장비 제작");

            var content = UITheme.MakePanel("Content", root.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceLG, UITheme.SpaceLG),
                new Vector2(-UITheme.SpaceLG, -56f),
                Color.clear);

            // ── 좌측: 보유 자원 (30%) ────────────────────────────────────────
            var leftCol = UITheme.MakePanel("LeftCol", content.transform,
                new Vector2(0f, 0f), new Vector2(0.3f, 1f),
                new Vector2(0f, 0f), new Vector2(-UITheme.SpaceSM, 0f),
                UITheme.BgPanel);
            ApplyColumnGlass(leftCol);

            MakeColTitle(leftCol.transform, "보유 자원");
            var steel0Txt = MakeResourceRow(leftCol.transform, "일반 강재", UITheme.ColInfo,    0.82f);
            var steel1Txt = MakeResourceRow(leftCol.transform, "합금 강재", UITheme.Accent,     0.71f);
            var steel2Txt = MakeResourceRow(leftCol.transform, "특수 강재", UITheme.ColSuccess, 0.60f);

            UITheme.MakeDivider("Div", leftCol.transform, UITheme.SpaceMD);

            MakeColTitle(leftCol.transform, "해금 약품", 0.3f);
            var agentTxt = UITheme.MakeText("AgentStatus", leftCol.transform,
                "—", UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Left);
            var agentRt = agentTxt.GetComponent<RectTransform>();
            agentRt.anchorMin = new Vector2(0f, 0.12f);
            agentRt.anchorMax = new Vector2(1f, 0.28f);
            agentRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            agentRt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);
            agentTxt.enableWordWrapping = true;

            // ── 중앙: 레시피 목록 — 긴 부유체 이름/재료가 겹치지 않도록 폭 확보
            var midCol = UITheme.MakePanel("MidCol", content.transform,
                new Vector2(0.3f, 0f), new Vector2(0.68f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(-UITheme.SpaceSM, 0f),
                UITheme.BgPanel);
            ApplyColumnGlass(midCol);

            MakeColTitle(midCol.transform, "레시피");

            // 레시피 목록 컨테이너 — VerticalLayoutGroup으로 행 자동 배치(텍스트 길이 변동 대응)
            var recipeList = UITheme.MakePanel("RecipeList", midCol.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, UITheme.SpaceSM),
                new Vector2(-UITheme.SpaceSM, -88f),
                Color.clear);
            UITheme.AddVerticalLayout(recipeList, UITheme.SpaceSM, UITheme.SpaceXS, true, false);

            // 레시피 행 6종 (부유체 3 + 업그레이드 3)
            string[] recipeIds    = { "buoy_1", "buoy_2", "buoy_3", "up_cargo", "up_battery", "gear_hull" };
            string[] recipeLabels = { "정화 부유체 Ⅰ", "정화 부유체 Ⅱ", "정화 부유체 Ⅲ",
                                      "적재 확장", "배터리 증설", "내압 프레임" };
            string[] recipeReqs   = { "일반강재 24kg·약품Ⅰ", "합금강재 30kg·약품Ⅱ", "특수강재 40kg·약품Ⅲ",
                                      "일반강재 16kg", "합금강재 18kg", "특수강재 30kg" };
            var recipeBtns = new Button[6];
            for (int i = 0; i < 6; i++) {
                recipeBtns[i] = BuildRecipeRow(recipeList.transform,
                    recipeLabels[i], recipeReqs[i], i);
            }

            // ── 우측: 선택 상세 + 제작 버튼 — 상세 텍스트는 줄바꿈 전제로 배치
            var rightCol = UITheme.MakePanel("RightCol", content.transform,
                new Vector2(0.68f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(0f, 0f),
                UITheme.BgPanel);
            ApplyColumnGlass(rightCol);

            MakeColTitle(rightCol.transform, "제작 상세");

            var detailName = UITheme.MakeText("DetailName", rightCol.transform,
                "항목을 선택하세요", UITheme.FontBody + 2f, UITheme.Accent, TextAlignmentOptions.Center);
            var detailNameRt = detailName.GetComponent<RectTransform>();
            detailNameRt.anchorMin = new Vector2(0f, 0.68f);
            detailNameRt.anchorMax = new Vector2(1f, 0.84f);
            detailNameRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            detailNameRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            detailName.enableWordWrapping = true;
            detailName.overflowMode = TextOverflowModes.Overflow;
            detailName.fontStyle = FontStyles.Bold;

            var detailReq = UITheme.MakeText("DetailReq", rightCol.transform,
                "", UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.Center);
            var detailReqRt = detailReq.GetComponent<RectTransform>();
            detailReqRt.anchorMin = new Vector2(0f, 0.50f);
            detailReqRt.anchorMax = new Vector2(1f, 0.68f);
            detailReqRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            detailReqRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            detailReq.enableWordWrapping = true;
            detailReq.overflowMode = TextOverflowModes.Overflow;

            var detailStatus = UITheme.MakeText("DetailStatus", rightCol.transform,
                "", UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center);
            var detailStatusRt = detailStatus.GetComponent<RectTransform>();
            detailStatusRt.anchorMin = new Vector2(0f, 0.38f);
            detailStatusRt.anchorMax = new Vector2(1f, 0.50f);
            detailStatusRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            detailStatusRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            detailStatus.overflowMode = TextOverflowModes.Overflow;

            // 제작 버튼 — 불투명 솔리드 액센트 배경(어둡고 반투명한 sci-fi 스프라이트는 라벨을 가려 미적용)
            Button craftExecBtn = UITheme.MakeButton("CraftExecBtn", rightCol.transform,
                "제작", UITheme.FontHeading,
                Vector2.zero, Vector2.zero,
                UITheme.Accent, UITheme.BgDeep);
            AddButtonIcon(craftExecBtn, "icon_craft", 24f, UITheme.SpaceLG + 12f);
            UITheme.AddScaleFeedback(craftExecBtn);
            // 제작 버튼 — 황금비 기반 비율(폭 = 높이 × φ³)로 하단 중앙 고정(요구: 제작 버튼도 황금비)
            float crH = 56f;
            float crW = Mathf.Round(crH * UITheme.Golden * UITheme.Golden * UITheme.Golden);   // ≈ 254
            var crRt = craftExecBtn.GetComponent<RectTransform>();
            crRt.anchorMin = new Vector2(0.5f, 0.08f);
            crRt.anchorMax = new Vector2(0.5f, 0.08f);
            crRt.pivot = new Vector2(0.5f, 0f);
            crRt.sizeDelta = new Vector2(crW, crH);
            crRt.anchoredPosition = new Vector2(0f, UITheme.SpaceLG);
            craftExecBtn.interactable = false;

            // 상태 텍스트 (기존 CraftPanel.cs가 사용)
            var statusTxt = UITheme.MakeText("StatusText", rightCol.transform,
                "", UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.Center);
            var statusRt = statusTxt.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0.08f);
            statusRt.offsetMin = new Vector2(UITheme.SpaceMD, UITheme.SpaceXS);
            statusRt.offsetMax = new Vector2(-UITheme.SpaceMD, -UITheme.SpaceXS);
            statusTxt.enableWordWrapping = true;

            // CraftPanel 컴포넌트 배선 (기존 CraftPanel.cs — buoyBtn/upgradeBtn 2종만 배선)
            var craftComp = root.AddComponent<CraftPanel>();
            var craftSo = new SerializedObject(craftComp);
            AssignRef(craftSo, "crafting",   crafting);
            AssignRef(craftSo, "research",   research);
            AssignRef(craftSo, "run",        run);
            AssignRef(craftSo, "collector",  collector);
            AssignRef(craftSo, "buoyBtn",    recipeBtns[0]);   // buoy_1 버튼을 기존 buoyBtn으로
            AssignRef(craftSo, "upgradeBtn", recipeBtns[3]);   // up_cargo 버튼을 기존 upgradeBtn으로
            AssignRef(craftSo, "statusText", statusTxt);
            craftSo.ApplyModifiedPropertiesWithoutUndo();

            // CraftPanelExtUI — 레시피 선택 UI / 자원 표시 / 제작 버튼
            var craftExt = root.AddComponent<CraftPanelExtUI>();
            var craftExtSo = new SerializedObject(craftExt);
            craftExtSo.Update();
            AssignRef(craftExtSo, "run",          run);
            AssignRef(craftExtSo, "crafting",     crafting);
            AssignRef(craftExtSo, "research",     research);
            AssignRef(craftExtSo, "collector",    collector);
            AssignRef(craftExtSo, "steel0Text",   steel0Txt);
            AssignRef(craftExtSo, "steel1Text",   steel1Txt);
            AssignRef(craftExtSo, "steel2Text",   steel2Txt);
            AssignRef(craftExtSo, "agentText",    agentTxt);
            AssignRef(craftExtSo, "detailName",   detailName);
            AssignRef(craftExtSo, "detailReq",    detailReq);
            AssignRef(craftExtSo, "detailStatus", detailStatus);
            AssignRef(craftExtSo, "craftExecBtn", craftExecBtn);
            // 레시피 버튼 배열 — 동일 SO 에서 연속 설정
            var rp = craftExtSo.FindProperty("recipeBtns");
            if (rp != null) {
                rp.arraySize = recipeBtns.Length;
                for (int i = 0; i < recipeBtns.Length; i++) {
                    rp.GetArrayElementAtIndex(i).objectReferenceValue = recipeBtns[i];
                }
            }
            var ri = craftExtSo.FindProperty("recipeIds");
            if (ri != null) {
                ri.arraySize = recipeIds.Length;
                for (int i = 0; i < recipeIds.Length; i++) {
                    ri.GetArrayElementAtIndex(i).stringValue = recipeIds[i];
                }
            }
            var rl = craftExtSo.FindProperty("recipeLabels");
            if (rl != null) {
                rl.arraySize = recipeLabels.Length;
                for (int i = 0; i < recipeLabels.Length; i++) {
                    rl.GetArrayElementAtIndex(i).stringValue = recipeLabels[i];
                }
            }
            var rreq = craftExtSo.FindProperty("recipeReqs");
            if (rreq != null) {
                rreq.arraySize = recipeReqs.Length;
                for (int i = 0; i < recipeReqs.Length; i++) {
                    rreq.GetArrayElementAtIndex(i).stringValue = recipeReqs[i];
                }
            }
            craftExtSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Build Craft Panel UI");
            Debug.Log("[HudUIBuilder] CraftPanel 생성");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 5. 오염체 수집 미니게임 (스타포스 타이밍바) — Collector가 표시 토글
        // varco 생성 에셋 배치 + 텍스트는 UITheme 토큰(기존 UI와 톤 일관)
        // ═══════════════════════════════════════════════════════════════════════
        static void BuildMinigamePanel(Transform canvas, Collector collector) {
            DestroyExisting(canvas, "MinigameRoot");

            // 항상 활성 루트 — CollectMinigame.Update 유지(표시 토글은 자식 panel)
            var mgRoot = new GameObject("MinigameRoot");
            mgRoot.transform.SetParent(canvas, false);
            var mgRootRt = mgRoot.AddComponent<RectTransform>();
            mgRootRt.anchorMin = Vector2.zero;
            mgRootRt.anchorMax = Vector2.one;
            mgRootRt.offsetMin = Vector2.zero;
            mgRootRt.offsetMax = Vector2.zero;

            // 딤 배경(클릭 차단) + 팝업 연출 — 모달 집중(ugui 권장)
            var panel = UITheme.MakeStretchPanel("MinigamePanel", mgRoot.transform,
                color: new Color(0.02f, 0.05f, 0.07f, 0.6f));
            panel.AddComponent<Game.UI.PopupPanel>();

            // 중앙 박스 — varco 패널 배경(황금비)
            var box = MakeGoldenPanel("MinigameBox", panel.transform, 820f);
            var boxImg = box.GetComponent<Image>();
            var panelSprite = LoadMinigameSprite("minigame_panel");
            if (panelSprite != null) {
                boxImg.sprite = panelSprite;
                boxImg.type = Image.Type.Sliced;
                boxImg.color = Color.white;
            }
            UITheme.AddShadow(box, 0.6f, 0f, -6f);

            // 타이틀/안내 — UITheme 토큰으로 기존 UI와 톤 통일
            var title = UITheme.MakeText("MgTitle", box.transform, "오염체 정제",
                UITheme.FontTitle, UITheme.Accent, TextAlignmentOptions.Center);
            title.fontStyle = UITheme.HeadingStyle;
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(0f, -64f);
            titleRt.offsetMax = new Vector2(0f, -16f);

            var hint = UITheme.MakeText("MgHint", box.transform, "커서가 초록 구간에 올 때 E!",
                UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center);
            var hintRt = hint.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.offsetMin = new Vector2(0f, -104f);
            hintRt.offsetMax = new Vector2(0f, -68f);

            // 트랙(중앙) — varco track 스프라이트(9-slice)
            var track = new GameObject("MgTrack");
            track.transform.SetParent(box.transform, false);
            var trackRt = track.AddComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.5f, 0.5f);
            trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(640f, 48f);
            trackRt.anchoredPosition = new Vector2(0f, -20f);
            var trackImg = track.AddComponent<Image>();
            var trackSprite = LoadMinigameSprite("minigame_track");
            if (trackSprite != null) {
                trackImg.sprite = trackSprite;
                trackImg.type = Image.Type.Sliced;
            } else {
                trackImg.color = UITheme.BgDeep;
            }

            // 타겟존(트랙 자식) — CollectMinigame이 위치/폭을 동적 설정
            var target = new GameObject("MgTarget");
            target.transform.SetParent(track.transform, false);
            var targetRt = target.AddComponent<RectTransform>();
            targetRt.anchorMin = new Vector2(0f, 0.5f);
            targetRt.anchorMax = new Vector2(0f, 0.5f);
            targetRt.sizeDelta = new Vector2(120f, 56f);
            var targetImg = target.AddComponent<Image>();
            var targetSprite = LoadMinigameSprite("minigame_target");
            if (targetSprite != null) {
                targetImg.sprite = targetSprite;
                targetImg.type = Image.Type.Sliced;
            } else {
                targetImg.color = UITheme.ColSuccess;
            }
            targetImg.raycastTarget = false;

            // 커서(트랙 자식) — 좌우 왕복
            var cursor = new GameObject("MgCursor");
            cursor.transform.SetParent(track.transform, false);
            var cursorRt = cursor.AddComponent<RectTransform>();
            cursorRt.anchorMin = new Vector2(0f, 0.5f);
            cursorRt.anchorMax = new Vector2(0f, 0.5f);
            cursorRt.sizeDelta = new Vector2(28f, 72f);
            var cursorImg = cursor.AddComponent<Image>();
            var cursorSprite = LoadMinigameSprite("minigame_cursor");
            if (cursorSprite != null) {
                cursorImg.sprite = cursorSprite;
                cursorImg.preserveAspect = true;
            } else {
                cursorImg.color = UITheme.Accent;
            }
            cursorImg.raycastTarget = false;

            // CollectMinigame 부착(활성 루트) + 참조 배선
            var mg = mgRoot.AddComponent<Game.UI.CollectMinigame>();
            var so = new SerializedObject(mg);
            AssignRef(so, "panel",  panel);
            AssignRef(so, "track",  trackRt);
            AssignRef(so, "cursor", cursorRt);
            AssignRef(so, "target", targetRt);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Collector에 미니게임 배선 — E 수집 시 미니게임 경유
            if (collector != null) {
                var cso = new SerializedObject(collector);
                AssignRef(cso, "minigame", mg);
                cso.ApplyModifiedPropertiesWithoutUndo();
            }

            panel.SetActive(false);   // 기본 숨김
            Undo.RegisterCreatedObjectUndo(mgRoot, "Build Minigame UI");
            Debug.Log("[HudUIBuilder] 수집 미니게임 패널 생성");
        }

        // 미니게임 전용 스프라이트 로드(4.Art/UI/minigame)
        static Sprite LoadMinigameSprite(string name) {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/4.Art/UI/minigame/{name}.png");
        }

        // ── UI 부품 헬퍼 ───────────────────────────────────────────────────────

        // 패널 상단 타이틀 헤더 바 — header_bar 스프라이트로 크롬 통일
        static void BuildPanelHeader(Transform parent, string title) {
            var header = UITheme.MakePanel("Header", parent,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -56f), new Vector2(0f, 0f),
                UITheme.BgHeader);
            // header_bar 스프라이트 — 보더 없으면 단색 BgHeader 유지(폴백 자동)
            UITheme.ApplyPanelSprite(header, "header_bar", new Color(1f, 1f, 1f, 0.08f));
            UITheme.AddShadow(header, 0.5f, 0f, -2f);
            // 헤더 하단 액센트 언더라인(이전엔 패널 정중앙에 잘못 배치되던 버그 수정)
            UITheme.MakeAccentBar("HeaderUnderline", header.transform, 0f, 2f, 0f, UITheme.Accent);
            // ■ 기호 제거 — 좌측 세로 아쿠아 액센트바로 대체
            MakeVAccentBar("HeaderAccent", header.transform, UITheme.SpaceLG, 30f);
            var t = UITheme.MakeText("HeaderTitle", header.transform,
                title, UITheme.FontTitle, UITheme.TextPrimary, TextAlignmentOptions.Left);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(UITheme.SpaceLG + 16f, 0f);
            rt.offsetMax = new Vector2(-UITheme.SpaceLG - 64f, 0f);
            t.fontStyle = UITheme.HeadingStyle;
            t.overflowMode = TextOverflowModes.Overflow;   // 56px 헤더에 40px 타이틀이 세로로 잘려 "…" 뜨던 것 방지(가로는 충분)

            // 우상단 닫기 버튼 — 폰트 글리프 안전한 "X" 사용(✕ 깨짐 회피), 스케일 피드백
            var closeBtn = UITheme.MakeButton("CloseBtn", header.transform, "X", UITheme.FontHeading,
                new Vector2(40f, 40f), Vector2.zero, UITheme.BgBorder, UITheme.TextPrimary);
            var cbRt = closeBtn.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(1f, 0.5f);
            cbRt.anchorMax = new Vector2(1f, 0.5f);
            cbRt.sizeDelta = new Vector2(40f, 40f);
            cbRt.anchoredPosition = new Vector2(-UITheme.SpaceLG - 20f, 0f);
            UITheme.AddScaleFeedback(closeBtn);
            closeBtn.gameObject.AddComponent<Game.UI.HubCloseButton>();
        }

        // 컬럼 타이틀 텍스트(anchorY 중심) — 큰 섹션 제목으로 가독성 확보
        static void MakeColTitle(Transform parent, string label, float anchorYCenter = 0.9f) {
            // 좌측 4px 세로 액센트 바(타이틀 높이만큼)
            var bar = new GameObject("ColAccent_" + label);
            bar.transform.SetParent(parent, false);
            var barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, anchorYCenter - 0.022f);
            barRt.anchorMax = new Vector2(0f, anchorYCenter + 0.022f);
            barRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            barRt.offsetMax = new Vector2(UITheme.SpaceMD + 4f, 0f);
            var barImg = bar.AddComponent<Image>();
            barImg.color = UITheme.Accent;
            barImg.raycastTarget = false;
            UITheme.ApplyRound(barImg, 4f);

            var t = UITheme.MakeText("Title_" + label, parent,
                label, UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.Left);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, anchorYCenter - 0.03f);
            rt.anchorMax = new Vector2(1f, anchorYCenter + 0.03f);
            rt.offsetMin = new Vector2(UITheme.SpaceMD + 12f, 0f);
            rt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);
            t.fontStyle = FontStyles.Bold;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        // 샘플 보유 행 — 레이블 + 개수 텍스트 반환
        static TMP_Text MakeSampleRow(Transform parent, string label, Color dotColor, float anchorYCenter) {
            // 도트 색상 인디케이터
            var dot = new GameObject("Dot_" + label);
            dot.transform.SetParent(parent, false);
            var dotRt = dot.AddComponent<RectTransform>();
            float h = 0.052f;
            dotRt.anchorMin = new Vector2(0f, anchorYCenter - h);
            dotRt.anchorMax = new Vector2(0f, anchorYCenter + h);
            dotRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            dotRt.offsetMax = new Vector2(UITheme.SpaceMD + 8f, 0f);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = dotColor;

            var lbl = UITheme.MakeText("Label_" + label, parent,
                label, UITheme.FontBody, UITheme.TextPrimary);
            var lblRt = lbl.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, anchorYCenter - h);
            lblRt.anchorMax = new Vector2(0.7f, anchorYCenter + h);
            lblRt.offsetMin = new Vector2(UITheme.SpaceMD + 14f, 0f);
            lblRt.offsetMax = new Vector2(0f, 0f);

            var cnt = UITheme.MakeText("Count_" + label, parent,
                "0", UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Right);
            var cntRt = cnt.GetComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(0.7f, anchorYCenter - h);
            cntRt.anchorMax = new Vector2(1f, anchorYCenter + h);
            cntRt.offsetMin = new Vector2(0f, 0f);
            cntRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            cnt.fontStyle = FontStyles.Bold;
            cnt.overflowMode = TextOverflowModes.Overflow;
            return cnt;
        }

        // 자원 보유 행 (제작 패널용)
        static TMP_Text MakeResourceRow(Transform parent, string label, Color accent, float anchorYCenter) {
            var lbl = UITheme.MakeText("Lbl_" + label, parent,
                label, UITheme.FontBody, UITheme.TextPrimary);
            var lblRt = lbl.GetComponent<RectTransform>();
            float h = 0.052f;
            lblRt.anchorMin = new Vector2(0f, anchorYCenter - h);
            lblRt.anchorMax = new Vector2(0.55f, anchorYCenter + h);
            lblRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            lblRt.offsetMax = new Vector2(0f, 0f);
            lbl.color = accent;

            var cnt = UITheme.MakeText("Val_" + label, parent,
                "0 kg", UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Right);
            var cntRt = cnt.GetComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(0.55f, anchorYCenter - h);
            cntRt.anchorMax = new Vector2(1f, anchorYCenter + h);
            cntRt.offsetMin = new Vector2(0f, 0f);
            cntRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            cnt.fontStyle = FontStyles.Bold;
            cnt.overflowMode = TextOverflowModes.Overflow;
            return cnt;
        }

        // 약품 해금 카드 — 좌측 액센트 바 + 이름/배지(상단) + 설명/요구(하단)로 카드 전체를 채움
        static GameObject BuildAgentCard(Transform parent, string label, string id,
                Color accentColor, float anchorYBottom, float anchorHeight,
                string desc, string req) {
            var card = UITheme.MakePanel("AgentCard_" + id, parent,
                new Vector2(0f, anchorYBottom),
                new Vector2(1f, anchorYBottom + anchorHeight),
                new Vector2(UITheme.SpaceXS, UITheme.SpaceXS),
                new Vector2(-UITheme.SpaceXS, -UITheme.SpaceXS),
                UITheme.BgPanel);
            UITheme.AddShadow(card, 0.4f, 0f, -2f);

            // 좌측 색상 바
            var bar = new GameObject("AccentBar");
            bar.transform.SetParent(card.transform, false);
            var barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(0f, 1f);
            barRt.offsetMin = new Vector2(UITheme.SpaceXS, UITheme.SpaceXS);
            barRt.offsetMax = new Vector2(UITheme.SpaceXS + 5f, -UITheme.SpaceXS);
            var barImg = bar.AddComponent<Image>();
            barImg.color = accentColor;
            UITheme.ApplyRound(barImg, 4f);

            // 약품명(상단 좌)
            var nameTxt = UITheme.MakeText("Name", card.transform,
                label, UITheme.FontHeading, UITheme.TextPrimary);
            var nameRt = nameTxt.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.58f);
            nameRt.anchorMax = new Vector2(0.7f, 1f);
            nameRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            nameRt.offsetMax = new Vector2(0f, -UITheme.SpaceSM);
            nameTxt.fontStyle = FontStyles.Bold;

            // 상태 배지(상단 우)
            var badgeTxt = UITheme.MakeText("Badge", card.transform,
                "잠금", UITheme.FontBody, UITheme.TextDisabled, TextAlignmentOptions.Right);
            var badgeRt = badgeTxt.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.6f, 0.58f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.offsetMin = new Vector2(0f, 0f);
            badgeRt.offsetMax = new Vector2(-UITheme.SpaceMD, -UITheme.SpaceSM);
            badgeTxt.fontStyle = FontStyles.Bold;

            // 설명(중단) — 카드 하단 공간을 채움
            var descTxt = UITheme.MakeText("Desc", card.transform,
                desc, UITheme.FontBody, UITheme.TextSecondary);
            var descRt = descTxt.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0.3f);
            descRt.anchorMax = new Vector2(1f, 0.56f);
            descRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            descRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            descTxt.enableWordWrapping = true;
            descTxt.overflowMode = TextOverflowModes.Overflow;

            // 요구 조건(하단)
            var reqTxt = UITheme.MakeText("Req", card.transform,
                req, UITheme.FontBody, accentColor);
            var reqRt = reqTxt.GetComponent<RectTransform>();
            reqRt.anchorMin = new Vector2(0f, 0.06f);
            reqRt.anchorMax = new Vector2(1f, 0.28f);
            reqRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            reqRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            reqTxt.overflowMode = TextOverflowModes.Overflow;

            // id 텍스트(숨김 — ExtUI가 참조용으로 id 식별)
            var idTxt = UITheme.MakeText("AgentId", card.transform,
                id, UITheme.FontCaption, Color.clear);
            idTxt.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            return card;
        }

        // 레시피 행 버튼 — VerticalLayoutGroup 자식. LayoutElement로 높이 고정, 너비는 그룹이 stretch
        static Button BuildRecipeRow(Transform parent, string name, string req, int idx) {
            var btn = UITheme.MakeButton("Recipe_" + idx, parent,
                "", UITheme.FontBody, Vector2.zero, Vector2.zero,
                UITheme.BgBorder, UITheme.TextPrimary);
            // 레이아웃 그룹이 크기를 제어하므로 LayoutElement로 선호 높이만 지정
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 82f;
            le.preferredHeight = 82f;
            UITheme.AddShadow(btn.gameObject, 0.35f, 0f, -2f);
            UITheme.AddScaleFeedback(btn);

            // 좌측 액센트 바 — 부유체(0~2)는 청록, 업그레이드(3~5)는 성공색으로 구분
            var bar = new GameObject("AccentBar");
            bar.transform.SetParent(btn.transform, false);
            var barRt = bar.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0.18f);
            barRt.anchorMax = new Vector2(0f, 0.82f);
            barRt.offsetMin = new Vector2(UITheme.SpaceSM, 0f);
            barRt.offsetMax = new Vector2(UITheme.SpaceSM + 4f, 0f);
            var barImg = bar.AddComponent<Image>();
            barImg.color = idx < 3 ? UITheme.Accent : UITheme.ColSuccess;
            UITheme.ApplyRound(barImg, 4f);

            // 레이블 텍스트(버튼 자식의 Label은 이미 있음)
            var lbl = btn.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (lbl != null) {
                lbl.text = name;
                lbl.alignment = TextAlignmentOptions.Left;
                lbl.fontSize = UITheme.FontBody + 1f;
                lbl.fontStyle = FontStyles.Bold;
                lbl.overflowMode = TextOverflowModes.Overflow;
                var lrt = lbl.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0.48f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(UITheme.SpaceMD + 10f, -UITheme.SpaceXS);
                lrt.offsetMax = new Vector2(-UITheme.SpaceSM, -UITheme.SpaceXS);
            }

            // 소재 요구 텍스트 — 이름 아래 한 줄로 분리해 글자 겹침 방지
            var reqTxt = UITheme.MakeText("Req", btn.transform,
                req, UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.Left);
            var reqRt = reqTxt.GetComponent<RectTransform>();
            reqRt.anchorMin = new Vector2(0f, 0f);
            reqRt.anchorMax = new Vector2(1f, 0.48f);
            reqRt.offsetMin = new Vector2(UITheme.SpaceMD + 10f, UITheme.SpaceXS);
            reqRt.offsetMax = new Vector2(-UITheme.SpaceSM, -UITheme.SpaceXS);
            reqTxt.enableWordWrapping = true;
            reqTxt.overflowMode = TextOverflowModes.Overflow;
            return btn;
        }

        // 수심 밴드 세그먼트(세로 색상 바)
        static void BuildDepthSegment(string name, Transform parent,
                float anchorY1, float anchorY2, Color color) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, anchorY1);
            rt.anchorMax = new Vector2(1f, anchorY2);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, 0.85f);   // 불투명도 상향(가독성)
            img.raycastTarget = false;   // 장식 — 레이캐스트 제외
        }

        // 수심 밴드 레이블 (우측) — 밴드 너비 확장 대응, 한글 라벨 가독성
        static void BuildDepthLabel(string name, Transform parent,
                float anchorY1, float anchorY2, string label, Color color) {
            var t = UITheme.MakeText(name, parent,
                label, UITheme.FontCaption, color, TextAlignmentOptions.Right);
            t.fontStyle = FontStyles.Bold;
            var rt = t.GetComponent<RectTransform>();
            // 밴드가 우측으로 이동 — 레이블은 밴드 왼쪽(화면 안쪽)에 우측정렬로 배치
            rt.anchorMin = new Vector2(0f, (anchorY1 + anchorY2) * 0.5f - 0.06f);
            rt.anchorMax = new Vector2(0f, (anchorY1 + anchorY2) * 0.5f + 0.06f);
            rt.offsetMin = new Vector2(-UITheme.SpaceSM - 56f, 0f);
            rt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);
        }

        // 현재 수심 가로 마커 — 실제 라인보다 넓은 반투명 층을 겹쳐 형광 발광처럼 보이게 함
        static void BuildDepthMarkerGlow(Transform parent) {
            MakeMarkerLayer("GlowOuter", parent, 30f, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.18f), 8f);
            MakeMarkerLayer("GlowInner", parent, 22f, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.35f), 5f);
            MakeMarkerLayer("Core", parent, 14f, UITheme.Accent, 3f);
        }

        static void MakeMarkerLayer(string name, Transform parent, float height, Color color, float round) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(0f, -height * 0.5f);
            rt.offsetMax = new Vector2(0f, height * 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            UITheme.ApplyRound(img, round);
        }

        // ── 레이아웃 유틸 ─────────────────────────────────────────────────────

        // HUD 코너 글래스 블록 — corner: 0=좌상 1=우상 2=좌하 3=우하. 안전여백·고정크기로 비겹침 보장
        static GameObject MakeCornerBlock(string name, Transform parent, int corner) {
            bool right  = corner == 1 || corner == 3;
            bool bottom = corner == 2 || corner == 3;
            float ax = right ? 1f : 0f;
            float ay = bottom ? 0f : 1f;
            Vector2 offMin, offMax;
            if (!right && !bottom) {            // 좌상
                offMin = new Vector2(HUD_SAFE, -HUD_SAFE - HUD_BLOCK_H);
                offMax = new Vector2(HUD_SAFE + HUD_BLOCK_W, -HUD_SAFE);
            } else if (right && !bottom) {      // 우상
                offMin = new Vector2(-HUD_SAFE - HUD_BLOCK_W, -HUD_SAFE - HUD_BLOCK_H);
                offMax = new Vector2(-HUD_SAFE, -HUD_SAFE);
            } else if (!right && bottom) {      // 좌하
                offMin = new Vector2(HUD_SAFE, HUD_SAFE);
                offMax = new Vector2(HUD_SAFE + HUD_BLOCK_W, HUD_SAFE + HUD_BLOCK_H);
            } else {                            // 우하
                offMin = new Vector2(-HUD_SAFE - HUD_BLOCK_W, HUD_SAFE);
                offMax = new Vector2(-HUD_SAFE, HUD_SAFE + HUD_BLOCK_H);
            }
            // RiseIn(수면 부상 모션)은 런타임 전용(편집 모드에서 alpha=0 베이크 방지) — 빌더에선 호출 안 함
            var block = UITheme.MakeGlassPanel(name, parent,
                new Vector2(ax, ay), new Vector2(ax, ay), offMin, offMax);
            return block;
        }

        // 컬럼 패널에 글래스 깊이감 부여 — 라운드 코너 + 1px 아쿠아 보더 + 상단 미세 하이라이트
        // 평평한 BgPanel 단색 컬럼을 카드처럼 떠 보이게 함(제작/연구 3열 공통)
        static void ApplyColumnGlass(GameObject col) {
            if (col == null) {
                return;
            }
            var img = col.GetComponent<Image>();
            if (img != null) {
                UITheme.ApplyRound(img);   // 라운드 코너(9슬라이스)
            }
            // 1px 아쿠아 보더
            var ol = col.AddComponent<Outline>();
            ol.effectColor = UITheme.GlassBorder;
            ol.effectDistance = new Vector2(1f, 1f);
            // 상단 미세 하이라이트 바(장식 — 레이캐스트 제외)
            var hi = UITheme.MakeAccentBar(col.name + "_Hi", col.transform, 1f, 2f, UITheme.SpaceSM, UITheme.GlassHighlight);
            var hiImg = hi.GetComponent<Image>();
            if (hiImg != null) {
                hiImg.raycastTarget = false;
            }
        }

        // 황금비 팝업 패널 — 화면 중앙 고정크기(가로:세로=1.618), 안전영역 내. longSide=가로
        static GameObject MakeGoldenPanel(string name, Transform canvas, float longSide) {
            Vector2 size = UITheme.GoldenSize(longSide, widthIsLong: true);
            var panel = UITheme.MakePanel(name, canvas,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, UITheme.BgDeep);
            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return panel;
        }

        // 세로 액센트바 — 부모 좌측 세로중앙, 타이틀 ■ 기호 대체용(4px 폭)
        static GameObject MakeVAccentBar(string name, Transform parent, float fromLeft, float height) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(fromLeft, -height * 0.5f);
            rt.offsetMax = new Vector2(fromLeft + 4f, height * 0.5f);
            var img = go.AddComponent<Image>();
            img.color = UITheme.Accent;
            img.raycastTarget = false;   // 장식 — 레이캐스트 제외
            UITheme.ApplyRound(img, 4f);
            return go;
        }

        static TMP_Text MakeStretchText(string name, Transform parent, string content,
                float fontSize, Color color,
                Vector2 anchorMin, Vector2 anchorMax,
                Vector2 offsetMin, Vector2 offsetMax,
                TextAlignmentOptions align = TextAlignmentOptions.Left) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            UITheme.ApplyFont(t);
            return t;
        }

        // 좌상단 고정 (anchoredPos from top-left corner)
        static void PinTopLeft(RectTransform rt, float x, float fromTop, float w, float h) {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(x, -fromTop - h);
            rt.offsetMax = new Vector2(x + w, -fromTop);
        }

        // 우상단 고정
        static void PinTopRight(RectTransform rt, float fromRight, float fromTop, float w, float h) {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(-fromRight - w, -fromTop - h);
            rt.offsetMax = new Vector2(-fromRight, -fromTop);
        }

        // 앵커 비율 기준 수직 중심 고정
        static void PinAbsCenter(RectTransform rt, float anchorY, float h, float padH) {
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.offsetMin = new Vector2(padH, -h * 0.5f);
            rt.offsetMax = new Vector2(-padH, h * 0.5f);
        }

        // 앵커 스트레치 + 절대 패딩
        static void SetAnchor(RectTransform rt,
                Vector2 anchorMin, Vector2 anchorMax,
                Vector2 offsetMin, Vector2 offsetMax) {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void SetAnchorStretch(RectTransform rt,
                float anchorYMin, float anchorYMax,
                float padLeft, float padBottom, float padRight, float padTop) {
            rt.anchorMin = new Vector2(0f, anchorYMin);
            rt.anchorMax = new Vector2(1f, anchorYMax);
            rt.offsetMin = new Vector2(padLeft, padBottom);
            rt.offsetMax = new Vector2(-padRight, -padTop);
        }

        // ── 라운드 스프라이트 생성 ────────────────────────────────────────────

        // 9슬라이스용 라운드 사각형 스프라이트를 1회 생성(없으면) 후 로드
        static Sprite EnsureRoundedSprite() {
            try {
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ROUND_SPRITE_PATH);
                if (existing != null) {
                    return existing;
                }
                const int S = 64;
                const int radius = 22;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                for (int y = 0; y < S; y++) {
                    for (int x = 0; x < S; x++) {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, RoundedAlpha(x, y, S, radius)));
                    }
                }
                tex.Apply();
                var dir = System.IO.Path.GetDirectoryName(ROUND_SPRITE_PATH);
                if (!System.IO.Directory.Exists(dir)) {
                    System.IO.Directory.CreateDirectory(dir);
                }
                System.IO.File.WriteAllBytes(ROUND_SPRITE_PATH, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(ROUND_SPRITE_PATH, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(ROUND_SPRITE_PATH);
                if (imp != null) {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spriteBorder = new Vector4(radius, radius, radius, radius);
                    imp.filterMode = FilterMode.Bilinear;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.SaveAndReimport();
                }
                Debug.Log("[HudUIBuilder] 라운드 스프라이트 생성: " + ROUND_SPRITE_PATH);
                return AssetDatabase.LoadAssetAtPath<Sprite>(ROUND_SPRITE_PATH);
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 라운드 스프라이트 생성 실패: {e.Message}");
                return null;
            }
        }

        // 라운드 사각형 안티에일리어싱 알파 — 모서리 원호 거리(1px 페더)
        static float RoundedAlpha(int x, int y, int size, float radius) {
            float px = x + 0.5f, py = y + 0.5f;
            float cx = Mathf.Clamp(px, radius, size - radius);
            float cy = Mathf.Clamp(py, radius, size - radius);
            float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(radius - d + 0.5f);
        }

        // ── 에셋·컴포넌트 검색 ────────────────────────────────────────────────

        static T FindAsset<T>(string name) where T : UnityEngine.Object {
            try {
                var guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
                if (guids.Length == 0) {
                    // 이름 불일치(예: RunData→Run_Default) 시 타입만으로 폴백
                    guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
                }
                if (guids.Length == 0) {
                    Debug.LogWarning($"[HudUIBuilder] {typeof(T).Name} '{name}' 에셋을 찾지 못했습니다.");
                    return null;
                }
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 에셋 검색 오류: {e.Message}");
                return null;
            }
        }

        static T FindComponent<T>() where T : Component {
            try {
                var obj = Object.FindFirstObjectByType<T>();
                if (obj == null) {
                    Debug.LogWarning($"[HudUIBuilder] {typeof(T).Name} 씬에서 찾지 못했습니다 — 수동 배선 필요.");
                }
                return obj;
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 컴포넌트 검색 오류: {e.Message}");
                return null;
            }
        }

        // 런타임 폰트 바인더 배선 — UIThemeFontBinder GO를 멱등 생성하고 koreanFont에 IyagiGGC 배선
        // 런타임(플레이) 생성 텍스트가 □로 깨지지 않도록 UITheme.UIFont를 Awake 시점에 주입한다
        static void WireFontBinder() {
            try {
                var existing = Object.FindFirstObjectByType<Game.UI.UIThemeFontBinder>();
                GameObject go;
                if (existing != null) {
                    go = existing.gameObject;   // 멱등 — 이미 있으면 재사용
                } else {
                    go = new GameObject("UIThemeFontBinder");
                    existing = go.AddComponent<Game.UI.UIThemeFontBinder>();
                    Undo.RegisterCreatedObjectUndo(go, "Create UIThemeFontBinder");
                }
                var font = LoadKoreanFont();
                if (font == null) {
                    Debug.LogWarning("[HudUIBuilder] 한글 폰트 미발견 — 바인더 koreanFont 미배선");
                    return;
                }
                var so = new SerializedObject(existing);
                AssignRef(so, "koreanFont", font);
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[HudUIBuilder] UIThemeFontBinder 배선 완료 — koreanFont = IyagiGGC SDF");
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 폰트 바인더 배선 오류: {e.Message}");
            }
        }

        // 패널 라우터 — GameState별 패널 토글 컴포넌트를 Canvas에 붙이고 4종 패널+부트스트랩 배선
        static void WireRouter(GameObject canvasGo, GameBootstrap bootstrap) {
            try {
                var router = canvasGo.GetComponent<Game.UI.UIPanelRouter>();
                if (router == null) {
                    router = canvasGo.AddComponent<Game.UI.UIPanelRouter>();
                }
                var dock = FindChild(canvasGo.transform, "DockConsole");
                var hud = FindChild(canvasGo.transform, "HudPanel");
                var research = FindChild(canvasGo.transform, "ResearchPanel");
                var craft = FindChild(canvasGo.transform, "CraftPanel");
                var so = new SerializedObject(router);
                AssignRef(so, "bootstrap", bootstrap);
                AssignRef(so, "dockConsole", dock);
                AssignRef(so, "hudPanel", hud);
                AssignRef(so, "researchPanel", research);
                AssignRef(so, "craftPanel", craft);
                so.ApplyModifiedPropertiesWithoutUndo();
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 라우터 배선 오류: {e.Message}");
            }
        }

        // Canvas 직속 자식을 이름으로 찾기(비활성 포함)
        static GameObject FindChild(Transform parent, string name) {
            for (int i = 0; i < parent.childCount; i++) {
                if (parent.GetChild(i).name == name) {
                    return parent.GetChild(i).gameObject;
                }
            }
            return null;
        }

        // SerializedObject를 통한 private 필드 배선
        static void AssignRef(SerializedObject so, string propName, Object value) {
            try {
                // so.Update()를 호출당 부르면 직전 미적용 변경을 폐기하므로, 각 배선을 즉시 적용해 누락 방지
                var prop = so.FindProperty(propName);
                if (prop != null) {
                    prop.objectReferenceValue = value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                } else {
                    Debug.LogWarning($"[HudUIBuilder] 프로퍼티 '{propName}' 를 찾지 못했습니다.");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 배선 오류 ({propName}): {e.Message}");
            }
        }

        // 씬에서 동명 패널 제거 (재실행 대비)
        static void DestroyExisting(Transform canvas, string name) {
            var existing = canvas.Find(name);
            if (existing != null) {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }
    }
}
#endif
