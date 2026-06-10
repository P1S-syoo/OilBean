using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Editor {
    // Freesound API(v2)로 CC0 사운드 검색 + 미리듣기(mp3) 다운로드
    // 원본 wav는 OAuth2(사용자 로그인) 필요 → 프로토타입은 미리듣기로 충분
    public static class FreesoundFetcher {
        const string TokenPref = "Freesound.ApiToken";   // EditorPrefs(레포 비노출)
        const string SaveDir = "Assets/5.Audio/Freesound";

        // API 토큰(머신 로컬 저장 — 코드/레포에 하드코딩 금지)
        public static string Token {
            get { return EditorPrefs.GetString(TokenPref, ""); }
            set { EditorPrefs.SetString(TokenPref, value); }
        }

        // CC0 사운드 검색 → 상위 count개 미리듣기 다운로드. 성공 개수 반환
        public static int SearchAndFetch(string query, int count) {
            if (string.IsNullOrEmpty(Token)) {
                Debug.LogError("[Freesound] API 토큰 미설정 — FreesoundFetcher.Token 설정 필요");
                return 0;
            }
            try {
                string url = "https://freesound.org/apiv2/search/text/?query=" + UnityWebRequest.EscapeURL(query)
                    + "&filter=" + UnityWebRequest.EscapeURL("license:\"Creative Commons 0\"")
                    + "&fields=name,license,previews&page_size=" + Mathf.Clamp(count, 1, 15)
                    + "&token=" + Token;
                string json;
                using (var req = UnityWebRequest.Get(url)) {
                    var op = req.SendWebRequest();
                    while (!op.isDone) {
                        EditorUtility.DisplayProgressBar("Freesound 검색", query, 0.3f);
                    }
                    EditorUtility.ClearProgressBar();
                    if (req.result != UnityWebRequest.Result.Success) {
                        Debug.LogError($"[Freesound] 검색 실패({req.responseCode}): {req.error}");
                        return 0;
                    }
                    json = req.downloadHandler.text;
                }
                var items = ExtractPreviews(json);
                Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", SaveDir)));
                int ok = 0;
                foreach (var it in items) {
                    if (DownloadPreview(it.Key, it.Value)) {
                        ok++;
                    }
                }
                AssetDatabase.Refresh();
                Debug.Log($"[Freesound] '{query}' CC0 {ok}/{items.Count}개 미리듣기 다운로드");
                return ok;
            } catch (System.Exception e) {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Freesound] 오류: {e.Message}");
                return 0;
            }
        }

        // 응답 JSON에서 (preview-hq-mp3 url, name) 추출 — 하이픈 키라 정규식 파싱
        static List<KeyValuePair<string, string>> ExtractPreviews(string json) {
            var list = new List<KeyValuePair<string, string>>();
            var names = Regex.Matches(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            var urls = Regex.Matches(json, "\"preview-hq-mp3\"\\s*:\\s*\"([^\"]+)\"");
            for (int i = 0; i < urls.Count; i++) {
                string u = urls[i].Groups[1].Value.Replace("\\/", "/");
                string nm = i < names.Count ? names[i].Groups[1].Value : ("sound" + i);
                list.Add(new KeyValuePair<string, string>(u, nm));
            }
            return list;
        }

        // 미리듣기 mp3 1건 다운로드(공개 CDN — 토큰 불필요)
        static bool DownloadPreview(string url, string name) {
            try {
                string safe = Regex.Replace(name, "[^a-zA-Z0-9_-]", "_");
                if (safe.Length > 40) {
                    safe = safe.Substring(0, 40);
                }
                string dest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SaveDir, safe + ".mp3"));
                using (var req = UnityWebRequest.Get(url)) {
                    var op = req.SendWebRequest();
                    while (!op.isDone) {
                        EditorUtility.DisplayProgressBar("Freesound 다운로드", name, req.downloadProgress);
                    }
                    EditorUtility.ClearProgressBar();
                    if (req.result != UnityWebRequest.Result.Success) {
                        Debug.LogError($"[Freesound] preview 실패({req.responseCode}): {name}");
                        return false;
                    }
                    File.WriteAllBytes(dest, req.downloadHandler.data);
                }
                return true;
            } catch (System.Exception e) {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Freesound] preview 오류({name}): {e.Message}");
                return false;
            }
        }
    }
}
