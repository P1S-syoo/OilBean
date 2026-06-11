using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Game.Surface;

namespace Game.Editor.Surface {
    // 폐허 스카이라인 배선 도구 — 배치 로직은 런타임 SkylineStreamer가 단독 소유(수상·수중 무한 스트리밍)
    // 메뉴는 스트리머 보장 + 에셋 참조 배선 + 에디터 정적 프리뷰 생성만 담당
    public static class SkylinePlacer {
        const float CoverExtra = 90f;   // 프리뷰 범위 — 스플라인 양 끝 너머 연장(런타임은 카메라 추적이라 무관)
        const string BridgePath = "Assets/VARCO3DImports/bridge_arch.glb";
        const string PillarMatPath = "Assets/4.Art/Materials/Surface/RuinPillar.mat";

        // 변주 소스 — (에셋 경로, 실물 높이 m). 새 빌딩은 여기에 추가
        static readonly (string path, float heightM)[] Variants = {
            ("Assets/VARCO3DImports/Ruins/ruin_block.glb", 22f),
            ("Assets/VARCO3DImports/Ruins/ruin_slab.glb", 42f),
            ("Assets/VARCO3DImports/Ruins/ruin_office.glb", 40f),
        };

        [MenuItem("Tools/한강/폐허 스카이라인 배치")]
        public static void Place() {
            try {
                var rig = GameObject.Find("SurfaceRig");
                if (rig == null) {
                    Debug.LogError("[SkylinePlacer] SurfaceRig가 없습니다 — Tools/한강/수상 리그 생성 먼저 실행");
                    return;
                }
                var streamer = EnsureStreamer(rig);
                if (streamer == null) {
                    return;
                }
                streamer.RebuildPreview(CoverExtra);
                EditorUtility.SetDirty(rig);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
            } catch (Exception e) {
                Debug.LogError($"[SkylinePlacer] 배치 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 스트리머 보장 + 에셋 참조 배선 — 수상 리그 생성(SurfaceRigBuilder)에서도 호출
        public static SkylineStreamer EnsureStreamer(GameObject rig) {
            var river = rig.GetComponentInChildren<SplineContainer>();
            if (river == null) {
                Debug.LogError("[SkylinePlacer] River 스플라인을 못 찾았습니다");
                return null;
            }
            var host = rig.transform.Find("Skyline")?.gameObject;
            if (host == null) {
                host = new GameObject("Skyline");
                host.transform.SetParent(rig.transform, false);
                Undo.RegisterCreatedObjectUndo(host, "스카이라인 스트리머");
            }
            var streamer = host.GetComponent<SkylineStreamer>();
            if (streamer == null) {
                streamer = host.AddComponent<SkylineStreamer>();
            }

            // 변주 프리팹 로드 — 없는 것은 경고 후 빈 슬롯(스트리머가 대체 선택)
            int loaded = 0;
            var so = new SerializedObject(streamer);
            so.FindProperty("river").objectReferenceValue = river;
            var prefabsProp = so.FindProperty("buildingPrefabs");
            var heightsProp = so.FindProperty("buildingHeightsM");
            prefabsProp.arraySize = Variants.Length;
            heightsProp.arraySize = Variants.Length;
            for (int v = 0; v < Variants.Length; v++) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Variants[v].path);
                if (prefab == null) {
                    Debug.LogWarning($"[SkylinePlacer] 모델 없음: {Variants[v].path} — roadview-to-3d로 생성 후 임포트");
                } else {
                    loaded++;
                }
                prefabsProp.GetArrayElementAtIndex(v).objectReferenceValue = prefab;
                heightsProp.GetArrayElementAtIndex(v).floatValue = Variants[v].heightM;
            }
            if (loaded == 0) {
                Debug.LogError("[SkylinePlacer] 사용 가능한 빌딩 모델이 없습니다");
            }
            var bridge = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePath);
            if (bridge == null) {
                Debug.LogWarning($"[SkylinePlacer] 다리 모델 없음: {BridgePath}");
            }
            so.FindProperty("bridgePrefab").objectReferenceValue = bridge;
            so.FindProperty("pillarMat").objectReferenceValue = GetOrCreatePillarMat();
            so.ApplyModifiedPropertiesWithoutUndo();
            return streamer;
        }

        // 기둥 머티리얼 — 잠긴 콘크리트 톤(URP Lit 단색)
        static Material GetOrCreatePillarMat() {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PillarMatPath);
            if (mat == null) {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) {
                    Debug.LogWarning("[SkylinePlacer] URP Lit 셰이더를 못 찾음 — 기둥 머티리얼 생략");
                    return null;
                }
                mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.16f, 0.21f, 0.22f));
                AssetDatabase.CreateAsset(mat, PillarMatPath);
            }
            return mat;
        }
    }
}
