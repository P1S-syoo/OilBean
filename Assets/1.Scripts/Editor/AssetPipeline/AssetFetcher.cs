using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Editor {
    // 선언적 AssetEntry를 받아 다운로드 → 저장 → 크레딧 기록. 위험 라이선스는 거부
    public static class AssetFetcher {
        // 단건 조달(에디터 동기 — 진행바 블로킹). 성공 시 true
        public static bool Fetch(AssetEntry e) {
            // 라이선스 안전 게이트(차단 목록이면 다운로드 자체를 막음)
            if (AssetCatalog.IsBlocked(e.license)) {
                Debug.LogError($"[Fetch] 차단된 라이선스({e.license}) — 조달 거부: {e.name}");
                return false;
            }
            try {
                using (var req = UnityWebRequest.Get(e.url)) {
                    var op = req.SendWebRequest();
                    while (!op.isDone) {
                        EditorUtility.DisplayProgressBar("리소스 조달", e.name, req.downloadProgress);
                    }
                    EditorUtility.ClearProgressBar();
                    if (req.result != UnityWebRequest.Result.Success) {
                        Debug.LogError($"[Fetch] 다운로드 실패({req.responseCode}): {e.name} — {req.error}");
                        return false;
                    }
                    string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", e.savePath));
                    if (e.unzip) {
                        Unzip(req.downloadHandler.data, abs);   // ZIP → savePath 폴더에 해제
                    } else {
                        Directory.CreateDirectory(Path.GetDirectoryName(abs));
                        File.WriteAllBytes(abs, req.downloadHandler.data);
                    }
                }
                AssetDatabase.Refresh();
                // OFL/CC-BY는 크레딧 의무
                if (AssetCatalog.NeedsAttribution(e.license)) {
                    CreditsWriter.Add(e);
                }
                Debug.Log($"[Fetch] 완료: {e.savePath}");
                return true;
            } catch (System.Exception ex) {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Fetch] 오류: {e.name} — {ex.Message}");
                return false;
            }
        }

        // ZIP 바이트를 폴더에 해제(엔트리 스트림 복사 — 호환성 우선)
        static void Unzip(byte[] data, string destDir) {
            Directory.CreateDirectory(destDir);
            using (var ms = new MemoryStream(data))
            using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read)) {
                foreach (var entry in zip.Entries) {
                    if (string.IsNullOrEmpty(entry.Name)) {
                        continue;   // 디렉토리 항목 건너뜀
                    }
                    string dest = Path.Combine(destDir, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var es = entry.Open())
                    using (var fs = File.Create(dest)) {
                        es.CopyTo(fs);
                    }
                }
            }
        }
    }
}
