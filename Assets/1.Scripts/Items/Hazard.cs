using System;
using UnityEngine;
using Game.Core;

namespace Game.Items {
    // 돌진형 오염원 — 경고(텔레그래프) 후 화면 한쪽 끝에서 반대편으로 직진 공격
    // 디렉터(HazardStreamer)가 Instantiate 직후 Setup으로 경로·속도 주입
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour {
        [SerializeField] Material poisonMat;     // 본체 비주얼(PoisonTrail)
        [SerializeField] Sprite warnSprite;      // 경고 아이콘(icon_warning)
        [SerializeField] float bodyScale = 1.4f; // 본체 크기
        [SerializeField] float warnScale = 0.48f; // 화면 경고 아이콘 크기 — 너무 커서 시야를 가리지 않게 축소
        [SerializeField] Vector2 hitBoxSize = new(0.88f, 0.48f);   // 보이는 본체보다 약간 작게 — 억울한 피격 방지
        [SerializeField] 위험설정 config;       // 위험 설정 — 연결 시 외형·피격 크기 적용

        enum Phase { Warn, Dash, Done }
        Phase phase = Phase.Done;

        float warnTime, dashSpeed;
        float laneY, startX, endX, warnX, dirSign;
        float timer, blink;

        Collider2D col;
        Transform body;          // 코드 생성 Quad 본체
        SpriteRenderer warnSr;   // 코드 생성 경고 아이콘

        void Awake() {
            try {
                ApplyConfig();
                col = GetComponent<Collider2D>();
                col = EnsurePreciseCollider();
                BuildVisual();
            } catch (Exception e) {
                Debug.LogError($"[Hazard] 초기화 실패: {e.Message}");
            }
        }

        // 위험체 표시 설정 적용 — 미연결 시 SO 기본값 사용
        void ApplyConfig() {
            var cfg = config != null ? config : 위험설정.기본;
            bodyScale = cfg.오염원본체크기;
            warnScale = bodyScale;
            hitBoxSize = cfg.오염원피격범위;
        }

        // 프리팹에 큰 원형 콜라이더가 남아 있어도 런타임은 보이는 본체 기준의 작은 박스 1개만 사용
        Collider2D EnsurePreciseCollider() {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) {
                box = gameObject.AddComponent<BoxCollider2D>();
            }
            box.isTrigger = true;
            box.size = hitBoxSize;
            box.offset = Vector2.zero;
            foreach (var c in GetComponents<Collider2D>()) {
                if (c != box) {
                    c.enabled = false;
                }
            }
            return box;
        }

        // 본체(Quad)+경고 아이콘을 코드로 구성 — 프리팹 의존 최소화
        void BuildVisual() {
            // 본체 Quad — 3D 콜라이더는 제거(2D 트리거만 사용)
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var c3d = q.GetComponent<Collider>();
            if (c3d != null) {
                Destroy(c3d);
            }
            q.name = "Body";
            q.transform.SetParent(transform, false);
            q.transform.localScale = Vector3.one * bodyScale;
            var mr = q.GetComponent<MeshRenderer>();
            if (poisonMat != null) {
                mr.sharedMaterial = poisonMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 40;   // 스프라이트 레이어 위로
            body = q.transform;

            // 경고 아이콘
            var w = new GameObject("Warn");
            w.transform.SetParent(transform, false);
            warnSr = w.AddComponent<SpriteRenderer>();
            warnSr.sprite = warnSprite;
            warnSr.color = new Color(1f, 0.3f, 0.2f, 1f);
            warnSr.sortingOrder = 500;
            w.transform.localScale = Vector3.one * warnScale;
        }

        // 디렉터 주입 — 경로/속도 설정 후 경고 단계 진입
        public void Setup(float laneY, float startX, float endX, float warnX, float warnTime, float dashSpeed) {
            this.laneY = laneY;
            this.startX = startX;
            this.endX = endX;
            this.warnX = warnX;
            this.warnTime = warnTime;
            this.dashSpeed = dashSpeed;
            dirSign = Mathf.Sign(endX - startX);
            transform.position = new Vector3(startX, laneY, 0f);
            timer = warnTime;
            blink = 0f;
            phase = Phase.Warn;
            if (col != null) {
                col.enabled = false;   // 경고 중엔 충돌 없음
            }
            if (body != null) {
                body.gameObject.SetActive(false);
                // 진행 방향으로 본체 정렬(트레일이 뒤로 흐르도록)
                body.localScale = new Vector3(dirSign * bodyScale, bodyScale, bodyScale);
            }
            if (warnSr != null) {
                warnSr.gameObject.SetActive(true);
                PlaceWarning();   // 화면 안쪽 진입 가장자리
            }
            Game.Audio.GameAudio.Instance?.PlayHazardWarn();   // 돌진 경고음
        }

        void Update() {
            if (phase == Phase.Warn) {
                timer -= Time.deltaTime;
                blink += Time.deltaTime * 12f;
                if (warnSr != null) {
                    var c = warnSr.color;
                    c.a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(blink));   // 경고 깜빡임
                    warnSr.color = c;
                    PlaceWarning();
                }
                if (timer <= 0f) {
                    EnterDash();
                }
            } else if (phase == Phase.Dash) {
                transform.position += new Vector3(dirSign * dashSpeed * Time.deltaTime, 0f, 0f);
                // 반대편 화면 밖 도달 → 소멸
                if ((dirSign > 0f && transform.position.x >= endX)
                    || (dirSign < 0f && transform.position.x <= endX)) {
                    Destroy(gameObject);
                }
            }
        }

        void PlaceWarning() {
            if (warnSr == null) {
                return;
            }
            warnSr.transform.position = new Vector3(warnX, laneY, -0.5f);
            warnSr.transform.rotation = Quaternion.identity;
        }

        // 경고 종료 → 돌진 시작(본체 표시, 충돌 활성)
        void EnterDash() {
            phase = Phase.Dash;
            if (warnSr != null) {
                warnSr.gameObject.SetActive(false);
            }
            if (body != null) {
                body.gameObject.SetActive(true);
            }
            if (col != null) {
                col.enabled = true;
            }
        }
    }
}
