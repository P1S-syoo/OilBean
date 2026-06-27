using DG.Tweening;
using UnityEngine;
using UnityEngine.Splines;
using Game.Core;
using Game.Surface;
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
            new(11.6f, 11.6f, 11.6f)
        };
        [SerializeField] float floatBob = 0.18f;
        [SerializeField] float spinDegPerSec = 12f;
        [SerializeField] SubNavigator navigator;
        [SerializeField] SplineContainer river;
        [SerializeField] float cleanRiverWidth = 44f;
        [SerializeField] int cleanSegmentCount = 28;

        GameObject current;
        Light glow;
        Camera mainCam;
        GameObject marker;
        GameObject cleanRiverRoot;
        int activeStage = 1;

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
            ResolveRiverRefs();
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
            var p = StagePosition(activeStage);
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
            activeStage = stage;
            if (current != null) {
                Destroy(current);
            }
            current = InstantiateStage(stage);
            current.name = $"FloatingPurifierStage{stage}";
            current.transform.SetParent(transform, false);
            current.transform.position = StagePosition(stage);
            current.transform.localScale = instant ? TargetScale(stage) : Vector3.zero;
            BuildAura(current.transform, stage);
            if (marker != null) {
                marker.SetActive(false);   // 설치 후에는 큰 마커 대신 실제 정화체만 보이게 함
            }
            EnsureGlow(stage);
            ShowCleanSection(stage, instant);
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
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{StageModelDir}/{StageAssetName(stage)}");
            }
#endif
            return prefab;
        }

        static string StageAssetName(int stage) {
            return $"hanriver_purifier_buoy_stage{Mathf.Clamp(stage, 1, 3):00}.glb";
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
            ConfigureTransparency(mat, color);
            return mat;
        }

        Material MakeLineMat(string name, Color color) {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            ConfigureTransparency(mat, color);
            return mat;
        }

        // 알파가 있는 정화 패치/빛 기둥이 실제 반투명으로 렌더되게 설정
        void ConfigureTransparency(Material mat, Color color) {
            if (mat == null || color.a >= 0.99f) {
                return;
            }
            if (mat.HasProperty("_BaseColor")) {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_Color")) {
                mat.SetColor("_Color", color);
            }
            if (mat.HasProperty("_Surface")) {
                mat.SetFloat("_Surface", 1f);
            }
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        Vector3 SurfacePosition() {
            return new Vector3(transform.position.x, WorldGen.WaterY, transform.position.z) + surfaceOffset;
        }

        Vector3 StagePosition(int stage) {
            if (stage <= 1) {
                return new Vector3(transform.position.x, WorldGen.WaterY - 2.6f, transform.position.z);
            }
            if (stage == 2) {
                return new Vector3(transform.position.x, WorldGen.WaterY - 1.4f, transform.position.z);
            }
            return SurfacePosition();
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
            glow.transform.position = StagePosition(stage) + Vector3.up * 1.5f;
            glow.range = 12f + stage * 4f;
            glow.intensity = 2.2f + stage * 1.1f;
        }

        void PlayCleanseBeat(int stage) {
            ApplyCleanAtmosphere(stage);
            if (stage >= 3) {
                PlayFinalCleansePulse();
            }
            if (mainCam != null && stage >= 3) {
                float fov = mainCam.fieldOfView;
                DOTween.Sequence().SetTarget(this)
                    .Append(DOTween.To(() => mainCam.fieldOfView, v => mainCam.fieldOfView = v, fov - 8f, 0.65f).SetEase(Ease.OutCubic))
                    .Append(DOTween.To(() => mainCam.fieldOfView, v => mainCam.fieldOfView = v, fov, 1.1f).SetEase(Ease.InOutCubic));
            }
        }

        // 현재 정화 목표까지의 스플라인 구간 전체를 맑은 물/하늘 패치로 표시
        void ShowCleanSection(int stage, bool instant) {
            ResolveRiverRefs();
            float fromT = navigator != null ? navigator.PreviousTargetT : 0f;
            float toT = navigator != null ? navigator.CurrentTargetT : 0.5f;
            if (river == null || toT <= fromT + 0.001f) {
                BuildLocalCleanFallback(stage, instant);
                return;
            }
            BuildCleanRiverSection(fromT, toT, stage, instant);
            HideFloatingDebris(fromT, toT);
        }

        void ResolveRiverRefs() {
            if (navigator == null) {
                navigator = FindFirstObjectByType<SubNavigator>(FindObjectsInactive.Include);
            }
            if (river == null && navigator != null) {
                river = navigator.River;
            }
        }

        void BuildCleanRiverSection(float fromT, float toT, int stage, bool instant) {
            if (cleanRiverRoot != null) {
                DestroyRuntimeSafe(cleanRiverRoot);
            }
            cleanRiverRoot = new GameObject("CleanRiverSection");
            cleanRiverRoot.transform.SetParent(transform, false);
            int count = Mathf.Clamp(cleanSegmentCount, 4, 64);
            for (int i = 0; i < count; i++) {
                float a = Mathf.Lerp(fromT, toT, i / (float)count);
                float b = Mathf.Lerp(fromT, toT, (i + 1f) / count);
                AddCleanSegment(i, a, b, stage);
            }
            AddLocalCleanHighlight(stage);
            if (!instant) {
                cleanRiverRoot.transform.localScale = Vector3.zero;
                cleanRiverRoot.transform.DOScale(Vector3.one, 1.3f)
                    .SetEase(Ease.OutCubic)
                    .SetTarget(this);
            }
        }

        void AddCleanSegment(int index, float fromT, float toT, int stage) {
            Vector3 a = RiverPoint(fromT);
            Vector3 b = RiverPoint(toT);
            Vector3 mid = (a + b) * 0.5f;
            Vector3 forward = b - a;
            float len = Mathf.Max(forward.magnitude + 1.5f, 1f);
            Quaternion rot = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward.normalized) : Quaternion.identity;
            AddCleanBox($"CleanWater_{index:00}", mid + Vector3.up * 0.64f, rot,
                new Vector3(cleanRiverWidth, 0.02f, len),
                CleanWaterColor(stage));
        }

        void AddCleanBox(string name, Vector3 pos, Quaternion rot, Vector3 scale, Color color) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(cleanRiverRoot.transform, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            DestroyRuntimeSafe(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = MakeMat(name + "Mat", color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildLocalCleanFallback(int stage, bool instant) {
            if (cleanRiverRoot != null) {
                DestroyRuntimeSafe(cleanRiverRoot);
            }
            cleanRiverRoot = new GameObject("CleanRiverSectionFallback");
            cleanRiverRoot.transform.SetParent(transform, false);
            AddLocalCleanHighlight(stage);
            if (!instant) {
                cleanRiverRoot.transform.localScale = Vector3.zero;
                cleanRiverRoot.transform.DOScale(Vector3.one, 1.2f).SetEase(Ease.OutCubic).SetTarget(this);
            }
        }

        void AddLocalCleanHighlight(int stage) {
            float radius = 18f + stage * 6f;
            AddCleanBox("CleanLocalWaterHalo", SurfacePosition() + Vector3.up * 0.68f, Quaternion.identity,
                new Vector3(radius, 0.018f, radius), CleanLocalColor(stage));
        }

        Color CleanWaterColor(int stage) {
            return stage switch {
                1 => new Color(0.18f, 0.76f, 0.92f, 0.26f),
                2 => new Color(0.20f, 0.86f, 0.98f, 0.40f),
                _ => new Color(0.42f, 0.96f, 1f, 0.62f)
            };
        }

        Color CleanLocalColor(int stage) {
            return stage switch {
                1 => new Color(0.20f, 0.88f, 1f, 0.28f),
                2 => new Color(0.24f, 0.94f, 1f, 0.38f),
                _ => new Color(0.58f, 1f, 0.96f, 0.52f)
            };
        }

        void ApplyCleanAtmosphere(int stage) {
            Color sky = stage >= 3 ? new Color(0.68f, 0.88f, 1f) : new Color(0.48f, 0.70f, 0.80f);
            Color equator = stage >= 3 ? new Color(0.50f, 0.82f, 0.92f) : new Color(0.36f, 0.62f, 0.70f);
            Color ground = stage >= 3 ? new Color(0.26f, 0.52f, 0.58f) : new Color(0.22f, 0.40f, 0.46f);
            DOTween.To(() => RenderSettings.ambientSkyColor, c => RenderSettings.ambientSkyColor = c, sky, 1.4f).SetTarget(this);
            DOTween.To(() => RenderSettings.ambientEquatorColor, c => RenderSettings.ambientEquatorColor = c, equator, 1.4f).SetTarget(this);
            DOTween.To(() => RenderSettings.ambientGroundColor, c => RenderSettings.ambientGroundColor = c, ground, 1.4f).SetTarget(this);
            RenderSettings.fog = true;
            DOTween.To(() => RenderSettings.fogColor, c => RenderSettings.fogColor = c,
                stage >= 3 ? new Color(0.64f, 0.86f, 0.94f) : new Color(0.36f, 0.58f, 0.66f), 1.4f).SetTarget(this);
            DOTween.To(() => RenderSettings.fogDensity, v => RenderSettings.fogDensity = v,
                stage >= 3 ? 0.006f : 0.012f, 1.4f).SetTarget(this);
        }

        Vector3 RiverPoint(float t) {
            if (river == null) {
                return SurfacePosition();
            }
            Vector3 p = (Vector3)river.EvaluatePosition(Mathf.Clamp01(t));
            p.y = WorldGen.WaterY;
            return p;
        }

        void HideFloatingDebris(float fromT, float toT) {
            var debrisRoot = GameObject.Find("FloatingDebris");
            if (debrisRoot == null || river == null) {
                return;
            }
            foreach (Transform child in debrisRoot.transform) {
                float t = NearestRiverT(child.position);
                if (t >= fromT - 0.02f && t <= toT + 0.02f) {
                    child.gameObject.SetActive(false);
                }
            }
        }

        float NearestRiverT(Vector3 pos) {
            float bestT = 0f;
            float best = float.MaxValue;
            const int samples = 48;
            for (int i = 0; i <= samples; i++) {
                float t = i / (float)samples;
                float d = ((Vector3)river.EvaluatePosition(t) - pos).sqrMagnitude;
                if (d < best) {
                    best = d;
                    bestT = t;
                }
            }
            return bestT;
        }

        // 3단계는 구역 패치를 한 번 더 밝게 펄스해 최종 정화를 강조
        void PlayFinalCleansePulse() {
            if (cleanRiverRoot == null || glow == null) {
                return;
            }
            DOTween.To(() => glow.intensity, v => glow.intensity = v, glow.intensity + 5f, 0.45f)
                .SetLoops(2, LoopType.Yoyo)
                .SetTarget(this);
        }
    }
}
