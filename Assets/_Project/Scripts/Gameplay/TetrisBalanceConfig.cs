using UnityEngine;

namespace BlockEscape.Tetris
{
    [CreateAssetMenu(menuName = "Block Escape/Tetris Balance", fileName = "TetrisBalanceConfig")]
    public sealed class TetrisBalanceConfig : ScriptableObject
    {
        [Header("Board")]
        [Min(4)] public int boardWidth = 14;
        [Min(8)] public int boardHeight = 20;
        [Min(0)] public int dangerStartRow = 18;
        [Min(0.1f)] public float overflowGraceSeconds = 3f;

        [Header("Falling pieces")]
        [Min(0f)] public float initialSpawnDelay = 0.5f;
        [Min(0f)] public float spawnDelay = 0.6f;
        [Min(0f), Tooltip("Optional spawn warning. Disabled because the next-piece preview is shown.")]
        public float telegraphSeconds = 0f;
        [Min(0.1f), Tooltip("Discrete grid steps per second. A value of 2 moves one cell every 0.5 seconds.")]
        public float fallSpeedCellsPerSecond = 2f;
        [Min(0f)] public float lockDelaySeconds = 0.12f;

        [Header("Row clear")]
        [Min(0f)] public float rowClearWarningSeconds = 0.6f;
        [Min(0f)] public float rowCollapseSeconds = 0.15f;

        [Header("Random")]
        [Tooltip("Use 0 for a different seed each run.")]
        public int seed;

        public void Sanitize()
        {
            boardWidth = Mathf.Max(4, boardWidth);
            boardHeight = Mathf.Max(8, boardHeight);
            dangerStartRow = Mathf.Clamp(dangerStartRow, 0, boardHeight - 1);
            overflowGraceSeconds = Mathf.Max(0.1f, overflowGraceSeconds);
            fallSpeedCellsPerSecond = Mathf.Max(0.1f, fallSpeedCellsPerSecond);
        }
    }
}
