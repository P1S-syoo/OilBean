using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Game.Core;
using Game.Score;
using Game.UI;
using Game.Stage;

namespace Game.EditorTools {
    // E6/E7 씬 배선 — ScoreSystem + 점수 HUD(콤보) + 정화 스팟 'E 홀드' 안내를 생성·배선
    public static class ScoreHudBuilder {
        const string SCORE_GO = "Game";   // ScoreSystem을 붙일 코디네이터 GO

        [MenuItem("Tools/한강/UI/스코어·E6E7 배선")]
        public static void Build() {
            try {
                BuildScoreSystem();
                BuildScoreHud();
                BuildHoldPrompt();
                Debug.Log("[ScoreHudBuilder] 스코어·E6E7 배선 완료");
            } catch (System.Exception e) {
                Debug.LogError($"[ScoreHudBuilder] 배선 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // ScoreSystem을 Game GO에 부착(없으면) — 런타임에 Collector/GameBootstrap 자가 탐색
        static void BuildScoreSystem() {
            var game = GameObject.Find(SCORE_GO);
            if (game == null) {
                Debug.LogWarning("[ScoreHudBuilder] Game GO 미발견 — ScoreSystem 부착 생략");
                return;
            }
            if (game.GetComponent<ScoreSystem>() == null) {
                Undo.AddComponent<ScoreSystem>(game);
                Debug.Log("[ScoreHudBuilder] ScoreSystem 부착");
            }
        }

        // HudPanel 상단 중앙에 점수/콤보 TMP + ScoreHud 부착·배선
        static void BuildScoreHud() {
            var hud = GameObject.Find("GameCanvas")?.transform.Find("HudPanel")?.gameObject;
            if (hud == null) {
                Debug.LogWarning("[ScoreHudBuilder] HudPanel 미발견 — 점수 HUD 생략(먼저 HUD를 생성하세요)");
                return;
            }
            var scoreText = MakeText(hud.transform, "ScoreText", new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(260f, 56f), 40, TextAlignmentOptions.Center);
            var comboText = MakeText(hud.transform, "ComboText", new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(200f, 40f), 28, TextAlignmentOptions.Center);
            comboText.text = string.Empty;
            comboText.color = new Color(0.18f, 0.77f, 0.71f);   // 청록 강조
            comboText.gameObject.SetActive(false);

            var sh = hud.GetComponent<ScoreHud>();
            if (sh == null) {
                sh = Undo.AddComponent<ScoreHud>(hud);
            }
            var so = new SerializedObject(sh);
            AssignRef(so, "scoreText", scoreText);
            AssignRef(so, "comboText", comboText);
        }

        // 정화 스팟 위에 'E 홀드 설치' 월드 안내(기본 비활성) + PurifyInstaller.holdPrompt 배선
        static void BuildHoldPrompt() {
            var spot = GameObject.Find("PurifySpot");
            if (spot == null) {
                Debug.LogWarning("[ScoreHudBuilder] PurifySpot 미발견 — 홀드 안내 생략");
                return;
            }
            var inst = spot.GetComponent<PurifyInstaller>();
            if (inst == null) {
                Debug.LogWarning("[ScoreHudBuilder] PurifyInstaller 미발견 — 홀드 안내 생략");
                return;
            }
            var existing = spot.transform.Find("HoldPrompt");
            GameObject prompt = existing != null ? existing.gameObject : new GameObject("HoldPrompt");
            if (existing == null) {
                Undo.RegisterCreatedObjectUndo(prompt, "HoldPrompt");
                prompt.transform.SetParent(spot.transform, false);
                prompt.transform.localPosition = new Vector3(0f, 2.2f, 0f);   // 스팟 머리 위
            }
            // 월드 TMP — 카메라를 향하는 안내 문구
            var tmp = prompt.GetComponent<TextMeshPro>();
            if (tmp == null) {
                tmp = prompt.AddComponent<TextMeshPro>();
            }
            tmp.text = "F 홀드 — 부유체 설치";
            tmp.fontSize = 6;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.18f, 0.77f, 0.71f);
            ApplyFont(tmp);
            prompt.SetActive(false);   // 준비 시 PurifyInstaller가 활성

            var so = new SerializedObject(inst);
            AssignRef(so, "holdPrompt", prompt);
        }

        // 스트레치 없는 고정 크기 TMP 텍스트 생성(HUD 자식)
        static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchor,
            Vector2 anchoredPos, Vector2 size, float fontSize, TextAlignmentOptions align) {
            // 멱등 — 재실행 시 기존 자식을 재사용(중복 생성 방지)
            var found = parent.Find(name);
            GameObject go;
            if (found != null) {
                go = found.gameObject;
            } else {
                go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, name);
                go.transform.SetParent(parent, false);
            }
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) {
                rt = go.AddComponent<RectTransform>();
            }
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (t == null) {
                t = go.AddComponent<TextMeshProUGUI>();
            }
            t.fontSize = fontSize;
            t.alignment = align;
            t.text = "0";
            t.color = Color.white;
            ApplyFont(t);
            return t;
        }

        // NotoSansKR 폰트 적용(UITheme.UIFont 우선, 없으면 에셋 직접 로드)
        static void ApplyFont(TMP_Text t) {
            var f = UITheme.UIFont;
            if (f == null) {
                f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/4.Art/Fonts/NotoSansKR SDF.asset");
            }
            if (f != null) {
                t.font = f;
            }
        }

        // 직렬화 참조 즉시 적용(누적 변경 유실 방지 — HudUIBuilder와 동일 규칙)
        static void AssignRef(SerializedObject so, string propName, Object value) {
            var prop = so.FindProperty(propName);
            if (prop != null) {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            } else {
                Debug.LogWarning($"[ScoreHudBuilder] 프로퍼티 '{propName}' 미발견");
            }
        }
    }
}
