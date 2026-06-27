using UnityEngine;
using UnityEngine.InputSystem;
using Game.Items;
using Game.Player;
using Game.Stage;
using Game.UI;
using Game.Craft;
using Game.Surface;

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
        [SerializeField] SurfaceBootstrap surface; // 수상 정화구역 도착 게이트
        [SerializeField] Toast toast;              // 행동 피드백 배너
        [SerializeField] bool autoStartDive;      // 3D 씬: 시작 시 바로 탐사 진입
        [SerializeField] bool startOnSurface;     // 수상 항해(Surface)로 시작 — 잠수 전까지 탐사 루프 잠금
        [SerializeField] 수집설정 config;       // 수집 설정 — 연결 시 손실 비율·적재 한계 적용(미연결 시 SO 기본값)

        GameFsm fsm;
        Vector3 dockPos;
        GameState hubOrigin = GameState.Dock;   // 연구/제작을 연 출발 상태 — 닫을 때 이 상태로 복귀
        InputAction researchInput;               // 수상/거점에서 연구 열기(1)
        InputAction craftInput;                  // 수상/거점에서 제작 열기(2)

        // 지연 생성 — EditMode 테스트처럼 Awake 없이 접근해도 동작(기존 필드 초기화와 동등)
        GameFsm Fsm {
            get {
                if (fsm == null) {
                    fsm = new GameFsm(startOnSurface ? GameState.Surface : GameState.Dock);
                }
                return fsm;
            }
        }
        InputAction returnInput;   // 플레이어 수동 복귀(R)

        public GameState State => Fsm.Current;
        public RunData Run => run;

        // 상태 전환 알림 — FSM 비공개를 유지한 채 오디오 등 외부 시스템이 구독
        public event System.Action<GameState, GameState> OnStateChanged;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : 수집설정.기본;
                forfeitRatio = cfg.손실비율;
                if (run == null) {
                    Debug.LogWarning("[GameBootstrap] RunData 미연결 — 인스펙터에서 할당하세요.");
                } else {
                    run.ResetRun();   // 세션 시작 시 초기화(자산 persist 잔재 제거)
                    run.SetMaxWeightBase(cfg.최대적재);   // 적재 한계 통합 설정(ResetRun 후 덮어씀)
                }
                // 컴포넌트 래치도 세션 초기화(재시작 시 클리어/정화 재현 가능)
                if (clearView != null) clearView.ResetState();
                if (purify != null) purify.ResetState();
                dockPos = sub != null ? sub.position : Vector3.zero;
                Fsm.OnChanged += HandleStateChanged;
                // 강제 복귀 트리거 구독(S2 이벤트 소비)
                if (battery != null) {
                    battery.OnEmpty += OnBatteryEmpty;
                    battery.SetDraining(Fsm.Current == GameState.Dive);   // 초기(Dock)엔 소모 off
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
                if (surface == null) {
                    surface = FindFirstObjectByType<SurfaceBootstrap>();
                }
                if (hazard != null) hazard.OnHit += OnHazardHit;
                if (purify != null) {
                    purify.OnPurified += OnPurified;
                    purify.SetArmed(Fsm.Current == GameState.Dive);   // 초기 Dock이면 설치 비활성
                }
                // 수동 복귀 입력(R) — 적재 한계 등에서 자발적 복귀
                returnInput = new InputAction("Return", InputActionType.Button, "<Keyboard>/r");
                returnInput.performed += OnReturnInput;
                returnInput.Enable();
                // 수상(정화선 거점)·Dock에서 연구/제작 열기 — 기획서의 '거점에서 연구·제작'
                researchInput = new InputAction("Research", InputActionType.Button, "<Keyboard>/1");
                researchInput.performed += OnResearchInput;
                researchInput.Enable();
                craftInput = new InputAction("Craft", InputActionType.Button, "<Keyboard>/2");
                craftInput.performed += OnCraftInput;
                craftInput.Enable();
                Debug.Log($"[GameBootstrap] 시작 상태: {Fsm.Current}");
                // 셰이더 깨짐 폴백(버전 안전망) — 씬 루트 전체에서 깨진 머티리얼 교체
                foreach (var go in gameObject.scene.GetRootGameObjects()) {
                    ShaderGuard.FixBroken(go);
                }
            } catch (System.Exception e) {
                Debug.LogError($"[GameBootstrap] Awake 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // 온보딩 안내 1회용 플래그 — 같은 안내가 반복되지 않도록
        bool diveHintShown;
        bool returnHintShown;

        // E6 push-your-luck — 강제 복귀 시 이번 탐사 미정착분 손실 비율(좌절 없는 선, 플레이로 조정)
        // 기본값은 수집설정.손실비율 (Awake에서 적용)
        [SerializeField] float forfeitRatio;
        bool forcedReturn;   // 직전 복귀가 강제(방전·충돌)였는지

        void Start() {
            // 3D 씬: 거점 UI 없이 시작 즉시 탐사 진입 (수상 시작이면 잠수 전까지 보류)
            if (autoStartDive && !startOnSurface) {
                StartDive();
            }
            // 인트로 안내 — 수상 거점에서 무엇을 할지(이동→정화 지점에서 잠수, 거점 메뉴)
            if (startOnSurface && toast != null) {
                toast.Show("정화선 거점 — 정화구역 도착 후 E로 거점 패널 열기");
            }
        }

        // 복귀 거점 좌표 갱신 — 잠수 인계 시 수면 모선(3D 잠수정) 아래 지점으로 지정
        public void SetDockPoint(Vector3 p) {
            dockPos = p;
        }

        // 수상에서 잠수 — Surface→Dock 인계(카메라 블렌드·하강 연출은 W5에서 확장)
        public bool EnterDockFromSurface() {
            if (!Fsm.Change(GameState.Dock)) {
                Debug.LogWarning("[GameBootstrap] 잠수 인계 거부됨 — 현재 상태: " + Fsm.Current);
                return false;
            }
            if (autoStartDive) {
                StartDive();   // 잠수 직후 바로 탐사 진입
            }
            return true;
        }

        void OnDestroy() {
            // 파괴 시점엔 새로 만들지 않도록 필드로 접근
            if (fsm != null) {
                fsm.OnChanged -= HandleStateChanged;
            }
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
            if (researchInput != null) {
                researchInput.performed -= OnResearchInput;
                researchInput.Disable();
                researchInput.Dispose();
            }
            if (craftInput != null) {
                craftInput.performed -= OnCraftInput;
                craftInput.Disable();
                craftInput.Dispose();
            }
        }

        // 수상/거점에서만 연구·제작 열기(1/2키) — 탐사 중엔 무시
        void OnResearchInput(InputAction.CallbackContext ctx) {
            if (HubInputAllowed()) {
                GoResearch();
            }
        }
        void OnCraftInput(InputAction.CallbackContext ctx) {
            if (HubInputAllowed()) {
                GoCraft();
            }
        }

        // 1/2키는 거점 또는 정화구역 도착 후 수면에서만 허용
        bool HubInputAllowed() {
            if (Fsm.Current == GameState.Dock) {
                return true;
            }
            if (Fsm.Current != GameState.Surface) {
                return false;
            }
            if (surface == null) {
                surface = FindFirstObjectByType<SurfaceBootstrap>();
            }
            return surface != null && surface.DiveReady;
        }

        void OnBatteryEmpty() => ForceReturn("배터리 방전");
        // 적재 한계 — 강제 복귀 대신 경고만(복귀는 R키로 플레이어 선택)
        void OnInvFull() { if (toast != null) toast.Show("적재 한계 — R키로 정화선 복귀"); }
        void OnHazardHit() {
            // 내압 프레임 장착 시 충돌 1회 흡수(장갑 소진)
            if (run != null && run.ConsumeHullArmor()) {
                if (toast != null) toast.Show("내압 프레임 — 충돌 흡수");
                return;
            }
            // 충돌 페널티 완화 — 강제 복귀 대신 배터리 10% 손실(피격 부담↓)
            if (battery != null) {
                battery.Drain(battery.Max * 0.1f);
            }
            if (toast != null) {
                toast.Show("오염원 충돌 — 배터리 10% 손실");
            }
        }
        // 플레이어 수동 복귀(R) — 탐사 중엔 거점 복귀, 클리어 후엔 수면 복귀(W6)
        void OnReturnInput(InputAction.CallbackContext ctx) {
            if (Fsm.Current == GameState.Dive) {
                ReturnDock();
            } else if (Fsm.Current == GameState.Clear) {
                Fsm.Change(GameState.Surface);
            }
        }
        // 행동 피드백 토스트(그동안 미구독이던 이벤트 소비)
        void OnCollected(ResourceKind k) {
            if (toast != null) {
                toast.Show(k == ResourceKind.Scrap ? "고철: 장비 제작 재료입니다." : "오염 샘플: 연구 재료입니다.");
            }
        }
        void OnUnlocked() { if (toast != null) toast.Show("분석 완료 — 정화 약품 해금"); }
        // C3 온보딩 — 제작 후 '무엇을 할지'를 명확히(잠수→정화 지점 설치)
        void OnBuoyCrafted() { if (toast != null) toast.Show("정화 부유체 완성 — 잠수해 정화 지점(양화대교 잔해)에서 F 홀드로 설치하세요"); }
        void OnUpgraded() { if (toast != null) toast.Show("탐사 기계 업그레이드"); }
        // 정화 완료 — 부유체 Ⅲ(3단계) 설치만 스테이지 클리어, 그 전 단계는 구역 정화(다음 단계 유도)
        void OnPurified() {
            if (run != null && run.BuoyStage < 3) {
                if (toast != null) {
                    toast.Show($"구역 정화 — 부유체 {run.BuoyStage}단계. 더 깊은 곳으로");
                }
                // 다음 단계 부유체를 같은 스팟에 재설치할 수 있도록 재무장(done 래치만 해제) — 미수정 시 클리어 도달 불가
                if (purify != null) {
                    purify.ReArm();
                }
                return;
            }
            if (!Fsm.Change(GameState.Clear)) {
                Debug.LogWarning($"[GameBootstrap] 정화 완료했으나 Clear 전환 거부됨(현재 {Fsm.Current})");
            }
        }

        // 탐사 강제 종료 → 거점 복귀
        public void ForceReturn(string reason) {
            // 클리어/거점 상태면 무시 — 정화 완료(Clear)와 강제 복귀가 같은 프레임에 경합해도 Clear 보존
            if (Fsm.Current == GameState.Clear || Fsm.Current == GameState.Dock) {
                return;
            }
            Debug.Log($"[GameBootstrap] 강제 복귀: {reason}");
            forcedReturn = true;   // 복귀 정산에서 미정착분 페널티 적용(E6)
            // Dock에서 정산(배터리 충전·미정착 정산) 후 수면(Surface)으로 부상 — 재잠수는 E
            // (SurfaceBootstrap이 Surface 진입을 감지해 수상 리그 재활성 + 항해 재개)
            if (Fsm.Change(GameState.Dock)) {
                Fsm.Change(GameState.Surface);
            }
        }

        void HandleStateChanged(GameState from, GameState to) {
            Debug.Log($"[GameBootstrap] 전환: {from} → {to} | 무게 {run?.Weight ?? 0}/{run?.MaxWeight ?? 0}");
            OnStateChanged?.Invoke(from, to);
            if (battery != null) {
                battery.SetDraining(to == GameState.Dive);   // 탐사 중에만 소모
            }
            if (hazard != null) {
                hazard.SetArmed(to == GameState.Dive);   // 탐사 중에만 발화(Dock 복귀 후 재발화 방지)
            }
            if (mover != null) {
                mover.SetDepthGate(to == GameState.Dive);   // 탐사 중에만 수심 게이트(거점/연출 좌표와 충돌 방지)
            }
            if (purify != null) {
                purify.SetArmed(to == GameState.Dive);   // 탐사 중에만 설치 가능(복귀 시 진행 취소)
            }
            // 첫 잠수 조작 안내 — 이동·수집·복귀 키
            if (to == GameState.Dive && !diveHintShown) {
                diveHintShown = true;
                if (toast != null) {
                    toast.Show("탐사 시작 — WASD 이동 · 자원 수집 · 배터리·오염원 주의 · R 거점 복귀");
                }
            }
            if (to == GameState.Dock) {
                OnReturnedToDock();
                // 첫 복귀 안내 — 거점에서 강화 루프(연구·제작) 진입
                if (from == GameState.Dive && !returnHintShown) {
                    returnHintShown = true;
                    if (toast != null) {
                        toast.Show("거점 복귀 — 1 연구로 정화 약품 해금 · 2 제작으로 부유체 강화");
                    }
                }
            }
            if (to == GameState.Clear && clearView != null) {
                if (mover != null) {
                    mover.Halt();   // 클리어 시 정지
                }
                clearView.Play();   // 정화 연출 + STAGE CLEAR
                if (toast != null) {
                    toast.Show("스테이지 클리어 — R키로 수면 복귀");
                }
            }
            // 수면 복귀(W6) — 클리어 오버레이 숨기고 다음 구역 항해 재개(SurfaceBootstrap이 리그 재활성)
            if (to == GameState.Surface && clearView != null) {
                clearView.ResetState();
            }
        }

        // 거점 복귀 정산 — 위치/관성 리셋 + 배터리 재충전 + 탐사 적재 비우기(거점 보유분은 유지)
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
            if (run != null) {
                // E6 정산 — 강제복귀면 미정착분 일부 손실, 정상복귀면 전량 확정
                if (forcedReturn) {
                    int lost = run.ForfeitDive(forfeitRatio);
                    if (toast != null) {
                        toast.Show(lost > 0 ? $"강제 복귀 — 미정착 자원 {lost}개 유실 (입고분은 보존)" : "강제 복귀 — 거점 입고분은 보존");
                    }
                } else {
                    run.CommitDive();   // 정상 복귀 — 이번 탐사분 확정
                }
                run.ClearCarry();   // 다음 탐사를 위해 세션 적재 초기화(입고분은 거점에 남음)
            }
            forcedReturn = false;   // 정산 후 플래그 해제
        }

        // 인스펙터 우클릭으로 흐름 검증(입력 백엔드 불필요)
        [ContextMenu("탐사 시작")] public void StartDive() => Fsm.Change(GameState.Dive);
        // 수동 복귀(R)도 Dock 정산(전량 확정) 후 수면(Surface) 부상으로 통일 — 강제복귀와 동선 일치, 재잠수는 E
        [ContextMenu("거점 복귀")] public void ReturnDock() {
            if (Fsm.Change(GameState.Dock)) {
                Fsm.Change(GameState.Surface);
            }
        }
        // 연구/제작 — 연 출발 상태(Surface/Dock)를 기억해 닫을 때 복귀
        [ContextMenu("연구")] public void GoResearch() {
            if (Fsm.Current == GameState.Surface || Fsm.Current == GameState.Dock) {
                hubOrigin = Fsm.Current;
            }
            Fsm.Change(GameState.Research);
        }
        [ContextMenu("제작")] public void GoCraft() {
            if (Fsm.Current == GameState.Surface || Fsm.Current == GameState.Dock) {
                hubOrigin = Fsm.Current;
            }
            Fsm.Change(GameState.Craft);
        }
        // 연구/제작 패널 닫기 — 연 출발 상태로 복귀(Dock 또는 Surface)
        [ContextMenu("거점 복귀(연구/제작 닫기)")] public void CloseHub() => Fsm.Change(hubOrigin);
        [ContextMenu("정화 설치")] public void GoPurify() => Fsm.Change(GameState.Purify);
        [ContextMenu("클리어")] public void ClearStage() => Fsm.Change(GameState.Clear);
    }
}
