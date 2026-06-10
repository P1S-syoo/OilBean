using UnityEngine;

namespace Game.World {
    // 블록 메타 추상화 — VARCO 모델 생성/적용을 위한 위치·역할·변형 식별 단위
    public interface IBlock {
        Vector3 WorldPos { get; }   // 월드 그리드 좌표
        BlockRole Role { get; }     // 역할 타입
        int Variant { get; }        // 같은 역할 내 패턴 변형 인덱스(같은 값=동일 블록)
    }
}
