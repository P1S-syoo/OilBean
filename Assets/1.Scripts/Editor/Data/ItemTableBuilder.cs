using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Core;

namespace Game.Editor.Data {
    // ItemDataTable 자산 생성 도구 — 엑셀(ItemDataTable.xlsx) 값을 코드로 이식 + NCAI FBX 임포트 정규화 + prefab 연결
    public static class ItemTableBuilder {
        const string FbxDir = "Assets/NCAI_Asset";
        const string TablePath = "Assets/3.Data/ItemDataTable.asset";

        // 엑셀 1행 = {id, kind, 표시명, grade(0~2), weight, minY, maxY, pollutionLevel, fbx하위경로}
        struct Row {
            public string id; public ResourceKind kind; public string name;
            public int grade; public float weight; public int minY; public int maxY; public int pol; public string fbx;
            public Row(string id, ResourceKind k, string n, int g, float w, int mn, int mx, int p, string fbx) {
                this.id = id; kind = k; name = n; grade = g; weight = w; minY = mn; maxY = mx; pol = p; this.fbx = fbx;
            }
        }

        static readonly Row[] Rows = {
            // 고철 11종 (grade: 0=일반 1=합금 2=특수 강재)
            new("scrap_Can",        ResourceKind.Scrap, "압축 캔 더미",        0,  4,  -3,   0, 0, "scrap/scrap_Can.fbx"),
            new("scrap_Bicycle",    ResourceKind.Scrap, "녹슨 자전거 프레임",  0,  7, -15,   0, 0, "scrap/scrap_Bicycle.fbx"),
            new("scrap_Cart",       ResourceKind.Scrap, "마트 카트 잔해",      0,  8, -12,   0, 0, "scrap/scrap_Cart.fbx"),
            new("scrap_Cone",       ResourceKind.Scrap, "교통 콘 묶음",        0,  5,  -8,   0, 0, "scrap/scrap_Cone.fbx"),
            new("scrap_Wire",       ResourceKind.Scrap, "지하철 배선 뭉치",    0,  9, -30,   0, 1, "scrap/scrap_Wire.fbx"),
            new("scrap_Rebar",      ResourceKind.Scrap, "공사용 철근",         1, 10, -30,   5, 1, "scrap/scrap_Rebar.fbx"),
            new("scrap_Manhole",    ResourceKind.Scrap, "맨홀 뚜껑",           1, 14, -30,   8, 1, "scrap/scrap_Manhole.fbx"),
            new("scrap_Scaffolding",ResourceKind.Scrap, "비계 파이프 묶음",    1, 11, -32,   2, 1, "scrap/scrap_Scaffolding.fbx"),
            new("scrap_Bolt",       ResourceKind.Scrap, "교각 볼트 세트",      2, 15, -50, -20, 2, "scrap/scrap_Bolt.fbx"),
            new("scrap_Hbeam",      ResourceKind.Scrap, "H빔",                 2, 30, -50, -37, 2, "scrap/scrap_Hbeam.fbx"),
            new("scrap_Anchor",     ResourceKind.Scrap, "선박 닻 파편",        2, 18, -50, -35, 2, "scrap/scrap_Anchor.fbx"),
            // 샘플 10종 (grade: 0=Common 1=Uncommon 2=Rare = pollutionLevel-1)
            new("sample_sewage",       ResourceKind.Sample, "생활하수 응고체",     0, 3,  -8,   0, 1, "sample/sample_sewage.fbx"),
            new("sample_microplastic", ResourceKind.Sample, "미세플라스틱 슬러지", 0, 4, -12,   0, 1, "sample/sample_microplastic.fbx"),
            new("sample_algae",        ResourceKind.Sample, "녹조 농축물",         0, 3,  -6,   0, 1, "sample/sample_algae.fbx"),
            new("sample_detergent",    ResourceKind.Sample, "계면활성제 거품",     0, 4, -10,   0, 1, "sample/sample_detergent.fbx"),
            new("sample_sludge",       ResourceKind.Sample, "공장 폐수 슬러지",    1, 7, -30,   0, 2, "sample/sample_sludge.fbx"),
            new("sample_heavymetal",   ResourceKind.Sample, "중금속 침전물",       1, 8, -35,  -5, 2, "sample/sample_heavymetal.fbx"),
            new("sample_paint",        ResourceKind.Sample, "건설 도료 침전물",    1, 6, -25,   5, 2, "sample/sample_paint.fbx"),
            new("sample_chemicalbarrel",ResourceKind.Sample,"화학 드럼통 잔류물",  2, 9, -42, -20, 3, "sample/sample_chemicalbarrel.fbx"),
            new("sample_pcb",          ResourceKind.Sample, "PCB 오염 퇴적물",     2, 8, -50, -40, 3, "sample/sample_pcb.fbx"),
            // 엑셀 id는 sample_oil_clump, FBX 파일명은 sample_oil.fbx
            new("sample_oil_clump",    ResourceKind.Sample, "중질유 덩어리",       2, 10,-50, -40, 3, "sample/sample_oil.fbx"),
        };

        [MenuItem("Tools/한강/데이터/아이템 테이블 생성")]
        public static void Build() {
            try {
                NormalizeImports();   // FBX 임포트 표준화(스케일·읽기·머티리얼)
                var table = AssetDatabase.LoadAssetAtPath<ItemDataTable>(TablePath);
                if (table == null) {
                    System.IO.Directory.CreateDirectory("Assets/3.Data");
                    table = ScriptableObject.CreateInstance<ItemDataTable>();
                    AssetDatabase.CreateAsset(table, TablePath);
                }
                var defs = new List<ItemDef>();
                int missing = 0;
                foreach (var r in Rows) {
                    string path = $"{FbxDir}/{r.fbx}";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) {
                        Debug.LogWarning($"[ItemTableBuilder] FBX 없음: {path}");
                        missing++;
                    }
                    defs.Add(new ItemDef {
                        id = r.id, kind = r.kind, displayName = r.name, grade = r.grade,
                        weight = r.weight, minSpawnY = r.minY, maxSpawnY = r.maxY,
                        pollutionLevel = r.pol, prefab = prefab,
                    });
                }
                table.items = defs.ToArray();
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ItemTableBuilder] 테이블 생성 완료 — {defs.Count}종 (FBX 누락 {missing}) → {TablePath}");
            } catch (Exception e) {
                Debug.LogError($"[ItemTableBuilder] 생성 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // NCAI FBX 임포트 표준화 — 스케일 1(이미 미터 단위), 메시 압축 적정, 머티리얼 임포트
        static void NormalizeImports() {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { FbxDir })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) {
                    continue;
                }
                bool dirty = false;
                if (imp.globalScale != 1f) { imp.globalScale = 1f; dirty = true; }
                if (!imp.useFileScale) { imp.useFileScale = true; dirty = true; }
                if (imp.importBlendShapes) { imp.importBlendShapes = false; dirty = true; }
                if (imp.importCameras) { imp.importCameras = false; dirty = true; }
                if (imp.importLights) { imp.importLights = false; dirty = true; }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.ImportStandard) {
                    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    dirty = true;
                }
                if (dirty) {
                    imp.SaveAndReimport();
                }
            }
        }
    }
}
