using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI {
    // 커스텀 홀로그램 커서 — OS 커서는 잠그지 않고 숨긴 뒤 UI 이미지와 클릭 파동으로 대체
    public class CustomCursorFx : MonoBehaviour {
        [SerializeField] Texture2D cursorTexture;
        [SerializeField] Vector2 hotspot = new(10f, 8f);
        [SerializeField] Vector2 cursorSize = new(46f, 46f);
        [SerializeField] Color cursorTint = Color.white;
        [SerializeField] Color clickColor = new(0.18f, 0.9f, 0.85f, 0.8f);

        Canvas canvas;
        RectTransform cursorRt;
        Image cursorImage;
        Sprite cursorSprite;
        Sprite ringSprite;
        Texture2D fallbackTexture;
        Texture2D ringTexture;
        InputAction click;

        void Awake() {
            try {
                BuildOverlay();
                click = new InputAction("CursorClickFx", InputActionType.Button, "<Mouse>/leftButton");
                click.performed += OnClick;
            } catch (Exception e) {
                Debug.LogError($"[CustomCursorFx] 초기화 실패: {e.Message}");
                enabled = false;
            }
        }

        void OnEnable() {
            click?.Enable();
            ApplyCursorState();
        }

        void OnDisable() {
            click?.Disable();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void OnDestroy() {
            if (click != null) {
                click.performed -= OnClick;
                click.Dispose();
            }
            if (cursorSprite != null) {
                Destroy(cursorSprite);
            }
            if (ringSprite != null) {
                Destroy(ringSprite);
            }
            if (fallbackTexture != null) {
                Destroy(fallbackTexture);
            }
            if (ringTexture != null) {
                Destroy(ringTexture);
            }
        }

        void Update() {
            ApplyCursorState();
            if (cursorRt == null || Mouse.current == null) {
                return;
            }
            cursorRt.position = Mouse.current.position.ReadValue() - hotspot;
        }

        void ApplyCursorState() {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        void BuildOverlay() {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null) {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 500);
            if (GetComponent<GraphicRaycaster>() == null) {
                gameObject.AddComponent<GraphicRaycaster>();
            }
            var cursorGo = new GameObject("HoloCursor", typeof(RectTransform), typeof(Image));
            cursorGo.transform.SetParent(transform, false);
            cursorRt = cursorGo.GetComponent<RectTransform>();
            cursorRt.sizeDelta = cursorSize;
            cursorRt.pivot = new Vector2(0f, 1f);
            cursorImage = cursorGo.GetComponent<Image>();
            cursorImage.raycastTarget = false;
            cursorImage.sprite = MakeCursorSprite();
            cursorImage.color = cursorTint;
            cursorImage.preserveAspect = true;
        }

        Sprite MakeCursorSprite() {
            Texture2D tex = cursorTexture != null ? cursorTexture : MakeFallbackTexture();
            cursorSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.08f, 0.92f), 100f);
            return cursorSprite;
        }

        Texture2D MakeFallbackTexture() {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            fallbackTexture = tex;
            tex.filterMode = FilterMode.Bilinear;
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < tex.height; y++) {
                for (int x = 0; x < tex.width; x++) {
                    tex.SetPixel(x, y, clear);
                }
            }
            for (int i = 0; i < 48; i++) {
                DrawPixel(tex, 8 + i / 2, 56 - i, clickColor);
                DrawPixel(tex, 9 + i / 2, 56 - i, clickColor);
            }
            for (int i = 0; i < 18; i++) {
                DrawPixel(tex, 20 + i, 26 - i / 2, Color.white);
            }
            tex.Apply();
            return tex;
        }

        void DrawPixel(Texture2D tex, int x, int y, Color c) {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height) {
                tex.SetPixel(x, y, c);
            }
        }

        void OnClick(InputAction.CallbackContext ctx) {
            if (Mouse.current == null) {
                return;
            }
            PulseAt(Mouse.current.position.ReadValue());
        }

        // 테스트·연출 공용 진입점 — 지정 스크린 좌표에 클릭 파동 표시
        public void PulseAt(Vector2 screenPos) {
            StartCoroutine(ClickPulse(screenPos));
        }

        IEnumerator ClickPulse(Vector2 screenPos) {
            var go = new GameObject("CursorClickPulse", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.position = screenPos;
            rt.sizeDelta = Vector2.one * 18f;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = MakeRingSprite();
            float t = 0f;
            const float dur = 0.28f;
            while (t < dur) {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                rt.sizeDelta = Vector2.one * Mathf.Lerp(18f, 86f, k);
                img.color = new Color(clickColor.r, clickColor.g, clickColor.b, Mathf.Lerp(0.85f, 0f, k));
                yield return null;
            }
            Destroy(go);
        }

        Sprite MakeRingSprite() {
            if (ringSprite != null) {
                return ringSprite;
            }
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            ringTexture = tex;
            tex.filterMode = FilterMode.Bilinear;
            Vector2 c = new(31.5f, 31.5f);
            for (int y = 0; y < 64; y++) {
                for (int x = 0; x < 64; x++) {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 23f) / 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f);
            return ringSprite;
        }
    }
}
