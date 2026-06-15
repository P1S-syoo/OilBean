using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;
using Game.Core;

namespace Game.Juice {
    // 수중 분위기 — 글로벌 Volume(탁함 그레이드) weight를 상태로 보간. 탐사=탁함, 정화/클리어=맑음
    // ClearView의 카메라 배경 트릭을 대체하는 화면 전체 전환(C1 근본 해결)
    public class AtmosphereController : MonoBehaviour {
        [SerializeField] Volume volume;          // 수중 탁함 그레이드(글로벌)
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] float murkyWeight = 1f; // 탐사 중 탁함 강도
        [SerializeField] float blend = 1.3f;     // 탁함↔맑음 전환 시간(초)

        void Start() {
            try {
                if (volume == null) {
                    var go = GameObject.Find("AtmosphereVolume");
                    if (go != null) {
                        volume = go.GetComponent<Volume>();
                    }
                }
                if (bootstrap == null) {
                    bootstrap = FindFirstObjectByType<GameBootstrap>();
                }
                if (bootstrap != null) {
                    bootstrap.OnStateChanged += OnState;
                    ApplyImmediate(bootstrap.State);   // 시작 상태(수상)는 맑음으로 즉시 세팅
                } else {
                    Debug.LogWarning("[AtmosphereController] GameBootstrap 미발견 — 분위기 전환 불가");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[AtmosphereController] 초기화 실패: {e.Message}");
            }
        }

        void OnDestroy() {
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= OnState;
            }
            if (volume != null) {
                DOTween.Kill(volume);
            }
        }

        void OnState(GameState from, GameState to) {
            TweenWeight(TargetFor(to));
        }

        // 탐사(Dive)만 탁함, 나머지(거점·연구·제작·클리어·수상)는 맑음 — 정화 후 Dive→Clear에서 탁함이 걷힘
        float TargetFor(GameState s) {
            return s == GameState.Dive ? murkyWeight : 0f;
        }

        void ApplyImmediate(GameState s) {
            if (volume != null) {
                volume.weight = TargetFor(s);
            }
        }

        void TweenWeight(float target) {
            if (volume == null) {
                return;
            }
            DOTween.Kill(volume);
            DOTween.To(() => volume.weight, w => volume.weight = w, target, blend)
                .SetUpdate(true)
                .SetTarget(volume);
        }
    }
}
