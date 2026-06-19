# Block Escape — Tetris Core

This milestone intentionally contains no platform player, enemy, pickups, or final menu. It provides a deterministic Tetris terrain system controlled with WASD.

## Implemented

- 14×20 board with two danger rows.
- Pure `BoardModel` separated from scene presentation.
- Seven tetromino shapes, normalized rotations, and deterministic 7-bag randomizer.
- Random initial column and rotation, grid-driven falling, lock delay, and static block colliders.
- Discrete one-cell falling steps, matching classic Tetris timing.
- A right-side preview showing the next piece already reserved by the 7-bag.
- `A/D` move, `W` rotates clockwise, and `S` soft-drops the active tetromino.
- Multi-row detection, warning flash, clear, and animated compaction.
- Three-second overflow grace period and stop condition.
- Runtime block pooling and a self-running demo HUD.
- EditMode tests for board bounds, locking, row compaction, danger rows, rotations, and 7-bag behavior.

## Run

Open `Assets/_Project/Scenes/TetrisDemo.unity` and press Play.

- `R`: reset with the same seed.
- `P`: pause/resume.
- `A/D`: move the falling tetromino.
- `W`: rotate clockwise.
- `S`: soft drop.

Use **Block Escape → Build Tetris Demo** to rebuild the scene and config asset.
