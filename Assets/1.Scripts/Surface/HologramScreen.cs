using UnityEngine;
using Game.Core;

namespace Game.Surface {
    // 홀로그램 스크린 연출 — 알파 깜빡임 + 가끔 강한 글리치로 "지지직" 느낌(셰이더 없이 MPB)
    public class HologramScreen : MonoBehaviour {
        [SerializeField] Renderer rend;
        [SerializeField] float baseAlpha = 0.88f;
        [SerializeField] float flickerAmount = 0.16f;   // 미세 깜빡임 폭
        [SerializeField] float glitchChance = 0.93f;    // 이 이상이면 강한 글리치(0~1)
        [SerializeField] 연출설정 config;               // 연출 설정 — 연결 시 홀로그램 깜빡임 적용

        MaterialPropertyBlock mpb;
        Renderer[] renderers;
        float t, seed;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake() {
            ApplyConfig();
            if (rend == null) {
                rend = GetComponent<Renderer>();
            }
            renderers = GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
            seed = transform.position.x * 0.37f + transform.position.z * 0.91f;   // 개체별 위상
        }

        // 홀로그램 연출 설정 적용 — 미연결 시 SO 기본값 사용
        void ApplyConfig() {
            var cfg = config != null ? config : 연출설정.기본;
            baseAlpha = cfg.홀로그램기본알파;
            flickerAmount = cfg.홀로그램깜빡임폭;
            glitchChance = cfg.홀로그램글리치기준;
        }

        void Update() {
            if ((renderers == null || renderers.Length == 0) && rend == null) {
                return;
            }
            t += Time.deltaTime;
            // 지지직 — 빠른 펄린 노이즈성 깜빡임
            float flicker = Mathf.PerlinNoise(t * 13f + seed, seed);
            float a = baseAlpha - flickerAmount * flicker;
            // 가끔 강한 글리치(짧게 크게 어두워짐)
            if (Mathf.PerlinNoise(t * 2.7f, seed + 5f) > glitchChance) {
                a *= 0.35f;
            }
            var c = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            if (renderers != null && renderers.Length > 0) {
                foreach (var r in renderers) {
                    ApplyAlpha(r, c);
                }
            } else {
                ApplyAlpha(rend, c);
            }
        }

        void ApplyAlpha(Renderer r, Color c) {
            if (r == null) {
                return;
            }
            if (mpb == null) {
                mpb = new MaterialPropertyBlock();
            }
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);   // URP Unlit
            mpb.SetColor(ColorId, c);       // 레거시 호환
            r.SetPropertyBlock(mpb);
        }
    }
}
