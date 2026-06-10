using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Core;
using Game.Surface;

namespace Game.Editor.Surface {
    // 수상 리그 그레이박스 생성 도구 — Main3D에 강 스플라인·잠수정·코디네이터를 코드로 배치(재생성 가능)
    public static class SurfaceRigBuilder {

        [MenuItem("Tools/한강/수상 리그 생성")]
        public static void Build() {
            try {
                var old = GameObject.Find("SurfaceRig");
                if (old != null) {
                    Undo.DestroyObjectImmediate(old);   // 재실행 시 기존 리그 교체
                }
                var root = new GameObject("SurfaceRig");
                Undo.RegisterCreatedObjectUndo(root, "수상 리그 생성");

                var river = BuildRiver(root.transform);
                var sub = BuildSub(root.transform, river);
                WireBootstrap(root, sub);

                EditorUtility.SetDirty(root);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
                Debug.Log("[SurfaceRigBuilder] 수상 리그 생성 완료 — River 스플라인 + Sub 그레이박스 + SurfaceBootstrap");
            } catch (Exception e) {
                Debug.LogError($"[SurfaceRigBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 해수면 위 완만한 S자 강 중심선(x -20→160, z ±6)
        static SplineContainer BuildRiver(Transform parent) {
            var go = new GameObject("River");
            go.transform.SetParent(parent, false);
            var container = go.AddComponent<SplineContainer>();
            float y = WorldGenWaterY() + 0.5f;   // 흘수선: 해수면 살짝 위
            float3[] knots = {
                new(-20f, y, 0f),
                new(20f, y, 5f),
                new(60f, y, -6f),
                new(100f, y, 4f),
                new(140f, y, -3f),
                new(160f, y, 0f),
            };
            foreach (var k in knots) {
                container.Spline.Add(new BezierKnot(k), TangentMode.AutoSmooth);
            }
            return container;
        }

        // 잠수정 그레이박스(박스 선체 + 부유 모션 모델 차일드) + 항해기
        static SubNavigator BuildSub(Transform parent, SplineContainer river) {
            var subRoot = new GameObject("Sub3D");
            subRoot.transform.SetParent(parent, false);

            var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "Hull";
            hull.transform.SetParent(subRoot.transform, false);
            hull.transform.localScale = new Vector3(1.6f, 1.2f, 4f);   // 진행축(Z) 길쭉한 선체
            UnityEngine.Object.DestroyImmediate(hull.GetComponent<Collider>());   // 그레이박스라 3D 충돌 불필요
            hull.AddComponent<FloatBob>();   // 부유 모션은 모델에만(루트는 항해 위치 고정)

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(hull.transform, false);
            tower.transform.localPosition = new Vector3(0f, 0.8f, 0.1f);
            tower.transform.localScale = new Vector3(0.4f, 0.5f, 0.25f);
            UnityEngine.Object.DestroyImmediate(tower.GetComponent<Collider>());

            var nav = subRoot.AddComponent<SubNavigator>();
            var so = new SerializedObject(nav);
            so.FindProperty("river").objectReferenceValue = river;
            so.ApplyModifiedPropertiesWithoutUndo();
            return nav;
        }

        // SurfaceBootstrap 배치 + GameBootstrap 연결(있으면)
        static void WireBootstrap(GameObject root, SubNavigator nav) {
            var boot = root.AddComponent<SurfaceBootstrap>();
            var game = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("game").objectReferenceValue = game;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (game == null) {
                Debug.LogWarning("[SurfaceRigBuilder] GameBootstrap을 못 찾음 — 씬에서 수동 연결 필요");
            }
        }

        // 해수면 높이 — WorldGen 상수와 일치(블록 좌표)
        static float WorldGenWaterY() {
            return Game.World.WorldGen.WaterY;
        }
    }
}
