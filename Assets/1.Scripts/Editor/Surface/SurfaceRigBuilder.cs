using System;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Core;
using Game.Surface;

namespace Game.Editor.Surface {
    // 수상 리그 그레이박스 생성 도구 — Main3D에 강 스플라인·잠수정·덱 캐릭터·궤도 카메라를 코드로 배치(재생성 가능)
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
                var player = BuildDeckPlayer(sub.transform);
                BuildOrbitCamera(root.transform, player);
                WireBootstrap(root, player);

                EditorUtility.SetDirty(root);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
                Debug.Log("[SurfaceRigBuilder] 수상 리그 생성 완료 — 강 스플라인 + 잠수정 + 덱 캐릭터 + 궤도 카메라");
            } catch (Exception e) {
                Debug.LogError($"[SurfaceRigBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 해수면 위 완만한 S자 강 중심선(x -20→160, z ±6)
        static SplineContainer BuildRiver(Transform parent) {
            var go = new GameObject("River");
            go.transform.SetParent(parent, false);
            var container = go.AddComponent<SplineContainer>();
            float y = Game.World.WorldGen.WaterY + 0.5f;   // 흘수선: 해수면 살짝 위
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

        // 잠수정 그레이박스 — 덱 위를 걸을 수 있게 선체를 넉넉히(4.5×1.5×11)
        static SubNavigator BuildSub(Transform parent, SplineContainer river) {
            var subRoot = new GameObject("Sub3D");
            subRoot.transform.SetParent(parent, false);

            var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "Hull";
            hull.transform.SetParent(subRoot.transform, false);
            hull.transform.localScale = new Vector3(4.5f, 1.5f, 11f);   // 진행축(Z) 길쭉한 넓은 덱
            UnityEngine.Object.DestroyImmediate(hull.GetComponent<Collider>());   // 그레이박스라 3D 충돌 불필요
            hull.AddComponent<FloatBob>();   // 부유 모션은 모델에만(루트는 항해 위치 고정)

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(hull.transform, false);
            tower.transform.localPosition = new Vector3(0f, 0.85f, 0.18f);
            tower.transform.localScale = new Vector3(0.25f, 0.7f, 0.12f);
            UnityEngine.Object.DestroyImmediate(tower.GetComponent<Collider>());

            var nav = subRoot.AddComponent<SubNavigator>();
            var so = new SerializedObject(nav);
            so.FindProperty("river").objectReferenceValue = river;
            so.ApplyModifiedPropertiesWithoutUndo();
            return nav;
        }

        // 덱 위 캐릭터 캡슐 — 잠수정 자식(로컬 이동), 덱 경계는 DeckCharacter가 클램프
        static DeckCharacter BuildDeckPlayer(Transform sub) {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "DeckPlayer";
            player.transform.SetParent(sub, false);
            player.transform.localPosition = new Vector3(0f, 1.65f, -2f);   // 덱 윗면 + 캡슐 절반
            player.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<Collider>());

            // 전방 표시(코) — 회전 방향 확인용
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.4f, 0.45f);
            nose.transform.localScale = new Vector3(0.25f, 0.15f, 0.4f);
            UnityEngine.Object.DestroyImmediate(nose.GetComponent<Collider>());

            return player.AddComponent<DeckCharacter>();
        }

        // 3인칭 궤도 카메라(Cinemachine) + 마우스 입력 드라이버, 메인 카메라에 Brain 보장
        static void BuildOrbitCamera(Transform parent, DeckCharacter player) {
            var camGo = new GameObject("OrbitCam");
            camGo.transform.SetParent(parent, false);
            var cm = camGo.AddComponent<CinemachineCamera>();
            cm.Follow = player.transform;
            cm.LookAt = player.transform;

            var orbital = camGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.Radius = 8f;
            orbital.TargetOffset = new Vector3(0f, 1.2f, 0f);
            orbital.VerticalAxis.Range = new Vector2(-20f, 65f);
            orbital.VerticalAxis.Value = 18f;
            camGo.AddComponent<CinemachineRotationComposer>();
            camGo.AddComponent<OrbitCameraDriver>();

            // 메인 카메라가 Cinemachine을 받도록 Brain 보장
            var main = Camera.main;
            if (main != null && main.GetComponent<CinemachineBrain>() == null) {
                Undo.AddComponent<CinemachineBrain>(main.gameObject);
            }

            // DeckCharacter의 방향 기준 카메라 = 메인 카메라
            if (main != null) {
                var pso = new SerializedObject(player);
                pso.FindProperty("cam").objectReferenceValue = main.transform;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // SurfaceBootstrap 배치 + GameBootstrap(수상 시작)·사이드뷰 카메라 연결
        static void WireBootstrap(GameObject root, DeckCharacter player) {
            var boot = root.AddComponent<SurfaceBootstrap>();
            var game = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            var side = UnityEngine.Object.FindFirstObjectByType<CamFollow>();
            var so = new SerializedObject(boot);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("sideCamera").objectReferenceValue = side;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (game != null) {
                // 씬 시작을 수상 항해로 전환
                var gso = new SerializedObject(game);
                gso.FindProperty("startOnSurface").boolValue = true;
                gso.ApplyModifiedPropertiesWithoutUndo();
            } else {
                Debug.LogWarning("[SurfaceRigBuilder] GameBootstrap을 못 찾음 — 씬에서 수동 연결 필요");
            }
        }
    }
}
