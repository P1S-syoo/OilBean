using UnityEngine;

namespace Game.Core {
    // 통합 설정 자산 — 컴포넌트마다 흩어진 기본값을 한 곳에서 조절(피쳐별 그룹). 각 컴포넌트가 Awake/Start에서 읽어 덮어씀
    // 미연결 컴포넌트는 자체 [SerializeField] 기본값을 폴백으로 유지 — 기존 동작·테스트 보존
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
    public class GameConfig : ScriptableObject {

        [Header("이동/조작 — PlayerMove")]
        public float playerAccel = 30f;          // 가속력
        public float playerMaxSpeed = 6f;        // 최고 속도
        public float playerTurnSpeed = 360f;     // 기수 선회 각속도(deg/s)

        [Header("이동/조작 — DeckCharacter")]
        public float deckMoveSpeed = 1.6f;       // 덱 보행 속도
        public float deckTurnSpeed = 12f;        // 덱 회전 보간 속도

        [Header("이동/조작 — SubNavigator")]
        public float navSpeed = 4f;              // 순항 속도(m/s)
        public float navBrakeDistance = 8f;      // 목표 앞 감속 시작 거리(m)
        public float navMinSpeed = 0.4f;         // 감속 중 최저 속도
        public float navTurnSpeed = 2f;          // 회전 보간 속도

        [Header("이동/조작 — OrbitCameraDriver")]
        public float orbitSensX = 0.15f;         // 수평 감도
        public float orbitSensY = 0.1f;          // 수직 감도

        [Header("자원/배터리 — Battery")]
        public float batteryMax = 100f;          // 최대 용량
        public float batteryDrainPerSec = 3.33f; // 초당 소모량

        [Header("자원/배터리 — 적재/수집")]
        public float runMaxWeight = 70f;         // RunData 최대 적재 무게(kg)
        public float forfeitRatio = 0.35f;       // 강제 복귀 시 미정착분 손실 비율
        public float collectorPickupRadius = 1.5f; // Collector 수집 감지 반경

        [Header("잠수/연출 — SurfaceBootstrap")]
        public float surfaceBlendTime = 2.2f;    // 카메라 블렌드·하강 연출 시간
        public float surfaceDescendDepth = 8f;   // 자동 하강 깊이
        public float surfacePlungeTime = 1.1f;   // 덱 입수 연출 시간
        public float surfacePlungeDistance = 6f; // 입수 시 측면 도약 거리

        [Header("잠수/연출 — AtmosphereController")]
        public float atmosphereMurkyWeight = 1f; // 탐사 중 탁함 강도
        public float atmosphereBlend = 1.3f;     // 탁함↔맑음 전환 시간(초)

        [Header("잠수/연출 — CamFollow")]
        public float camSmoothTime = 0.18f;      // 카메라 추적 지연

        [Header("위험/스폰 — Hazard")]
        public float hazardSpeed = 2f;           // 오염원 이동 속도(레거시 왕복 — 미사용)
        public float hazardWarnTime = 0.8f;      // 돌진 전 경고(텔레그래프) 시간(s)
        public float hazardDashSpeed = 14f;      // 돌진 속도(u/s)
        public float hazardAttackInterval = 2.2f;// 돌진 공격 발생 간격(s)

        [Header("위험/스폰 — HazardStreamer")]
        public float hazardSurfaceDensity = 8f;  // 수면 근처 스폰 확률(%)
        public float hazardDeepDensity = 55f;    // 해저 근처 스폰 확률(%)
        public int hazardMaxPerCell = 2;         // 셀당 최대 슬롯 수

        [Header("위험/스폰 — CollectibleStreamer")]
        public int collectibleClusterDensity = 90; // 군집 채움 확률(%)
        public int collectibleOpenDensity = 26;    // 개방 수역 채움 확률(%)

        [Header("정화/점수 — PurifyInstaller")]
        public float purifyInstallTime = 2.5f;   // 설치 소요(초)

        [Header("정화/점수 — ScoreSystem")]
        public float scoreComboWindow = 3f;      // 콤보 유지 시간(초)
        public int scoreBaseValue = 100;         // 수집 1회 기본 점수
        public int scoreGradeS = 5000;           // S 등급 임계값
        public int scoreGradeA = 2500;           // A 등급 임계값

        [Header("내레이션 — 인트로 대사 (Surface 최초 진입)")]
        [TextArea] public string[] introLines = {
            "2042년, 한강은 죽었다.",
            "오염 물질이 강바닥을 덮고, 물고기는 사라졌다.",
            "당신은 정화선의 마지막 잠수사.",
            "잠수정을 타고 내려가 강을 되살려라.",
        };

        [Header("내레이션 — 클리어 대사 (Clear 진입)")]
        [TextArea] public string[] clearLines = {
            "정화 부유체가 자리를 잡았다.",
            "탁한 물이 조금씩 맑아지기 시작한다.",
            "강은 아직 기억하고 있었다 — 흐르는 법을.",
        };

        [Header("내레이션 — 출력")]
        public float narrationCharDelay = 0.04f; // 글자 간 딜레이(초)
    }
}
