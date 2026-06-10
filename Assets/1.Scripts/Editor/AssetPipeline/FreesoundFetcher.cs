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
                // 토큰은 URL 쿼리스트링 대신 헤더로 전달(URL 로그 노출 방지)
                string url = "https://freesound.org/apiv2/search/text/?query=" + UnityWebRequest.EscapeURL(query)
                    + "&filter=" + UnityWebRequest.EscapeURL("license:\"Creative Commons 0\"")
                    + "&fields=name,license,previews&page_size=" + Mathf.Clamp(count, 1, 15);
                string json;
                using (var req = UnityWebRequest.Get(url)) {
                    req.timeout = 30;   // 무한 대기 방지
                    req.SetRequestHeader("Authorization", "Token " + Token);
                    var op = req.SendWebRequest();
                    while (!op.isDone) {
                        // 사용자가 취소하면 요청 중단
                        if (EditorUtility.DisplayCancelableProgressBar("Freesound 검색", query, 0.3f)) {
                            req.Abort();
                            EditorUtility.ClearProgressBar();
                            Debug.LogWarning("[Freesound] 사용자 취소(검색)");
                            return 0;
                        }
                        System.Threading.Thread.Sleep(16);   // CPU 점유 감소
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
        // preview 매치 위치 앞의 가장 가까운 name을 짝으로 삼아 result 객체 단위로 페어링
        static List<KeyValuePair<string, string>> ExtractPreviews(string json) {
            var list = new List<KeyValuePair<string, string>>();
            try {
                var names = Regex.Matches(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                var urls = Regex.Matches(json, "\"preview-hq-mp3\"\\s*:\\s*\"([^\"]+)\"");
                for (int i = 0; i < urls.Count; i++) {
                    string u = urls[i].Groups[1].Value.Replace("\\/", "/");
                    int urlIdx = urls[i].Index;
                    // url 위치보다 앞에 있는 name 중 가장 가까운 것을 짝으로 선택
                    string nm = "sound" + i;
                    int bestDist = int.MaxValue;
                    foreach (Match nm_match in names) {
                        if (nm_match.Index < urlIdx) {
                            int dist = urlIdx - nm_match.Index;
                            if (dist < bestDist) {
                                bestDist = dist;
                                nm = nm_match.Groups[1].Value;
                            }
                        }
                    }
                    list.Add(new KeyValuePair<string, string>(u, nm));
                }
            } catch (System.Exception e) {
                Debug.LogError($"[Freesound] JSON 파싱 오류: {e.Message}");
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
                    req.timeout = 30;   // 무한 대기 방지
                    var op = req.SendWebRequest();
                    while (!op.isDone) {
                        // 사용자가 취소하면 요청 중단
                        if (EditorUtility.DisplayCancelableProgressBar("Freesound 다운로드", name, req.downloadProgress)) {
                            req.Abort();
                            EditorUtility.ClearProgressBar();
                            Debug.LogWarning($"[Freesound] 사용자 취소(다운로드): {name}");
                            return false;
                        }
                        System.Threading.Thread.Sleep(16);   // CPU 점유 감소
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
