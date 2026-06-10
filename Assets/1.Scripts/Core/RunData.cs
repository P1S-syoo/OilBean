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
        [SerializeField] float maxWeight = 50f;
        [SerializeField] float baseMaxWeight = 50f;   // 업그레이드 전 기준(리셋용)
        [SerializeField] int scrapCount;      // 고철 개수(표시용)
        [SerializeField] int sampleCount;     // 샘플 개수(표시용)

        [Header("진행")]
        [SerializeField] float purify;        // 정화 진행도 0~1
        [SerializeField] bool buoyReady;      // 정화 부유체 제작 완료(설치 대기)
        [SerializeField] int researchProgress; // 연구로 분석한 샘플 수(리셋 대상)
        [SerializeField] List<string> recipes = new();  // 해금된 레시피 id

        public float Weight => weight;
        public float MaxWeight => maxWeight;
        public int ScrapCount => scrapCount;
        public int SampleCount => sampleCount;
        public float Purify => purify;
        public bool BuoyReady => buoyReady;
        public int ResearchProgress => researchProgress;

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
            researchProgress = 0;
            maxWeight = baseMaxWeight;   // 업그레이드 초기화
            recipes.Clear();
        }
    }
}
