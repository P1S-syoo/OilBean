using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Player;

namespace Game.Stage {
    // 정화 부유체 설치 스팟 — 스팟 안 + 부유체 준비 + F 홀드로 설치(정화) 진행 (E6 hold-to-install)
    [RequireComponent(typeof(Collider2D))]
    public class PurifyInstaller : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] float installTime;   // 설치 소요(초, 홀드 누적) — 기본값은 제작설정.설치시간
        [SerializeField] Transform progressFill;      // 월드 진행 게이지(스케일 X = 진행), 선택
        [SerializeField] GameObject holdPrompt;        // 'F 홀드' 안내 오브젝트, 선택
        [SerializeField] 제작설정 config;            // 제작 설정 — 연결 시 설치 시간 적용(미연결 시 SO 기본값)

        bool inside;       // 스팟 안에 탐사 기계가 있나
        bool armed;        // 탐사 중에만 설치 허용(코디네이터가 토글)
        bool done;
        float t;           // 설치 누적 시간(홀드 중단 시 보존 — 부분진행 유지)
        bool holdOverride; // 키보드 없는 환경(PlayMode 테스트)에서 홀드를 대체
        GameObject marker; // 설치 위치 2D 마커 — 수면에서는 숨김
        GameObject holdFxRoot;
        LineRenderer holdRing;
        LineRenderer holdArc;
        GameObject beaconFxRoot;
        LineRenderer beaconRing;
        LineRenderer beaconPulse;

        // 정화 완료 — 코디네이터가 클리어 전환에 사용
        public event Action OnPurified;
        public bool CanShowInstallNotice => !done && armed && inside && run != null && run.BuoyReady;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : 제작설정.기본;
                installTime = cfg.설치시간;
                GetComponent<Collider2D>().isTrigger = true;
                marker = transform.Find("Marker")?.gameObject;
                ShowMarker(false);
                EnsureBeaconFx();
                EnsureHoldFx();
            } catch (Exception e) {
                Debug.LogError($"[PurifyInstaller] 설정 적용 실패: {e.Message}");
            }
        }

        void OnTriggerEnter2D(Collider2D other) {
            // 탐사 기계 진입 표시(부유체 게이트는 Update에서 — 진입 후 제작해도 인식)
            if (other.GetComponentInParent<PlayerMove>() != null) {
                inside = true;
            }
        }

        void OnTriggerExit2D(Collider2D other) {
            if (other.GetComponentInParent<PlayerMove>() != null) {
                inside = false;
            }
        }

        void Update() {
            if (done || run == null || !armed) {
                ShowPrompt(false);
                ShowMarker(false);
                ShowHoldFx(false, 0f, false);
                ShowBeaconFx(false);
                return;
            }
            bool ready = CanShowInstallNotice;
            bool hasBuoy = run.BuoyReady;
            ShowMarker(hasBuoy);
            ShowBeaconFx(hasBuoy);
            // F 홀드 중에만 진행(능동감) — 수집(E)과 키 충돌 회피. 중단해도 t 보존(부분진행 유지)
            bool keyHeld = Keyboard.current != null && Keyboard.current.fKey.isPressed;
            bool holding = ready && (keyHeld || holdOverride);
            ShowPrompt(ready && !holding);   // 준비됐는데 아직 안 누르면 'F 홀드' 안내
            ShowHoldFx(ready, InstallProgress(), holding);
            if (!holding) {
                return;
            }
            t += Time.deltaTime;
            float p = InstallProgress();
            run.SetPurify(p);   // HUD 게이지
            UpdateFill(p);      // 월드 게이지
            ShowHoldFx(true, p, true);
            if (t >= installTime) {
                done = true;
                int installed = run.InstallPendingBuoy();   // 설치 완료 후에만 수심 게이트 단계 상승
                run.SetBuoyReady(false);   // 부유체 소비
                ShowPrompt(false);
                ShowMarker(false);
                ShowBeaconFx(false);
                ShowHoldFx(false, 0f, false);
                OnPurified?.Invoke();
                Debug.Log($"[PurifyInstaller] 정화 부유체 {installed}단계 설치 완료");
            }
        }

        float InstallProgress() {
            return Mathf.Clamp01(t / Mathf.Max(installTime, 0.001f));
        }

        void EnsureHoldFx() {
            if (holdFxRoot != null) {
                return;
            }
            holdFxRoot = new GameObject("HoldInstallFx");
            holdFxRoot.transform.SetParent(transform, false);
            holdFxRoot.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            holdRing = CreateLine(holdFxRoot.transform, "HoldRing", 0.04f, new Color(0.15f, 0.95f, 1f, 0.35f));
            holdArc = CreateLine(holdFxRoot.transform, "HoldArc", 0.08f, new Color(0.45f, 1f, 0.95f, 0.95f));
            DrawCircle(holdRing, 0.9f);
            DrawArc(holdArc, 0f, 0.92f);
            holdFxRoot.SetActive(false);
        }

        LineRenderer CreateLine(Transform parent, string name, float width, Color color) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.widthMultiplier = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 6;
            line.sortingOrder = 30;
            line.material = MakeLineMaterial(name + "Mat", color);
            return line;
        }

        void EnsureBeaconFx() {
            if (beaconFxRoot != null) {
                return;
            }
            beaconFxRoot = new GameObject("PurifyTargetBeacon");
            beaconFxRoot.transform.SetParent(transform, false);
            beaconFxRoot.transform.localPosition = Vector3.zero;
            beaconRing = CreateLine(beaconFxRoot.transform, "TargetRing", 0.055f, new Color(0.18f, 0.95f, 1f, 0.75f));
            beaconPulse = CreateLine(beaconFxRoot.transform, "TargetPulse", 0.035f, new Color(0.72f, 1f, 0.94f, 0.45f));
            DrawCircle(beaconRing, 1.55f);
            DrawCircle(beaconPulse, 2.2f);
            beaconFxRoot.SetActive(false);
        }

        Material MakeLineMaterial(string name, Color color) {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { name = name, color = color };
            return mat;
        }

        void ShowHoldFx(bool show, float progress, bool holding) {
            EnsureHoldFx();
            if (holdFxRoot.activeSelf != show) {
                holdFxRoot.SetActive(show);
            }
            if (!show) {
                return;
            }
            float pulse = holding ? 1f + Mathf.Sin(Time.time * 12f) * 0.06f : 1f;
            holdFxRoot.transform.localScale = Vector3.one * pulse;
            holdFxRoot.transform.Rotate(Vector3.forward, (holding ? -110f : -28f) * Time.deltaTime, Space.Self);
            DrawArc(holdArc, Mathf.Clamp01(progress), 0.92f);
        }

        void ShowBeaconFx(bool show) {
            EnsureBeaconFx();
            if (beaconFxRoot.activeSelf != show) {
                beaconFxRoot.SetActive(show);
            }
            if (!show) {
                return;
            }
            float pulse = 1f + Mathf.Sin(Time.time * 3.5f) * 0.08f;
            beaconFxRoot.transform.localScale = Vector3.one * pulse;
            beaconFxRoot.transform.Rotate(Vector3.forward, 18f * Time.deltaTime, Space.Self);
        }

        void DrawCircle(LineRenderer line, float radius) {
            const int count = 64;
            line.positionCount = count + 1;
            for (int i = 0; i <= count; i++) {
                float a = i / (float)count * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        void DrawArc(LineRenderer line, float progress, float radius) {
            const int count = 64;
            int visible = Mathf.Clamp(Mathf.CeilToInt(count * progress), 1, count);
            line.positionCount = visible + 1;
            for (int i = 0; i <= visible; i++) {
                float a = (i / (float)count) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        // 월드 진행 게이지 스케일 갱신(있을 때만)
        void UpdateFill(float p) {
            if (progressFill != null) {
                var s = progressFill.localScale;
                progressFill.localScale = new Vector3(Mathf.Clamp01(p), s.y, s.z);
            }
        }

        // 'F 홀드' 안내 표시 토글(있을 때만)
        void ShowPrompt(bool show) {
            if (holdPrompt != null && holdPrompt.activeSelf != show) {
                holdPrompt.SetActive(show);
            }
        }

        // 구역 정화 후 재무장 — done 래치만 해제(다음 단계 부유체 재설치 허용). 진행 게이지 리셋
        public void ReArm() {
            done = false;
            inside = false;
            t = 0f;
            UpdateFill(0f);
            ShowPrompt(false);
            ShowMarker(false);
        }

        // 세션 재시작용 상태 리셋(done 래치 해제 — ResetRun과 함께 호출)
        public void ResetState() {
            done = false;
            inside = false;
            t = 0f;
            UpdateFill(0f);
            ShowPrompt(false);
            ShowMarker(false);
            if (run != null) {
                run.SetPurify(0f);
            }
        }

        // 탐사 상태 토글 — 비탐사(Dock 등)면 진행 중 설치 취소 + 신규 설치 차단
        public void SetArmed(bool v) {
            armed = v;
            if (!v) {
                inside = false;   // 텔레포트(복귀)로 OnTriggerExit이 누락돼도 잔류 제거 — 재잠수 시 자동정화 방지
                CancelInstall();
                ShowMarker(false);
                ShowBeaconFx(false);
                ShowHoldFx(false, 0f, false);
                ShowPrompt(false);
            }
        }

        // 강제 복귀 등으로 설치 중단 — 게이지/타이머 리셋(부유체는 유지해 재시도 가능)
        public void CancelInstall() {
            if (done || t <= 0f) {
                return;
            }
            t = 0f;
            UpdateFill(0f);
            ShowPrompt(false);
            if (run != null) {
                run.SetPurify(0f);   // HUD 게이지 잔류 제거
            }
            Debug.Log("[PurifyInstaller] 설치 중단 — 진행 리셋");
        }

        void ShowMarker(bool show) {
            if (marker != null && marker.activeSelf != show) {
                marker.SetActive(show);
            }
        }
    }
}
