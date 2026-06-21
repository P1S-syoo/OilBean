using UnityEngine;

namespace Game.Surface {
    // 홀로그램 스크린 연출 — 알파 깜빡임 + 가끔 강한 글리치로 "지지직" 느낌(셰이더 없이 MPB)
    public class HologramScreen : MonoBehaviour {
        [SerializeField] Renderer rend;
        [SerializeField] float baseAlpha = 0.88f;
        [SerializeField] float flickerAmount = 0.16f;   // 미세 깜빡임 폭
        [SerializeField] float glitchChance = 0.93f;    // 이 이상이면 강한 글리치(0~1)

        MaterialPropertyBlock mpb;
        float t, seed;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake() {
            if (rend == null) {
                rend = GetComponent<Renderer>();
            }
            mpb = new MaterialPropertyBlock();
            seed = transform.position.x * 0.37f + transform.position.z * 0.91f;   // 개체별 위상
        }

        void Update() {
            if (rend == null) {
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
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);   // URP Unlit
            mpb.SetColor(ColorId, c);       // 레거시 호환
            rend.SetPropertyBlock(mpb);
        }
    }
}
