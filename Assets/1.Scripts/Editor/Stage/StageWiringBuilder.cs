using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Stage;
using Game.UI;

namespace Game.Editor.Stage {
    // 정화·클리어 사슬 생성 — Main3D에 빠져 있던 ClearView(클리어 오버레이)와 PurifySpot(정화 설치 트리거)을
    // 코드로 생성하고 GameBootstrap.purify/clearView에 배선(루프 완성). 재실행 시 기존 것 교체.
    public static class StageWiringBuilder {
        const string RunDataPath = "Assets/6.Data/Run_Default.asset";
        const float SpotX = 38f;    // 양화대교 잔해 X — 정화 지점을 랜드마크에 정렬
        const float SpotY = 21f;    // 얕은 수심(≈9m) — 모든 부유체 단계에서 도달 가능
        const float SpotRadius = 3.2f;

        [MenuItem("Tools/한강/정화·클리어 생성")]
        public static void Build() {
            try {
                var game = Object.FindFirstObjectByType<GameBootstrap>();
                if (game == null) {
                    Debug.LogError("[StageWiringBuilder] GameBootstrap을 씬에서 못 찾음");
                    return;
                }
                var run = AssetDatabase.LoadAssetAtPath<RunData>(RunDataPath);
                if (run == null) {
                    Debug.LogError($"[StageWiringBuilder] RunData 없음: {RunDataPath}");
                    return;
                }
                // 한글 폰트 주입(ClearView/Toast 텍스트 깨짐 방지) — 도메인 리로드 후 단독 실행 대비
                if (UITheme.UIFont == null) {
                    UITheme.UIFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/4.Art/Fonts/NotoSansKR SDF.asset");
                }

                var clearView = BuildClearView();
                var purify = BuildPurifySpot(run);
                var toast = BuildToast();

                // GameBootstrap 배선 — 즉시 적용(직전 변경 폐기 방지)
                var so = new SerializedObject(game);
                AssignRef(so, "purify", purify);
                AssignRef(so, "clearView", clearView);
                if (toast != null) {
                    AssignRef(so, "toast", toast);   // 구형 HUD 삭제로 사라진 Toast 복원
                }

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[StageWiringBuilder] 정화·클리어 생성·배선 완료 — PurifySpot + ClearView → GameBootstrap");
            } catch (System.Exception e) {
                Debug.LogError($"[StageWiringBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 클리어 오버레이(STAGE CLEAR) — GameCanvas 아래 풀스크린, 기본 비활성. ClearView가 토글
        static ClearView BuildClearView() {
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null) {
                Debug.LogError("[StageWiringBuilder] GameCanvas 없음 — UI 빌더 먼저 실행");
                return null;
            }
            // ClearView 컴포넌트 호스트(항상 활성)
            var hostT = canvas.transform.Find("ClearView");
            GameObject host = hostT != null ? hostT.gameObject : new GameObject("ClearView");
            if (hostT == null) {
                host.transform.SetParent(canvas.transform, false);
            }
            var view = host.GetComponent<ClearView>();
            if (view == null) {
                view = host.AddComponent<ClearView>();
            }

            // 오버레이(클리어 시 표시) — 기존 것 교체
            var oldOverlay = canvas.transform.Find("ClearOverlay");
            if (oldOverlay != null) {
                Object.DestroyImmediate(oldOverlay.gameObject);
            }
            var overlay = UITheme.MakeStretchPanel("ClearOverlay", canvas.transform,
                color: new Color(0.04f, 0.12f, 0.14f, 0.78f));
            var title = UITheme.MakeText("Title", overlay.transform, "STAGE CLEAR",
                UITheme.FontTitle + 16f, UITheme.Accent, TextAlignmentOptions.Center);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 0.62f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            title.fontStyle = FontStyles.Bold;
            var sub = UITheme.MakeText("Sub", overlay.transform, "한강이 맑아졌습니다",
                UITheme.FontHeading, UITheme.TextPrimary, TextAlignmentOptions.Center);
            var subRt = sub.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 0.42f);
            subRt.anchorMax = new Vector2(1f, 0.5f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;
            var hint = UITheme.MakeText("Hint", overlay.transform, "R 키 — 수면으로 복귀",
                UITheme.FontBody, UITheme.TextSecondary, TextAlignmentOptions.Center);
            var hintRt = hint.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 0.33f);
            hintRt.anchorMax = new Vector2(1f, 0.4f);
            hintRt.offsetMin = Vector2.zero;
            hintRt.offsetMax = Vector2.zero;
            overlay.SetActive(false);   // 기본 숨김 — ClearView.Play가 켬

            // ClearView 배선(즉시 적용)
            var vso = new SerializedObject(view);
            AssignRef(vso, "cam", Camera.main);
            AssignRef(vso, "clearText", overlay);
            return view;
        }

        // 정화 설치 지점 — 2.5D 다리 X 얕은 수심에 트리거 + 청록 마커
        static PurifyInstaller BuildPurifySpot(RunData run) {
            var old = GameObject.Find("PurifySpot");
            if (old != null) {
                Object.DestroyImmediate(old);
            }
            var go = new GameObject("PurifySpot");
            go.transform.position = new Vector3(SpotX, SpotY, 0f);
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = SpotRadius;
            var spot = go.AddComponent<PurifyInstaller>();
            var so = new SerializedObject(spot);
            AssignRef(so, "run", run);

            // 청록 마커(빌보드 쿼드) — 플레이어가 정화 지점을 찾도록
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "Marker";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localScale = new Vector3(SpotRadius * 2f, SpotRadius * 2f, 1f);
            var mc = marker.GetComponent<Collider>();
            if (mc != null) {
                Object.DestroyImmediate(mc);
            }
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.18f, 0.77f, 0.71f, 0.5f));
                mat.SetFloat("_Surface", 1f);   // 투명
                marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return spot;
        }

        // 액션 피드백 Toast — 하단 중앙 배너(구형 HUD 삭제로 사라진 것 재생성)
        static Toast BuildToast() {
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null) {
                return null;
            }
            var old = canvas.transform.Find("ActionToast");
            if (old != null) {
                Object.DestroyImmediate(old.gameObject);
            }
            var panel = UITheme.MakePanel("ActionToast", canvas.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-230f, 92f), new Vector2(230f, 140f),
                new Color(0.05f, 0.14f, 0.16f, 0.92f));
            var label = UITheme.MakeText("Label", panel.transform, "",
                UITheme.FontBody, UITheme.Accent, TextAlignmentOptions.Center);
            var toast = panel.AddComponent<Toast>();   // RequireComponent로 CanvasGroup 자동 추가
            var tso = new SerializedObject(toast);
            AssignRef(tso, "label", label);
            return toast;
        }

        // SerializedObject 배선 — 호출마다 즉시 적용(직전 미적용 변경 폐기 방지)
        static void AssignRef(SerializedObject so, string propName, Object value) {
            try {
                var prop = so.FindProperty(propName);
                if (prop != null) {
                    prop.objectReferenceValue = value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                } else {
                    Debug.LogWarning($"[StageWiringBuilder] 프로퍼티 '{propName}' 못 찾음");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[StageWiringBuilder] 배선 오류 ({propName}): {e.Message}");
            }
        }
    }
}
