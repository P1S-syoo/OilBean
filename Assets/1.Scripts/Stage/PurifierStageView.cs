using DG.Tweening;
using UnityEngine;
using Game.Core;
using Game.World;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Stage {
    // 정화 부유체 수면 연출 — 설치 완료 단계에 맞춰 수면 위 대형 정화체를 표시
    public class PurifierStageView : MonoBehaviour {
        const string StageModelDir = "Assets/4.Art/Varco/PurifierModels";

        [SerializeField] RunData run;
        [SerializeField] PurifyInstaller installer;
        [SerializeField] GameObject[] stagePrefabs = new GameObject[3];
        [SerializeField] Vector3 surfaceOffset = new(0f, 2.2f, 0f);
        [SerializeField] Vector3[] stageScales = {
            new(3.2f, 3.2f, 3.2f),
            new(4.4f, 4.4f, 4.4f),
            new(5.8f, 5.8f, 5.8f)
        };
        [SerializeField] float floatBob = 0.18f;
        [SerializeField] float spinDegPerSec = 12f;

        GameObject current;
        Light glow;
        Camera mainCam;
        GameObject marker;

        void Awake() {
            if (installer == null) {
                installer = GetComponent<PurifyInstaller>();
            }
            if (installer != null) {
                installer.OnPurified += ShowCurrentStage;
            }
            var mt = transform.Find("Marker");
            marker = mt != null ? mt.gameObject : null;
            mainCam = Camera.main;
        }

        void Start() {
            if (run != null && run.BuoyStage > 0) {
                ShowStage(run.BuoyStage, instant: true);
            }
        }

        void OnDestroy() {
            if (installer != null) {
                installer.OnPurified -= ShowCurrentStage;
            }
            DOTween.Kill(this);
        }

        void Update() {
            if (current == null) {
                return;
            }
            current.transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.World);
            var p = SurfacePosition();
            p.y += Mathf.Sin(Time.time * 1.35f) * floatBob;
            current.transform.position = p;
            if (glow != null) {
                glow.transform.position = p + Vector3.up * 1.5f;
            }
        }

        void ShowCurrentStage() {
            int stage = run != null ? run.BuoyStage : 1;
            ShowStage(stage, instant: false);
        }

        public void ShowStage(int stage, bool instant = false) {
            stage = Mathf.Clamp(stage, 1, 3);
            if (current != null) {
                Destroy(current);
            }
            current = InstantiateStage(stage);
            current.name = $"FloatingPurifierStage{stage}";
            current.transform.SetParent(transform, false);
            current.transform.position = SurfacePosition();
            current.transform.localScale = instant ? TargetScale(stage) : Vector3.zero;
            BuildAura(current.transform, stage);
            if (marker != null) {
                marker.SetActive(false);   // 설치 후에는 큰 마커 대신 실제 정화체만 보이게 함
            }
            EnsureGlow(stage);
            if (!instant) {
                current.transform.DOScale(TargetScale(stage), 0.85f)
                    .SetEase(Ease.OutBack)
                    .SetTarget(this);
                PlayCleanseBeat(stage);
            }
        }

        GameObject InstantiateStage(int stage) {
            var prefab = StagePrefab(stage);
            if (prefab != null) {
                return Instantiate(prefab);
            }
            return BuildFallback(stage);
        }

        GameObject StagePrefab(int stage) {
            var prefab = stagePrefabs != null && stagePrefabs.Length >= stage ? stagePrefabs[stage - 1] : null;
#if UNITY_EDITOR
            if (prefab == null) {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{StageModelDir}/purifier_stage{stage}.glb");
            }
#endif
            return prefab;
        }

        void BuildAura(Transform parent, int stage) {
            var cyan = MakeMat("PurifierAuraCyan", new Color(0.05f, 0.85f, 1f, 0.85f));
            float r = 1.2f + stage * 0.35f;
            AddRing(parent, "CleanseRingOuter", r * 1.5f, 0f, stage);
            AddRing(parent, "CleanseRingInner", r, 0.18f, stage);
            AddPart(parent, PrimitiveType.Cylinder, "CleanseBeam", new Vector3(0f, 0.75f, 0f),
                new Vector3(0.18f + stage * 0.06f, 1.2f + stage * 0.35f, 0.18f + stage * 0.06f), cyan);
        }

        void AddRing(Transform parent, string name, float radius, float y, int stage) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 48;
            line.widthMultiplier = 0.035f + stage * 0.012f;
            line.material = MakeLineMat(name + "Mat", new Color(0.18f, 0.92f, 1f, 0.95f));
            for (int i = 0; i < line.positionCount; i++) {
                float a = i / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        GameObject BuildFallback(int stage) {
            var root = new GameObject($"PurifierFallback{stage}");
            var white = MakeMat("PurifierWhite", new Color(0.9f, 0.97f, 1f, 1f));
            var cyan = MakeMat("PurifierCyan", new Color(0.05f, 0.9f, 1f, 1f));
            AddPart(root.transform, PrimitiveType.Cylinder, "Core", new Vector3(0f, 0f, 0f), new Vector3(0.65f, 1.1f + stage * 0.22f, 0.65f), cyan);
            AddPart(root.transform, PrimitiveType.Cylinder, "HaloA", new Vector3(0f, 0.55f, 0f), new Vector3(1.8f + stage * 0.55f, 0.08f, 1.8f + stage * 0.55f), white);
            AddPart(root.transform, PrimitiveType.Cylinder, "HaloB", new Vector3(0f, -0.35f, 0f), new Vector3(1.35f + stage * 0.45f, 0.08f, 1.35f + stage * 0.45f), white);
            for (int i = 0; i < 4; i++) {
                float a = i * Mathf.PI * 0.5f;
                var pos = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.9f + stage * 0.25f);
                var fin = AddPart(root.transform, PrimitiveType.Cube, "FloatFin", pos, new Vector3(0.28f, 0.75f, 0.95f), white);
                fin.transform.rotation = Quaternion.Euler(0f, -i * 90f, 0f);
            }
            return root;
        }

        GameObject AddPart(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat) {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) {
                DestroyRuntimeSafe(col);
            }
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = mat;
            }
            return go;
        }

        static void DestroyRuntimeSafe(Object obj) {
            if (obj == null) {
                return;
            }
            if (Application.isPlaying) {
                Destroy(obj);
            } else {
                DestroyImmediate(obj);
            }
        }

        Material MakeMat(string name, Color color) {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            return mat;
        }

        Material MakeLineMat(string name, Color color) {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            return mat;
        }

        Vector3 SurfacePosition() {
            return new Vector3(transform.position.x, WorldGen.WaterY, transform.position.z) + surfaceOffset;
        }

        Vector3 TargetScale(int stage) {
            int i = Mathf.Clamp(stage - 1, 0, stageScales.Length - 1);
            return stageScales[i];
        }

        void EnsureGlow(int stage) {
            if (glow == null) {
                var go = new GameObject("PurifierGlow");
                go.transform.SetParent(transform, false);
                glow = go.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.3f, 0.95f, 1f);
                glow.shadows = LightShadows.None;
            }
            glow.transform.position = SurfacePosition() + Vector3.up * 1.5f;
            glow.range = 12f + stage * 4f;
            glow.intensity = 2.2f + stage * 1.1f;
        }

        void PlayCleanseBeat(int stage) {
            DOTween.To(() => RenderSettings.ambientSkyColor,
                    c => RenderSettings.ambientSkyColor = c,
                    stage >= 3 ? new Color(0.58f, 0.78f, 0.9f) : new Color(0.44f, 0.66f, 0.74f),
                    1.2f)
                .SetTarget(this);
            if (mainCam != null && stage >= 3) {
                float fov = mainCam.fieldOfView;
                DOTween.Sequence().SetTarget(this)
                    .Append(DOTween.To(() => mainCam.fieldOfView, v => mainCam.fieldOfView = v, fov - 8f, 0.65f).SetEase(Ease.OutCubic))
                    .Append(DOTween.To(() => mainCam.fieldOfView, v => mainCam.fieldOfView = v, fov, 1.1f).SetEase(Ease.InOutCubic));
            }
        }
    }
}
