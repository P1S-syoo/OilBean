using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.UI;

namespace Game.Minigame {
    // 한붓그리기 연구 미니게임 — 아무 노드에서 시작해 누른 채 5개 노드를 모두 지나면 성공
    // 경로는 빛나는 선으로 그려진다. 외부 API(Open/Cancel/IsOpen)는 기존 호환 유지
    public class ResearchMinigame : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
        enum State { Closed, Open }
        struct PathSeg {
            public Vector2 a;
            public Vector2 b;
        }

        const int PopupSortingOrder = 1500;
        const float PathResetDistance = 5.5f;

        State state = State.Closed;
        Action onSolved;

        [SerializeField] Game.Core.연출설정 config;   // 연출 설정 — 미연결 시 SO 기본값(연출설정.기본) 사용

        int NodeCount;           // 미니게임 노드 수 — 기본값은 연출설정.연구미니게임노드수
        float HitRadius;         // 노드 통과 판정 반경(px) — 기본값은 연출설정.연구미니게임판정반경

        GameObject panelRoot;
        RectTransform field;
        readonly List<RectTransform> nodes = new();
        readonly List<Image> nodeImgs = new();
        readonly List<Image> segs = new();          // 붓으로 그린 경로 선
        readonly List<PathSeg> strokeSegs = new();  // 현재 붓 궤적 — 되돌아 그리기 판정
        Vector2 lastDragPoint;               // 직전 드래그 샘플 위치
        bool[] visited;                      // 노드 방문 상태 — 순서 자유
        int visitedCount;
        bool drawing;
        bool solved;

        public bool IsOpen => state == State.Open;

        void Awake() {
            try {
                // 연출 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거, UI 빌드 전에 적용)
                var cfg = config != null ? config : Game.Core.연출설정.기본;
                NodeCount = cfg.연구미니게임노드수;
                HitRadius = cfg.연구미니게임판정반경;
                EnsureTopCanvas();
                BuildUI();
                if (panelRoot != null) {
                    panelRoot.SetActive(false);
                }
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] 설정 적용 실패: {e.Message}");
            }
        }

        // 자체 캔버스를 보장해 연구/제작 패널 뒤에 깔리지 않게 한다
        void EnsureTopCanvas() {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = PopupSortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        // 외부 — 미니게임 시작(노드 수는 5 고정, 난이도는 배치 변화로만 반영)
        public void Open(int difficulty, Action solved) {
            try {
                onSolved = solved;
                Setup();
                state = State.Open;
                if (panelRoot != null) {
                    EnsureTopCanvas();
                    BringPanelToFront();
                    panelRoot.SetActive(true);
                }
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] Open 오류: {e.Message}");
                state = State.Closed;
                solved?.Invoke();   // 실패해도 분석은 진행(폴백)
            }
        }

        // 연구 퍼즐은 모달이라 열릴 때 항상 다른 패널보다 위에 둔다
        void BringPanelToFront() {
            panelRoot.transform.SetAsLastSibling();
            transform.SetAsLastSibling();
        }

        public void Cancel() {
            state = State.Closed;
            drawing = false;
            if (panelRoot != null) {
                panelRoot.SetActive(false);
            }
            onSolved = null;
        }

        // ── 입력 ────────────────────────────────────────────────
        public void OnPointerDown(PointerEventData e) {
            if (state != State.Open) {
                return;
            }
            Vector2 p = ToLocal(e);
            int start = FindNodeAt(p);
            if (start < 0) {
                return;
            }
            ResetPath();
            drawing = true;
            solved = false;
            lastDragPoint = nodes[start].anchoredPosition;
            VisitNode(start);
        }

        public void OnDrag(PointerEventData e) {
            if (!drawing) {
                return;
            }
            Vector2 p = ToLocal(e);
            if (TouchesOldPath(lastDragPoint, p)) {
                FailAndReset();
                return;
            }
            DrawStroke(lastDragPoint, p);
            VisitNodesOnSegment(lastDragPoint, p);
            lastDragPoint = p;
        }

        public void OnPointerUp(PointerEventData e) {
            if (!drawing || solved) {
                return;
            }
            drawing = false;
            FailAndReset();   // 완성 전에 손을 떼면 실패
        }

        void FinishSuccess() {
            solved = true;
            drawing = false;
            Game.Audio.GameAudio.Instance?.PlayPuzzleDone();   // 완성 성공음
            state = State.Closed;
            var cb = onSolved;
            onSolved = null;
            if (panelRoot != null) {
                panelRoot.SetActive(false);
            }
            cb?.Invoke();
        }

        void FailAndReset() {
            Game.Audio.GameAudio.Instance?.PlayMgMiss();   // 실패음
            ResetPath();   // 실패 — 리셋 후 재시도
        }

        // ── 구성 ────────────────────────────────────────────────
        void Setup() {
            EnsureVisited();
            ResetPath();
            // field.rect는 첫 Open 시 레이아웃 전이라 0일 수 있어 고정 분산 반경 사용(노드 겹침 방지)
            float w = 280f;
            float h = 190f;
            for (int i = 0; i < NodeCount; i++) {
                float ang = (i / (float)NodeCount) * Mathf.PI * 2f + i * 1.7f;
                var pos = new Vector2(
                    Mathf.Cos(ang) * w * (0.6f + 0.4f * ((i * 7) % 5) / 4f),
                    Mathf.Sin(ang) * h * (0.6f + 0.4f * ((i * 3) % 5) / 4f));
                nodes[i].anchoredPosition = pos;
                MarkNode(i, false);
            }
        }

        void EnsureVisited() {
            if (visited == null || visited.Length != NodeCount) {
                visited = new bool[NodeCount];
            }
        }

        void ResetPath() {
            foreach (var s in segs) {
                if (s != null) {
                    DestroySeg(s.gameObject);
                }
            }
            segs.Clear();
            strokeSegs.Clear();
            drawing = false;
            solved = false;
            visitedCount = 0;
            EnsureVisited();
            Array.Clear(visited, 0, visited.Length);
            for (int i = 0; i < nodeImgs.Count; i++) {
                MarkNode(i, false);
            }
        }

        // 노드 방문 표시(색)
        void MarkNode(int i, bool visited) {
            if (i < 0 || i >= nodeImgs.Count || nodeImgs[i] == null) {
                return;
            }
            nodeImgs[i].color = visited ? UITheme.Accent : UITheme.BgBorder;
        }

        int FindNodeAt(Vector2 p) {
            float r2 = HitRadius * HitRadius;
            for (int i = 0; i < nodes.Count; i++) {
                if ((p - nodes[i].anchoredPosition).sqrMagnitude <= r2) {
                    return i;
                }
            }
            return -1;
        }

        void VisitNode(int i) {
            if (i < 0 || i >= NodeCount || visited[i]) {
                return;
            }
            visited[i] = true;
            visitedCount++;
            MarkNode(i, true);
            Game.Audio.GameAudio.Instance?.PlayNodePass();   // 노드 통과음
            if (visitedCount >= NodeCount) {
                FinishSuccess();
            }
        }

        void VisitNodesOnSegment(Vector2 a, Vector2 b) {
            for (int i = 0; i < nodes.Count; i++) {
                if (!visited[i] && SegmentNearPoint(a, b, nodes[i].anchoredPosition, HitRadius)) {
                    VisitNode(i);
                }
            }
        }

        // ── UI 생성(1회) ─────────────────────────────────────────
        void BuildUI() {
            panelRoot = NewChild("OneStrokePopup", transform);
            StretchFull(panelRoot.GetComponent<RectTransform>());
            var dim = panelRoot.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.07f, 0.72f);

            var box = NewChild("Box", panelRoot.transform);
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(740f, 580f);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.05f, 0.12f, 0.14f, 0.97f);

            var title = NewText("Title", box.transform, "연구 — 한붓그리기  (누른 채 5개 노드 모두 통과)", 28);
            var tRt = title.rectTransform;
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = new Vector2(20f, -64f);
            tRt.offsetMax = new Vector2(-20f, -12f);
            title.alignment = TextAlignmentOptions.Center;
            title.color = UITheme.Accent;

            var f = NewChild("Field", box.transform);
            field = f.GetComponent<RectTransform>();
            field.anchorMin = new Vector2(0f, 0f);
            field.anchorMax = new Vector2(1f, 1f);
            field.offsetMin = new Vector2(40f, 40f);
            field.offsetMax = new Vector2(-40f, -76f);
            var fImg = f.AddComponent<Image>();   // 드래그 입력 수신용(거의 투명)
            fImg.color = new Color(0f, 0f, 0f, 0.004f);

            for (int i = 0; i < NodeCount; i++) {
                var n = NewChild("Node" + (i + 1), field);
                var nrt = n.GetComponent<RectTransform>();
                nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
                nrt.sizeDelta = new Vector2(64f, 64f);
                var img = n.AddComponent<Image>();
                img.color = UITheme.BgBorder;
                img.raycastTarget = false;
                UITheme.ApplyRound(img, 32f);
                var num = NewText("Num", n.transform, (i + 1).ToString(), 30);
                StretchFull(num.rectTransform);
                num.alignment = TextAlignmentOptions.Center;
                num.color = UITheme.TextPrimary;
                num.raycastTarget = false;
                nodes.Add(nrt);
                nodeImgs.Add(img);
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────────
        Vector2 ToLocal(PointerEventData e) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                field, e.position, e.pressEventCamera, out var local);
            return local;
        }

        Image NewSeg() {
            var go = NewChild("Seg", field);
            var img = go.AddComponent<Image>();
            img.color = UITheme.Accent;
            img.raycastTarget = false;
            go.transform.SetSiblingIndex(0);   // 노드 뒤로
            return img;
        }

        // 두 점(필드 로컬) 사이를 선분 Image로 그림
        static void DrawSeg(Image seg, Vector2 a, Vector2 b) {
            if (seg == null) {
                return;
            }
            var rt = seg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(len, 9f);
            rt.localEulerAngles = new Vector3(0f, 0f, ang);
        }

        void DrawStroke(Vector2 a, Vector2 b) {
            var seg = NewSeg();
            DrawSeg(seg, a, b);
            segs.Add(seg);
            strokeSegs.Add(new PathSeg { a = a, b = b });
        }

        static GameObject NewChild(string name, Transform parent) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        static void DestroySeg(GameObject go) {
            if (go == null) {
                return;
            }
            if (Application.isPlaying) {
                Destroy(go);
            } else {
                DestroyImmediate(go);
            }
        }

        static void StretchFull(RectTransform rt) {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static TMP_Text NewText(string name, Transform parent, string text, float size) {
            var go = NewChild(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            if (UITheme.UIFont != null) {
                t.font = UITheme.UIFont;
            }
            return t;
        }

        // 이미 그린 선을 다시 밟거나 교차하면 한붓그리기 실패
        bool TouchesOldPath(Vector2 a, Vector2 b) {
            if ((b - a).sqrMagnitude < 1f) {
                return false;
            }
            for (int i = 0; i < strokeSegs.Count; i++) {
                var s = strokeSegs[i];
                if (i == strokeSegs.Count - 1 && IsForwardContinuation(s, a, b)) {
                    continue;
                }
                if (PassesNearSegment(a, b, s.a, s.b)) {
                    return true;
                }
            }
            return false;
        }

        static bool IsForwardContinuation(PathSeg oldSeg, Vector2 a, Vector2 b) {
            if ((oldSeg.b - a).sqrMagnitude > 4f) {
                return false;
            }
            Vector2 oldDir = oldSeg.b - oldSeg.a;
            Vector2 newDir = b - a;
            if (oldDir.sqrMagnitude < 0.001f || newDir.sqrMagnitude < 0.001f) {
                return false;
            }
            return Vector2.Dot(oldDir.normalized, newDir.normalized) > 0.15f;
        }

        static bool PassesNearSegment(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            if (CrossesSegment(a, b, c, d)) {
                return true;
            }
            if (PointNearSegment(b, c, d)) {
                return true;
            }
            return PointNearSegment(c, a, b) || PointNearSegment(d, a, b);
        }

        static bool SegmentNearPoint(Vector2 a, Vector2 b, Vector2 p, float radius) {
            float lenSqr = (b - a).sqrMagnitude;
            if (lenSqr < 0.001f) {
                return (p - b).sqrMagnitude <= radius * radius;
            }
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / lenSqr);
            Vector2 closest = a + (b - a) * t;
            return (p - closest).sqrMagnitude <= radius * radius;
        }

        static bool PointNearSegment(Vector2 p, Vector2 a, Vector2 b) {
            float lenSqr = (b - a).sqrMagnitude;
            if (lenSqr < 0.001f) {
                return false;
            }
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / lenSqr);
            if (t <= 0.05f || t >= 0.95f) {
                return false;
            }
            return Vector2.Distance(p, a + (b - a) * t) <= PathResetDistance;
        }

        static bool CrossesSegment(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            if (SharesOnlyEndpoint(a, b, c, d)) {
                return false;
            }
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);
            if (Mathf.Abs(abC) < 0.001f && Mathf.Abs(abD) < 0.001f) {
                return OverlapsOnLine(a, b, c, d);
            }
            return abC * abD < 0f && cdA * cdB < 0f;
        }

        static bool SharesOnlyEndpoint(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            return (a - c).sqrMagnitude < 4f || (a - d).sqrMagnitude < 4f
                || (b - c).sqrMagnitude < 4f || (b - d).sqrMagnitude < 4f;
        }

        static bool OverlapsOnLine(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            Vector2 axis = (b - a).sqrMagnitude >= (d - c).sqrMagnitude ? b - a : d - c;
            if (axis.sqrMagnitude < 0.001f) {
                return false;
            }
            float a0 = Vector2.Dot(a, axis);
            float a1 = Vector2.Dot(b, axis);
            float c0 = Vector2.Dot(c, axis);
            float c1 = Vector2.Dot(d, axis);
            float left = Mathf.Max(Mathf.Min(a0, a1), Mathf.Min(c0, c1));
            float right = Mathf.Min(Mathf.Max(a0, a1), Mathf.Max(c0, c1));
            return right - left > PathResetDistance * axis.magnitude;
        }

        static float Cross(Vector2 a, Vector2 b) {
            return a.x * b.y - a.y * b.x;
        }

        void OnDisable() {
            drawing = false;
        }

        // ── 테스트 지원(입력 경로 우회) ──────────────────────────
        public int NodesTotal => NodeCount;
        public int VisitedCount => visitedCount;

        // 아무 노드에서 시작해 모든 노드를 통과하는 정상 경로 시뮬
        public bool SimulateSolve() {
            if (state != State.Open) {
                return false;
            }
            ResetPath();
            drawing = true;
            lastDragPoint = nodes[0].anchoredPosition;
            VisitNode(0);
            for (int i = 1; i < NodeCount && state == State.Open; i++) {
                Vector2 next = nodes[i].anchoredPosition;
                DrawStroke(lastDragPoint, next);
                VisitNodesOnSegment(lastDragPoint, next);
                lastDragPoint = next;
            }
            return solved;
        }

        // 테스트 지원 — 기존 경로를 되짚으면 초기화되는지 확인
        public bool SimulateBacktrackReset() {
            if (state != State.Open || NodeCount < 2) {
                return false;
            }
            ResetPath();
            drawing = true;
            Vector2 a = nodes[0].anchoredPosition;
            Vector2 b = nodes[1].anchoredPosition;
            DrawStroke(a, b);
            bool reset = TouchesOldPath(b, Vector2.Lerp(a, b, 0.5f));
            if (reset) {
                ResetPath();
            }
            return reset && VisitedCount == 0;
        }

        // 테스트 지원 — 완성 전 클릭을 놓으면 실패하고 초기화되는지 확인
        public bool SimulateReleaseBeforeComplete() {
            if (state != State.Open || NodeCount < 2) {
                return false;
            }
            ResetPath();
            drawing = true;
            VisitNode(0);
            OnPointerUp(new PointerEventData(EventSystem.current));
            return state == State.Open && VisitedCount == 0;
        }
    }
}
