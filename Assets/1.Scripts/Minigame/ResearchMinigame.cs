using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using Game.UI;

namespace Game.Minigame {
    // 오염 구조 연결 미니게임 — 팝업 안에 노드를 생성하고 드래그로 모두 연결하면 성공
    public class ResearchMinigame : MonoBehaviour,
            IPointerDownHandler, IDragHandler, IPointerUpHandler {

        // ── 내부 상태 ────────────────────────────────────────────────
        enum State { Closed, Open, Animating }

        // 노드 정보 구조체
        struct NodeInfo {
            public RectTransform rt;
            public Image img;
            public bool visited;
        }

        // ── 런타임 필드 ──────────────────────────────────────────────
        CanvasGroup _cg;           // 팝업 페이드/블록 용도
        State _state = State.Closed;
        Action _onSolved;          // 성공 콜백 (1회 호출 후 null)

        // 노드·선 목록 (매 Open마다 재생성)
        List<NodeInfo> _nodes = new List<NodeInfo>();
        List<GameObject> _lines = new List<GameObject>();

        // 드래그 추적
        bool _dragging;
        int _lastConnectedIndex = -1;   // 드래그 중 마지막으로 연결된 노드 인덱스
        int _visitedCount;              // 방문된 노드 수

        // 진행 텍스트 참조
        TMP_Text _progressText;

        // 노드 반경 (연결 판정 기준, 픽셀)
        const float NODE_RADIUS = 36f;
        const float NODE_SIZE   = 72f;
        const float LINE_THICKNESS = 6f;

        // ── Unity 생명주기 ────────────────────────────────────────────

        void Awake() {
            try {
                // CanvasGroup 확보
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null) {
                    _cg = gameObject.AddComponent<CanvasGroup>();
                }
                // 시작 시 비활성
                _cg.alpha = 0f;
                _cg.blocksRaycasts = false;
                _cg.interactable = false;
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] Awake 초기화 오류: {e.Message}");
            }
        }

        // 비정상 비활성에도 트윈 정리 + 상태 복구(Animating 고착 → 재오픈 차단 방지)
        void OnDisable() {
            KillAllTweens();
            _state = State.Closed;
            _dragging = false;
        }

        void OnDestroy() {
            KillAllTweens();
        }

        // transform·CanvasGroup·노드 Image에 걸린 트윈을 모두 정리(파괴 시 죽은 타깃 경고 방지)
        void KillAllTweens() {
            DOTween.Kill(transform);
            if (_cg != null) {
                DOTween.Kill(_cg);
            }
            // 성공 연출 색상 트윈은 각 노드 Image를 타깃으로 하므로 별도 정리
            if (_nodes != null) {
                for (int i = 0; i < _nodes.Count; i++) {
                    if (_nodes[i].img != null) {
                        DOTween.Kill(_nodes[i].img);
                    }
                }
            }
        }

        // ── 공개 API ─────────────────────────────────────────────────

        // 팝업 공개: difficulty 1~3 (노드 수 = 3 + difficulty)
        public void Open(int difficulty, Action onSolved) {
            if (_state == State.Open || _state == State.Animating) {
                return;
            }
            try {
                transform.SetAsLastSibling();   // 같은 sort 내에서도 최상단(연구 패널 위) 보장
                _onSolved = onSolved;
                int nodeCount = 3 + Mathf.Clamp(difficulty, 1, 3);
                BuildUI(nodeCount);
                PlayOpenAnim();
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] Open 오류 (difficulty={difficulty}): {e.Message}");
            }
        }

        // 취소 — onSolved 미호출
        public void Cancel() {
            if (_state == State.Closed) {
                return;
            }
            try {
                _onSolved = null;
                PlayCloseAnim(onDone: null);
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] Cancel 오류: {e.Message}");
            }
        }

        // 팝업 열림 여부
        public bool IsOpen => _state == State.Open || _state == State.Animating;

        // ── 테스트 전용 시드 ──────────────────────────────────────────

        // 모든 노드를 실제 연결 판정(ConnectNode) 경로로 순서대로 이어 OnPuzzleSolved 발화
        // 가짜로 콜백만 부르지 않고 _visitedCount·선그리기·완료판정을 실제 코드로 거친다
        internal bool SimulateConnectAll() {
            // Open이 애니메이션 중이면 즉시 Open 상태로 확정(트윈 대기 없이 입력 가능 상태)
            if (_state == State.Animating) {
                _state = State.Open;
                if (_cg != null) {
                    _cg.interactable = true;
                }
            }
            if (_state != State.Open || _nodes.Count == 0) {
                return false;
            }
            _dragging = true;
            _lastConnectedIndex = -1;
            // 모든 노드를 차례대로 실제 연결 판정에 통과시킨다
            for (int i = 0; i < _nodes.Count; i++) {
                ConnectNode(i);
            }
            _dragging = false;
            // 전부 방문되면 OnPuzzleSolved가 호출되어 _state가 Animating으로 전이된다
            return _visitedCount >= _nodes.Count;
        }

        // 방문 노드 수(테스트 검증용)
        internal int VisitedCount => _visitedCount;

        // 전체 노드 수(테스트 검증용)
        internal int NodeCount => _nodes.Count;

        // ── UI 빌드 ────────────────────────────────────────────────────

        // 기존 자식(노드·선)을 제거하고 새로 퍼즐을 구성한다
        void BuildUI(int nodeCount) {
            try {
                ClearPuzzleChildren();
                BuildBackground();
                BuildHeader();
                BuildProgressText(nodeCount);
                BuildCloseButton();
                BuildCancelButton();
                BuildNodes(nodeCount);

                _dragging = false;
                _lastConnectedIndex = -1;
                _visitedCount = 0;
                UpdateProgressText();
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] UI 빌드 오류: {e.Message}");
            }
        }

        // 이전 퍼즐 자식 오브젝트 제거 (노드·선 + 재생성 자식 정리 → 재오픈 중복 스택 방지)
        void ClearPuzzleChildren() {
            foreach (var n in _nodes) {
                if (n.rt != null) {
                    Destroy(n.rt.gameObject);
                }
            }
            _nodes.Clear();
            foreach (var l in _lines) {
                if (l != null) {
                    Destroy(l);
                }
            }
            _lines.Clear();
            _progressText = null;

            // BuildHeader/Progress/Close/Cancel는 매 Open마다 새로 생성하므로 기존 것을 먼저 제거(누적 방지)
            // PanelSolid/TopAccent는 BuildBackground가 find-or-create로 멱등 처리하므로 유지한다
            DestroyChildByName("Header");
            DestroyChildByName("Progress");
            DestroyChildByName("BtnClose");
            DestroyChildByName("BtnCancel");
        }

        // 이름으로 직속 자식을 찾아 제거(없으면 무시)
        void DestroyChildByName(string childName) {
            var child = transform.Find(childName);
            if (child != null) {
                Destroy(child.gameObject);
            }
        }

        // 팝업 배경 패널 — 글래스 표면(반투명 + 아쿠아 테두리 + 상단 하이라이트)
        void BuildBackground() {
            var rt = GetComponent<RectTransform>();
            // 포커스 트랩 — 풀스크린 딤 백드롭(뒤 패널 클릭 차단). 골든 루트보다 크게 깔아 화면 전체를 덮음
            EnsureFocusBackdrop();
            // 배경 Image가 없으면 추가 — 글래스 표면색
            var bgImg = GetComponent<Image>();
            if (bgImg == null) {
                bgImg = gameObject.AddComponent<Image>();
            }
            bgImg.color = UITheme.GlassFill;
            UITheme.ApplyRound(bgImg);
            // 아쿠아 1px 테두리(글래스모피즘 근사) — 중복 부착 방지
            if (GetComponent<Outline>() == null) {
                var ol = gameObject.AddComponent<Outline>();
                ol.effectColor = UITheme.GlassBorder;
                ol.effectDistance = new Vector2(1f, 1f);
            }
            // 가독성용 불투명 표면 한 겹 + 상단 액센트 바 — 재오픈 중복 생성 방지
            if (transform.Find("PanelSolid") == null) {
                var solid = UITheme.MakePanel("PanelSolid", transform,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(UITheme.BgModal.r, UITheme.BgModal.g, UITheme.BgModal.b, 0.94f));
                solid.transform.SetAsFirstSibling();
                solid.GetComponent<Image>().raycastTarget = false;
            }
            if (transform.Find("TopAccent") == null) {
                UITheme.MakeAccentBar("TopAccent", transform, 1f, 3f, UITheme.SpaceMD, UITheme.Accent);
            }
        }

        // 풀스크린 딤 백드롭 — 골든 루트(780×482)보다 크게 깔아 화면 전체를 덮고 뒤 패널 클릭을 차단
        // 멱등: 이미 있으면 재사용. 루트 자식이라 루트 CanvasGroup 알파를 따라 함께 페이드(포커스 모달)
        void EnsureFocusBackdrop() {
            try {
                if (transform.Find("FocusBackdrop") != null) {
                    return;
                }
                var go = new GameObject("FocusBackdrop", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var brt = go.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = new Vector2(4000f, 4000f);   // 골든 루트보다 크게 — 화면 전체 덮음
                var img = go.AddComponent<Image>();
                img.color = new Color(UITheme.BgDeep.r, UITheme.BgDeep.g, UITheme.BgDeep.b, 0.72f);
                img.raycastTarget = true;   // 뒤 패널 클릭 차단(포커스 트랩)
                go.transform.SetAsFirstSibling();   // 퍼즐 콘텐츠 뒤에 배치
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 포커스 백드롭 생성 오류: {e.Message}");
            }
        }

        // 상단 안내 텍스트 — 위계 상향(FontHeading)
        void BuildHeader() {
            var header = UITheme.MakeText(
                "Header", transform,
                "오염 구조를 연결하세요 — 모든 노드를 잇기",
                UITheme.FontHeading,
                UITheme.TextPrimary,
                TextAlignmentOptions.Center);
            // 상단 고정
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(UITheme.SpaceMD, -64f);
            rt.offsetMax = new Vector2(-UITheme.SpaceMD, -UITheme.SpaceSM);
            header.enableWordWrapping = true;
        }

        // 진행 표시 텍스트 (연결 n/N)
        void BuildProgressText(int total) {
            _progressText = UITheme.MakeFixedText(
                "Progress", transform,
                $"연결 0/{total}",
                UITheme.FontCaption,
                new Vector2(160f, 32f),
                new Vector2(0f, -GetComponent<RectTransform>().rect.height * 0.5f + UITheme.SpaceXL));
            _progressText.color = UITheme.TextSecondary;
            _progressText.alignment = TextAlignmentOptions.Center;
        }

        // 닫기(X) 버튼 — 우상단 고정
        void BuildCloseButton() {
            var btn = UITheme.MakeButton(
                "BtnClose", transform,
                "✕", UITheme.FontBody,
                new Vector2(40f, 40f),
                Vector2.zero,
                UITheme.BgBorder, UITheme.TextPrimary);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-UITheme.SpaceSM - 20f, -UITheme.SpaceSM - 20f);
            btn.onClick.AddListener(Cancel);
        }

        // 취소 버튼 — 하단 중앙
        void BuildCancelButton() {
            var btn = UITheme.MakeButton(
                "BtnCancel", transform,
                "취소", UITheme.FontCaption,
                new Vector2(100f, 36f),
                new Vector2(0f, UITheme.SpaceSM + 18f),
                UITheme.BgBorder, UITheme.TextSecondary);
            btn.onClick.AddListener(Cancel);
        }

        // 노드를 겹치지 않게 의사난수 배치
        void BuildNodes(int count) {
            try {
                var rt = GetComponent<RectTransform>();
                float w = rt.rect.width;
                float h = rt.rect.height;

                // 퍼즐 영역: 상단 헤더/하단 버튼 여백 제외
                float padL = UITheme.SpaceXL;
                float padR = UITheme.SpaceXL;
                float padTop = 80f;
                float padBot = 80f;

                float minX = -w * 0.5f + padL + NODE_RADIUS;
                float maxX =  w * 0.5f - padR - NODE_RADIUS;
                float minY = -h * 0.5f + padBot + NODE_RADIUS;
                float maxY =  h * 0.5f - padTop - NODE_RADIUS;

                // 의사난수 시드 — Time 기반
                var rng = new System.Random(Environment.TickCount);
                var positions = new List<Vector2>();

                for (int i = 0; i < count; i++) {
                    Vector2 pos = FindNonOverlappingPos(rng, positions, minX, maxX, minY, maxY);
                    positions.Add(pos);
                    var node = CreateNode(i, pos);
                    _nodes.Add(node);
                }
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 노드 배치 오류: {e.Message}");
            }
        }

        // 겹치지 않는 위치를 최대 100회 시도해 찾는다
        Vector2 FindNonOverlappingPos(System.Random rng, List<Vector2> placed,
                float minX, float maxX, float minY, float maxY) {
            float minDist = NODE_SIZE * 1.6f;
            for (int attempt = 0; attempt < 100; attempt++) {
                float x = (float)(rng.NextDouble() * (maxX - minX) + minX);
                float y = (float)(rng.NextDouble() * (maxY - minY) + minY);
                var candidate = new Vector2(x, y);
                bool overlaps = false;
                foreach (var p in placed) {
                    if (Vector2.Distance(candidate, p) < minDist) {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps) {
                    return candidate;
                }
            }
            // 100회 시도 실패 시 마지막 후보 그대로 사용(좌절 방지)
            float fx = (float)(rng.NextDouble() * (maxX - minX) + minX);
            float fy = (float)(rng.NextDouble() * (maxY - minY) + minY);
            return new Vector2(fx, fy);
        }

        // 노드 GameObject 생성 (원형 Image)
        NodeInfo CreateNode(int index, Vector2 anchoredPos) {
            var go = new GameObject($"Node_{index}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            // 미연결 상태: AccentDim 색
            img.color = UITheme.AccentDim;

            // 테두리 원 효과: Outline 컴포넌트로 테두리 표현
            var outline = go.AddComponent<Outline>();
            outline.effectColor = UITheme.BgBorder;
            outline.effectDistance = new Vector2(2f, -2f);

            // 노드 번호 레이블
            UITheme.MakeFixedText($"NodeLabel_{index}", go.transform,
                (index + 1).ToString(),
                UITheme.FontCaption,
                new Vector2(NODE_SIZE, NODE_SIZE),
                Vector2.zero,
                UITheme.TextPrimary,
                TextAlignmentOptions.Center);

            return new NodeInfo { rt = rt, img = img, visited = false };
        }

        // ── 선(Line) 그리기 ────────────────────────────────────────────

        // 두 RectTransform 사이에 얇은 Image bar를 그린다
        void DrawLine(Vector2 fromPos, Vector2 toPos) {
            try {
                var go = new GameObject("Line", typeof(RectTransform));
                go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);

                Vector2 dir = toPos - fromPos;
                float dist = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                rt.sizeDelta = new Vector2(dist, LINE_THICKNESS);
                rt.anchoredPosition = (fromPos + toPos) * 0.5f;
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);

                // 선 렌더링 레이어를 노드 아래로 (SetSiblingIndex 0)
                go.transform.SetSiblingIndex(0);

                var img = go.AddComponent<Image>();
                img.color = UITheme.Accent;

                _lines.Add(go);
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 선 그리기 오류: {e.Message}");
            }
        }

        // ── 드래그 입력 처리 ────────────────────────────────────────────

        // 드래그 시작: 포인터 아래 노드가 있으면 연결 시작
        public void OnPointerDown(PointerEventData eventData) {
            if (_state != State.Open) {
                return;
            }
            try {
                _dragging = true;
                _lastConnectedIndex = -1;
                // 시작 노드 연결 시도
                TryConnectNodeAtPointer(eventData.position);
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 포인터 다운 처리 오류: {e.Message}");
            }
        }

        // 드래그 중: 포인터가 새 노드 반경 안에 들어오면 연결
        public void OnDrag(PointerEventData eventData) {
            if (!_dragging || _state != State.Open) {
                return;
            }
            try {
                TryConnectNodeAtPointer(eventData.position);
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 드래그 처리 오류: {e.Message}");
            }
        }

        // 드래그 종료
        public void OnPointerUp(PointerEventData eventData) {
            _dragging = false;
        }

        // 스크린 좌표 → 패널 로컬 좌표 변환 후 노드 반경 판정
        void TryConnectNodeAtPointer(Vector2 screenPos) {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) {
                return;
            }

            // 스크린 → 로컬 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                GetComponent<RectTransform>(), screenPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPos);

            for (int i = 0; i < _nodes.Count; i++) {
                var node = _nodes[i];
                float dist = Vector2.Distance(localPos, node.rt.anchoredPosition);
                if (dist <= NODE_RADIUS) {
                    // 이미 마지막 연결 노드면 재방문 무시
                    if (i == _lastConnectedIndex) {
                        return;
                    }
                    ConnectNode(i);
                    return;
                }
            }
        }

        // 노드 연결 처리
        void ConnectNode(int index) {
            try {
                var node = _nodes[index];

                // 처음 방문하는 노드면 방문 카운트 증가
                if (!node.visited) {
                    node.visited = true;
                    node.img.color = UITheme.Accent;
                    _nodes[index] = node;
                    _visitedCount++;
                    UpdateProgressText();
                }

                // 직전 연결 노드와 선 그리기
                if (_lastConnectedIndex >= 0 && _lastConnectedIndex != index) {
                    var prevPos = _nodes[_lastConnectedIndex].rt.anchoredPosition;
                    var currPos = node.rt.anchoredPosition;
                    DrawLine(prevPos, currPos);
                }

                _lastConnectedIndex = index;

                // 모든 노드 방문 완료 시 성공
                if (_visitedCount >= _nodes.Count) {
                    OnPuzzleSolved();
                }
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 노드 연결 처리 오류 (index={index}): {e.Message}");
            }
        }

        // ── 진행 텍스트 갱신 ──────────────────────────────────────────

        void UpdateProgressText() {
            if (_progressText == null) {
                return;
            }
            _progressText.text = $"연결 {_visitedCount}/{_nodes.Count}";
            // 완료 시 성공 색으로 변경
            if (_visitedCount >= _nodes.Count) {
                _progressText.color = UITheme.ColSuccess;
            }
        }

        // ── 성공 연출 ─────────────────────────────────────────────────

        void OnPuzzleSolved() {
            try {
                _state = State.Animating;
                _dragging = false;
                _cg.interactable = false;   // 입력 차단

                // 성공 플래시: 전체 스케일 punch 후 닫기
                // SetTarget(transform) — KillAllTweens의 Kill(transform)이 시퀀스째 정리하도록(파괴 중 콜백 방지)
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(transform);
                seq.Append(transform.DOPunchScale(Vector3.one * 0.12f, 0.4f, 6, 0.5f).SetUpdate(true));
                // 노드 전부 성공 색 flash — Image.DOColor는 UI 모듈 의존이라 제네릭 To로
                foreach (var n in _nodes) {
                    var img = n.img;
                    if (img != null) {
                        seq.Join(DOTween.To(() => img.color, c => img.color = c, UITheme.ColSuccess, 0.25f)
                            .SetUpdate(true).SetLoops(2, LoopType.Yoyo).SetTarget(img));
                    }
                }
                seq.AppendInterval(0.15f);
                seq.OnComplete(() => {
                    // 콜백 1회 호출 후 null
                    var cb = _onSolved;
                    _onSolved = null;
                    PlayCloseAnim(onDone: () => cb?.Invoke());
                });
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 성공 연출 오류: {e.Message}");
            }
        }

        // ── 오픈/클로즈 애니메이션 ────────────────────────────────────

        void PlayOpenAnim() {
            _state = State.Animating;
            _cg.blocksRaycasts = true;
            transform.localScale = Vector3.one * 0.86f;

            // 수면 부상(RiseIn) — 위로 살짝 띄웠다 정착 + 페이드(루트 _cg 사용)
            UITheme.RiseIn(gameObject, 28f, 0.28f);

            // SetTarget(transform) — KillAllTweens의 Kill(transform)이 시퀀스째 정리하도록(파괴 중 콜백 방지)
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(transform);
            seq.Append(transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true));
            seq.OnComplete(() => {
                _state = State.Open;
                _cg.interactable = true;
            });
        }

        void PlayCloseAnim(Action onDone) {
            _state = State.Animating;
            _cg.interactable = false;

            // SetTarget(transform) — KillAllTweens의 Kill(transform)이 시퀀스째 정리하도록(파괴 중 콜백 방지)
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(transform);
            seq.Append(DOTween.To(() => _cg.alpha, a => _cg.alpha = a, 0f, 0.18f).SetUpdate(true).SetTarget(_cg));
            seq.Join(transform.DOScale(0.86f, 0.18f).SetEase(Ease.InBack).SetUpdate(true));
            seq.OnComplete(() => {
                _state = State.Closed;
                _cg.blocksRaycasts = false;
                onDone?.Invoke();
            });
        }
    }
}
