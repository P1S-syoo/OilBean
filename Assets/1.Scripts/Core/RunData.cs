using System.Collections.Generic;
using UnityEngine;

namespace Game.Core {
    // 한 판의 진행 데이터 (자원·해금 레시피·정화 진행). 모드 전환에도 유지
    // ⚠️ 공유 자산을 직접 변형함 → GameBootstrap.Awake에서 ResetRun()으로 세션 시작 시 초기화
    //    (모드 토글로 Sub 비활성화가 생기면 세션 사본 Instantiate로 전환 검토 — S3)
    [CreateAssetMenu(fileName = "RunData", menuName = "Game/RunData")]
    public class RunData : ScriptableObject {
        [Header("부유체 수심 게이트")]
        // 주의: 수심 게이트는 제작설정에서 직접, 적재 한계(최대적재)는 수집설정에서 GameBootstrap.SetMaxWeightBase로 주입 — 출처 분리
        [SerializeField] 제작설정 config;     // 제작 설정 — 연결 시 부유체 단계별 수심 게이트 덮어씀(미연결 시 15/35/50 폴백)

        [Header("적재(통합 무게)")]
        [SerializeField] float weight;        // 현재 총 적재 무게(kg) — 단일 회계
        [SerializeField] float maxWeight = 70f;
        // ⚠️ 적재 한계의 단일 원천(SSOT)은 수집설정.최대적재 — GameBootstrap이 SetMaxWeightBase로 주입
        [SerializeField] float baseMaxWeight = 70f;   // 업그레이드 전 기준(리셋용) — 기획 70kg, 미연결 시 폴백
        [SerializeField] int scrapCount;      // 고철 개수(표시용)
        [SerializeField] int sampleCount;     // 샘플 개수(표시용)

        [Header("거점 보유 제작 소재 (복귀 시 입고)")]
        [SerializeField] float[] bankSteel = new float[3];   // 강재 등급별 누적 kg (0=일반 1=합금 2=특수)
        [SerializeField] int[] bankSample = new int[3];      // 오염수준별 보유 샘플 개수 (idx = pollutionLevel-1, lvl 1~3)

        [Header("이번 탐사 미정착분 (E6 강제복귀 손실 대상)")]
        [SerializeField] float[] pendingSteel = new float[3];  // 이번 탐사에 모은 강재 kg (정상복귀=보존, 강제복귀=일부 손실)
        [SerializeField] int[] pendingSample = new int[3];     // 이번 탐사에 모은 샘플 개수

        [Header("연구")]
        [SerializeField] int researchPoints;   // 누적 분석포인트(= 분석한 샘플의 pollutionLevel 합)
        [SerializeField] int maxAnalyzedLevel; // 분석한 최고 오염수준 — 고농도 약품 게이트

        [Header("진행")]
        [SerializeField] float purify;        // 정화 진행도 0~1
        [SerializeField] bool buoyReady;      // 정화 부유체 제작 완료(설치 대기)
        [SerializeField] int buoyStage;       // 정화 부유체 단계 0~3 (수심 게이트: 0=~15m 1=~35m 2=~50m 3=클리어)
        [SerializeField] bool hullArmor;      // 내압 프레임 — 오염원 충돌 1회 흡수(제작 장비)
        [SerializeField] int researchProgress; // 연구로 분석한 샘플 수(리셋 대상)
        [SerializeField] List<string> recipes = new();  // 해금된 레시피 id
        [SerializeField] int surfaceTargetIdx; // 수상 항해 현재 목표 인덱스(잠수 시 +1 기록 — 복귀 항해 재개용)

        public float Weight => weight;
        public float MaxWeight => maxWeight;
        public int ScrapCount => scrapCount;
        public int SampleCount => sampleCount;
        public float Purify => purify;
        public bool BuoyReady => buoyReady;
        public int BuoyStage => buoyStage;
        public bool HullArmor => hullArmor;
        public int ResearchProgress => researchProgress;
        public int ResearchPoints => researchPoints;
        public int MaxAnalyzedLevel => maxAnalyzedLevel;
        public int SurfaceTargetIdx => surfaceTargetIdx;

        // 현재 진입 가능 최대 수심(m, 양수) — 부유체 단계로 해제. 미연결 시 SO 기본값(제작설정.기본 = 15/35/50)
        public float MaxDepth() {
            var cfg = config != null ? config : 제작설정.기본;
            switch (buoyStage) {
                case 0: return cfg.부유수심1;
                case 1: return cfg.부유수심2;
                default: return cfg.부유수심3;
            }
        }

        // 수상 목표 인덱스 기록(잠수 시점에 다음 목표를 저장)
        public void SetSurfaceTarget(int idx) {
            surfaceTargetIdx = Mathf.Max(0, idx);
        }

        // 무게 w를 더 실을 수 있나(통합 기준)
        public bool HasRoom(float w) {
            return weight + w <= maxWeight;
        }

        // 자원 적재 시도 — 한계 초과면 실지 않고 false
        public bool TryAdd(ResourceKind kind, float w) {
            w = Mathf.Max(0f, w);
            if (!HasRoom(w)) {
                return false;
            }
            weight += w;
            if (kind == ResourceKind.Scrap) {
                scrapCount++;
            } else {
                sampleCount++;
            }
            return true;
        }

        // 샘플 1개 소비(연구 재료) — 개수↓ + 무게 차감. 없으면 false
        public bool TryConsumeSample(float unitWeight) {
            if (sampleCount <= 0) {
                return false;
            }
            sampleCount--;
            weight = Mathf.Max(0f, weight - Mathf.Max(0f, unitWeight));
            return true;
        }

        // 고철 n개 소비(제작 재료) — 개수↓ + 무게 차감. 부족하면 false
        public bool TryConsumeScrap(int n, float unitWeight) {
            if (n <= 0 || scrapCount < n) {
                return false;
            }
            scrapCount -= n;
            weight = Mathf.Max(0f, weight - n * Mathf.Max(0f, unitWeight));
            return true;
        }

        // ── 거점 보유 소재 (강재 등급별 kg) ──
        public float GetSteel(int grade) {
            return (grade >= 0 && grade < 3 && bankSteel != null) ? bankSteel[grade] : 0f;
        }

        public void AddSteel(int grade, float kg) {
            if (grade >= 0 && grade < 3) {
                EnsureBanks();
                float v = Mathf.Max(0f, kg);
                bankSteel[grade] += v;
                pendingSteel[grade] += v;   // 이번 탐사 미정착분으로도 추적(강제복귀 손실 대상)
            }
        }

        // 강재 소비(제작) — 누적 부족하면 false
        public bool TryConsumeSteel(int grade, float kg) {
            EnsureBanks();
            if (grade < 0 || grade >= 3 || kg < 0f || bankSteel[grade] < kg) {
                return false;
            }
            bankSteel[grade] -= kg;
            // 소비한 만큼 미정착분도 감소 — 강제복귀 페널티 과다 방지(이미 제작에 쓴 분은 손실 대상 아님)
            pendingSteel[grade] = Mathf.Max(0f, pendingSteel[grade] - kg);
            return true;
        }

        // ── 거점 보유 샘플 (오염수준 1~3별 개수) ──
        public int GetSampleCount(int level) {
            int i = level - 1;
            return (i >= 0 && i < 3 && bankSample != null) ? bankSample[i] : 0;
        }

        public void AddSampleAt(int level) {
            int i = level - 1;
            if (i >= 0 && i < 3) {
                EnsureBanks();
                bankSample[i]++;
                pendingSample[i]++;   // 이번 탐사 미정착분으로도 추적(강제복귀 손실 대상)
            }
        }

        // 거점 보유 샘플 총합(연구 UI 표시용)
        public int TotalBankSamples => GetSampleCount(1) + GetSampleCount(2) + GetSampleCount(3);

        // 샘플 1개 소비(연구 분석) — 해당 오염수준 보유분에서. 없으면 false
        public bool TryConsumeSampleAt(int level) {
            EnsureBanks();
            int i = level - 1;
            if (i < 0 || i >= 3 || bankSample[i] <= 0) {
                return false;
            }
            bankSample[i]--;
            // 소비한 만큼 미정착분도 감소 — 강제복귀 페널티 과다 방지
            pendingSample[i] = Mathf.Max(0, pendingSample[i] - 1);
            return true;
        }

        // 분석포인트 누적 + 최고 분석 오염수준 갱신(약품 게이트용)
        public void AddResearchPoints(int level) {
            researchPoints += Mathf.Max(0, level);
            maxAnalyzedLevel = Mathf.Max(maxAnalyzedLevel, level);
        }

        // 정화 부유체 단계 설정(수심 게이트)
        public void SetBuoyStage(int s) {
            buoyStage = Mathf.Clamp(s, 0, 3);
        }

        // 내압 프레임 장착/해제
        public void SetHullArmor(bool v) {
            hullArmor = v;
        }

        // 충돌 시 내압 프레임으로 1회 흡수 — 흡수했으면 true(장갑 소진)
        public bool ConsumeHullArmor() {
            if (hullArmor) {
                hullArmor = false;
                return true;
            }
            return false;
        }

        // 배열 직렬화 누락/길이 불일치 방어
        void EnsureBanks() {
            if (bankSteel == null || bankSteel.Length != 3) {
                bankSteel = new float[3];
            }
            if (bankSample == null || bankSample.Length != 3) {
                bankSample = new int[3];
            }
            if (pendingSteel == null || pendingSteel.Length != 3) {
                pendingSteel = new float[3];
            }
            if (pendingSample == null || pendingSample.Length != 3) {
                pendingSample = new int[3];
            }
        }

        // 정상 복귀 — 이번 탐사 미정착분을 거점에 확정(추적만 비움, bank는 이미 반영됨)
        public void CommitDive() {
            EnsureBanks();
            System.Array.Clear(pendingSteel, 0, pendingSteel.Length);
            System.Array.Clear(pendingSample, 0, pendingSample.Length);
        }

        // 강제 복귀 페널티 — 이번 탐사 미정착분의 lossRatio 비율을 거점 보유분에서 차감. 잃은 샘플 총개수 반환(토스트용)
        public int ForfeitDive(float lossRatio) {
            EnsureBanks();
            lossRatio = Mathf.Clamp01(lossRatio);
            for (int g = 0; g < 3; g++) {
                float lost = Mathf.Min(pendingSteel[g] * lossRatio, bankSteel[g]);   // bank 잔량 초과 손실 방지
                bankSteel[g] = Mathf.Max(0f, bankSteel[g] - lost);
            }
            int lostSamples = 0;
            for (int i = 0; i < 3; i++) {
                int lost = Mathf.RoundToInt(pendingSample[i] * lossRatio);
                lost = Mathf.Min(lost, bankSample[i]);
                bankSample[i] -= lost;
                lostSamples += lost;
            }
            System.Array.Clear(pendingSteel, 0, pendingSteel.Length);
            System.Array.Clear(pendingSample, 0, pendingSample.Length);
            return lostSamples;
        }

        // 탐사 적재 비우기(복귀 시) — 거점 보유분(bank)은 수집 즉시 입고되므로 유지, 세션 적재만 초기화
        public void ClearCarry() {
            weight = 0f;
            scrapCount = 0;
            sampleCount = 0;
        }

        // 무게 한계 업그레이드
        public void AddMaxWeight(float d) {
            maxWeight += Mathf.Max(0f, d);
        }

        // 적재 한계 기준값 설정(통합 설정 적용) — base/current 동시 갱신, ResetRun 후 호출 전제
        public void SetMaxWeightBase(float w) {
            baseMaxWeight = Mathf.Max(0f, w);
            maxWeight = baseMaxWeight;
        }

        // 정화 부유체 제작 완료 표시
        public void SetBuoyReady(bool v) {
            buoyReady = v;
        }

        // 연구 진행 1스텝(분석한 샘플 수↑)
        public void AddResearch() {
            researchProgress++;
        }

        // 레시피 해금/조회
        public void Unlock(string id) {
            if (!string.IsNullOrEmpty(id) && !recipes.Contains(id)) {
                recipes.Add(id);
            }
        }

        public bool IsUnlocked(string id) => recipes.Contains(id);

        public void SetPurify(float v) {
            purify = Mathf.Clamp01(v);
        }

        // 새 판 초기화
        public void ResetRun() {
            weight = 0f;
            scrapCount = 0;
            sampleCount = 0;
            purify = 0f;
            buoyReady = false;
            buoyStage = 0;
            hullArmor = false;
            researchProgress = 0;
            researchPoints = 0;
            maxAnalyzedLevel = 0;
            maxWeight = baseMaxWeight;   // 업그레이드 초기화
            recipes.Clear();
            surfaceTargetIdx = 0;
            bankSteel = new float[3];
            bankSample = new int[3];
            pendingSteel = new float[3];
            pendingSample = new int[3];
        }
    }
}
