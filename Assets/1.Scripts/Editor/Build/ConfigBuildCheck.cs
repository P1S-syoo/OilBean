using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Game.Core;

namespace Game.Editor.Build {
    // 빌드 프리프로세서 — 10개 카테고리 설정 .asset 중 하나라도 없으면 빌드를 중단시킨다(완료 게이트 C2)
    // 누락 자산이 빌드에 섞여 폴백 기본값으로 출시되는 사고를 방지
    public class ConfigBuildCheck : IPreprocessBuildWithReport {
        const string ConfigDir = "Assets/3.Data/Config";

        // 검사 대상 10개 카테고리 타입 (게임설정도구.CategoryTypes와 동일해야 함)
        static readonly Type[] CategoryTypes = {
            typeof(수면위설정),
            typeof(잠수설정),
            typeof(월드설정),
            typeof(오디오설정),
            typeof(위험설정),
            typeof(수집설정),
            typeof(제작설정),
            typeof(점수설정),
            typeof(연출설정),
            typeof(디버그설정),
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            var missing = new List<string>();
            try {
                foreach (var t in CategoryTypes) {
                    string path = $"{ConfigDir}/{t.Name}.asset";
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null) {
                        missing.Add(path);
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[ConfigBuildCheck] 설정 자산 점검 중 오류: {e.Message}");
                throw new BuildFailedException($"[ConfigBuildCheck] 설정 자산 점검 실패: {e.Message}");
            }
            if (missing.Count > 0) {
                var sb = new StringBuilder();
                sb.AppendLine($"[ConfigBuildCheck] 설정 자산 {missing.Count}개 누락 — 빌드 중단:");
                foreach (var path in missing) {
                    sb.AppendLine($"  - {path}");
                }
                sb.Append("게임설정/설정 생성 및 배선 메뉴로 생성 후 다시 빌드하세요");
                Debug.LogError(sb.ToString());
                throw new BuildFailedException(sb.ToString());
            }
            Debug.Log($"[ConfigBuildCheck] 설정 자산 {CategoryTypes.Length}개 확인 — 빌드 진행");
        }
    }
}
