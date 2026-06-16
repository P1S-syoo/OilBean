using System.Runtime.CompilerServices;

// 테스트 어셈블리에 internal 시드 메서드 노출 — 미니게임 드래그 판정 자동 검증용
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]
[assembly: InternalsVisibleTo("Game.Tests.EditMode")]
