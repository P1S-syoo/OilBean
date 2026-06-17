# OilBean UI 개선 리서치 종합 (2026-06-17)

세 갈래 리서치(현재 코드 진단 / uGUI 레이아웃 베스트프랙티스 / 게임 비주얼 디자인 원칙)를 교차 종합.
핵심: UI 문제는 전부 빌더 코드(UITheme + HudUIBuilder)에 집중 → 디자인 토큰 수정이 전 화면에 일괄 반영.

---

## 1. 현재 코드 진단 (designer, 파일·라인 근거)

| 순위 | 문제 | 근거 |
|---|---|---|
| 1 | 고채도 6색 동시노출 + 팔레트서 튀는 보라 + 하드코딩 RGBA | UITheme.cs:40–42, ScoreHudBuilder.cs:103, HudUIBuilder.cs:399 |
| 2 | 폰트 위계 붕괴(거의 15px), FontTitle 36f 미사용, 임의 40f/28f/6 | UITheme.cs:45–48, ScoreHudBuilder.cs:99,101,142 |
| 3 | 절대좌표·그리드밖 수치(-136f), LayoutGroup 전무, 패널 비율 제각각 | HudUIBuilder.cs:285,154, 0.68/0.4/0.3 분할 |
| 4 | 레거시 Shadow(블러 없음), 버튼 hover 15% Lerp 인지 불가, ✕ 글리프 깨짐 | UITheme.cs:90,191, HudUIBuilder.cs:764 |
| 5 | UiTween 미사용, 게이지 즉각변경(트윈 0), 점수 PunchScale 과함 | UiTween.cs:7, ScoreHud.cs:52 |
| 6 | BgDeep↔BgPanel 대비 1.7:1 (레이어감 없음) | UITheme.cs:16–18 |
| 7 | 수심 밴드 8px+반투명+영문 라벨 (정보 전달 실패) | HudUIBuilder.cs:345–358 |
| 8 | 타이틀 ■ 기호 (폰트 불일치·노이즈) | HudUIBuilder.cs:114,451,568 |
| 9 | 게이지 트랙 비가시(트랙-패널 대비 부족), 10px 높이 | UITheme.cs:62, HudUIBuilder.cs:299 |

## 2. uGUI 레이아웃 베스트프랙티스 (출처: Unity Docs/Learn, Medium, MoldStud)

- **Canvas Scaler**: ScaleWithScreenSize, Ref 1920×1080, Match 0.5 (Constant Pixel Size 금지)
- **앵커/피벗**: 정규화 좌표, RectTransform 정수 유지(소수점=서브픽셀 블러), Safe Area 컨테이너
- **LayoutGroup**: 동적/반복 목록 = Vertical/Horizontal/Grid LayoutGroup + ContentSizeFitter + LayoutElement. 고정 2~3개만 수동 앵커
- **9-slice**: 크기 가변 패널/버튼 = Image.type=Sliced, PPU 일치
- **8pt 그리드**: 모든 Padding/Spacing 8의 배수(4,8,16,24,32,48), 부모 패딩 ≥ 자식 간격
- **TMP**: auto-size는 단일요소만, outline 0.15, 행간 +10~20%, 등폭 숫자(tabular)
- **안티패턴**: 절대좌표/Constant Pixel/레거시 Text/Best Fit/무지개 색/raycastTarget 전부 켜기/소수점 Rect

출처: docs.unity3d.com (CanvasScaler·UIAutoLayout·9-slicing·TMP SDF), learn.unity.com, medium(@dariarodionovano), moldstud.com, rejuvenate.digital(8pt), uxplanet.org

## 3. 게임 비주얼 디자인 원칙 (출처: ColorArchive, Refactoring UI/Muzli, Player Research, Game UI Database)

### 색상 — 60/30/10 + 4계층
```
배경 60% : 딥 네이비 #08111C
패널 30% : 미드 틸  #0F2030
강조 10% : 발광 아쿠아 #00E5CC (인터랙션·CTA 전용)
의미색   : 위험 #FF4D4D / 경고 #FFB347 / 성공 #7AFF8C / 산소 #00E5CC
```
- 어두운 배경 가독성: 텍스트 아래 반투명 패드(rgba 0,0,0,0.55), 게임 UI 대비 7:1 권장
- 비비드 색은 액션 레이어에만 — 다른 곳 쓰면 게임월드↔UI 구분 붕괴

### 타이포 — 3단계 + 숫자 강조
```
수치(자원·점수) : 28–36px Bold/ExtraBold
헤더·레이블     : 18–22px SemiBold
본문·캡션       : 14–16px Regular (최소 16)
```
- 숫자 tabular figures(등폭) 필수, 단위는 한 tier 작게
- 한글: Pretendard(weight 9종) > Spoqa > NotoSansKR

### 깊이 — 다크테마는 그림자 말고 "밝기"
```
Elevation0 배경 #08111C → 1 패널 #0F2030 → 2 모달 #162A40 → 3 툴팁 #1E3650
```
- 글래스 패널: rgba(0,30,50,0.6) + blur + 아쿠아 1px 테두리 + 상단 inset 하이라이트
- 베젤/구분선 rgba(0,229,204,0.15), 상단→하단 미세 그라데이션

### HUD 배치
- 좌상 산소(최우선), 우상 수심/내비, 좌하 정화도/임무, 우하 인벤
- 주변시 = 색+형태+모션(텍스트 최소), 위험 시 펄스, 인접 게이지 시각 구분
- 산소: 원형 아크 게이지 > 수평 바, 값 따라 아쿠아→앰버→레드

### 레퍼런스
- **Dave the Diver**: 탐사 HUD 미니멀 ↔ 거점 UI 풍부 (모드별 정보밀도 분리)
- **Subnautica**: 평소 아이콘 → 메뉴 시 수치(2단계), 최상단 수심계, F6 토글
- **Deep Rock Galactic**: 베젤/금속 프레임 세계관 질감
- **Hollow Knight**: 게이지를 세계관 오브젝트로, 극미니멀

### 마이크로 인터랙션 (타이밍)
- 호버 100–150ms ease-out / 프레스 80–120ms ease-in / 패널 열기 200–300ms ease-out
- 버튼: hover scale1.04, press scale0.96 / 획득 spring overshoot / 위험 600ms 펄스
- 수중 특화: UI 등장 translateY(+20px)→0 ease-out 250ms "수면 부상"

---

## 4. 개선안 (A→B→C + 사용자 추가 제약)

### Phase A — 디자인 토큰 (위험 0, 효과 80%)
- A1 색 팔레트 60/30/10 재정의(보라 제거, 하드코딩 일원화)
- A2 폰트 위계 3단계 + 수치 크게(36px) + 등폭 숫자
- A3 깊이=밝기 elevation, 패널 아쿠아 테두리+상단 하이라이트
- A4 게이지 fillAmount 트윈(0.3s OutCubic), 위험 펄스, 점수 punch 완화

### Phase B — 레이아웃 구조 (근본)
- B1 반복요소 VerticalLayoutGroup+ContentSizeFitter
- B2 8pt 그리드 상수만, 그리드밖 수치 제거
- B3 Canvas Scaler(1920×1080 Match0.5), 9-slice(Image.Sliced), 장식 raycastTarget=false

### Phase C — 디테일·세계관
- C1 글래스모피즘 패널
- C2 수면 부상 모션 통일
- C3 정화도 게이지 = "오염 결정이 맑아지는" 세계관 오브젝트
- C4 Dive HUD(미니멀) ↔ Dock UI(풍부) 분리

### 사용자 추가 제약 (필수)
- **HUD 요소 비겹침** + 시각화 잘되게 (요소 간 충돌 0, 안전영역 준수)
- **패널 가로:세로 = 황금비(1.618:1)** 지향
- **폰트 크게** (수치 36px+, 본문도 키움)
- **폰트 에셋 교체는 사용자가 직접** → 코드는 크기 위계만, UIFont 경로 유지

출처 URL 전체는 각 리서치 에이전트 로그 참조(Unity Docs, Game UI Database, Refactoring UI, Player Research 등).
