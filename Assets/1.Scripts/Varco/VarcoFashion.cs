using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Varco {
    // Art Fashion API 행동단위 래퍼 - 모든 응답은 PNG 이미지 바이트 -> Texture2D로 반환
    public static class VarcoFashion {
        // 가상 착장(의상) - clothes_image 필수, model/mask 없으면 clothes_spec 기반 생성 (실패 시 null)
        public static Task<Texture2D> TryOnClothes(byte[] clothesImage, byte[] modelImage = null, byte[] maskImage = null, string category = "dresses", int seed = -1, string clothesSpecJson = null) {
            var form = NewForm();
            AddFile(form, "clothes_image", clothesImage);
            AddFile(form, "model_image", modelImage);
            AddFile(form, "mask_image", maskImage);
            string seedPart = seed >= 0 ? seed.ToString() : "null";
            form.Add(new MultipartFormDataSection("vton", $"{{\"category\":\"{EscapeJson(category)}\",\"generator_seed\":{seedPart}}}"));
            if (!string.IsNullOrEmpty(clothesSpecJson)) {
                form.Add(new MultipartFormDataSection("clothes_spec", clothesSpecJson));
            }
            return PostForTexture("/fashion/vton/v1/clothes", form);
        }

        // 가상 착장(악세사리) - model_image 필수, 가방 사용 시 specs(bag_carry_style, bag_size) 필요 (실패 시 null)
        public static Task<Texture2D> TryOnAccessories(byte[] modelImage, byte[] bagImage = null, byte[] hatImage = null, byte[] shoesImage = null, string bagCarryStyle = null, string bagSize = null) {
            var form = NewForm();
            AddFile(form, "model_image", modelImage);
            AddFile(form, "bag_image", bagImage);
            AddFile(form, "hat_image", hatImage);
            AddFile(form, "shoes_image", shoesImage);
            if (bagCarryStyle != null && bagSize != null) {
                form.Add(new MultipartFormDataSection("specs", $"{{\"bag_carry_style\":\"{EscapeJson(bagCarryStyle)}\",\"bag_size\":\"{EscapeJson(bagSize)}\"}}"));
            }
            return PostForTexture("/fashion/vton/v1/accessories", form);
        }

        // 헤드스왑 - 이미지 크기 100x100~3500x3500, 얼굴 미검출 시 400 (실패 시 null)
        public static Task<Texture2D> Headswap(byte[] modelImage, byte[] faceImage, string prompt = "", int seed = -1) {
            var form = NewForm();
            AddFile(form, "model_image", modelImage);
            AddFile(form, "face_image", faceImage);
            string seedPart = seed >= 0 ? seed.ToString() : "null";
            form.Add(new MultipartFormDataSection("headswap", $"{{\"prompt\":\"{EscapeJson(prompt)}\",\"generator_seed\":{seedPart}}}"));
            return PostForTexture("/fashion/vton-headswap/v1/headswap", form);
        }

        // 마스크 영역 지우기 (실패 시 null)
        public static Task<Texture2D> Eraser(byte[] image, byte[] maskImage) {
            var form = NewForm();
            AddFile(form, "image", image);
            AddFile(form, "mask_image", maskImage);
            return PostForTexture("/fashion/edit/v1/eraser", form);
        }

        // 텍스트 프롬프트 인페인트 - model: sdxl 또는 nano_banana (실패 시 null)
        public static Task<Texture2D> InpaintText(byte[] image, byte[] maskImage, string prompt, string model = "sdxl") {
            var form = NewForm();
            form.Add(new MultipartFormDataSection("model", model));
            AddFile(form, "image", image);
            AddFile(form, "mask_image", maskImage);
            form.Add(new MultipartFormDataSection("prompt", prompt));
            return PostForTexture("/fashion/edit/v1/inpaint", form);
        }

        // 참조 이미지 인페인트 - 마스크 영역을 reference 이미지로 채움 (실패 시 null)
        public static Task<Texture2D> InpaintImage(byte[] image, byte[] maskImage, byte[] referenceImage, byte[] referenceMaskImage) {
            var form = NewForm();
            AddFile(form, "image", image);
            AddFile(form, "mask_image", maskImage);
            AddFile(form, "reference_image", referenceImage);
            AddFile(form, "reference_mask_image", referenceMaskImage);
            return PostForTexture("/fashion/edit/v1/inpaint-image", form);
        }

        // 텍스처 교체 - 바운딩박스(x, y, width, height, angle)에 texture_image 합성 (실패 시 null)
        public static Task<Texture2D> ReplaceTexture(byte[] image, byte[] maskImage, byte[] textureImage, int x, int y, int width, int height, float angle) {
            var form = NewForm();
            AddFile(form, "image", image);
            AddFile(form, "mask_image", maskImage);
            AddFile(form, "texture_image", textureImage);
            AddBox(form, x, y, width, height, angle);
            return PostForTexture("/fashion/edit/v1/texture", form);
        }

        // 제품 시점 변경 - view: front/side/top/back/isometric (실패 시 null)
        public static Task<Texture2D> Perspective(byte[] image, string view, string prompt = null) {
            var form = NewForm();
            AddFile(form, "image", image);
            form.Add(new MultipartFormDataSection("view", view));
            if (!string.IsNullOrEmpty(prompt)) {
                form.Add(new MultipartFormDataSection("prompt", prompt));
            }
            return PostForTexture("/fashion/edit/v1/perspective", form);
        }

        // 로고 합성 - texture: embossed/debossed/embroidered/printed/metal (실패 시 null)
        public static Task<Texture2D> Graphic(byte[] image, byte[] graphicImage, int x, int y, int width, int height, float angle, string texture = null) {
            var form = NewForm();
            AddFile(form, "image", image);
            AddFile(form, "graphic_image", graphicImage);
            AddBox(form, x, y, width, height, angle);
            if (!string.IsNullOrEmpty(texture)) {
                form.Add(new MultipartFormDataSection("texture", texture));
            }
            return PostForTexture("/fashion/edit/v1/graphic", form);
        }

        // 배경 교체 - mode: person/product, image는 전경 + 회색(128,128,128) 배경 합성본 (실패 시 null)
        public static Task<Texture2D> Background(byte[] image, string mode, string backgroundPrompt = null, string seasonPrompt = null, string timePrompt = null, string colorPrompt = null, string lightingPrompt = null, string additionalPrompt = null, string prompt = null, string outputAspectRatio = "1:1", string outputImageSize = "2K") {
            var form = NewForm();
            form.Add(new MultipartFormDataSection("mode", mode));
            AddFile(form, "image", image);
            AddOptional(form, "prompt", prompt);
            AddOptional(form, "background_prompt", backgroundPrompt);
            AddOptional(form, "season_prompt", seasonPrompt);
            AddOptional(form, "time_prompt", timePrompt);
            AddOptional(form, "color_prompt", colorPrompt);
            AddOptional(form, "lighting_prompt", lightingPrompt);
            AddOptional(form, "additional_prompt", additionalPrompt);
            form.Add(new MultipartFormDataSection("output_aspect_ratio", outputAspectRatio));
            form.Add(new MultipartFormDataSection("output_image_size", outputImageSize));
            return PostForTexture("/fashion/edit/v1/background", form);
        }

        // 업스케일 - 입력 최대 2048x2048, scale_factor 2~6 (실패 시 null)
        public static Task<Texture2D> Upscale(byte[] image, int scaleFactor = 4) {
            var form = NewForm();
            AddFile(form, "image", image);
            form.Add(new MultipartFormDataSection("scale_factor", scaleFactor.ToString()));
            return PostForTexture("/fashion/upscale/v1/super-resolution", form);
        }

        // JSON 문자열 값 내 특수문자 이스케이프 (인젝션 방지)
        private static string EscapeJson(string value) {
            if (value == null) {
                return "";
            }
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static List<IMultipartFormSection> NewForm() {
            return new List<IMultipartFormSection>();
        }

        private static void AddFile(List<IMultipartFormSection> form, string field, byte[] data) {
            if (data != null) {
                form.Add(new MultipartFormFileSection(field, data, field + ".png", "image/png"));
            }
        }

        private static void AddOptional(List<IMultipartFormSection> form, string field, string value) {
            if (!string.IsNullOrEmpty(value)) {
                form.Add(new MultipartFormDataSection(field, value));
            }
        }

        // 합성 위치 바운딩박스 공통 필드 추가
        private static void AddBox(List<IMultipartFormSection> form, int x, int y, int width, int height, float angle) {
            form.Add(new MultipartFormDataSection("x", x.ToString()));
            form.Add(new MultipartFormDataSection("y", y.ToString()));
            form.Add(new MultipartFormDataSection("width", width.ToString()));
            form.Add(new MultipartFormDataSection("height", height.ToString()));
            form.Add(new MultipartFormDataSection("angle", angle.ToString()));
        }

        // 공통 호출 후 PNG 바이트를 Texture2D로 변환
        private static async Task<Texture2D> PostForTexture(string path, List<IMultipartFormSection> form) {
            byte[] bytes = await VarcoApiClient.PostMultipartAsync(path, form);
            if (bytes == null) {
                return null;
            }
            try {
                Texture2D tex = new Texture2D(2, 2);
                if (!tex.LoadImage(bytes)) {
                    Debug.LogError($"응답 이미지 디코딩 실패 ({path})");
                    return null;
                }
                return tex;
            } catch (Exception e) {
                Debug.LogError($"텍스처 변환 실패 ({path}): {e.Message}");
                return null;
            }
        }
    }
}
