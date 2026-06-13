using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.Surface {
    // PlayerAnim.controller 확장 도구 — Walk(덱 보행)·RunToDive(입수) 스테이트와 OnDeck/Dive 파라미터를 코드로 배선(재실행 안전)
    public static class CharacterAnimBuilder {
        const string CtrlPath = "Assets/4.Art/Characters/PlayerAnim.controller";
        const string WalkFbx = "Assets/4.Art/Characters/Walking.fbx";
        const string DiveFbx = "Assets/4.Art/Characters/RunToDive.fbx";
        const string IdleFbx = "Assets/4.Art/Characters/IdleStand.fbx";

        [MenuItem("Tools/한강/캐릭터 애니 갱신")]
        public static void Build() {
            try {
                ConfigureImport(WalkFbx, true);
                ConfigureImport(DiveFbx, false);
                ConfigureImport(IdleFbx, true);
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
                if (ctrl == null) {
                    Debug.LogError($"[CharacterAnimBuilder] 컨트롤러 없음: {CtrlPath}");
                    return;
                }
                EnsureParam(ctrl, "OnDeck", AnimatorControllerParameterType.Bool);
                EnsureParam(ctrl, "Dive", AnimatorControllerParameterType.Trigger);
                var sm = ctrl.layers[0].stateMachine;
                var idle = FindState(sm, "Idle");
                var swim = FindState(sm, "Swim");
                if (idle == null || swim == null) {
                    Debug.LogError("[CharacterAnimBuilder] Idle/Swim 스테이트를 찾지 못함 — 컨트롤러 구조 확인 필요");
                    return;
                }
                var walk = EnsureState(sm, "Walk", LoadClip(WalkFbx), new Vector3(420f, 60f, 0f));
                walk.speed = 1f;   // 이동 속도(1.4m/s)와 보폭 동기화 — 클립 원배속이 실제 보행과 일치
                var dive = EnsureState(sm, "RunToDive", LoadClip(DiveFbx), new Vector3(420f, 180f, 0f));
                var idleDeck = EnsureState(sm, "IdleDeck", LoadClip(IdleFbx), new Vector3(420f, -60f, 0f));
                // 기본 스테이트 = 덱 서있기 — 수상 시작 직후 수영 모션이 한 프레임 깜빡이는 문제 방지
                if (sm.defaultState != idleDeck) {
                    sm.defaultState = idleDeck;
                }

                // 구버전 전환 정리 — Idle(트레딩)↔Walk 직결을 IdleDeck 경유로 교체
                RemoveTransitions(idle, walk);
                RemoveTransitions(walk, idle);
                // 덱 진입/이탈: Idle(물)↔IdleDeck(덱 서있기)
                if (!idle.transitions.Any(t => t.destinationState == idleDeck)) {
                    var t = idle.AddTransition(idleDeck);
                    t.hasExitTime = false;
                    t.duration = 0.15f;
                    t.AddCondition(AnimatorConditionMode.If, 0f, "OnDeck");
                }
                if (!idleDeck.transitions.Any(t => t.destinationState == idle)) {
                    var t = idleDeck.AddTransition(idle);
                    t.hasExitTime = false;
                    t.duration = 0.15f;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0f, "OnDeck");
                }
                // 덱 보행: IdleDeck↔Walk(Speed 임계 0.1)
                if (!idleDeck.transitions.Any(t => t.destinationState == walk)) {
                    var t = idleDeck.AddTransition(walk);
                    t.hasExitTime = false;
                    t.duration = 0.15f;
                    t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                }
                if (!walk.transitions.Any(t => t.destinationState == idleDeck)) {
                    var t = walk.AddTransition(idleDeck);
                    t.hasExitTime = false;
                    t.duration = 0.15f;
                    t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                }
                // 기존 Idle→Swim은 물에서만 — OnDeck=false 조건 보강
                foreach (var t in idle.transitions.Where(t => t.destinationState == swim)) {
                    if (!t.conditions.Any(c => c.parameter == "OnDeck")) {
                        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "OnDeck");
                    }
                }
                // 입수: AnyState→RunToDive(Dive 트리거), 클립 종료 후 Swim 인계
                if (!sm.anyStateTransitions.Any(t => t.destinationState == dive)) {
                    var t = sm.AddAnyStateTransition(dive);
                    t.duration = 0.1f;
                    t.canTransitionToSelf = false;
                    t.AddCondition(AnimatorConditionMode.If, 0f, "Dive");
                }
                if (!dive.transitions.Any(t => t.destinationState == swim)) {
                    var t = dive.AddTransition(swim);
                    t.hasExitTime = true;
                    t.exitTime = 0.9f;
                    t.duration = 0.25f;
                }
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log("[CharacterAnimBuilder] 캐릭터 애니 갱신 완료 — Walk/RunToDive + OnDeck/Dive 배선");
            } catch (Exception e) {
                Debug.LogError($"[CharacterAnimBuilder] 갱신 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // FBX 임포트 설정 — 휴머노이드 리그(기존 Mixamo 클립과 동일) + 루프 여부
        static void ConfigureImport(string path, bool loop) {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) {
                Debug.LogError($"[CharacterAnimBuilder] FBX 없음: {path}");
                return;
            }
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Human) {
                imp.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            foreach (var c in clips) {
                if (c.loopTime != loop || c.lockRootPositionXZ || !c.lockRootHeightY || !c.lockRootRotation) {
                    c.loopTime = loop;
                    // XZ 이동은 베이크 OFF(루트모션으로 추출) — applyRootMotion=false가 버려서 진짜 제자리 재생
                    // (베이크 ON이면 이동이 포즈에 남아 몸이 전진했다 루프마다 원복됨)
                    c.lockRootPositionXZ = false;
                    c.lockRootHeightY = true;    // 힙 상하 바운스는 포즈 유지
                    c.lockRootRotation = true;   // 회전 드리프트는 포즈에 고정
                    dirty = true;
                }
            }
            if (dirty) {
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
            }
        }

        static AnimationClip LoadClip(string path) {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) {
                Debug.LogError($"[CharacterAnimBuilder] 클립 없음: {path}");
            }
            return clip;
        }

        static void EnsureParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type) {
            if (!ctrl.parameters.Any(p => p.name == name)) {
                ctrl.AddParameter(name, type);
            }
        }

        static AnimatorState FindState(AnimatorStateMachine sm, string name) {
            return sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        }

        // 특정 목적지로 가는 전환 전부 제거(구버전 배선 정리용)
        static void RemoveTransitions(AnimatorState from, AnimatorState to) {
            foreach (var t in from.transitions.Where(t => t.destinationState == to).ToArray()) {
                from.RemoveTransition(t);
            }
        }

        static AnimatorState EnsureState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos) {
            var state = FindState(sm, name);
            if (state == null) {
                state = sm.AddState(name, pos);
            }
            if (clip != null) {
                state.motion = clip;
            }
            return state;
        }
    }
}
