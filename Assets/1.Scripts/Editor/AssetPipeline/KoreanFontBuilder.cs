using System.IO;
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace Game.EditorTools {
    // 한글 TTF → Dynamic SDF TMP 폰트에셋 생성 + TMP 기본 폰트로 지정
    // 한글은 글리프가 1만+개라 Dynamic(필요 글자만 런타임 래스터)로 생성해야 아틀라스가 안 터진다
    public static class KoreanFontBuilder {
        const string TTF_PATH = "Assets/4.Art/Fonts/IyagiGGC.ttf";
        const string SDF_PATH = "Assets/4.Art/Fonts/IyagiGGC SDF.asset";

        [MenuItem("Tools/한강/폰트/IyagiGGC TMP 생성·기본지정")]
        public static void Build() {
            try {
                var font = AssetDatabase.LoadAssetAtPath<Font>(TTF_PATH);
                if (font == null) {
                    Debug.LogError($"[KoreanFontBuilder] TTF 미발견: {TTF_PATH}");
                    return;
                }

                var fontAsset = CreateOrReuseFontAsset(font);
                if (fontAsset == null) {
                    return;
                }
                SetAsTmpDefault(fontAsset);
                // IyagiGGC에 없는 글리프(—, 특수문자 등)는 NotoSansKR로 폴백 — 두부(□) 방지
                AddFallback(fontAsset);
                // UITheme static에도 즉시 주입(에디터 빌더가 만드는 텍스트용)
                Game.UI.UITheme.UIFont = fontAsset;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[KoreanFontBuilder] IyagiGGC SDF 생성·기본지정 완료");
            } catch (System.Exception e) {
                Debug.LogError($"[KoreanFontBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // NotoSansKR SDF를 폴백으로 등록 — IyagiGGC에 없는 글자를 대체 렌더(멱등)
        static void AddFallback(TMP_FontAsset fontAsset) {
            try {
                var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/4.Art/Fonts/NotoSansKR SDF.asset");
                if (noto == null) {
                    Debug.LogWarning("[KoreanFontBuilder] NotoSansKR SDF 미발견 — 폴백 등록 생략");
                    return;
                }
                if (fontAsset.fallbackFontAssetTable == null) {
                    fontAsset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                }
                // 이미 들어있으면 중복 추가 안 함
                if (!fontAsset.fallbackFontAssetTable.Contains(noto)) {
                    fontAsset.fallbackFontAssetTable.Add(noto);
                    EditorUtility.SetDirty(fontAsset);
                    Debug.Log("[KoreanFontBuilder] 폴백 등록: NotoSansKR SDF");
                } else {
                    Debug.Log("[KoreanFontBuilder] 폴백 이미 등록됨");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[KoreanFontBuilder] 폴백 등록 오류: {e.Message}");
            }
        }

        // Dynamic SDF 폰트에셋 생성(이미 있으면 재사용·멱등). 아틀라스/머티리얼을 서브에셋으로 저장
        static TMP_FontAsset CreateOrReuseFontAsset(Font font) {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SDF_PATH);
            if (existing != null) {
                Debug.Log("[KoreanFontBuilder] 기존 SDF 재사용");
                return existing;
            }
            try {
                // 샘플 90pt · 패딩 9 · SDFAA · 1024 아틀라스 · Dynamic · 멀티아틀라스 허용
                var fa = TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (fa == null) {
                    Debug.LogError("[KoreanFontBuilder] CreateFontAsset 실패(null 반환)");
                    return null;
                }
                fa.name = "IyagiGGC SDF";
                AssetDatabase.CreateAsset(fa, SDF_PATH);

                // 아틀라스 텍스처·머티리얼을 서브에셋으로 묶어 영속화(없으면 import 시 사라짐)
                if (fa.atlasTextures != null && fa.atlasTextures.Length > 0) {
                    fa.atlasTextures[0].name = "IyagiGGC SDF Atlas";
                    AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                }
                if (fa.material != null) {
                    fa.material.name = "IyagiGGC SDF Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(SDF_PATH);
                Debug.Log("[KoreanFontBuilder] Dynamic SDF 생성");
                return fa;
            } catch (System.Exception e) {
                Debug.LogError($"[KoreanFontBuilder] 폰트에셋 생성 오류: {e.Message}");
                return null;
            }
        }

        // TMP Settings의 기본 폰트로 지정 — 런타임 포함 모든 TMP 텍스트가 자동 사용
        static void SetAsTmpDefault(TMP_FontAsset fontAsset) {
            try {
                var settings = Resources.Load<TMP_Settings>("TMP Settings");
                if (settings == null) {
                    Debug.LogWarning("[KoreanFontBuilder] TMP Settings 미발견 — 기본 폰트 지정 생략(UITheme.UIFont만 적용)");
                    return;
                }
                var so = new SerializedObject(settings);
                var defProp = so.FindProperty("m_defaultFontAsset");
                if (defProp != null) {
                    defProp.objectReferenceValue = fontAsset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                    Debug.Log("[KoreanFontBuilder] TMP 기본 폰트 = IyagiGGC SDF");
                } else {
                    Debug.LogWarning("[KoreanFontBuilder] m_defaultFontAsset 프로퍼티 미발견");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[KoreanFontBuilder] TMP 기본 폰트 지정 오류: {e.Message}");
            }
        }
    }
}
