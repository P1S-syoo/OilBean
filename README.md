# OilBean — 한강 오염 탐사

2.5D 수중 탐사 게임. 오염된 한강 바닥을 탐사하며 수집·연구·제작·정화하는 루프.
(맵=3D 복셀 블록, 캐릭터·UI=2D/3D, Dave the Diver풍 스타일라이즈드 + VARCO 리얼 텍스처)

## 요구사항

- **Unity 6000.3.11f1** (정확히 이 버전 권장 — Unity Hub에서 설치)
- 첫 실행 시 **인터넷 필요** (git/레지스트리 패키지 자동 해석)

## 다른 PC에서 셋업 (당겨오면 바로 동작)

```bash
git clone https://github.com/P1S-syoo/OilBean.git
```

1. **Unity Hub → Add** 으로 클론한 폴더 열기 (Unity 6000.3.11f1).
2. 첫 오픈 시 Unity가 자동으로:
   - `Library/`·`Temp/` 등 재생성 (gitignore로 제외돼 있음 — 정상)
   - 패키지 해석: Unity 레지스트리 + git 의존성
     (`unity-mcp`, `urpvolumetricfog`) + **임베드된 `VARCO3D Bridge`**(`Packages/`)
   - USD Importer 의존성 다운로드(첫 설치 수 분 소요 가능)
3. **`Assets/2.Scenes/Main3D.unity`** 열고 Play.

> 별도 임포트/설정 불필요. 패키지·에셋이 전부 포함/자동해석됩니다.

## 구조

| 폴더 | 내용 |
|------|------|
| `Assets/1.Scripts/` | 게임 코드 (Core/Player/Items/World/UI/Stage/Craft) |
| `Assets/2.Scenes/Main3D.unity` | 메인 씬 |
| `Assets/4.Art/` | 모델·텍스처·머티리얼·셰이더(triplanar 지형 등) |
| `Assets/9.Tests/` | EditMode/PlayMode 테스트 |
| `Packages/com.ncai.varco3d.bridge/` | VARCO3D Bridge (임베드) |
| `docs/배치규칙.md` | 씬 배치·블록·섹션 규칙 문서 |

## 핵심 시스템

- **절차 생성 지형**(`World/WorldGen`) + **청크 스트리밍/풀링**(`ChunkStreamer`) — 대규모 맵 메모리 상수
- **섹션 시퀀스**(통로/터널/개방) + 경계 블렌드, **해수면 위 강변 백드롭**
- **triplanar 월드 UV 지형 셰이더**(`4.Art/Shaders/TriplanarTerrain`) — VARCO 텍스처 연속 매핑
- **캐릭터 애니**(Mixamo Humanoid, Idle↔Swim Animator)

> 제외(gitignore): `Library/`, `Logs/`, `Assets/Screenshots/`(디버그 캡처), `.codegraph/`, `.omc/` — 모두 재생성/불필요.
