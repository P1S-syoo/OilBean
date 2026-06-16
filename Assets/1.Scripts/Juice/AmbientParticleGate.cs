using UnityEngine;
using Game.Core;

namespace Game.Juice {
    // 부유입자 게이트 — 수중 분위기 입자를 탐사(Dive) 중에만 방출(수면 3D 단계에선 숨김)
    [RequireComponent(typeof(ParticleSystem))]
    public class AmbientParticleGate : MonoBehaviour {
        [SerializeField] GameBootstrap bootstrap;
        ParticleSystem ps;

        void Awake() {
            ps = GetComponent<ParticleSystem>();
        }

        void Start() {
            try {
                if (bootstrap == null) {
                    bootstrap = FindFirstObjectByType<GameBootstrap>();
                }
                if (bootstrap != null) {
                    bootstrap.OnStateChanged += OnState;
                    Apply(bootstrap.State);
                } else {
                    // bootstrap 미발견 — 안전 기본값으로 방출 정지(수면에 입자 뜨는 것 방지)
                    if (ps != null) {
                        var emission = ps.emission;
                        emission.enabled = false;
                    }
                    Debug.LogWarning("[AmbientParticleGate] GameBootstrap 미발견 — 입자 방출 정지(안전 기본값)");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[AmbientParticleGate] 초기화 실패: {e.Message}");
            }
        }

        void OnDestroy() {
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= OnState;
            }
        }

        void OnState(GameState from, GameState to) {
            Apply(to);
        }

        // 탐사 중에만 방출 — 그 외 상태는 정지(잔여 입자는 자연 소멸)
        void Apply(GameState s) {
            if (ps == null) {
                return;
            }
            var emission = ps.emission;
            emission.enabled = (s == GameState.Dive);
        }
    }
}
