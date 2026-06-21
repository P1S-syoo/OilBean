using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.UI;

namespace Game.Minigame {
    // 한붓그리기 연구 미니게임 — 1번을 누른 채 2,3,4,5를 순서대로 통과하고 5에서 떼면 성공
    // 경로는 빛나는 선으로 그려진다. 외부 API(Open/Cancel/IsOpen)는 기존 호환 유지
    public class ResearchMinigame : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
        enum State { Closed, Open }
        State state = State.Closed;
        Action onSolved;

        const int NodeCount = 5;
        const float HitRadius = 46f;   // 노드 통과 판정 반경(px)

        GameObject panelRoot;
        RectTransform field;
        readonly List<RectTransform> nodes = new();
        readonly List<Image> nodeImgs = new();
        readonly List<Image> segs = new();   // 확정된 경로 선
        Image activeLine;                    // 마우스를 따라 그려지는 선
        Vector2 lastPoint;                   // 마지막 확정 노드 위치(필드 로컬)
        int nextIdx;                         // 다음 통과 목표 노드(1=2번)
        bool drawing;

        public bool IsOpen => state == State.Open;

        void Awake() {
            BuildUI();
            if (panelRoot != null) {
                panelRoot.SetActive(false);
            }
        }

        // 외부 — 미니게임 시작(노드 수는 5 고정, 난이도는 배치 변화로만 반영)
        public void Open(int difficulty, Action solved) {
            try {
                onSolved = solved;
                Setup();
                state = State.Open;
                if (panelRoot != null) {
                    panelRoot.SetActive(true);
                }
            } catch (Exception e) {
                Debug.LogError($"[ResearchMinigame] Open 오류: {e.Message}");
                state = State.Closed;
                solved?.Invoke();   // 실패해도 분석은 진행(폴백)
            }
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
            // 1번 노드 근처에서만 시작
            if ((p - nodes[0].anchoredPosition).sqrMagnitude > HitRadius * HitRadius) {
                return;
            }
            drawing = true;
            nextIdx = 1;
            lastPoint = nodes[0].anchoredPosition;
            MarkNode(0, true);
            if (activeLine != null) {
                activeLine.gameObject.SetActive(true);
            }
        }

        public void OnDrag(PointerEventData e) {
            if (!drawing) {
                return;
            }
            Vector2 p = ToLocal(e);
            // 마우스 추종 선
            DrawSeg(activeLine, lastPoint, p);
            // 다음 목표 노드 통과 판정
            if (nextIdx < NodeCount
                && (p - nodes[nextIdx].anchoredPosition).sqrMagnitude <= HitRadius * HitRadius) {
                var seg = NewSeg();
                DrawSeg(seg, lastPoint, nodes[nextIdx].anchoredPosition);
                segs.Add(seg);
                lastPoint = nodes[nextIdx].anchoredPosition;
                MarkNode(nextIdx, true);
                nextIdx++;
            }
        }

        public void OnPointerUp(PointerEventData e) {
            if (!drawing) {
                return;
            }
            drawing = false;
            Finish(nextIdx >= NodeCount);   // 5번까지 모두 순서대로 통과 시 성공
        }

        void Finish(bool success) {
            if (activeLine != null) {
                activeLine.gameObject.SetActive(false);
            }
            if (success) {
                state = State.Closed;
                var cb = onSolved;
                onSolved = null;
                if (panelRoot != null) {
                    panelRoot.SetActive(false);
                }
                cb?.Invoke();
            } else {
                ResetPath();   // 실패 — 리셋 후 재시도
            }
        }

        // ── 구성 ────────────────────────────────────────────────
        void Setup() {
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

        void ResetPath() {
            foreach (var s in segs) {
                if (s != null) {
                    Destroy(s.gameObject);
                }
            }
            segs.Clear();
            drawing = false;
            nextIdx = 0;
            for (int i = 0; i < nodeImgs.Count; i++) {
                MarkNode(i, false);
            }
            if (activeLine != null) {
                activeLine.gameObject.SetActive(false);
            }
        }

        // 노드 방문 표시(색)
        void MarkNode(int i, bool visited) {
            if (i < 0 || i >= nodeImgs.Count || nodeImgs[i] == null) {
                return;
            }
            nodeImgs[i].color = visited ? UITheme.Accent : UITheme.BgBorder;
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

            var title = NewText("Title", box.transform, "연구 — 한붓그리기  (1번을 누른 채 5번까지 통과)", 28);
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

            activeLine = NewSeg();   // 마우스 추종 선(항상 1개)
            activeLine.color = new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.6f);
            activeLine.gameObject.SetActive(false);

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

        static GameObject NewChild(string name, Transform parent) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
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

        void OnDisable() {
            drawing = false;
        }

        // ── 테스트 지원(입력 경로 우회) ──────────────────────────
        public int NodesTotal => NodeCount;
        public int VisitedCount => nextIdx;

        // 1→5 순서대로 통과 시뮬 — 실제 통과 판정/선 그리기 경로를 타고 Finish까지
        public bool SimulateSolve() {
            if (state != State.Open) {
                return false;
            }
            nextIdx = 1;
            lastPoint = nodes[0].anchoredPosition;
            MarkNode(0, true);
            for (int i = 1; i < NodeCount; i++) {
                var seg = NewSeg();
                DrawSeg(seg, lastPoint, nodes[i].anchoredPosition);
                segs.Add(seg);
                lastPoint = nodes[i].anchoredPosition;
                MarkNode(i, true);
                nextIdx++;
            }
            bool success = nextIdx >= NodeCount;
            drawing = false;
            Finish(success);
            return success;
        }
    }
}
