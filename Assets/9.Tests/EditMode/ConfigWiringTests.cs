using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Core;

namespace Game.Tests {
    // 10개 카테고리 설정 .asset이 Assets/3.Data/Config에 실제 존재하는지 단언 — 누락 시 실패시켜 생성·커밋을 강제(C2 완료 게이트)
    public class ConfigWiringTests {
        const string ConfigDir = "Assets/3.Data/Config";

        // 생성·배선 대상 10개 카테고리 타입 (게임설정도구.CategoryTypes와 동일해야 함)
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

        [Test]
        public void All_category_config_assets_exist([ValueSource(nameof(CategoryTypes))] Type type) {
            string path = $"{ConfigDir}/{type.Name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Assert.IsNotNull(asset,
                $"설정 자산 누락: {path} — 게임설정/설정 생성 및 배선 메뉴로 생성 후 커밋하세요");
            Assert.AreEqual(type, asset.GetType(),
                $"{path}의 타입이 {type.Name}이 아님");
        }
    }
}
