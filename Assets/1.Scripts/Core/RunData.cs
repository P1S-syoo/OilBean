using System.Collections.Generic;
using UnityEngine;

namespace Game.Core {
    // 한 판의 진행 데이터 (자원·해금 레시피·정화 진행). 모드 전환에도 유지
    // ⚠️ 공유 자산을 직접 변형함 → GameBootstrap.Awake에서 ResetRun()으로 세션 시작 시 초기화
    //    (모드 토글로 Sub 비활성화가 생기면 세션 사본 Instantiate로 전환 검토 — S3)
    [CreateAssetMenu(fileName = "RunData", menuName = "Game/RunData")]
    public class RunData : ScriptableObject {
        [Header("적재(통합 무게)")]
        [SerializeField] float weight;        // 현재 총 적재 무게(kg) — 단일 회계
        [SerializeField] float maxWeight = 70f;
        [SerializeField] float baseMaxWeight = 70f;   // 업그레이드 전 기준(리셋용) — 기획 70kg
        [SerializeField] int scrapCount;      // 고철 개수(표시용)
        [SerializeField] int sampleCount;     // 샘플 개수(표시용)

        [Header("거점 보유 제작 소재 (복귀 시 입고)")]
        [SerializeField] float[] bankSteel = new float[3];   // 강재 등급별 누적 kg (0=일반 1=합금 2=특수)
        [SerializeField] int[] bankSample = new int[3];      // 오염수준별 보유 샘플 개수 (idx = pollutionLevel-1, lvl 1~3)

        [Header("연구")]
        [SerializeField] int researchPoints;   // 누적 분석포인트(= 분석한 샘플의 pollutionLevel 합)
        [SerializeField] int maxAnalyzedLevel; // 분석한 최고 오염수준 — 고농도 약품 게이트

        [Header("진행")]
        [SerializeField] float purify;        // 정화 진행도 0~1
        [SerializeField] bool buoyReady;      // 정화 부유체 제작 완료(설치 대기)
        [SerializeField] int buoyStage;       // 정화 부유체 단계 0~3 (수심 게이트: 0=~15m 1=~35m 2=~50m 3=클리어)
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
        public int ResearchProgress => researchProgress;
        public int ResearchPoints => researchPoints;
        public int MaxAnalyzedLevel => maxAnalyzedLevel;
        public int SurfaceTargetIdx => surfaceTargetIdx;

        // 현재 진입 가능 최대 수심(m, 양수) — 부유체 단계로 해제(기본 15 → 35 → 50)
        public float MaxDepth() {
            switch (buoyStage) {
                case 0: return 15f;
                case 1: return 35f;
                default: return 50f;
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
                bankSteel[grade] += Mathf.Max(0f, kg);
            }
        }

        // 강재 소비(제작) — 누적 부족하면 false
        public bool TryConsumeSteel(int grade, float kg) {
            EnsureBanks();
            if (grade < 0 || grade >= 3 || kg < 0f || bankSteel[grade] < kg) {
                return false;
            }
            bankSteel[grade] -= kg;
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
            }
        }

        // 샘플 1개 소비(연구 분석) — 해당 오염수준 보유분에서. 없으면 false
        public bool TryConsumeSampleAt(int level) {
            EnsureBanks();
            int i = level - 1;
            if (i < 0 || i >= 3 || bankSample[i] <= 0) {
                return false;
            }
            bankSample[i]--;
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

        // 배열 직렬화 누락/길이 불일치 방어
        void EnsureBanks() {
            if (bankSteel == null || bankSteel.Length != 3) {
                bankSteel = new float[3];
            }
            if (bankSample == null || bankSample.Length != 3) {
                bankSample = new int[3];
            }
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
            researchProgress = 0;
            researchPoints = 0;
            maxAnalyzedLevel = 0;
            maxWeight = baseMaxWeight;   // 업그레이드 초기화
            recipes.Clear();
            surfaceTargetIdx = 0;
            bankSteel = new float[3];
            bankSample = new int[3];
        }
    }
}
