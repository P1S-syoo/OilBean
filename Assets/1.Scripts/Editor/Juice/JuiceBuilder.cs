using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Items;
using Game.Stage;
using Game.Craft;
using Game.Player;
using Game.Juice;

namespace Game.Editor.Juice {
    // 게임필(E3) 생성·배선 — ScreenFlash(전체화면 플래시)·CameraShaker(메인 카메라)·스파클 버스트·JuiceController 생성 후 이벤트 소스 배선
    public static class JuiceBuilder {
        static readonly Color Teal = new Color(0.37f, 0.85f, 0.80f);

        [MenuItem("Tools/한강/연출(Juice) 생성")]
        public static void Build() {
            try {
                var flash = BuildScreenFlash();
                var shaker = BuildCameraShaker();
                var (controllerGo, burst) = BuildController();
                if (controllerGo == null) {
                    return;
                }
                var jc = controllerGo.GetComponent<JuiceController>();

                // 이벤트 소스 + 참조 배선(즉시 적용)
                var so = new SerializedObject(jc);
                AssignRef(so, "collector", Object.FindFirstObjectByType<Collector>());
                AssignRef(so, "hazard", Object.FindFirstObjectByType<HazardDetector>());
                AssignRef(so, "purify", Object.FindFirstObjectByType<PurifyInstaller>());
                AssignRef(so, "crafting", Object.FindFirstObjectByType<Crafting>());
                AssignRef(so, "bootstrap", Object.FindFirstObjectByType<GameBootstrap>());
                var pm = Object.FindFirstObjectByType<PlayerMove>();
                AssignRef(so, "player", pm != null ? pm.transform : null);
                var spot = GameObject.Find("PurifySpot");
                AssignRef(so, "purifySpot", spot != null ? spot.transform : null);
                AssignRef(so, "flash", flash);
                AssignRef(so, "shaker", shaker);
                AssignRef(so, "burst", burst);

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[JuiceBuilder] 게임필 생성·배선 완료 — ScreenFlash + CameraShaker + Burst + JuiceController");
            } catch (System.Exception e) {
                Debug.LogError($"[JuiceBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 전체화면 플래시 Image — GameCanvas 최상위(패널 위), 입력 비차단, 알파 0
        static ScreenFlash BuildScreenFlash() {
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null) {
                Debug.LogError("[JuiceBuilder] GameCanvas 없음 — UI 빌더 먼저 실행");
                return null;
            }
            var old = canvas.transform.Find("ScreenFlash");
            if (old != null) {
                Object.DestroyImmediate(old.gameObject);
            }
            var go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();   // 모든 패널 위에서 번쩍
            return go.AddComponent<ScreenFlash>();
        }

        // 메인 카메라에 셰이커 부착
        static CameraShaker BuildCameraShaker() {
            var cam = Camera.main;
            if (cam == null) {
                Debug.LogWarning("[JuiceBuilder] Main Camera 없음 — 셰이크 생략");
                return null;
            }
            var shaker = cam.GetComponent<CameraShaker>();
            if (shaker == null) {
                shaker = cam.gameObject.AddComponent<CameraShaker>();
            }
            return shaker;
        }

        // JuiceController 루트 + 재사용 스파클 버스트
        static (GameObject, ParticleSystem) BuildController() {
            var old = GameObject.Find("Juice");
            if (old != null) {
                Object.DestroyImmediate(old);
            }
            var root = new GameObject("Juice");
            Undo.RegisterCreatedObjectUndo(root, "Juice 생성");
            root.AddComponent<JuiceController>();

            var burstGo = new GameObject("Burst");
            burstGo.transform.SetParent(root.transform, false);
            var ps = burstGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 3.2f;
            main.startSize = 0.28f;
            main.maxParticles = 40;
            main.startColor = Teal;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Teal, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) {
                r.material = new Material(shader);
            }
            return (root, ps);
        }

        static void AssignRef(SerializedObject so, string propName, Object value) {
            try {
                var prop = so.FindProperty(propName);
                if (prop != null) {
                    prop.objectReferenceValue = value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                } else {
                    Debug.LogWarning($"[JuiceBuilder] 프로퍼티 '{propName}' 못 찾음");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[JuiceBuilder] 배선 오류 ({propName}): {e.Message}");
            }
        }
    }
}
