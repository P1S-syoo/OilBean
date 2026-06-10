using System.Collections.Generic;

namespace Game.Editor {
    // 라이선스 종류 — 상업 안전성 분류(파이프라인이 위험 라이선스를 차단)
    public enum LicenseKind {
        CC0,        // 퍼블릭 도메인 — attribution 불필요
        OFL,        // 폰트 오픈 라이선스 — 라이선스 파일 동봉
        CC_BY,      // 크레딧 필수
        CC_BY_NC,   // 상업 금지(차단)
        CC_BY_SA,   // 카피레프트 전파(차단)
        GPL,        // 카피레프트(차단)
        Unknown     // 미상(차단)
    }

    // 조달 대상 에셋 1건(선언적 정의)
    public class AssetEntry {
        public string name;
        public string url;        // 직접 다운로드 URL
        public string savePath;   // 단일 파일=저장 경로 / unzip=해제할 폴더
        public LicenseKind license;
        public string author;
        public string source;     // 출처 페이지 URL
        public bool unzip;        // true면 ZIP을 savePath 폴더에 해제

        public AssetEntry(string name, string url, string savePath, LicenseKind license, string author, string source, bool unzip = false) {
            this.name = name; this.url = url; this.savePath = savePath;
            this.license = license; this.author = author; this.source = source;
            this.unzip = unzip;
        }
    }

    // 라이선스 안전 규칙 + 검증된 클린 소스 카탈로그
    public static class AssetCatalog {
        // 상업 프로토타입에서 차단할 라이선스(체크리스트 강제)
        public static bool IsBlocked(LicenseKind k) {
            return k == LicenseKind.CC_BY_NC || k == LicenseKind.CC_BY_SA || k == LicenseKind.GPL || k == LicenseKind.Unknown;
        }

        // 크레딧 기록이 필요한 라이선스
        public static bool NeedsAttribution(LicenseKind k) {
            return k == LicenseKind.CC_BY || k == LicenseKind.OFL;
        }

        // 검증된 클린(CC0/OFL) 항목 — 코드 수정 없이 여기에 추가
        public static List<AssetEntry> Default() {
            return new List<AssetEntry> {
                // 한글 폰트(OFL, variable) — E2 TMP 토대
                new AssetEntry(
                    "Noto Sans KR (Variable)",
                    "https://raw.githubusercontent.com/google/fonts/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf",
                    "Assets/4.Art/Fonts/NotoSansKR-VF.ttf",
                    LicenseKind.OFL, "Google / Noto Project",
                    "https://fonts.google.com/noto/specimen/Noto+Sans+KR"),
                // OFL 라이선스 원문 — 폰트와 함께 빌드 동봉(OFL 의무)
                new AssetEntry(
                    "Noto Sans KR — OFL License",
                    "https://raw.githubusercontent.com/google/fonts/main/ofl/notosanskr/OFL.txt",
                    "Assets/4.Art/Fonts/NotoSansKR-OFL.txt",
                    LicenseKind.OFL, "Google / Noto Project",
                    "https://fonts.google.com/noto/specimen/Noto+Sans+KR"),
                // Kenney CC0 팩(ZIP 해제) — 추후 VARCO로 단계 전환 예정
                new AssetEntry(
                    "Kenney Particle Pack",
                    "https://kenney.nl/media/pages/assets/particle-pack/f8fe0f8cb8-1677578741/kenney_particle-pack.zip",
                    "Assets/4.Art/Kenney/ParticlePack",
                    LicenseKind.CC0, "Kenney", "https://kenney.nl/assets/particle-pack", true),
                new AssetEntry(
                    "Kenney UI Pack",
                    "https://kenney.nl/media/pages/assets/ui-pack/f651646eab-1718203990/kenney_ui-pack.zip",
                    "Assets/7.UI/Kenney/UIPack",
                    LicenseKind.CC0, "Kenney", "https://kenney.nl/assets/ui-pack", true),
                new AssetEntry(
                    "Kenney Interface Sounds",
                    "https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip",
                    "Assets/5.Audio/Kenney/InterfaceSounds",
                    LicenseKind.CC0, "Kenney", "https://kenney.nl/assets/interface-sounds", true),
                new AssetEntry(
                    "Kenney Impact Sounds",
                    "https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip",
                    "Assets/5.Audio/Kenney/ImpactSounds",
                    LicenseKind.CC0, "Kenney", "https://kenney.nl/assets/impact-sounds", true),
                new AssetEntry(
                    "Kenney Sci-fi Sounds",
                    "https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip",
                    "Assets/5.Audio/Kenney/SciFiSounds",
                    LicenseKind.CC0, "Kenney", "https://kenney.nl/assets/sci-fi-sounds", true),
            };
        }
    }
}
