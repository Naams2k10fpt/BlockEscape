using UnityEngine;

namespace BlockEscape.Player
{
    [CreateAssetMenu(menuName = "Block Escape/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float moveSpeed = 7f;
        [Min(0f)] public float jumpVelocity = 11f;
        [Min(0f)] public float gravityScale = 5f;
        [Min(0f)] public float coyoteTime = 0.10f;
        [Min(0f)] public float jumpBufferTime = 0.12f;
        [Min(0f)] public float maxFallSpeed = 18f;
        [Range(0.1f, 1f)] public float variableJumpMultiplier = 0.5f;

        [Header("Ground check")]
        public Vector2 groundCheckSize = new Vector2(0.58f, 0.08f);
        public float groundCheckOffsetY = -0.78f;

        [Header("Crouch")]
        public Vector2 standingColliderSize = new Vector2(0.72f, 1.45f);
        public Vector2 standingColliderOffset = new Vector2(0f, -0.02f);
        public Vector2 crouchColliderSize = new Vector2(0.72f, 0.82f);
        public Vector2 crouchColliderOffset = new Vector2(0f, -0.335f);

        public void Sanitize()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            gravityScale = Mathf.Max(0f, gravityScale);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
            variableJumpMultiplier = Mathf.Clamp(variableJumpMultiplier, 0.1f, 1f);
            groundCheckSize.x = Mathf.Max(0.05f, groundCheckSize.x);
            groundCheckSize.y = Mathf.Max(0.02f, groundCheckSize.y);
            standingColliderSize.x = Mathf.Max(0.1f, standingColliderSize.x);
            standingColliderSize.y = Mathf.Max(0.1f, standingColliderSize.y);
            crouchColliderSize.x = Mathf.Max(0.1f, crouchColliderSize.x);
            crouchColliderSize.y = Mathf.Max(0.1f, crouchColliderSize.y);
        }
    }
}
