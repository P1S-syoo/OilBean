using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Items;
using Game.Stage;
using Game.Craft;
using Game.Player;

namespace Game.Juice {
    // 게임필 코디네이터 — 핵심 행동 이벤트에 시각 피드백(플래시·셰이크·히트포즈·파티클 버스트) 부여
    // 사운드는 GameAudio가 같은 이벤트로 별도 처리. 이 컴포넌트는 '보는' 피드백 전담
    public class JuiceController : MonoBehaviour {
        [SerializeField] Collector collector;
        [SerializeField] HazardDetector hazard;
        [SerializeField] PurifyInstaller purify;
        [SerializeField] Crafting crafting;
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Transform player;        // 2.5D 잠수정 — 버스트 위치 기준
        [SerializeField] Transform purifySpot;    // 정화 지점 — 정화 버스트 위치
        [SerializeField] ScreenFlash flash;
        [SerializeField] CameraShaker shaker;
        [SerializeField] ParticleSystem burst;    // 재사용 스파클 버스트

        static readonly Color ColCollect = new Color(0.18f, 0.77f, 0.71f);   // 청록
        static readonly Color ColHazard  = new Color(0.85f, 0.27f, 0.24f);   // 적
        static readonly Color ColPurify  = new Color(0.37f, 0.85f, 0.80f);   // 밝은 청록
        static readonly Color ColClear   = Color.white;

        void OnEnable() {
            if (collector != null) {
                collector.OnCollect += OnCollect;
            }
            if (hazard != null) {
                hazard.OnHit += OnHazard;
            }
            if (purify != null) {
                purify.OnPurified += OnPurified;
            }
            if (crafting != null) {
                crafting.OnBuoyCrafted += OnBuoy;
            }
            if (bootstrap != null) {
                bootstrap.OnStateChanged += OnState;
            }
        }

        void OnDisable() {
            if (collector != null) {
                collector.OnCollect -= OnCollect;
            }
            if (hazard != null) {
                hazard.OnHit -= OnHazard;
            }
            if (purify != null) {
                purify.OnPurified -= OnPurified;
            }
            if (crafting != null) {
                crafting.OnBuoyCrafted -= OnBuoy;
            }
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= OnState;
            }
        }

        // 수집 — 가벼운 청록 플래시 + 잠수정 위치 스파클
        void OnCollect(ResourceKind kind) {
            Flash(ColCollect, 0.16f, 0.22f);
            BurstAt(player);
        }

        // 오염원 충돌 — 강한 적색 플래시 + 셰이크 + 히트포즈(임팩트)
        void OnHazard() {
            Flash(ColHazard, 0.5f, 0.4f);
            if (shaker != null) {
                shaker.Shake(0.55f, 0.35f);
            }
            StartHitPose(0.09f);
        }

        // 정화 완료 — 밝은 청록 플래시 + 셰이크 + 정화지점 버스트
        void OnPurified() {
            Flash(ColPurify, 0.45f, 0.55f);
            if (shaker != null) {
                shaker.Shake(0.4f, 0.4f);
            }
            BurstAt(purifySpot != null ? purifySpot : player);
        }

        // 부유체 제작 — 짧은 청록 플래시(거점 피드백)
        void OnBuoy() {
            Flash(ColCollect, 0.22f, 0.3f);
        }

        // 스테이지 클리어 — 흰 플래시 + 셰이크 + 히트포즈
        void OnState(GameState from, GameState to) {
            if (to == GameState.Clear) {
                Flash(ColClear, 0.6f, 0.8f);
                if (shaker != null) {
                    shaker.Shake(0.5f, 0.5f);
                }
                StartHitPose(0.08f);
            }
        }

        void Flash(Color c, float peak, float dur) {
            if (flash != null) {
                flash.Flash(c, peak, dur);
            }
        }

        void BurstAt(Transform t) {
            if (burst == null || t == null) {
                return;
            }
            burst.transform.position = new Vector3(t.position.x, t.position.y, t.position.z - 1f);
            burst.Play();
        }

        void StartHitPose(float dur) {
            try {
                StartCoroutine(HitPose(dur));
            } catch (System.Exception e) {
                Debug.LogError($"[JuiceController] 히트포즈 실패: {e.Message}");
                Time.timeScale = 1f;
            }
        }

        // 짧은 슬로우모션 후 복원 — 입력 타격감. realtime으로 대기(timeScale 영향 제거)
        IEnumerator HitPose(float dur) {
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(dur);
            Time.timeScale = 1f;
        }
    }
}
