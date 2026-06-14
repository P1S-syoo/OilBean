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

        // ── 진입점 ────────────────────────────────────────────────────────────
        [MenuItem("Tools/한강/UI/HUD·패널 생성")]
        static void Build() {
            try {
                // 라운드 코너 스프라이트 준비 + 주입 — 전 패널 공통 룩(둥근 모서리)
                UITheme.RoundSprite = EnsureRoundedSprite();
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

                // ── 패널 4종 생성 ──────────────────────────────────────────────
                BuildDockConsole(canvasTr, run, research, crafting, bootstrap);
                BuildHudPanel(canvasTr, run, battery, collector);
                BuildResearchPanel(canvasTr, run, research, collector);
                BuildCraftPanel(canvasTr, run, research, crafting, collector);

                // ── 패널 라우터 배선 (GameState별 토글) ──
                WireRouter(canvasGo, bootstrap);

                // 씬 더티 표시
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                Debug.Log("[HudUIBuilder] UI 4종 생성 완료 — GameCanvas 아래를 확인하세요.");
            } catch (System.Exception e) {
                Debug.LogError($"[HudUIBuilder] 생성 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // ── Canvas 획득/생성 ──────────────────────────────────────────────────
        static GameObject GetOrCreateCanvas() {
            var existing = GameObject.Find(CANVAS_NAME);
            if (existing != null) {
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

            // 루트 패널 — 전체 화면 반투명 오버레이
            var root = UITheme.MakeStretchPanel("DockConsole", canvas, color: new Color(0.05f, 0.12f, 0.14f, 0.96f));
            root.SetActive(false);   // 기본 비활성

            // ── 상단 자원 바 (높이 72) ──────────────────────────────────────
            var topBar = UITheme.MakePanel("TopBar", root.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -72f), new Vector2(0f, 0f),
                UITheme.BgHeader);

            // 타이틀
            var titleTxt = UITheme.MakeText("Title", topBar.transform,
                "■ 정화선 거점", UITheme.FontHeading, UITheme.Accent,
                TextAlignmentOptions.Left);
            var titleRt = titleTxt.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.3f, 1f);
            titleRt.offsetMin = new Vector2(UITheme.SpaceLG, 0f);
            titleRt.offsetMax = new Vector2(0f, 0f);

            // 자원 텍스트 (강재 / 샘플 / 적재 / 정화제)
            var resTxt = MakeStretchText("ResourceBar", topBar.transform,
                "강재: — / 샘플: — / 적재: — / 정화제: —",
                UITheme.FontBody, UITheme.TextSecondary,
                new Vector2(0.3f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(-UITheme.SpaceLG, 0f));

            // ── 중앙 영역 ────────────────────────────────────────────────────
            var center = UITheme.MakePanel("Center", root.transform,
                new Vector2(0f, 0f), new Vector2(0.68f, 1f),
                new Vector2(UITheme.SpaceLG, UITheme.SpaceLG),
                new Vector2(-UITheme.SpaceSM, -72f - UITheme.SpaceSM),
                UITheme.BgPanel);
            UITheme.AddShadow(center, 0.4f, 0f, -3f);

            // 주행동: 탐사 시작 (대형 버튼)
            var diveBtn = UITheme.MakeButton("DiveBtn", center.transform,
                "▼  탐사 시작 — 수중 잠수", UITheme.FontHeading,
                new Vector2(480f, 72f), new Vector2(0f, 60f),
                UITheme.Accent, UITheme.BgDeep);
            SetAnchor(diveBtn.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceMD, -UITheme.SpaceMD - 72f),
                new Vector2(-UITheme.SpaceMD, -UITheme.SpaceMD));
            UITheme.AddShadow(diveBtn.gameObject, 0.5f, 0f, -3f);   // 주행동 버튼 강조 깊이

            // 보조 버튼 행 (연구 / 제작 / 정화 설치)
            float subBtnY = -UITheme.SpaceMD * 2f - 72f - UITheme.SpaceSM;
            Button researchBtn = MakeSubButton("ResearchBtn",  center.transform, "연구",    subBtnY, 0f);
            Button craftBtn    = MakeSubButton("CraftBtn",     center.transform, "제작",    subBtnY, 1f);
            Button purifyBtn   = MakeSubButton("PurifyBtn",    center.transform, "정화 설치", subBtnY, 2f);

            // ── 중앙 하단: 탐사 브리핑 — 버튼 아래 빈 공간을 안내 정보로 채움 ──
            var brief = UITheme.MakePanel("Brief", center.transform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceMD, UITheme.SpaceMD),
                new Vector2(-UITheme.SpaceMD, -190f),
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
            rt.anchorMin = new Vector2(w * idx, 1f);
            rt.anchorMax = new Vector2(w * (idx + 1f), 1f);
            rt.offsetMin = new Vector2(idx == 0 ? UITheme.SpaceMD : UITheme.SpaceXS, topOffset - 40f);
            rt.offsetMax = new Vector2(idx == 2 ? -UITheme.SpaceMD : -UITheme.SpaceXS, topOffset);
            return btn;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. 인게임 HUD — GameState.Dive 시 표시
        // 레이아웃: 좌상 배터리 / 우상 적재+수심 / 좌측 수심 밴드 / 중앙하단 토스트 / 상단 경고
        // ═══════════════════════════════════════════════════════════════════════
        static void BuildHudPanel(Transform canvas,
                RunData run, Battery battery, Collector collector) {
            DestroyExisting(canvas, "HudPanel");

            // 루트 — 완전 투명(개별 자식만 색)
            var root = UITheme.MakeStretchPanel("HudPanel", canvas, color: Color.clear);
            root.SetActive(false);

            // ── 좌상: 배터리 게이지 블록 ────────────────────────────────────
            var batBlock = UITheme.MakePanel("BatteryBlock", root.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UITheme.SpaceMD, -136f),
                new Vector2(220f, -UITheme.SpaceMD),
                UITheme.BgPanel);
            UITheme.AddShadow(batBlock, 0.5f, 0f, -3f);

            var batLabel = UITheme.MakeText("BatLabel", batBlock.transform,
                "배터리", UITheme.FontCaption, UITheme.TextSecondary);
            PinTopLeft(batLabel.GetComponent<RectTransform>(), UITheme.SpaceSM, UITheme.SpaceSM, 80f, 18f);

            Image batFill = UITheme.MakeStretchGauge("BatGauge", batBlock.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceSM, UITheme.SpaceSM),
                new Vector2(-UITheme.SpaceSM, UITheme.SpaceSM + UITheme.GaugeHeight),
                UITheme.ColSuccess);

            var batPct = UITheme.MakeText("BatPct", batBlock.transform,
                "100%", UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.Right);
            PinTopRight(batPct.GetComponent<RectTransform>(), UITheme.SpaceSM, UITheme.SpaceSM, 60f, 18f);

            // ── 우상: 적재 게이지 + 수심 표시 ───────────────────────────────
            var cargoBlock = UITheme.MakePanel("CargoBlock", root.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-220f, -136f),
                new Vector2(-UITheme.SpaceMD, -UITheme.SpaceMD),
                UITheme.BgPanel);
            UITheme.AddShadow(cargoBlock, 0.5f, 0f, -3f);

            var cargoLabel = UITheme.MakeText("CargoLabel", cargoBlock.transform,
                "적재", UITheme.FontCaption, UITheme.TextSecondary);
            PinTopLeft(cargoLabel.GetComponent<RectTransform>(), UITheme.SpaceSM, UITheme.SpaceSM, 60f, 18f);

            var depthTxt = UITheme.MakeText("DepthTxt", cargoBlock.transform,
                "수심 0m", UITheme.FontCaption, UITheme.ColInfo, TextAlignmentOptions.Right);
            PinTopRight(depthTxt.GetComponent<RectTransform>(), UITheme.SpaceSM, UITheme.SpaceSM, 80f, 18f);

            Image cargoFill = UITheme.MakeStretchGauge("CargoGauge", cargoBlock.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(UITheme.SpaceSM, UITheme.SpaceSM),
                new Vector2(-UITheme.SpaceSM, UITheme.SpaceSM + UITheme.GaugeHeight),
                UITheme.ColWarn);

            var cargoTxt = UITheme.MakeText("CargoTxt", cargoBlock.transform,
                "0 / 70 kg", UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.Right);
            PinTopRight(cargoTxt.GetComponent<RectTransform>(), UITheme.SpaceSM, UITheme.SpaceSM, 100f, 18f);

            // ── 좌측 세로: 수심 밴드 ─────────────────────────────────────────
            var depthBand = UITheme.MakePanel("DepthBand", root.transform,
                new Vector2(0f, 0.1f), new Vector2(0f, 0.9f),
                new Vector2(UITheme.SpaceSM, 0f),
                new Vector2(UITheme.SpaceSM + 8f, 0f),
                UITheme.BgBorder);

            // Common(상) / Uncommon(중) / Rare(하) 3구간
            BuildDepthSegment("DepthCommon",   depthBand.transform, 0.67f, 1f,   UITheme.DepthCommon);
            BuildDepthSegment("DepthUncommon", depthBand.transform, 0.33f, 0.67f, UITheme.DepthUncommon);
            BuildDepthSegment("DepthRare",     depthBand.transform, 0f,   0.33f, UITheme.DepthRare);

            // 수심 밴드 레이블
            BuildDepthLabel("LblCommon",   depthBand.transform, 0.67f, 1f,   "Common",   UITheme.DepthCommon);
            BuildDepthLabel("LblUncommon", depthBand.transform, 0.33f, 0.67f, "Uncommon", UITheme.DepthUncommon);
            BuildDepthLabel("LblRare",     depthBand.transform, 0f,   0.33f, "Rare",     UITheme.DepthRare);

            // ── 상단 경고 배너 ───────────────────────────────────────────────
            var warnBanner = UITheme.MakePanel("WarnBanner", root.transform,
                new Vector2(0.2f, 1f), new Vector2(0.8f, 1f),
                new Vector2(0f, -48f), new Vector2(0f, 0f),
                UITheme.ColDanger);
            warnBanner.SetActive(false);   // 평상시 숨김

            var warnTxt = UITheme.MakeText("WarnText", warnBanner.transform,
                "배터리 방전 — 정화선 복귀", UITheme.FontBody,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            warnTxt.fontStyle = FontStyles.Bold;

            // ── 중앙 하단: 수집 토스트 ───────────────────────────────────────
            // Toast 컴포넌트는 기존 Toast.cs 사용 — 별도 게임오브젝트에 부착
            var toastGo = UITheme.MakePanel("CollectToast", root.transform,
                new Vector2(0.25f, 0f), new Vector2(0.75f, 0f),
                new Vector2(0f, UITheme.SpaceXXL),
                new Vector2(0f, UITheme.SpaceXXL + 44f),
                new Color(0.08f, 0.2f, 0.24f, 0.92f));
            var toastTxt = UITheme.MakeText("ToastTxt", toastGo.transform,
                "", UITheme.FontBody, UITheme.Accent, TextAlignmentOptions.Center);
            toastGo.SetActive(false);

            // Hud 컴포넌트 배선 (기존 Hud.cs)
            var hudComp = root.AddComponent<Hud>();
            var hudSo = new SerializedObject(hudComp);
            AssignRef(hudSo, "run",         run);
            AssignRef(hudSo, "battery",     battery);
            AssignRef(hudSo, "infoText",    cargoTxt);
            AssignRef(hudSo, "batteryFill", batFill);
            AssignRef(hudSo, "warnText",    warnTxt);
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // HudExtUI — 배터리 % / 수심 / 경고 배너 표시 확장
            var hudExt = root.AddComponent<HudExtUI>();
            var extSo = new SerializedObject(hudExt);
            AssignRef(extSo, "run",          run);
            AssignRef(extSo, "battery",      battery);
            AssignRef(extSo, "collector",    collector);
            AssignRef(extSo, "batteryPct",   batPct);
            AssignRef(extSo, "cargoFill",    cargoFill);
            AssignRef(extSo, "depthText",    depthTxt);
            AssignRef(extSo, "warnBanner",   warnBanner);
            AssignRef(extSo, "toastRoot",    toastGo);
            AssignRef(extSo, "toastText",    toastTxt);
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

            var root = UITheme.MakePanel("ResearchPanel", canvas,
                new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f),
                Vector2.zero, Vector2.zero,
                UITheme.BgDeep);
            root.SetActive(false);

            // 헤더
            BuildPanelHeader(root.transform, "■ 연구실 — 샘플 분석");

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

            // 섹션 타이틀
            MakeColTitle(leftCol.transform, "보유 샘플");

            // 샘플 수량 — 3레벨(오염수준 1/2/3)
            var smp1Txt = MakeSampleRow(leftCol.transform, "Lv.1  오염 저농도", UITheme.DepthCommon,   0.80f);
            var smp2Txt = MakeSampleRow(leftCol.transform, "Lv.2  오염 중농도", UITheme.DepthUncommon, 0.69f);
            var smp3Txt = MakeSampleRow(leftCol.transform, "Lv.3  오염 고농도", UITheme.DepthRare,     0.58f);

            UITheme.MakeDivider("Div", leftCol.transform, UITheme.SpaceMD);

            // 분석 포인트 게이지
            MakeColTitle(leftCol.transform, "누적 분석포인트", 0.32f);
            Image progFill;
            UITheme.MakeGauge("AnalyzeGauge", leftCol.transform,
                new Vector2(-UITheme.SpaceMD * 2f, UITheme.GaugeHeight + 2f),
                new Vector2(0f, 0.2f),
                UITheme.Accent, out progFill);

            // 다음 약품까지 텍스트
            var nextTxt = UITheme.MakeText("NextTxt", leftCol.transform,
                "다음: 약품Ⅰ — 0/3pt", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            PinAbsCenter(nextTxt.GetComponent<RectTransform>(), 0.12f, 28f, 16f);

            // 분석 버튼
            Button analyzeBtn = UITheme.MakeButton("AnalyzeBtn", leftCol.transform,
                "샘플 분석", UITheme.FontBody,
                new Vector2(0f, 40f), new Vector2(0f, 0.05f + 40f / 2f),
                UITheme.Accent, UITheme.BgDeep);
            SetAnchorStretch(analyzeBtn.GetComponent<RectTransform>(),
                0f, 0.05f, UITheme.SpaceMD, UITheme.SpaceLG + 40f,
                UITheme.SpaceMD, UITheme.SpaceLG);

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

            var root = UITheme.MakePanel("CraftPanel", canvas,
                new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f),
                Vector2.zero, Vector2.zero,
                UITheme.BgDeep);
            root.SetActive(false);

            // 헤더
            BuildPanelHeader(root.transform, "■ 제작소 — 장비 제작");

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

            // ── 중앙: 레시피 목록 (40%) ──────────────────────────────────────
            var midCol = UITheme.MakePanel("MidCol", content.transform,
                new Vector2(0.3f, 0f), new Vector2(0.65f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(-UITheme.SpaceSM, 0f),
                UITheme.BgPanel);

            MakeColTitle(midCol.transform, "레시피");

            // 레시피 행 6종 (부유체 3 + 업그레이드 3)
            string[] recipeIds    = { "buoy_1", "buoy_2", "buoy_3", "up_cargo", "up_battery", "gear_hull" };
            string[] recipeLabels = { "정화 부유체 Ⅰ", "정화 부유체 Ⅱ", "정화 부유체 Ⅲ",
                                      "적재 확장", "배터리 증설", "내압 프레임" };
            string[] recipeReqs   = { "일반강재 24kg·약품Ⅰ", "합금강재 30kg·약품Ⅱ", "특수강재 40kg·약품Ⅲ",
                                      "일반강재 16kg", "합금강재 18kg", "특수강재 30kg" };
            var recipeBtns = new Button[6];
            for (int i = 0; i < 6; i++) {
                float anchorY1 = 1f - (i + 1) * (0.85f / 6f) - 0.08f;
                float anchorY2 = 1f - i       * (0.85f / 6f) - 0.08f;
                recipeBtns[i] = BuildRecipeRow(midCol.transform,
                    recipeLabels[i], recipeReqs[i], i, anchorY1, anchorY2);
            }

            // ── 우측: 선택 상세 + 제작 버튼 (35%) ───────────────────────────
            var rightCol = UITheme.MakePanel("RightCol", content.transform,
                new Vector2(0.65f, 0f), new Vector2(1f, 1f),
                new Vector2(UITheme.SpaceSM, 0f), new Vector2(0f, 0f),
                UITheme.BgPanel);

            MakeColTitle(rightCol.transform, "제작 상세");

            var detailName = UITheme.MakeText("DetailName", rightCol.transform,
                "항목을 선택하세요", UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Center);
            var detailNameRt = detailName.GetComponent<RectTransform>();
            detailNameRt.anchorMin = new Vector2(0f, 0.7f);
            detailNameRt.anchorMax = new Vector2(1f, 0.85f);
            detailNameRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            detailNameRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);

            var detailReq = UITheme.MakeText("DetailReq", rightCol.transform,
                "", UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center);
            var detailReqRt = detailReq.GetComponent<RectTransform>();
            detailReqRt.anchorMin = new Vector2(0f, 0.55f);
            detailReqRt.anchorMax = new Vector2(1f, 0.7f);
            detailReqRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            detailReqRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            detailReq.enableWordWrapping = true;

            var detailStatus = UITheme.MakeText("DetailStatus", rightCol.transform,
                "", UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.Center);
            var detailStatusRt = detailStatus.GetComponent<RectTransform>();
            detailStatusRt.anchorMin = new Vector2(0f, 0.45f);
            detailStatusRt.anchorMax = new Vector2(1f, 0.55f);
            detailStatusRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            detailStatusRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);

            // 제작 버튼
            Button craftExecBtn = UITheme.MakeButton("CraftExecBtn", rightCol.transform,
                "제작", UITheme.FontHeading,
                Vector2.zero, Vector2.zero,
                UITheme.Accent, UITheme.BgDeep);
            SetAnchorStretch(craftExecBtn.GetComponent<RectTransform>(),
                0.08f, 0.08f, UITheme.SpaceMD, UITheme.SpaceLG + 52f,
                UITheme.SpaceMD, UITheme.SpaceLG);
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

        // ── UI 부품 헬퍼 ───────────────────────────────────────────────────────

        // 패널 상단 타이틀 헤더 바
        static void BuildPanelHeader(Transform parent, string title) {
            var header = UITheme.MakePanel("Header", parent,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -56f), new Vector2(0f, 0f),
                UITheme.BgHeader);
            UITheme.AddShadow(header, 0.5f, 0f, -2f);
            // 헤더 하단 액센트 언더라인(이전엔 패널 정중앙에 잘못 배치되던 버그 수정)
            UITheme.MakeAccentBar("HeaderUnderline", header.transform, 0f, 2f, 0f, UITheme.Accent);
            var t = UITheme.MakeText("HeaderTitle", header.transform,
                title, UITheme.FontHeading, UITheme.Accent, TextAlignmentOptions.Left);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(UITheme.SpaceLG, UITheme.SpaceXS);
            rt.offsetMax = new Vector2(-UITheme.SpaceLG, -UITheme.SpaceXS);
            t.fontStyle = FontStyles.Bold;
        }

        // 컬럼 타이틀 텍스트 (anchorY 중심)
        static void MakeColTitle(Transform parent, string label, float anchorYCenter = 0.9f) {
            var t = UITheme.MakeText("Title_" + label, parent,
                label, UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.Left);
            var rt = t.GetComponent<RectTransform>();
            float h = 20f;
            rt.anchorMin = new Vector2(0f, anchorYCenter - 0.03f);
            rt.anchorMax = new Vector2(1f, anchorYCenter + 0.03f);
            rt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            rt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);
            t.fontStyle = FontStyles.Bold;
        }

        // 샘플 보유 행 — 레이블 + 개수 텍스트 반환
        static TMP_Text MakeSampleRow(Transform parent, string label, Color dotColor, float anchorYCenter) {
            // 도트 색상 인디케이터
            var dot = new GameObject("Dot_" + label);
            dot.transform.SetParent(parent, false);
            var dotRt = dot.AddComponent<RectTransform>();
            float h = 0.04f;
            dotRt.anchorMin = new Vector2(0f, anchorYCenter - h);
            dotRt.anchorMax = new Vector2(0f, anchorYCenter + h);
            dotRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            dotRt.offsetMax = new Vector2(UITheme.SpaceMD + 8f, 0f);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = dotColor;

            var lbl = UITheme.MakeText("Label_" + label, parent,
                label, UITheme.FontCaption, UITheme.TextSecondary);
            var lblRt = lbl.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, anchorYCenter - h);
            lblRt.anchorMax = new Vector2(0.7f, anchorYCenter + h);
            lblRt.offsetMin = new Vector2(UITheme.SpaceMD + 14f, 0f);
            lblRt.offsetMax = new Vector2(0f, 0f);

            var cnt = UITheme.MakeText("Count_" + label, parent,
                "0", UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.Right);
            var cntRt = cnt.GetComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(0.7f, anchorYCenter - h);
            cntRt.anchorMax = new Vector2(1f, anchorYCenter + h);
            cntRt.offsetMin = new Vector2(0f, 0f);
            cntRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            cnt.fontStyle = FontStyles.Bold;
            return cnt;
        }

        // 자원 보유 행 (제작 패널용)
        static TMP_Text MakeResourceRow(Transform parent, string label, Color accent, float anchorYCenter) {
            var lbl = UITheme.MakeText("Lbl_" + label, parent,
                label, UITheme.FontCaption, UITheme.TextSecondary);
            var lblRt = lbl.GetComponent<RectTransform>();
            float h = 0.04f;
            lblRt.anchorMin = new Vector2(0f, anchorYCenter - h);
            lblRt.anchorMax = new Vector2(0.55f, anchorYCenter + h);
            lblRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            lblRt.offsetMax = new Vector2(0f, 0f);
            lbl.color = accent;

            var cnt = UITheme.MakeText("Val_" + label, parent,
                "0 kg", UITheme.FontBody, UITheme.TextPrimary, TextAlignmentOptions.Right);
            var cntRt = cnt.GetComponent<RectTransform>();
            cntRt.anchorMin = new Vector2(0.55f, anchorYCenter - h);
            cntRt.anchorMax = new Vector2(1f, anchorYCenter + h);
            cntRt.offsetMin = new Vector2(0f, 0f);
            cntRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            cnt.fontStyle = FontStyles.Bold;
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
                label, UITheme.FontBody, UITheme.TextPrimary);
            var nameRt = nameTxt.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.58f);
            nameRt.anchorMax = new Vector2(0.7f, 1f);
            nameRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            nameRt.offsetMax = new Vector2(0f, -UITheme.SpaceSM);
            nameTxt.fontStyle = FontStyles.Bold;

            // 상태 배지(상단 우)
            var badgeTxt = UITheme.MakeText("Badge", card.transform,
                "잠금", UITheme.FontCaption, UITheme.TextDisabled, TextAlignmentOptions.Right);
            var badgeRt = badgeTxt.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.6f, 0.58f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.offsetMin = new Vector2(0f, 0f);
            badgeRt.offsetMax = new Vector2(-UITheme.SpaceMD, -UITheme.SpaceSM);
            badgeTxt.fontStyle = FontStyles.Bold;

            // 설명(중단) — 카드 하단 공간을 채움
            var descTxt = UITheme.MakeText("Desc", card.transform,
                desc, UITheme.FontCaption, UITheme.TextSecondary);
            var descRt = descTxt.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0.3f);
            descRt.anchorMax = new Vector2(1f, 0.56f);
            descRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            descRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);
            descTxt.enableWordWrapping = true;
            descTxt.overflowMode = TextOverflowModes.Ellipsis;

            // 요구 조건(하단)
            var reqTxt = UITheme.MakeText("Req", card.transform,
                req, UITheme.FontCaption, accentColor);
            var reqRt = reqTxt.GetComponent<RectTransform>();
            reqRt.anchorMin = new Vector2(0f, 0.06f);
            reqRt.anchorMax = new Vector2(1f, 0.28f);
            reqRt.offsetMin = new Vector2(UITheme.SpaceMD, 0f);
            reqRt.offsetMax = new Vector2(-UITheme.SpaceMD, 0f);

            // id 텍스트(숨김 — ExtUI가 참조용으로 id 식별)
            var idTxt = UITheme.MakeText("AgentId", card.transform,
                id, UITheme.FontCaption, Color.clear);
            idTxt.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            return card;
        }

        // 레시피 행 버튼
        static Button BuildRecipeRow(Transform parent, string name, string req,
                int idx, float anchorY1, float anchorY2) {
            var btn = UITheme.MakeButton("Recipe_" + idx, parent,
                "", UITheme.FontBody, Vector2.zero, Vector2.zero,
                UITheme.BgBorder, UITheme.TextPrimary);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, anchorY1);
            rt.anchorMax = new Vector2(1f, anchorY2);
            rt.offsetMin = new Vector2(UITheme.SpaceSM, UITheme.SpaceXS);
            rt.offsetMax = new Vector2(-UITheme.SpaceSM, -UITheme.SpaceXS);
            UITheme.AddShadow(btn.gameObject, 0.35f, 0f, -2f);

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
                var lrt = lbl.GetComponent<RectTransform>();
                lrt.offsetMin = new Vector2(UITheme.SpaceMD + 6f, lrt.offsetMin.y);
            }

            // 소재 요구 텍스트
            var reqTxt = UITheme.MakeText("Req", btn.transform,
                req, UITheme.FontCaption, UITheme.TextDisabled, TextAlignmentOptions.Right);
            var reqRt = reqTxt.GetComponent<RectTransform>();
            reqRt.anchorMin = new Vector2(0.5f, 0f);
            reqRt.anchorMax = new Vector2(1f, 1f);
            reqRt.offsetMin = new Vector2(0f, UITheme.SpaceXS);
            reqRt.offsetMax = new Vector2(-UITheme.SpaceSM, -UITheme.SpaceXS);
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
            img.color = new Color(color.r, color.g, color.b, 0.5f);
        }

        // 수심 밴드 레이블 (우측)
        static void BuildDepthLabel(string name, Transform parent,
                float anchorY1, float anchorY2, string label, Color color) {
            var t = UITheme.MakeText(name, parent,
                label, UITheme.FontCaption, color, TextAlignmentOptions.Left);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, (anchorY1 + anchorY2) * 0.5f - 0.04f);
            rt.anchorMax = new Vector2(1f, (anchorY1 + anchorY2) * 0.5f + 0.04f);
            rt.offsetMin = new Vector2(UITheme.SpaceXS, 0f);
            rt.offsetMax = new Vector2(UITheme.SpaceXS + 80f, 0f);
        }

        // ── 레이아웃 유틸 ─────────────────────────────────────────────────────

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
