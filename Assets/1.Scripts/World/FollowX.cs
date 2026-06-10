using UnityEngine;

namespace Game.World {
    // 대상의 X만 따라가고 Y·Z는 고정 — 하늘 배경/해수면 경계가 화면을 늘 덮되 월드 높이는 유지
    public class FollowX : MonoBehaviour {
        [SerializeField] Transform target;
        [SerializeField] float fixedY;
        [SerializeField] float fixedZ;

        public void Set(Transform t, float y, float z) {
            target = t;
            fixedY = y;
            fixedZ = z;
        }

        void LateUpdate() {
            if (target == null) {
                return;
            }
            transform.position = new Vector3(target.position.x, fixedY, fixedZ);
        }
    }
}
