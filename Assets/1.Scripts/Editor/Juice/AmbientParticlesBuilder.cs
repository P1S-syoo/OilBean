using UnityEngine;
using UnityEditor;

namespace Game.EditorTools {
    // E4 부유입자 — 카메라를 따라다니는 저속·저알파 수중 부유물(먼지/플랑크톤) 상시 연출
    public static class AmbientParticlesBuilder {
        // [MenuItem("Tools/한강/연출/부유입자 생성")]
        public static void Build() {
            try {
                var cam = Camera.main;
                if (cam == null) {
                    Debug.LogWarning("[AmbientParticlesBuilder] Main Camera 미발견 — 생략");
                    return;
                }
                // 기존 것 제거 후 재생성(멱등)
                var prev = cam.transform.Find("AmbientParticles");
                if (prev != null) {
                    Object.DestroyImmediate(prev.gameObject);
                }
                var go = new GameObject("AmbientParticles");
                Undo.RegisterCreatedObjectUndo(go, "AmbientParticles");
                go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 12f);   // 카메라 앞 평면

                var ps = go.AddComponent<ParticleSystem>();
                ConfigureParticles(ps, go);
                // 탐사(Dive) 중에만 방출 — 수면 3D 단계에선 숨김
                go.AddComponent<Game.Juice.AmbientParticleGate>();
                Debug.Log("[AmbientParticlesBuilder] 부유입자 생성 완료");
            } catch (System.Exception e) {
                Debug.LogError($"[AmbientParticlesBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // 느리게 떠다니는 저알파 입자 — 월드 시뮬레이션으로 카메라 이동과 분리된 부유감
        static void ConfigureParticles(ParticleSystem ps, GameObject go) {
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.7f, 0.9f, 0.92f, 0.16f), new Color(0.55f, 0.78f, 0.82f, 0.10f));
            main.gravityModifier = -0.02f;        // 아주 약하게 상승(부유)
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // 카메라가 움직여도 입자는 월드에 남음
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 11f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 13f, 1f);   // 화면보다 넓게 — 가장자리 비지 않게

            // 미세한 흔들림(물살) — Noise
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.3f;

            // 수명에 따라 페이드아웃(자연스러운 소멸)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // 렌더러 — 기본 Sprites 머티리얼(추가 에셋 불필요), 약간 가산 느낌
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) {
                    renderer.sharedMaterial = new Material(shader);
                }
                renderer.sortingOrder = 5;
            }
        }
    }
}
