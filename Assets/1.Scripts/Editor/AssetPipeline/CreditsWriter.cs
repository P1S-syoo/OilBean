using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor {
    // CC-BY/OFL 에셋의 출처·라이선스를 CREDITS.txt에 중복 없이 기록
    public static class CreditsWriter {
        const string CreditsPath = "Assets/CREDITS.txt";

        // 항목 1건 기록(이미 있으면 무시)
        public static void Add(AssetEntry e) {
            try {
                string line = $"- {e.name} | {e.license} | {e.author} | {e.source}";
                string body = File.Exists(CreditsPath) ? File.ReadAllText(CreditsPath) : "# 에셋 크레딧 (자동 생성 — 배포 시 동봉)\n\n";
                if (body.Contains(line)) {
                    return;   // 중복 방지
                }
                body += line + "\n";
                File.WriteAllText(CreditsPath, body);
                AssetDatabase.Refresh();
                Debug.Log($"[Credits] 기록: {e.name}");
            } catch (System.Exception ex) {
                Debug.LogError($"[Credits] 기록 실패: {ex.Message}");
            }
        }
    }
}
