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
        // 실물 모델 에셋 — VARCO 아포칼립스 잠수정 우선, 없으면 PolyPizza→그레이박스 폴백
        const string SubModelPath = "Assets/VARCO3DImports/sub_apoc.glb";
        const string SubFallbackPath = "Assets/4.Art/Models/PolyPizza/submarine1.glb";
        const string CharModelPath = "Assets/4.Art/Characters/MainCharacter.fbx";
        const string CharAnimPath = "Assets/4.Art/Characters/PlayerAnim.controller";
        const float SubLength = 15f;          // 잠수정 진행축(Z) 목표 길이 — 덱 보행 공간 확보
        const float SubWidthMul = 1.5f;       // 가로(X) 추가 배수 — 함교 양옆 통로 확보(앞↔뒤 이동 가능)
        const float CharacterHeight = 1.7f;   // 덱 캐릭터 목표 키
        const float HullLift = 0.35f;         // 흘수 보정 — 함교 포함 바운즈 중심 정렬은 선체가 깊이 잠겨 위로 올림(extents.y 비율)

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
                var (nav, deckTop, deckHalf, deckCols) = BuildSub(root.transform, river);
                var player = BuildDeckPlayer(nav.transform, deckTop, deckHalf, deckCols);
                var (orbitCam, orbitDriver) = BuildOrbitCamera(root.transform, player);
                var diveCam = BuildDiveCam(root.transform);
                BuildIntro(root.transform, nav, player, orbitCam, orbitDriver);
                BuildEnvironment(root.transform);
                WireBootstrap(root, nav, player, orbitCam, orbitDriver, diveCam);
                SkylinePlacer.EnsureStreamer(root);   // 스카이라인 스트리머(수상·수중 무한) 배선

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

        // 잠수정 — 실물 모델 바운즈 기반 축 정렬·스케일, 없으면 큐브 그레이박스 폴백. 선체 콜라이더로 보행 표면 제공
        static (SubNavigator nav, float deckTop, Vector2 deckHalf, Collider[] deckCols) BuildSub(Transform parent, SplineContainer river) {
            var subRoot = new GameObject("Sub3D");
            subRoot.transform.SetParent(parent, false);
            float deckTop = 0.75f;                        // 그레이박스 선체 윗면 기본값
            var deckHalf = new Vector2(1.8f, 4.6f);
            var deckCols = new System.Collections.Generic.List<Collider>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SubModelPath);
            bool needRetint = false;
            if (prefab == null) {
                // VARCO 모델이 없으면 PolyPizza 폴백 — 장난감 톤이라 재도색 필요
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SubFallbackPath);
                needRetint = prefab != null;
            }
            if (prefab != null) {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, subRoot.transform);
                model.name = "Model";
                var b = CalcBounds(model);
                bool rotated = false;
                if (b.size.x > b.size.z) {
                    model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // 긴 축을 진행축(Z)으로
                    b = CalcBounds(model);
                    rotated = true;
                }
                float scale = SubLength / Mathf.Max(b.size.z, 0.001f);
                // 폭 배수는 '월드 X'에 닿는 로컬 축에 적용 — Y90 회전 시 월드 X = 모델 로컬 Z
                var ls = Vector3.one * scale;
                if (rotated) {
                    ls.z *= SubWidthMul;
                } else {
                    ls.x *= SubWidthMul;
                }
                model.transform.localScale = ls;
                b = CalcBounds(model);
                model.transform.position -= b.center;     // 선체 중심을 흘수선(루트 원점)에
                model.transform.position += Vector3.up * (b.extents.y * HullLift);   // 수면 위로 흘수 보정
                b = CalcBounds(model);
                // 선체 윗면 — 단일 메시 모델은 HullTopY가 함교 꼭대기를 반환하므로 원통 선체 근사(바닥+지름)로 캡
                deckTop = Mathf.Min(HullTopY(model), b.min.y + b.extents.x * 2f);
                // 1차 클램프는 거친 한계만 — 실제 보행 경계는 선체 콜라이더 레이캐스트가 결정하므로 넉넉히
                deckHalf = new Vector2(Mathf.Max(b.extents.x * 0.92f, 0.6f), b.extents.z * 0.92f);
                if (needRetint) {
                    RetintSubmarine(model);               // 폴백 모델만 — VARCO 모델은 자체 아포칼립스 텍스처 유지
                }
                // 선체 메시 콜라이더 — 캐릭터가 실제 곡면 표면 위로만 걷도록(레이캐스트 표적)
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>()) {
                    if (mf.sharedMesh != null) {
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        deckCols.Add(mc);
                    }
                }
                model.AddComponent<FloatBob>();           // 부유 모션은 모델에만(루트는 항해 위치 고정)
            } else {
                Debug.LogWarning($"[SurfaceRigBuilder] 잠수정 모델 없음({SubModelPath}) — 큐브 그레이박스로 대체");
                var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hull.name = "Hull";
                hull.transform.SetParent(subRoot.transform, false);
                hull.transform.localScale = new Vector3(4.5f, 1.5f, 11f);
                UnityEngine.Object.DestroyImmediate(hull.GetComponent<Collider>());
                hull.AddComponent<FloatBob>();
            }

            // 에디터에서도 항해 시작점에 진행 방향으로 정박 — 플레이 시 SubNavigator가 동일 지점에서 출발
            subRoot.transform.position = river.EvaluatePosition(0f);
            Vector3 startTangent = ((Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(0f));
            startTangent.y = 0f;
            if (startTangent.sqrMagnitude > 0.0001f) {
                subRoot.transform.rotation = Quaternion.LookRotation(startTangent.normalized);
            }

            var nav = subRoot.AddComponent<SubNavigator>();
            var so = new SerializedObject(nav);
            so.FindProperty("river").objectReferenceValue = river;
            so.ApplyModifiedPropertiesWithoutUndo();
            return (nav, deckTop, deckHalf, deckCols.ToArray());
        }

        // 덱 위 캐릭터 — 2.5D와 같은 수영 플레이어 모델(MainCharacter+PlayerAnim) 재사용, 없으면 캡슐 폴백
        static DeckCharacter BuildDeckPlayer(Transform sub, float deckTop, Vector2 deckHalf, Collider[] deckCols) {
            var playerRoot = new GameObject("DeckPlayer");
            playerRoot.transform.SetParent(sub, false);
            // 중앙·선미 쪽 스폰(함교는 보통 중앙이라 선미는 비어 있음) — robust 레이가 평평한 선체 윗면을 보장
            var spawnLocal = new Vector3(0f, deckTop, -deckHalf.y * 0.6f);
            // 스폰 높이 실측 — 모델 전체 높이를 덮는 긴 하향 레이로 평평한(walkable) 선체 윗면만 채택(안테나·함교 곡면 건너뜀)
            if (deckCols != null && deckCols.Length > 0) {
                Physics.SyncTransforms();   // 에디터 — 갓 추가된 MeshCollider를 물리 씬에 반영해야 Raycast 적중
                Bounds mb = deckCols[0].bounds;
                for (int i = 1; i < deckCols.Length; i++) {
                    if (deckCols[i] != null) {
                        mb.Encapsulate(deckCols[i].bounds);
                    }
                }
                Vector3 worldXZ = sub.TransformPoint(new Vector3(spawnLocal.x, 0f, spawnLocal.z));
                var ray = new Ray(new Vector3(worldXZ.x, mb.max.y + 2f, worldXZ.z), Vector3.down);
                float bestDist = float.MaxValue;
                bool found = false;
                foreach (var c in deckCols) {
                    if (c != null && c.Raycast(ray, out var hit, mb.size.y + 4f) && hit.normal.y >= 0.5f && hit.distance < bestDist) {
                        bestDist = hit.distance;
                        spawnLocal.y = sub.InverseTransformPoint(hit.point).y - 0.12f;
                        found = true;
                    }
                }
                if (!found) {
                    Debug.LogWarning("[SurfaceRigBuilder] 덱 스폰 면을 못 찾음 — deckTop 폴백(공중부양 가능)");
                }
            }
            playerRoot.transform.localPosition = spawnLocal;

            Animator animator = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharModelPath);
            if (prefab != null) {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, playerRoot.transform);
                model.name = "Model";
                var b = CalcBounds(model);
                if (b.size.y > 0.0001f) {
                    model.transform.localScale = Vector3.one * (CharacterHeight / b.size.y);
                    b = CalcBounds(model);
                    // 발바닥(바운즈 바닥)을 루트(덱 윗면)에 스냅
                    model.transform.position += playerRoot.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
                }
                animator = model.GetComponentInChildren<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharAnimPath);
                if (animator != null && ctrl != null) {
                    animator.runtimeAnimatorController = ctrl;
                    animator.applyRootMotion = false;   // 이동은 DeckCharacter가 전담 — 클립 루트 모션으로 드리프트 금지
                    // 클립별 루트 기준 보정 — 수영 vs 보행/입수(발 기준). 0.85: 덱에서 무릎 잠김 실측 보정
                    var hip = model.AddComponent<ClipHipOffset>();
                    var hso = new SerializedObject(hip);
                    hso.FindProperty("animator").objectReferenceValue = animator;
                    hso.FindProperty("swimOffset").floatValue = 0.85f;
                    hso.ApplyModifiedPropertiesWithoutUndo();
                }
            } else {
                Debug.LogWarning($"[SurfaceRigBuilder] 캐릭터 모델 없음({CharModelPath}) — 캡슐 그레이박스로 대체");
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Model";
                capsule.transform.SetParent(playerRoot.transform, false);
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                UnityEngine.Object.DestroyImmediate(capsule.GetComponent<Collider>());
            }

            var deck = playerRoot.AddComponent<DeckCharacter>();
            var dso = new SerializedObject(deck);
            dso.FindProperty("deckHalf").vector2Value = deckHalf;
            dso.FindProperty("animator").objectReferenceValue = animator;
            // 선체 콜라이더 배선 — 실제 표면 위로만 보행
            var colsProp = dso.FindProperty("deckColliders");
            colsProp.arraySize = deckCols != null ? deckCols.Length : 0;
            for (int i = 0; i < colsProp.arraySize; i++) {
                colsProp.GetArrayElementAtIndex(i).objectReferenceValue = deckCols[i];
            }
            dso.ApplyModifiedPropertiesWithoutUndo();
            return deck;
        }

        // 원본 색으로 부위 분류해 잿빛 폐허 톤으로 교체 — 주황(프로펠러)=녹슨 액센트, 시안(창)=유지, 나머지=군함 회청
        static void RetintSubmarine(GameObject model) {
            var hull = GetOrCreateMat("SubHull", new Color(0.30f, 0.36f, 0.38f));
            var accent = GetOrCreateMat("SubAccent", new Color(0.42f, 0.24f, 0.12f));
            foreach (var r in model.GetComponentsInChildren<Renderer>()) {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++) {
                    var m = mats[i];
                    if (m == null) {
                        continue;
                    }
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                            : m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor")
                            : m.HasProperty("_Color") ? m.color : Color.white;
                    if (c.b > c.r * 1.25f && c.b > 0.5f) {
                        continue;   // 창문(시안 발광 포인트)은 원본 유지
                    }
                    mats[i] = (c.r > 0.45f && c.r > c.g * 1.3f) ? accent : hull;
                    changed = true;
                }
                if (changed) {
                    r.sharedMaterials = mats;
                }
            }
        }

        // 가장 부피 큰 렌더러(선체)의 윗면 Y — 함교·잠망경·프로펠러가 바운즈를 부풀리는 것을 배제
        static float HullTopY(GameObject model) {
            Renderer hull = null;
            float best = 0f;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) {
                var s = r.bounds.size;
                float v = s.x * s.y * s.z;
                if (v > best) {
                    best = v;
                    hull = r;
                }
            }
            return hull != null ? hull.bounds.max.y : 0.75f;
        }

        // 수면 그리드 메시 — 정점 웨이브용 분할 평면(에셋으로 저장해 씬 비대 방지, 있으면 재사용)
        static Mesh GetOrCreateWaterGrid(float width, float depth, int segX, int segZ) {
            const string path = "Assets/4.Art/Models/WaterGrid.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) {
                return mesh;
            }
            try {
                mesh = new Mesh { name = "WaterGrid" };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                var verts = new Vector3[(segX + 1) * (segZ + 1)];
                for (int z = 0; z <= segZ; z++) {
                    for (int x = 0; x <= segX; x++) {
                        verts[z * (segX + 1) + x] = new Vector3((x / (float)segX - 0.5f) * width, 0f, (z / (float)segZ - 0.5f) * depth);
                    }
                }
                var tris = new int[segX * segZ * 6];
                int ti = 0;
                for (int z = 0; z < segZ; z++) {
                    for (int x = 0; x < segX; x++) {
                        int v = z * (segX + 1) + x;
                        tris[ti++] = v;
                        tris[ti++] = v + segX + 1;
                        tris[ti++] = v + 1;
                        tris[ti++] = v + 1;
                        tris[ti++] = v + segX + 1;
                        tris[ti++] = v + segX + 2;
                    }
                }
                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(width, 2f, depth));   // 정점 웨이브 변위 컬링 여유
                AssetDatabase.CreateAsset(mesh, path);
            } catch (Exception e) {
                Debug.LogError($"[SurfaceRigBuilder] 수면 그리드 생성 실패: {e.Message}");
            }
            return mesh;
        }

        // 모든 렌더러를 합친 월드 바운즈 — 모델 피벗 위치와 무관한 스케일/스냅 기준
        static Bounds CalcBounds(GameObject go) {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) {
                return new Bounds(go.transform.position, Vector3.zero);
            }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        // 3인칭 궤도 카메라(Cinemachine) + 마우스 입력 드라이버, 메인 카메라에 Brain 보장
        static (CinemachineCamera, OrbitCameraDriver) BuildOrbitCamera(Transform parent, DeckCharacter player) {
            var camGo = new GameObject("OrbitCam");
            camGo.transform.SetParent(parent, false);
            var cm = camGo.AddComponent<CinemachineCamera>();
            cm.Follow = player.transform;
            cm.LookAt = player.transform;

            var orbital = camGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.Radius = 8f;
            orbital.TargetOffset = new Vector3(0f, 1.2f, 0f);
            orbital.VerticalAxis.Range = new Vector2(3f, 65f);   // 하한 +3° — 카메라가 수면 아래로 안 내려가게(물속 노출 방지)
            orbital.VerticalAxis.Value = 18f;
            camGo.AddComponent<CinemachineRotationComposer>();
            var driver = camGo.AddComponent<OrbitCameraDriver>();

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
            return (cm, driver);
        }

        // 잠수 블렌드 목적지 카메라 — 위치는 잠수 시점에 코드로 설정, 평소엔 비활성
        static CinemachineCamera BuildDiveCam(Transform parent) {
            var go = new GameObject("DiveCam");
            go.transform.SetParent(parent, false);
            var cm = go.AddComponent<CinemachineCamera>();
            go.SetActive(false);
            return cm;
        }

        // 인트로 컷신 — 부감 돌리 카메라 + 디렉터(시작 시 자동 재생, Space/Esc 스킵)
        static void BuildIntro(Transform parent, SubNavigator nav, DeckCharacter player,
            CinemachineCamera orbitCam, OrbitCameraDriver orbitDriver) {
            var camGo = new GameObject("IntroCam");
            camGo.transform.SetParent(parent, false);
            var introCam = camGo.AddComponent<CinemachineCamera>();
            camGo.SetActive(false);

            var dirGo = new GameObject("IntroDirector");
            dirGo.transform.SetParent(parent, false);
            var director = dirGo.AddComponent<IntroDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("introCam").objectReferenceValue = introCam;
            so.FindProperty("orbitCam").objectReferenceValue = orbitCam;
            so.FindProperty("focus").objectReferenceValue = nav.transform;
            so.FindProperty("deck").objectReferenceValue = player;
            so.FindProperty("orbitDriver").objectReferenceValue = orbitDriver;
            so.FindProperty("nav").objectReferenceValue = nav;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // SurfaceBootstrap 배치 + 잠수 시퀀스에 필요한 전체 참조 배선
        static void WireBootstrap(GameObject root, SubNavigator nav, DeckCharacter player,
            CinemachineCamera orbitCam, OrbitCameraDriver orbitDriver, CinemachineCamera diveCam) {
            var boot = root.AddComponent<SurfaceBootstrap>();
            var game = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            var side = UnityEngine.Object.FindFirstObjectByType<CamFollow>();
            var dive2d = UnityEngine.Object.FindFirstObjectByType<Game.Player.PlayerMove>();
            var toast = UnityEngine.Object.FindFirstObjectByType<Game.UI.Toast>();
            var so = new SerializedObject(boot);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("sideCamera").objectReferenceValue = side;
            so.FindProperty("nav").objectReferenceValue = nav;
            so.FindProperty("deck").objectReferenceValue = player;
            so.FindProperty("orbitDriver").objectReferenceValue = orbitDriver;
            so.FindProperty("orbitCam").objectReferenceValue = orbitCam;
            so.FindProperty("diveCam").objectReferenceValue = diveCam;
            so.FindProperty("sideTarget").objectReferenceValue = dive2d != null ? dive2d.transform : null;
            so.FindProperty("toast").objectReferenceValue = toast;
            // 수중 부유 입자(카메라 자식) — 수상 동안 끄기 위한 참조
            var camParticles = Camera.main != null ? Camera.main.GetComponentInChildren<ParticleSystem>(true) : null;
            so.FindProperty("camParticles").objectReferenceValue = camParticles;
            // 2.5D 백드롭(블록 스트리머·하늘 퀀드) — 수상에선 강변 블록 벽이 다리처럼 보여 숨김
            var streamer = UnityEngine.Object.FindFirstObjectByType<Game.World.ChunkStreamer>();
            so.FindProperty("worldBlocks").objectReferenceValue = streamer != null ? streamer.gameObject : null;
            so.FindProperty("skyQuad").objectReferenceValue = GameObject.Find("Sky");
            so.ApplyModifiedPropertiesWithoutUndo();
            if (game != null) {
                // 씬 시작을 수상 항해로 전환
                var gso = new SerializedObject(game);
                gso.FindProperty("startOnSurface").boolValue = true;
                gso.ApplyModifiedPropertiesWithoutUndo();
            } else {
                Debug.LogWarning("[SurfaceRigBuilder] GameBootstrap을 못 찾음 — 씬에서 수동 연결 필요");
            }
            if (dive2d == null) {
                Debug.LogWarning("[SurfaceRigBuilder] 2.5D 잠수정(PlayerMove)을 못 찾음 — sideTarget 수동 연결 필요");
            }
        }

        // W4: 수면·폐허 도시 그레이박스 환경

        // 좌표 결정적 해시 — 빌딩 높이/기울기 변주(재생성해도 같은 모양)
        static int Hash(int a, int b) {
            int h = a * 73856093 ^ b * 19349663;
            return (h % 1000 + 1000) % 1000;
        }

        // 머티리얼 에셋 확보 — 있으면 로드, 없으면 생성(기본 URP Lit, shaderName 지정 시 해당 셰이더)
        static Material GetOrCreateMat(string name, Color color, string shaderName = null) {
            const string dir = "Assets/4.Art/Materials/Surface";
            if (!AssetDatabase.IsValidFolder(dir)) {
                AssetDatabase.CreateFolder("Assets/4.Art/Materials", "Surface");
            }
            string path = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                var shader = shaderName != null ? Shader.Find(shaderName) : null;
                if (shader == null) {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }
                mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) {
                    mat.SetColor("_BaseColor", color);
                }
                if (mat.HasProperty("_Color")) {
                    mat.SetColor("_Color", color);
                }
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        // 레퍼런스 이미지의 잿빛 톤 — 수면·폐허 도시·부유 잔해·안개·흐린 광원
        static void BuildEnvironment(Transform parent) {
            var env = new GameObject("Environment");
            env.transform.SetParent(parent, false);

            float waterY = Game.World.WorldGen.WaterY;
            var waterMat = GetOrCreateMat("MurkyWater", new Color(0.10f, 0.16f, 0.16f));
            var debrisMat = GetOrCreateMat("RuinDebris", new Color(0.20f, 0.21f, 0.22f));

            // 수면 전용 셰이더(노이즈 흐름+거품)로 업그레이드 — 없으면 URP Lit 단색 유지
            var waterShader = Shader.Find("Game/MurkyWaterFlow");
            if (waterShader != null && waterMat.shader != waterShader) {
                waterMat.shader = waterShader;
                EditorUtility.SetDirty(waterMat);
            }

            // 수면 — 정점 웨이브용 분할 그리드(600×300m). Unity Plane(10×10)은 정점이 적어 웨이브가 안 보임
            var water = new GameObject("WaterSurface", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(env.transform, false);
            water.transform.position = new Vector3(70f, waterY + 0.58f, 0f);   // 블록 윗면(WaterY+0.5)보다 위
            water.GetComponent<MeshFilter>().sharedMesh = GetOrCreateWaterGrid(600f, 300f, 150, 40);
            water.GetComponent<MeshRenderer>().sharedMaterial = waterMat;

            // 강변 스카이라인은 그레이박스 대신 LandmarkPlacer(Tools/한강/명소 배치)가 실물 3D로 채운다

            // 부유 쓰레기/잔해 — 강 위에 해시 산포
            var trash = new GameObject("FloatingDebris");
            trash.transform.SetParent(env.transform, false);
            for (int i = 0; i < 36; i++) {
                int h = Hash(i, 77);
                var d = GameObject.CreatePrimitive(h % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                d.name = $"Debris_{i:00}";
                d.transform.SetParent(trash.transform, false);
                float x = -18f + (h % 100) * 1.7f;
                float z = -8f + (h >> 2) % 17;
                d.transform.position = new Vector3(x, waterY + 0.68f, z);
                float s = 0.3f + (h % 7) * 0.12f;
                d.transform.localScale = new Vector3(s * 1.6f, s * 0.35f, s);
                d.transform.rotation = Quaternion.Euler(0f, h % 360, 0f);
                UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
                d.GetComponent<MeshRenderer>().sharedMaterial = debrisMat;
                // 파도 흔들림 — 개체별 진폭/주기/위상/롤 해시 변주(일제히 같은 박자로 움직이는 것 방지)
                var bob = d.AddComponent<FloatBob>();
                var bso = new SerializedObject(bob);
                bso.FindProperty("amplitude").floatValue = 0.06f + (h % 5) * 0.02f;
                bso.FindProperty("period").floatValue = 2.2f + (h % 7) * 0.35f;
                bso.FindProperty("rollDegrees").floatValue = 4f + (h % 9);
                bso.FindProperty("phase").floatValue = (h % 100) / 100f;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 수중 헤이즈 막 — 수면 아래 잠긴 빌딩 하부에 물 톤(2.5D 시점에서 침수 도시 연속감), 플레이어 X 추적
            var hazeMat = GetOrCreateMat("UnderwaterHaze", new Color(0.06f, 0.28f, 0.30f, 0.45f), "Game/UnderwaterHaze");
            var hazeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hazeQuad.name = "UnderwaterHaze";
            hazeQuad.transform.SetParent(env.transform, false);
            float hazeTop = waterY + 0.58f;                    // 수면 평면 높이와 일치
            float hazeBottom = Game.World.WorldGen.MinY;
            hazeQuad.transform.position = new Vector3(70f, (hazeTop + hazeBottom) * 0.5f, 16f);   // 수중 지형(z+4) 뒤, 빌딩(z19+) 앞
            hazeQuad.transform.localScale = new Vector3(400f, hazeTop - hazeBottom, 1f);
            UnityEngine.Object.DestroyImmediate(hazeQuad.GetComponent<Collider>());
            hazeQuad.GetComponent<MeshRenderer>().sharedMaterial = hazeMat;
            var dive2dMove = UnityEngine.Object.FindFirstObjectByType<Game.Player.PlayerMove>();
            if (dive2dMove != null) {
                var follow = hazeQuad.AddComponent<Game.World.FollowX>();
                var fso = new SerializedObject(follow);
                fso.FindProperty("target").objectReferenceValue = dive2dMove.transform;
                fso.FindProperty("fixedY").floatValue = hazeQuad.transform.position.y;
                fso.FindProperty("fixedZ").floatValue = 16f;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 잿빛 안개 + 흐린 광원 — 레퍼런스의 흐린 하늘 톤(원경 빌딩이 안개에 잠김)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.40f, 0.44f, 0.46f);
            RenderSettings.fogDensity = 0.011f;
            var light = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (light != null && light.type == LightType.Directional) {
                light.intensity = 0.75f;
                light.color = new Color(0.78f, 0.80f, 0.83f);
            }
        }
    }
}
