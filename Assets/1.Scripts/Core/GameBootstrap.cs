using UnityEngine;
using UnityEngine.InputSystem;
using Game.Items;
using Game.Player;
using Game.Stage;
using Game.UI;
using Game.Craft;

namespace Game.Core {
    // 게임 진입점 겸 코디네이터 — FSM/RunData 소유 + 강제 복귀(배터리·적재·충돌) 배선
    public class GameBootstrap : MonoBehaviour {
        [SerializeField] RunData run;             // 6.Data의 RunData 에셋
        [SerializeField] Battery battery;         // 탐사 제한 게이지
        [SerializeField] Collector collector;     // 수집기
        [SerializeField] HazardDetector hazard;   // 오염원 충돌 감지
        [SerializeField] PlayerMove mover;        // 복귀 시 정지
        [SerializeField] Transform sub;           // 복귀 시 원위치
        [SerializeField] PurifyInstaller purify;  // 정화 설치 스팟
        [SerializeField] ClearView clearView;     // 클리어 연출
        [SerializeField] Research research;        // 연구(해금 토스트용)
        [SerializeField] Crafting crafting;        // 제작(완료 토스트용)
        [SerializeField] Toast toast;              // 행동 피드백 배너
        [SerializeField] bool autoStartDive;      // 3D 씬: 시작 시 바로 탐사 진입

        readonly GameFsm fsm = new();
        Vector3 dockPos;
        InputAction returnInput;   // 플레이어 수동 복귀(R)

        public GameState State => fsm.Current;
        public RunData Run => run;

        void Awake() {
            try {
                if (run == null) {
                    Debug.LogWarning("[GameBootstrap] RunData 미연결 — 인스펙터에서 할당하세요.");
                } else {
                    run.ResetRun();   // 세션 시작 시 초기화(자산 persist 잔재 제거)
                }
                // 컴포넌트 래치도 세션 초기화(재시작 시 클리어/정화 재현 가능)
                if (clearView != null) clearView.ResetState();
                if (purify != null) purify.ResetState();
                dockPos = sub != null ? sub.position : Vector3.zero;
                fsm.OnChanged += OnStateChanged;
                // 강제 복귀 트리거 구독(S2 이벤트 소비)
                if (battery != null) {
                    battery.OnEmpty += OnBatteryEmpty;
                    battery.SetDraining(fsm.Current == GameState.Dive);   // 초기(Dock)엔 소모 off
                }
                if (collector != null) {
                    collector.OnFull += OnInvFull;
                    collector.OnCollect += OnCollected;   // 수집 토스트
                }
                if (research != null) research.OnUnlocked += OnUnlocked;
                if (crafting != null) {
                    crafting.OnBuoyCrafted += OnBuoyCrafted;
                    crafting.OnUpgraded += OnUpgraded;
                }
                if (hazard != null) hazard.OnHit += OnHazardHit;
                if (purify != null) {
                    purify.OnPurified += OnPurified;
                    purify.SetArmed(fsm.Current == GameState.Dive);   // 초기 Dock이면 설치 비활성
                }
                // 수동 복귀 입력(R) — 적재 한계 등에서 자발적 복귀
                returnInput = new InputAction("Return", InputActionType.Button, "<Keyboard>/r");
                returnInput.performed += OnReturnInput;
                returnInput.Enable();
                Debug.Log($"[GameBootstrap] 시작 상태: {fsm.Current}");
            } catch (System.Exception e) {
                Debug.LogError($"[GameBootstrap] Awake 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        void Start() {
            // 3D 씬: 거점 UI 없이 시작 즉시 탐사 진입
            if (autoStartDive) {
                StartDive();
            }
        }

        void OnDestroy() {
            fsm.OnChanged -= OnStateChanged;
            if (battery != null) battery.OnEmpty -= OnBatteryEmpty;
            if (collector != null) {
                collector.OnFull -= OnInvFull;
                collector.OnCollect -= OnCollected;
            }
            if (research != null) research.OnUnlocked -= OnUnlocked;
            if (crafting != null) {
                crafting.OnBuoyCrafted -= OnBuoyCrafted;
                crafting.OnUpgraded -= OnUpgraded;
            }
            if (hazard != null) hazard.OnHit -= OnHazardHit;
            if (purify != null) purify.OnPurified -= OnPurified;
            if (returnInput != null) {
                returnInput.performed -= OnReturnInput;
                returnInput.Disable();
                returnInput.Dispose();
            }
        }

        void OnBatteryEmpty() => ForceReturn("배터리 방전");
        // 적재 한계 — 강제 복귀 대신 경고만(복귀는 R키로 플레이어 선택)
        void OnInvFull() { if (toast != null) toast.Show("적재 한계 — R키로 정화선 복귀"); }
        void OnHazardHit() => ForceReturn("오염원 충돌");
        // 플레이어 수동 복귀(R) — 탐사 중에만 동작
        void OnReturnInput(InputAction.CallbackContext ctx) {
            if (fsm.Current == GameState.Dive) {
                ReturnDock();
            }
        }
        // 행동 피드백 토스트(그동안 미구독이던 이벤트 소비)
        void OnCollected(ResourceKind k) { if (toast != null) toast.Show(k == ResourceKind.Scrap ? "고철 수집" : "샘플 수집"); }
        void OnUnlocked() { if (toast != null) toast.Show("분석 완료 — 정화 약품 해금"); }
        void OnBuoyCrafted() { if (toast != null) toast.Show("정화 부유체 제작 완료"); }
        void OnUpgraded() { if (toast != null) toast.Show("탐사 기계 업그레이드"); }
        // 정화 완료 → 스테이지 클리어(전환 거부 시 진단 로그)
        void OnPurified() {
            if (!fsm.Change(GameState.Clear)) {
                Debug.LogWarning($"[GameBootstrap] 정화 완료했으나 Clear 전환 거부됨(현재 {fsm.Current})");
            }
        }

        // 탐사 강제 종료 → 거점 복귀
        public void ForceReturn(string reason) {
            // 클리어/거점 상태면 무시 — 정화 완료(Clear)와 강제 복귀가 같은 프레임에 경합해도 Clear 보존
            if (fsm.Current == GameState.Clear || fsm.Current == GameState.Dock) {
                return;
            }
            Debug.Log($"[GameBootstrap] 강제 복귀: {reason}");
            fsm.Change(GameState.Dock);
        }

        void OnStateChanged(GameState from, GameState to) {
            Debug.Log($"[GameBootstrap] 전환: {from} → {to} | 무게 {run?.Weight ?? 0}/{run?.MaxWeight ?? 0}");
            if (battery != null) {
                battery.SetDraining(to == GameState.Dive);   // 탐사 중에만 소모
            }
            if (purify != null) {
                purify.SetArmed(to == GameState.Dive);   // 탐사 중에만 설치 가능(복귀 시 진행 취소)
            }
            if (to == GameState.Dock) {
                OnReturnedToDock();
            }
            if (to == GameState.Clear && clearView != null) {
                if (mover != null) {
                    mover.Halt();   // 클리어 시 정지
                }
                clearView.Play();   // 정화 연출 + STAGE CLEAR
            }
        }

        // 거점 복귀 정산 — 위치/관성 리셋 + 배터리 재충전(자원은 RunData에 유지)
        void OnReturnedToDock() {
            if (sub != null) {
                sub.position = dockPos;
            }
            if (mover != null) {
                mover.Halt();
            }
            if (battery != null) {
                battery.Refill();
            }
        }

        // 인스펙터 우클릭으로 흐름 검증(입력 백엔드 불필요)
        [ContextMenu("탐사 시작")] public void StartDive() => fsm.Change(GameState.Dive);
        [ContextMenu("거점 복귀")] public void ReturnDock() => fsm.Change(GameState.Dock);
        [ContextMenu("연구")] public void GoResearch() => fsm.Change(GameState.Research);
        [ContextMenu("제작")] public void GoCraft() => fsm.Change(GameState.Craft);
        [ContextMenu("정화 설치")] public void GoPurify() => fsm.Change(GameState.Purify);
        [ContextMenu("클리어")] public void ClearStage() => fsm.Change(GameState.Clear);
    }
}
