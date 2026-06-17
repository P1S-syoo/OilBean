using UnityEngine;
using TMPro;

namespace Game.UI {
    // 런타임 한글 폰트 주입기 — 씬에 1개 배치하고 koreanFont에 IyagiGGC SDF(또는 교체 폰트)를 배선한다.
    // UITheme.UIFont는 에디터 빌더에서만 세팅돼 플레이(런타임) 생성 텍스트가 □로 깨지는 문제를 막는다.
    // DefaultExecutionOrder(-1000)로 다른 UI의 Awake보다 먼저 주입한다.
    [DefaultExecutionOrder(-1000)]
    public class UIThemeFontBinder : MonoBehaviour {

        // 사용자가 직접 교체할 한글 폰트 에셋 — 인스펙터에서 배선(특정 경로 하드코딩 금지)
        [SerializeField] TMP_FontAsset koreanFont;

        void Awake() {
            try {
                BindFont();
            } catch (System.Exception e) {
                Debug.LogError($"[UIThemeFontBinder] 폰트 주입 오류: {e.Message}");
            }
        }

        // koreanFont가 배선돼 있으면 UITheme.UIFont로 주입(런타임 텍스트가 한글 폰트를 타게 함)
        void BindFont() {
            if (koreanFont == null) {
                Debug.LogWarning("[UIThemeFontBinder] koreanFont 미배선 — UITheme.UIFont 폴백(TMP 기본)에 의존");
                return;
            }
            UITheme.UIFont = koreanFont;
        }
    }
}
