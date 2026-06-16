using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using Game.UI;

namespace Game.Minigame {
    // 제작(슬롯 배치) 미니게임 팝업 — 런타임 UI 자가 생성, 씬 자식에 의존하지 않음
    public class CraftMinigame : MonoBehaviour {

        // ── 내부 데이터 ───────────────────────────────────────────────

        // 슬롯 한 개의 상태
        internal class SlotData {
            public RectTransform Rt;
            public Image         Img;
            public int           TypeTag;    // 허용하는 토큰 타입
            public bool          IsFilled;   // 올바른 토큰으로 채워졌는지
            public TokenData     OccupiedBy; // 현재 안착한 토큰
        }

        // 드래그 가능한 토큰 한 개의 상태
        internal class TokenData {
            public RectTransform Rt;
            public Image         Img;
            public int           TypeTag;       // 토큰 타입
            public bool          IsDecoy;       // 오답 부품 여부
            public Vector2       TrayPos;       // 트레이 원위치
            public SlotData      DockedSlot;    // 현재 안착한 슬롯 (없으면 null)
        }

        // ── 런타임 필드 ───────────────────────────────────────────────

        private CanvasGroup  _cg;          // 팝업 루트 CanvasGroup(페이드/차단)
        private Canvas       _rootCanvas;  // 캔버스 스케일 보정용
        private Action       _onSolved;    // 성공 콜백 (1회만 사용)
        private bool         _isOpen;

        private List<SlotData>  _slots  = new List<SlotData>();
        private List<TokenData> _tokens = new List<TokenData>();

        // 드래그 중인 토큰 상태
        private TokenData _dragging;
        private Vector2   _dragLocalStart; // 드래그 시작 시 anchoredPosition

        // 버튼 참조
        private Button _confirmBtn;

        // ── 공개 API ──────────────────────────────────────────────────

        public bool IsOpen => _isOpen;

        // 미니게임 열기 — 호출마다 UI를 새로 생성(멱등)
        public void Open(string recipeLabel, int slotCount, Action onSolved) {
            try {
                slotCount = Mathf.Clamp(slotCount, 2, 4);
                _onSolved = onSolved;

                // 이전 자식 정리
                ClearChildren();

                // 캔버스 참조 확보
                _rootCanvas = GetComponentInParent<Canvas>();

                // UI 구성
                BuildUI(recipeLabel, slotCount);

                // 팝업 등장 연출
                gameObject.SetActive(true);
                _cg.alpha = 0f;
                transform.localScale = Vector3.one * 0.75f;
                // CanvasGroup.DOFade는 UI 모듈 의존이라 제네릭 To로 알파 보간
                DOTween.To(() => _cg.alpha, a => _cg.alpha = a, 1f, 0.25f).SetUpdate(true).SetTarget(_cg);
                transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

                _isOpen = true;
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] Open 실패: {e.Message}");
            }
        }

        // 취소(X) — onSolved 미호출
        public void Cancel() {
            try {
                ClosePopup(false);
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] Cancel 실패: {e.Message}");
            }
        }

        // ── 테스트 전용 시드 ──────────────────────────────────────────

        // 각 정답 토큰을 올바른 슬롯에 실제 도킹 판정(OnTokenEndDrag) 경로로 배치
        // FindOverlappingSlot·타입일치검사·DockToSlot·RefreshConfirmButton을 실제로 거친다
        internal bool SimulatePlaceCorrect() {
            if (_rootCanvas == null) {
                _rootCanvas = GetComponentInParent<Canvas>();
            }
            // 정답 토큰을 타입이 일치하는 슬롯 위로 옮긴 뒤 실제 종료-드래그 판정 실행
            foreach (var td in _tokens) {
                if (td.IsDecoy) {
                    continue;
                }
                SlotData target = null;
                foreach (var sd in _slots) {
                    if (sd.TypeTag == td.TypeTag) {
                        target = sd;
                        break;
                    }
                }
                if (target == null) {
                    continue;
                }
                // 토큰을 슬롯과 동일 부모·좌표로 겹쳐 FindOverlappingSlot이 명중하도록 한다
                td.Rt.SetParent(target.Rt.parent, false);
                td.Rt.anchorMin = target.Rt.anchorMin;
                td.Rt.anchorMax = target.Rt.anchorMax;
                td.Rt.pivot = target.Rt.pivot;
                td.Rt.anchoredPosition = target.Rt.anchoredPosition;
                // 월드 좌표 갱신 — FindOverlappingSlot의 GetWorldCorners 판정이 최신 위치를 쓰도록
                Canvas.ForceUpdateCanvases();
                // 실제 도킹 판정 경로(타입 일치 검사 포함)를 탄다
                OnTokenEndDrag(td, new PointerEventData(EventSystem.current));
            }
            return AllSlotsFilled();
        }

        // 확정(제작 확인) 판정을 실제 OnConfirm 경로로 실행 — 성공 시 onSolved 발화 예약
        internal bool SimulateConfirm() {
            if (!AllSlotsFilled()) {
                return false;
            }
            OnConfirm();
            return true;
        }

        // 모든 슬롯 충전 여부(테스트 검증용)
        internal bool AllFilledForTest => AllSlotsFilled();

        // ── Unity 생명주기 ────────────────────────────────────────────

        private void Awake() {
            try {
                // CanvasGroup이 없으면 추가
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null) {
                    _cg = gameObject.AddComponent<CanvasGroup>();
                }
                // 초기 상태는 비활성·투명
                _cg.alpha = 0f;
                gameObject.SetActive(false);
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] Awake 실패: {e.Message}");
            }
        }

        // 비정상 비활성에도 진행 중 트윈 정리(ResearchMinigame과 일관)
        private void OnDisable() {
            KillAllTweens();
        }

        // 파괴 시 진행 중 트윈 정리(죽은 객체 콜백 방지)
        private void OnDestroy() {
            KillAllTweens();
        }

        // transform·CanvasGroup·슬롯/토큰 그래픽에 걸린 트윈을 모두 정리
        private void KillAllTweens() {
            DOTween.Kill(transform);
            if (_cg != null) {
                DOTween.Kill(_cg);
            }
            // 슬롯/토큰 RectTransform·Image 타깃 트윈은 자식 파괴 전에 별도 정리
            if (_tokens != null) {
                foreach (var td in _tokens) {
                    if (td == null) {
                        continue;
                    }
                    if (td.Rt != null) {
                        DOTween.Kill(td.Rt);
                    }
                    if (td.Img != null) {
                        DOTween.Kill(td.Img);
                    }
                }
            }
            if (_slots != null) {
                foreach (var sd in _slots) {
                    if (sd != null && sd.Img != null) {
                        DOTween.Kill(sd.Img);
                    }
                }
            }
        }

        // ── UI 빌드 ──────────────────────────────────────────────────

        // 팝업 전체 레이아웃을 런타임 생성
        private void BuildUI(string recipeLabel, int slotCount) {
            var root = transform;

            // ── 배경 패널 (전체 채우기) ─────────────────────────────
            var bg = UITheme.MakeStretchPanel("BgPanel", root, 0, 0, 0, 0, UITheme.BgPanel);
            UITheme.AddShadow(bg, 0.6f, 0f, -4f);

            // ── 헤더 영역 ───────────────────────────────────────────
            var header = UITheme.MakePanel("Header", root,
                new Vector2(0f, 0.82f), new Vector2(1f, 1f),
                new Vector2(0, 0), new Vector2(0, 0),
                UITheme.BgHeader);

            // 레시피 제목
            var title = UITheme.MakeText("RecipeLabel", header.transform,
                recipeLabel, UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);

            // 안내 문구
            var guide = UITheme.MakeText("GuideText", header.transform,
                "부품을 슬롯에 배치하세요", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Center);
            // 안내 문구를 제목 아래로 내림
            var guideRt = guide.GetComponent<RectTransform>();
            guideRt.anchorMin = new Vector2(0f, 0f);
            guideRt.anchorMax = new Vector2(1f, 0.45f);
            guideRt.offsetMin = new Vector2(UITheme.SpaceSM, 0f);
            guideRt.offsetMax = new Vector2(-UITheme.SpaceSM, 0f);

            // 닫기(X) 버튼 — 우상단 고정
            var closeBtn = UITheme.MakeButton("CloseBtn", root,
                "✕", UITheme.FontBody,
                new Vector2(36f, 36f), Vector2.zero,
                UITheme.ColDanger, UITheme.TextPrimary);
            var closeBtnRt = closeBtn.GetComponent<RectTransform>();
            closeBtnRt.anchorMin = new Vector2(1f, 1f);
            closeBtnRt.anchorMax = new Vector2(1f, 1f);
            closeBtnRt.pivot     = new Vector2(1f, 1f);
            closeBtnRt.anchoredPosition = new Vector2(-UITheme.SpaceSM, -UITheme.SpaceSM);
            closeBtn.onClick.AddListener(Cancel);

            // ── 슬롯 영역 ───────────────────────────────────────────
            var slotArea = UITheme.MakePanel("SlotArea", root,
                new Vector2(0f, 0.45f), new Vector2(1f, 0.82f),
                new Vector2(UITheme.SpaceMD, 0f), new Vector2(-UITheme.SpaceMD, 0f),
                new Color(0f, 0f, 0f, 0f));
            BuildSlots(slotArea.transform, slotCount);

            // ── 트레이 영역 ─────────────────────────────────────────
            var trayArea = UITheme.MakePanel("TrayArea", root,
                new Vector2(0f, 0.1f), new Vector2(1f, 0.44f),
                new Vector2(UITheme.SpaceMD, 0f), new Vector2(-UITheme.SpaceMD, 0f),
                new Color(0f, 0f, 0f, 0f));
            BuildTokens(trayArea.transform, slotCount);

            // ── 확인 버튼 ───────────────────────────────────────────
            _confirmBtn = UITheme.MakeButton("ConfirmBtn", root,
                "제작 확인", UITheme.FontBody,
                new Vector2(160f, 44f), Vector2.zero,
                UITheme.AccentDim, UITheme.TextPrimary);
            var confirmRt = _confirmBtn.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.5f, 0f);
            confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot     = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(0f, UITheme.SpaceSM);
            _confirmBtn.interactable = false;
            _confirmBtn.onClick.AddListener(OnConfirm);
        }

        // 슬롯 가로 배치 — 타입 태그는 0..slotCount-1
        private void BuildSlots(Transform parent, int slotCount) {
            _slots.Clear();
            float totalW  = 320f;
            float slotW   = Mathf.Min(80f, (totalW - UITheme.SpaceSM * (slotCount - 1)) / slotCount);
            float slotH   = 72f;
            float startX  = -(slotCount - 1) * (slotW + UITheme.SpaceSM) * 0.5f;

            for (int i = 0; i < slotCount; i++) {
                var go = new GameObject($"Slot_{i}", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(slotW, slotH);
                rt.anchoredPosition = new Vector2(startX + i * (slotW + UITheme.SpaceSM), 0f);

                // 슬롯 배경 — 점선 느낌의 테두리 색
                var img = go.AddComponent<Image>();
                img.color = UITheme.BgBorder;
                UITheme.ApplyRound(img);

                // 슬롯 번호 레이블(작게)
                UITheme.MakeText($"SlotLbl_{i}", rt,
                    (i + 1).ToString(), UITheme.FontCaption,
                    UITheme.TextDisabled, TextAlignmentOptions.Center);

                var sd = new SlotData {
                    Rt      = rt,
                    Img     = img,
                    TypeTag = i,         // 슬롯 i는 타입 i를 요구
                    IsFilled = false,
                };
                _slots.Add(sd);
            }
        }

        // 토큰 빌드 — 정답 토큰 slotCount개 + 오답 1개, 셔플 배치
        private void BuildTokens(Transform parent, int slotCount) {
            _tokens.Clear();

            // 토큰 색 팔레트 (타입별로 약간씩 다름)
            Color[] palette = {
                UITheme.Accent,
                UITheme.ColInfo,
                UITheme.ColWarn,
                UITheme.ColSuccess,
                UITheme.DepthUncommon, // 오답 decoy 색
            };

            // 정답 토큰 slotCount개
            var tokenTypes = new List<(int typeTag, bool isDecoy)>();
            for (int i = 0; i < slotCount; i++) {
                tokenTypes.Add((i, false));
            }
            // 오답 decoy 1개 — 슬롯 타입 범위 밖 값(slotCount)
            tokenTypes.Add((slotCount, true));

            // Fisher-Yates 셔플
            for (int i = tokenTypes.Count - 1; i > 0; i--) {
                int j = UnityEngine.Random.Range(0, i + 1);
                (tokenTypes[i], tokenTypes[j]) = (tokenTypes[j], tokenTypes[i]);
            }

            int tokenCount = tokenTypes.Count;
            float totalW   = 320f;
            float tokenW   = Mathf.Min(70f, (totalW - UITheme.SpaceSM * (tokenCount - 1)) / tokenCount);
            float tokenH   = 60f;
            float startX   = -(tokenCount - 1) * (tokenW + UITheme.SpaceSM) * 0.5f;

            for (int i = 0; i < tokenCount; i++) {
                var (typeTag, isDecoy) = tokenTypes[i];
                Vector2 pos = new Vector2(startX + i * (tokenW + UITheme.SpaceSM), 0f);

                var go = new GameObject($"Token_{i}", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(tokenW, tokenH);
                rt.anchoredPosition = pos;

                // 토큰 배경색 — 타입별 팔레트
                int palIdx = Mathf.Clamp(typeTag, 0, palette.Length - 1);
                var img = go.AddComponent<Image>();
                img.color = palette[palIdx];
                UITheme.ApplyRound(img);
                UITheme.AddShadow(go, 0.35f, 0f, -2f);

                // 타입 레이블
                string lbl = isDecoy ? "?" : ((char)('A' + typeTag)).ToString();
                UITheme.MakeText($"TokenLbl_{i}", rt,
                    lbl, UITheme.FontBody,
                    UITheme.TextPrimary, TextAlignmentOptions.Center);

                var td = new TokenData {
                    Rt        = rt,
                    Img       = img,
                    TypeTag   = typeTag,
                    IsDecoy   = isDecoy,
                    TrayPos   = pos,
                    DockedSlot = null,
                };
                _tokens.Add(td);

                // 드래그 핸들러 부착
                var handler = go.AddComponent<TokenDragHandler>();
                handler.Init(td, this);
            }
        }

        // ── 드래그 콜백 ──────────────────────────────────────────────

        // 드래그 시작 — 토큰을 최상위로 올려 가림 방지
        internal void OnTokenBeginDrag(TokenData td, PointerEventData eventData) {
            try {
                // 이미 슬롯에 안착해 있으면 슬롯을 비움
                if (td.DockedSlot != null) {
                    UndockFromSlot(td);
                }
                _dragging = td;
                td.Rt.SetAsLastSibling();
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] 드래그 시작 실패: {e.Message}");
            }
        }

        // 드래그 중 — 캔버스 스케일 보정으로 이동
        internal void OnTokenDrag(TokenData td, PointerEventData eventData) {
            try {
                if (_rootCanvas == null) return;
                // 스크린 델타를 로컬 델타로 변환
                td.Rt.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] 드래그 중 실패: {e.Message}");
            }
        }

        // 드래그 끝 — 가장 가까운 슬롯과 타입 체크 후 안착 or 원위치
        internal void OnTokenEndDrag(TokenData td, PointerEventData eventData) {
            try {
                _dragging = null;
                SlotData bestSlot = FindOverlappingSlot(td.Rt);

                if (bestSlot != null && !bestSlot.IsFilled && td.TypeTag == bestSlot.TypeTag) {
                    // 타입 일치 — 슬롯에 안착
                    DockToSlot(td, bestSlot);
                } else {
                    // 불일치 또는 이미 채워진 슬롯 — 원위치 복귀
                    ReturnToTray(td);
                }
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] 드래그 종료 실패: {e.Message}");
            }
        }

        // ── 슬롯 안착·해제 ───────────────────────────────────────────

        // 토큰을 슬롯에 스냅
        private void DockToSlot(TokenData td, SlotData sd) {
            sd.IsFilled   = true;
            sd.OccupiedBy = td;
            td.DockedSlot = sd;

            // 슬롯 중심으로 스냅 — 슬롯 RectTransform 로컬 좌표로 이동
            td.Rt.SetParent(sd.Rt, false);
            td.Rt.anchorMin = new Vector2(0.5f, 0.5f);
            td.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            td.Rt.anchoredPosition = Vector2.zero;

            // 슬롯 채움 색 표시
            sd.Img.color = UITheme.ColSuccess;

            // 채움 연출
            td.Rt.DOScale(Vector3.one * 1.08f, 0.1f).SetUpdate(true)
              .OnComplete(() => td.Rt.DOScale(Vector3.one, 0.1f).SetUpdate(true));

            RefreshConfirmButton();
        }

        // 토큰을 슬롯에서 분리
        private void UndockFromSlot(TokenData td) {
            var sd = td.DockedSlot;
            if (sd == null) return;

            sd.IsFilled   = false;
            sd.OccupiedBy = null;
            td.DockedSlot = null;
            sd.Img.color  = UITheme.BgBorder;

            // 부모를 트레이 부모(token 원래 부모)로 복구 — 트레이 기준 좌표로 재배치
            var trayParent = GetTrayParent();
            if (trayParent != null) {
                td.Rt.SetParent(trayParent, false);
                td.Rt.anchorMin = new Vector2(0.5f, 0.5f);
                td.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                td.Rt.anchoredPosition = td.TrayPos;   // 트레이 원위치 복원(겹침 방지)
            }
            RefreshConfirmButton();
        }

        // 거부 연출 후 트레이 원위치로 복귀 — UI 모듈 셰이크/앵커 트윈 대신 제네릭 To로 복귀
        private void ReturnToTray(TokenData td) {
            if (td.Rt == null) {
                return;
            }
            var trayParent = GetTrayParent();
            if (trayParent != null) {
                td.Rt.SetParent(trayParent, false);
                td.Rt.anchorMin = new Vector2(0.5f, 0.5f);
                td.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            }
            var rt = td.Rt;
            DOTween.To(() => rt.anchoredPosition, p => rt.anchoredPosition = p, td.TrayPos, 0.18f)
                .SetUpdate(true).SetTarget(rt);
        }

        // ── 판정 / 확인 ──────────────────────────────────────────────

        // 모든 슬롯이 채워졌는지 확인
        private bool AllSlotsFilled() {
            foreach (var sd in _slots) {
                if (!sd.IsFilled) return false;
            }
            return true;
        }

        // 확인 버튼 활성/비활성 갱신
        private void RefreshConfirmButton() {
            if (_confirmBtn == null) return;
            bool filled = AllSlotsFilled();
            _confirmBtn.interactable = filled;

            // 색으로 상태 표시
            var img = _confirmBtn.GetComponent<Image>();
            if (img != null) {
                img.color = filled ? UITheme.Accent : UITheme.AccentDim;
            }
        }

        // 확인 버튼 클릭 — 성공 연출 후 콜백·닫기
        private void OnConfirm() {
            try {
                if (!AllSlotsFilled()) return;

                // 성공 색 플래시 — Image.DOColor는 UI 모듈 의존이라 제네릭 To로
                foreach (var sd in _slots) {
                    if (sd.Img != null) {
                        var img = sd.Img;
                        DOTween.To(() => img.color, c => img.color = c, UITheme.ColSuccess, 0.15f)
                            .SetUpdate(true).SetTarget(img);
                    }
                }

                // 약간 지연 후 닫기
                DOVirtual.DelayedCall(0.4f, () => {
                    ClosePopup(true);
                }, true);
            } catch (Exception e) {
                Debug.LogError($"[CraftMinigame] 확인 처리 실패: {e.Message}");
            }
        }

        // ── 팝업 닫기 ────────────────────────────────────────────────

        // solved=true면 onSolved 콜백 호출
        private void ClosePopup(bool solved) {
            if (!_isOpen) return;
            _isOpen = false;

            DOTween.To(() => _cg.alpha, a => _cg.alpha = a, 0f, 0.2f).SetUpdate(true).SetTarget(_cg);
            transform.DOScale(0.85f, 0.2f).SetUpdate(true)
              .OnComplete(() => {
                  gameObject.SetActive(false);
                  ClearChildren();

                  if (solved && _onSolved != null) {
                      var cb = _onSolved;
                      _onSolved = null;  // 1회만 호출
                      cb.Invoke();
                  } else {
                      _onSolved = null;
                  }
              });
        }

        // ── 유틸 ─────────────────────────────────────────────────────

        // 토큰 RectTransform과 겹치는 슬롯 반환 (없으면 null)
        private SlotData FindOverlappingSlot(RectTransform tokenRt) {
            if (_rootCanvas == null) return null;

            // 토큰 월드 중심점
            Vector3[] corners = new Vector3[4];
            tokenRt.GetWorldCorners(corners);
            Vector2 tokenCenter = (corners[0] + corners[2]) * 0.5f;

            SlotData best     = null;
            float    bestDist = float.MaxValue;

            foreach (var sd in _slots) {
                // 슬롯 RectTransform이 스크린 포인트를 포함하는지 확인
                Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _rootCanvas.worldCamera;

                // 토큰 중심이 슬롯 사각형 안에 있는지
                bool inside = RectTransformUtility.RectangleContainsScreenPoint(
                    sd.Rt, tokenCenter, cam);

                if (inside) {
                    // 겹치는 슬롯이 여럿이면 중심 거리로 최근접 선택
                    Vector3[] slotCorners = new Vector3[4];
                    sd.Rt.GetWorldCorners(slotCorners);
                    Vector2 slotCenter = (slotCorners[0] + slotCorners[2]) * 0.5f;
                    float dist = Vector2.Distance(tokenCenter, slotCenter);
                    if (dist < bestDist) {
                        bestDist = dist;
                        best     = sd;
                    }
                }
            }
            return best;
        }

        // TrayArea 의 Transform 반환 — 토큰 복귀 시 부모 재설정에 사용
        private Transform GetTrayParent() {
            var trayGo = transform.Find("TrayArea");
            return trayGo != null ? trayGo : transform;
        }

        // 팝업 자식 UI 전체 정리
        private void ClearChildren() {
            _slots.Clear();
            _tokens.Clear();
            _dragging    = null;
            _confirmBtn  = null;

            for (int i = transform.childCount - 1; i >= 0; i--) {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        // ── 내부 드래그 핸들러 컴포넌트 ─────────────────────────────

        // 토큰 GameObject에 AddComponent로 부착하는 드래그 핸들러
        private class TokenDragHandler : MonoBehaviour,
            IBeginDragHandler, IDragHandler, IEndDragHandler {

            private TokenData    _td;
            private CraftMinigame _owner;

            // 초기화 — TokenData와 소유 미니게임 연결
            public void Init(TokenData td, CraftMinigame owner) {
                _td    = td;
                _owner = owner;
            }

            public void OnBeginDrag(PointerEventData eventData) {
                _owner.OnTokenBeginDrag(_td, eventData);
            }

            public void OnDrag(PointerEventData eventData) {
                _owner.OnTokenDrag(_td, eventData);
            }

            public void OnEndDrag(PointerEventData eventData) {
                _owner.OnTokenEndDrag(_td, eventData);
            }
        }
    }
}
