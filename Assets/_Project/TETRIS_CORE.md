# Block Escape — Tetris Core trong gameplay tích hợp

Tài liệu này mô tả subsystem Tetris đang chạy trong demo gameplay tích hợp. `TetrisDemo` hiện còn có player, drone, dynamic event, pickup, HUD, Pause và Game Over; các hệ thống đó không được phép thay đổi trực tiếp dữ liệu lưới của `BoardModel`.

## Implemented

- 14×20 board with two danger rows.
- Pure `BoardModel` separated from scene presentation.
- Seven tetromino shapes, normalized rotations, and deterministic 7-bag randomizer.
- Random initial column and rotation, grid-driven falling, lock delay, and static block colliders.
- Discrete one-cell falling steps, matching classic Tetris timing.
- A right-side preview showing the next piece already reserved by the 7-bag.
- A translucent ghost piece showing the current tetromino's landing position.
- `A/D` move, `W` rotates clockwise, and `S` soft-drops the active tetromino.
- Multi-row detection, warning flash, clear, and animated compaction.
- Three-second overflow grace period and stop condition.
- Runtime block pooling and a self-running demo HUD.
- Three-second pre-run countdown; no tetromino spawns before `Playing`.
- One spawn coroutine and at most one tracked active tetromino; reset stops the
  old loop and removes stray active pieces before restarting.
- EditMode tests for board bounds, locking, row compaction, danger rows, rotations, and 7-bag behavior.

## Run

Open `Assets/_Project/Scenes/MainMenu.unity` and press Play. Start loads `TetrisDemo`; board overflow opens a run summary with Restart and Main Menu actions.

Input is provided by the persistent `Input Service (Persistent)` object. Tetris, Player, and System controls use separate action maps from `Assets/InputSystem_Actions.inputactions`.

- `R`: open the reset confirmation dialog.
- `Esc`: open or close the pause menu.
- `A/D`: move the falling tetromino.
- `W`: rotate clockwise.
- `S`: soft drop.

Use **Block Escape → Build Classroom Tetris Scene** to rebuild the scene and config asset.
