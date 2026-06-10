# 한강 오염 탐사 — 개발 핸드오프 (세션 연속용)

> 다음 세션에서 이 파일 + `한강오염탐사_plan.json`(편집) + `한강오염탐사_구현계획.md`(렌더) + 전역 메모리 `project_hangang_unity.md`를 읽고 이어서 개발.

## ▶ 다음 세션 시작 멘트 (이걸 그대로 붙여넣기)
```
/Users/syoo/Project4 한강 오염 탐사 Unity 프로젝트 이어서 개발.
먼저 DEV_HANDOFF.md + 한강오염탐사_plan.json 읽고 현재 상태 파악해.
Unity MCP(unity=mcp-for-unity 9.7.1) set_unity_project_root /Users/syoo/Project4 하고
read_console로 컴파일 확인. unity-developer 스킬로 S6(정화 설치+클리어, 코드는 작성됨/씬배선·테스트 남음)부터 이어서.
검증은 EditMode+execute_code 위주 + PlayMode 1회, 포커스 강제(osascript) 금지. DOTween 사용 가능.
```
(agentmemory도 켜져 있으면 `/agentmemory:handoff` 또는 `/agentmemory:recall "한강 정화 설치 S6"`로 보조. 단 최근 작업은 인덱싱이 약하니 이 파일이 1차 기준.)

## 프로젝트 사실
- 경로(절대): **`/Users/syoo/Project4`** (⚠️ `~/syoo/...` 아님 — `/Users/syoo/syoo`로 풀림)
- 엔진: **Unity 6000.3.11f1**, 2D URP. inputsystem 1.19 / URP 17.3 / test-framework 1.6.
- **DOTween Pro 설치됨** (`Assets/Plugins/Demigiant/`, DG.Tweening 사용 가능, Setup 완료). 연출을 DOTween 트윈으로 작성 가능.
- Unity MCP: 서버 `unity` = **mcp-for-unity (mcpforunityserver==9.7.1)**. 사용 전 필요시 `set_unity_project_root /Users/syoo/Project4`.
- 스킬 체인: unity-planner → unity-developer → unity-varco. 코드 컨벤션: K&R·한 줄 한글 주석·try-catch+로그·짧은 네이밍·decision-tree 패턴.

## 진행 상태 (2026-06-04 기준)
- ✅ **S0~S5 done** (전체 22/31, 71%). 핵심 탐사 루프 + 연구/제작 완성.
  - S0 상태머신(GameFsm)·RunData / S1 이동(PlayerMove·CamFollow) / S2 수집·인벤토리·배터리(Collector·Battery·Hud) / S3 오염원·강제복귀(Hazard·HazardDetector·GameBootstrap 코디네이터) / S4 연구(Research·ResearchPanel) / S5 제작(Crafting·CraftPanel).
- 🟨 **S6 진행 중** (정화 설치 + 클리어): **코드 작성·컴파일 OK**(타입 로드 확인), **씬 배선·테스트 미완**.
  - 작성됨: `Stage/PurifyInstaller.cs`, `UI/ClearView.cs`(카메라 배경색 보간—DOTween 교체 가능), `GameFsm` Dive→Clear 추가, `GameBootstrap` OnPurified→Clear+ClearView.Play 배선, `Hud` 정화 게이지, `9.Tests/PlayMode/PurifyInstallerTests.cs`.
  - **남은 일**: ① EditMode/PlayMode 실행 ② 씬에 정화 스팟(PurifyInstaller)+ClearView(카메라/clearText/파티클)+HUD purifyFill 배선 + GameBootstrap purify/clearView 참조 ③ 전체 흐름 검증(부유체 준비→스팟 설치→정화→STAGE CLEAR) ④ progress.py S6=done.
  - (선택) ClearView 연출을 DOTween `cam.DOColor`로 교체.

## 밸런스(A안 적용됨)
- 배터리 drainPerSec 3.33(~30초), 수집물 11개(총 80kg, 한계 50 도달 가능), 오염원 3개.
- **B안(보류)**: 자발적 복귀 버튼 + 강제복귀 시 자원 ~50% 손실(push-your-luck). 기획서가 "손상 추후"라 미도입 — S6 이후 검토 후보.

## 미해결/주의
- 연구 퍼즐·제작 퍼즐은 플레이스홀더(버튼). 규칙 확정 시 Research.Analyze/Crafting 내부 교체(UI·해금 배선 재사용).
- 함정: Input System 프로젝트라 EventSystem은 **InputSystemUIInputModule**(스킬 함정#9). 컴파일 유발 작업 직후 에디터 **포그라운드 유지**(백그라운드 리로드 멈춤→어셈블리 멤버십 꼬임. 풀리면 에디터 재시작).

## 검증 규약
- 모든 검증 Unity MCP로: read_console(에러0) → run_tests(EditMode/PlayMode) → execute_code 흐름검증. 포커스 강제(osascript) 금지.
- 테스트 현황: EditMode 23, PlayMode 11 (S6 추가 시 +PurifyInstaller 2 등).
