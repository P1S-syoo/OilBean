using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core {
    // 게임 모드 상태머신 (enum 기반, 상태 비대해지면 IState로 분리)
    public class GameFsm {
        // 허용 전환 표 — 잘못된 전환을 막아 흐름을 명확히 유지
        static readonly Dictionary<GameState, HashSet<GameState>> Allowed = new() {
            { GameState.Surface,  new() { GameState.Dock, GameState.Research, GameState.Craft } },
            { GameState.Dock,     new() { GameState.Dive, GameState.Research, GameState.Craft, GameState.Purify } },
            { GameState.Dive,     new() { GameState.Dock, GameState.Clear } },
            { GameState.Research, new() { GameState.Dock, GameState.Surface } },
            { GameState.Craft,    new() { GameState.Dock, GameState.Surface } },
            { GameState.Purify,   new() { GameState.Dock, GameState.Clear } },
            { GameState.Clear,    new() { GameState.Dock, GameState.Surface } },
        };

        public GameState Current { get; private set; } = GameState.Dock;

        // 씬별 시작 상태 지정(수상 씬=Surface). 기본은 기존과 동일하게 Dock
        public GameFsm(GameState initial = GameState.Dock) {
            Current = initial;
        }

        // 상태 변경 알림 (HUD·사운드 등이 구독) — from, to
        public event Action<GameState, GameState> OnChanged;

        // 전환 가능 여부
        public bool CanGo(GameState next) {
            return Allowed.TryGetValue(Current, out var set) && set.Contains(next);
        }

        // 상태 전환. 허용되지 않으면 false 반환(예외 없이 흐름 유지)
        public bool Change(GameState next) {
            if (next == Current) {
                return false;
            }
            if (!CanGo(next)) {
                Debug.LogWarning($"[GameFsm] 잘못된 전환: {Current} → {next}");
                return false;
            }
            var prev = Current;
            Current = next;
            try {
                OnChanged?.Invoke(prev, next);
            } catch (Exception e) {
                // 구독자 예외가 상태머신을 멈추지 않도록 격리
                Debug.LogError($"[GameFsm] OnChanged 처리 오류: {e.Message}\n{e.StackTrace}");
            }
            return true;
        }
    }
}
