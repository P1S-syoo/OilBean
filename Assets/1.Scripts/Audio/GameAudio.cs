using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Items;
using Game.Player;
using Game.Stage;
using Game.Craft;

namespace Game.Audio {
    // 게임 오디오 허브 — 상태별 BGM 전환 + 이벤트 효과음 + 캐릭터 대사(보이스 큐) 재생
    public class GameAudio : MonoBehaviour {
        [Header("배선 대상")]
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Collector collector;
        [SerializeField] Battery battery;
        [SerializeField] HazardDetector hazard;
        [SerializeField] PurifyInstaller purify;
        [SerializeField] Crafting crafting;
        [SerializeField] Research research;

        [Header("BGM")]
        [SerializeField] AudioClip bgmDive;
        [SerializeField] AudioClip bgmSurface;
        [SerializeField] float bgmVolume;   // 배경음 볼륨 — 기본값은 오디오설정.배경음크기
        [SerializeField] float bgmFade;     // 배경음 교차 페이드 — 기본값은 오디오설정.배경음전환시간

        [Header("SFX")]
        [SerializeField] AudioClip sfxCollect;
        [SerializeField] AudioClip sfxInvFull;
        [SerializeField] AudioClip sfxHazardHit;
        [SerializeField] AudioClip sfxPurifyDone;
        [SerializeField] AudioClip sfxStageClear;
        [SerializeField] AudioClip sfxToast;
        [SerializeField] AudioClip sfxBatteryLow;
        [SerializeField] AudioClip sfxDive;
        [SerializeField] AudioClip sfxCraftDenied;   // 제작 불가 클릭 — 둔탁한 거부음
        [SerializeField] AudioClip sfxCraftDone;     // 제작/업그레이드 완료음
        [SerializeField] AudioClip sfxMgHit;         // 수집 미니게임 명중
        [SerializeField] AudioClip sfxMgMiss;        // 미니게임 빗나감/실패
        [SerializeField] AudioClip sfxNodePass;      // 한붓그리기 노드 통과
        [SerializeField] AudioClip sfxPuzzleDone;    // 미니게임 성공 완성
        [SerializeField] AudioClip sfxSlotPlace;     // 제작 슬롯 배치
        [SerializeField] AudioClip sfxHazardWarn;    // 위험체 돌진 경고
        [SerializeField] AudioClip sfxUiClick;       // UI 버튼 클릭
        [SerializeField] float sfxVolume;   // 효과음 볼륨 — 기본값은 오디오설정.효과음크기

        [Header("오디오 설정")]
        [SerializeField] 오디오설정 config;   // 오디오 설정 — 연결 시 볼륨/페이드 덮어씀(미연결 시 위 기본값 유지)

        [Header("캐릭터 대사")]
        [SerializeField] AudioClip voiceIntro;
        [SerializeField] AudioClip voiceDive;
        [SerializeField] AudioClip voiceInvFull;
        [SerializeField] AudioClip voiceBattery;
        [SerializeField] AudioClip voiceHazard;
        [SerializeField] AudioClip voiceBuoy;
        [SerializeField] AudioClip voicePurify;
        [SerializeField] AudioClip voiceClear;
        [SerializeField] float voiceVolume = 1f;

        // 상태별 BGM 종류 — 매핑을 순수 함수로 분리(테스트 가능)
        public enum BgmKind { Surface, Dive }

        AudioSource bgmSource;
        AudioSource sfxSource;
        AudioSource voiceSource;
        readonly Queue<AudioClip> voiceQueue = new Queue<AudioClip>();
        Coroutine fading;

        // UI(static)·미니게임에서 효과음을 간단히 재생하기 위한 전역 접근점
        public static GameAudio Instance { get; private set; }

        void Awake() {
            Instance = this;
            try {
                // 오디오 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거, 소스 생성 전에 적용)
                var cfg = config != null ? config : 오디오설정.기본;
                bgmVolume = cfg.배경음크기;
                bgmFade = cfg.배경음전환시간;
                sfxVolume = cfg.효과음크기;
                voiceVolume = cfg.음성크기;
                bgmSource = CreateSource("BgmSource", true);
                sfxSource = CreateSource("SfxSource", false);
                voiceSource = CreateSource("VoiceSource", false);
            } catch (Exception e) {
                Debug.LogError($"[GameAudio] 오디오 소스 생성 실패: {e.Message}");
            }
        }

        void Start() {
            if (bootstrap == null) {
                Debug.LogWarning("[GameAudio] GameBootstrap 미연결 — 상태 BGM/대사 비활성");
            } else {
                bootstrap.OnStateChanged += HandleStateChanged;
                ApplyBgm(bootstrap.State, true);
                // 수상 시작이면 작전 개시 안내 대사
                if (bootstrap.State == GameState.Surface) {
                    PlayVoice(voiceIntro);
                }
            }
            if (collector != null) {
                collector.OnCollect += HandleCollect;
                collector.OnFull += HandleInvFull;
            }
            if (battery != null) {
                battery.OnEmpty += HandleBatteryEmpty;
            }
            if (hazard != null) {
                hazard.OnHit += HandleHazardHit;
            }
            if (purify != null) {
                purify.OnPurified += HandlePurified;
            }
            if (crafting != null) {
                crafting.OnBuoyCrafted += HandleBuoyCrafted;
                crafting.OnUpgraded += HandleUpgraded;
            }
            if (research != null) {
                research.OnUnlocked += HandleToast;
            }
        }

        void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= HandleStateChanged;
            }
            if (collector != null) {
                collector.OnCollect -= HandleCollect;
                collector.OnFull -= HandleInvFull;
            }
            if (battery != null) {
                battery.OnEmpty -= HandleBatteryEmpty;
            }
            if (hazard != null) {
                hazard.OnHit -= HandleHazardHit;
            }
            if (purify != null) {
                purify.OnPurified -= HandlePurified;
            }
            if (crafting != null) {
                crafting.OnBuoyCrafted -= HandleBuoyCrafted;
                crafting.OnUpgraded -= HandleUpgraded;
            }
            if (research != null) {
                research.OnUnlocked -= HandleToast;
            }
        }

        void Update() {
            // 보이스 큐 소비 — 재생 중이 아니면 다음 대사 재생
            if (voiceQueue.Count > 0 && voiceSource != null && !voiceSource.isPlaying) {
                var next = voiceQueue.Dequeue();
                voiceSource.clip = next;
                voiceSource.Play();
            }
        }

        // 상태 → BGM 매핑(순수 함수) — 탐사 중에만 수중 BGM, 그 외는 수상 BGM
        public static BgmKind BgmFor(GameState state) {
            return state == GameState.Dive ? BgmKind.Dive : BgmKind.Surface;
        }

        void HandleStateChanged(GameState from, GameState to) {
            if (to == GameState.Dive) {
                PlaySfx(sfxDive);
                PlayVoice(voiceDive);
            }
            if (to == GameState.Clear) {
                PlaySfx(sfxStageClear);
                PlayVoice(voiceClear);
            }
            ApplyBgm(to, false);
        }

        void HandleCollect(ResourceKind kind) => PlaySfx(sfxCollect);
        void HandleToast() => PlaySfx(sfxToast);

        void HandleInvFull() {
            PlaySfx(sfxInvFull);
            PlayVoice(voiceInvFull);
        }

        void HandleBatteryEmpty() {
            PlaySfx(sfxBatteryLow);
            PlayVoice(voiceBattery);
        }

        void HandleHazardHit() {
            PlaySfx(sfxHazardHit);
            PlayVoice(voiceHazard);
        }

        void HandleBuoyCrafted() {
            PlaySfx(sfxCraftDone);   // 부유체 제작 완료음
            PlayVoice(voiceBuoy);
        }

        // 업그레이드(적재/배터리/내압) 완료음 — 연구 해금(HandleToast)과 분리
        void HandleUpgraded() {
            PlaySfx(sfxCraftDone);
        }

        // 제작 불가 클릭 — 둔탁한 거부음(UI에서 호출)
        public void PlayCraftDenied() {
            PlaySfx(sfxCraftDenied);
        }

        // ── 미니게임/UI 효과음(static Instance로 외부에서 호출) ──
        public void PlayMgHit()      => PlaySfx(sfxMgHit);
        public void PlayMgMiss()     => PlaySfx(sfxMgMiss);
        public void PlayNodePass()   => PlaySfx(sfxNodePass);
        public void PlayPuzzleDone() => PlaySfx(sfxPuzzleDone);
        public void PlaySlotPlace()  => PlaySfx(sfxSlotPlace);
        public void PlayHazardWarn() => PlaySfx(sfxHazardWarn);
        public void PlayUiClick()    => PlaySfx(sfxUiClick);

        void HandlePurified() {
            PlaySfx(sfxPurifyDone);
            PlayVoice(voicePurify);
        }

        // 효과음 1회 재생(클립 미할당이면 무시)
        public void PlaySfx(AudioClip clip) {
            if (clip == null || sfxSource == null) {
                return;
            }
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // 대사 재생 — 재생 중이면 큐 대기(최대 3개, 초과분은 버림)
        public void PlayVoice(AudioClip clip) {
            if (clip == null || voiceSource == null) {
                return;
            }
            if (voiceSource.isPlaying || voiceQueue.Count > 0) {
                if (voiceQueue.Count < 3) {
                    voiceQueue.Enqueue(clip);
                }
                return;
            }
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        // 상태에 맞는 BGM 적용 — 같은 곡이면 유지, 다르면 페이드 전환
        void ApplyBgm(GameState state, bool instant) {
            AudioClip target = BgmFor(state) == BgmKind.Dive ? bgmDive : bgmSurface;
            if (target == null || bgmSource == null || bgmSource.clip == target) {
                return;
            }
            if (fading != null) {
                StopCoroutine(fading);
                fading = null;
            }
            if (instant || !isActiveAndEnabled) {
                bgmSource.clip = target;
                bgmSource.volume = bgmVolume;
                bgmSource.Play();
                return;
            }
            fading = StartCoroutine(FadeTo(target));
        }

        // 현재 곡 페이드아웃 → 교체 → 페이드인
        IEnumerator FadeTo(AudioClip target) {
            float half = Mathf.Max(0.05f, bgmFade * 0.5f);
            float from = bgmSource.volume;
            for (float t = 0f; t < half; t += Time.deltaTime) {
                bgmSource.volume = Mathf.Lerp(from, 0f, t / half);
                yield return null;
            }
            bgmSource.clip = target;
            bgmSource.Play();
            for (float t = 0f; t < half; t += Time.deltaTime) {
                bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t / half);
                yield return null;
            }
            bgmSource.volume = bgmVolume;
            fading = null;
        }

        // 2D 오디오 소스 자식으로 생성
        AudioSource CreateSource(string name, bool loop) {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = loop ? bgmVolume : 1f;
            return src;
        }
    }
}
