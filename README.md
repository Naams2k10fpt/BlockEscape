# Block Escape

Block Escape là game 2D kết hợp **Tetris**, **platform** và **sinh tồn**, được phát triển bằng Unity cho final project PRU213.

Người chơi sẽ điều khiển tetromino để tạo địa hình, sau đó điều khiển một nhân vật bên trong đấu trường để né block, tận dụng địa hình và sống sót qua các phase ngày càng khó.

> Project đang trong giai đoạn phát triển. Xem checklist và công việc tiếp theo tại [PROGRESS.md](PROGRESS.md).

## Trạng thái hiện tại

**Cột mốc:** Player sandbox playable
**Tiến độ tổng thể ước tính:** 51%

Đã có:

- Board Tetris 14×20 và hai hàng nguy hiểm.
- Bảy loại tetromino cùng bộ sinh 7-bag.
- Block rơi từng ô theo nhịp Tetris.
- Điều khiển block bằng WASD.
- Preview tetromino tiếp theo.
- Ghost piece trong suốt hiển thị vị trí block sẽ khóa.
- Khóa block, phát hiện/xóa nhiều hàng và dịch địa hình.
- Board overflow, Pause Menu có thống kê lượt chơi, xác nhận reset và HUD demo.
- Main Menu có nút Bắt đầu/Thoát và màn Game Over tổng kết lượt chơi.
- Pause Menu có nút trở về Main Menu với hộp xác nhận để tránh mất lượt nhầm.
- Input System được tách thành ba map `Tetris`, `Player`, `System` và quản lý tập trung qua `InputService`.
- Game session runtime quản lý trạng thái Playing/Paused/Game Over, thời gian sống sót, phase độ khó, điểm theo thời gian và điểm xóa hàng.
- HUD, Pause Menu và Game Over summary đọc cùng một nguồn session/score nên số liệu không bị lệch nhau.
- Phase độ khó tăng theo thời gian sống và làm piece mới rơi nhanh dần theo config.
- Player runtime spawn trong `TetrisDemo`, có di chuyển, nhảy, cúi, health, iFrame và death event.
- Player movement được quản lý trong `PlayerConfig`, hiện đặt `jumpVelocity = 12.5` và `gravityScale = 2` để nhảy cao hơn và rơi nhẹ hơn.
- HUD hiển thị máu bằng 3 tim thay cho dạng số.
- Player có thể rơi xuống đáy đấu trường; tường trái/phải/đáy có collider layer `World` để không lọt khỏi map.
- Falling tetromino dùng solid collider để chặn player ngay khi đang rơi.
- Player, falling block và biên đấu trường dùng frictionless physics material để giảm lỗi bám tường.
- Active/locked block chỉ mở Game Over khi player bị đè và không còn đường thoát ngang; nhảy đụng block thì bị chặn, không chết ngay.
- Khi bị block đè mà còn tim, player mất 1 tim rồi respawn ở điểm trống gần giữa arena, cao hơn block đã khóa cao nhất 5 đơn vị Y; player đứng lơ lửng 0.75 giây rồi rơi, nhấp nháy bất tử 3 giây, chỉ hết tim mới Game Over.
- Falling block vẫn tiếp tục rơi khi player đứng cạnh/đứng dưới nhưng còn đường thoát, tránh kẹt piece giữa không trung.
- Falling block kiểm tra vị trí kế tiếp trước khi bước xuống/rotate để không xuyên qua hoặc húc player văng ngang khi soft drop.
- Nếu player nhảy lên vào block đang xuống, player bị bật ngược xuống và block vẫn tiếp tục rơi.
- Nếu player đang leo/chạm cạnh falling block, block sẽ tách player ngang ra thay vì kéo player xuống.
- Phase 2 trở đi có runtime Drone AI patrol ở nửa trên arena, detect/telegraph/dash gây Enemy damage; falling block phá drone cộng 300 điểm và drone respawn sau 12 giây.
- Dynamic Event Director đã có Block Overdrive: từ phase 2 trở đi tăng tốc 3 tetromino tiếp theo theo lịch seed-based rồi trả tốc độ phase hiện tại.
- TetrisDemo có clamp bảo vệ để player không bị ép văng khỏi đấu trường.
- Scene có Hierarchy rõ ràng để kiểm tra và trình bày.
- EditMode tests cho các quy tắc của board.

Drone AI, dynamic event, pickup, menu hoàn chỉnh, save data, art và audio chưa được triển khai.

## Yêu cầu

- Unity `6000.4.4f1`.
- Windows 10/11.
- Git.
- Unity Input System và URP 2D đã được khai báo trong `Packages/manifest.json`.

Nên mở project bằng đúng phiên bản Unity để tránh thay đổi format scene hoặc asset ngoài ý muốn.

## Cách tải và chạy

```bash
git clone https://github.com/Naams2k10fpt/BlockEscape.git
```

Sau đó:

1. Mở Unity Hub.
2. Chọn **Add project from disk**.
3. Chọn thư mục `BlockEscape`.
4. Mở scene `Assets/_Project/Scenes/MainMenu.unity`.
5. Nhấn **Play**.

Nếu scene bị mất reference hoặc Hierarchy chưa đúng, chọn:

```text
Block Escape → Build Classroom Tetris Scene
```

Công cụ trên tạo đồng thời `MainMenu` và `TetrisDemo`, đồng thời cập nhật Build Settings theo thứ tự Main Menu → Gameplay.

## Điều khiển hiện tại

| Phím | Chức năng |
|---|---|
| `A / D` | Di chuyển tetromino trái/phải |
| `W` | Xoay tetromino theo chiều kim đồng hồ |
| `S` | Soft drop |
| `Left / Right Arrow` | Di chuyển player |
| `Up Arrow` | Player nhảy |
| `Down Arrow` | Player cúi |
| `Esc` | Mở/đóng Pause Menu |
| `R` | Mở hộp xác nhận reset lượt chơi |

Tetris dùng WASD, player dùng phím mũi tên để hai hệ điều khiển không xung đột.

Không đọc `Keyboard.current` trực tiếp trong script gameplay mới. Thành viên phải lấy action tương ứng từ `InputService` để Pause có thể vô hiệu hóa gameplay thống nhất.

## InputService là gì?

`InputService` là service trung tâm trong `BlockEscape.Core` dùng để quản lý toàn bộ input của game. Nó giữ một `InputActionAsset`, tìm ba action map chính và bật/tắt chúng đúng lúc:

| Action Map | Dùng cho | Binding hiện tại |
|---|---|---|
| `Tetris` | Điều khiển tetromino | `A/D`, `W`, `S` |
| `Player` | Điều khiển nhân vật | `Left/Right/Up/Down Arrow` |
| `System` | Lệnh hệ thống | `Esc`, `R` |

Khi Pause, Game Over hoặc reset confirmation, gameplay input có thể bị tắt bằng một lệnh chung thay vì từng script tự xử lý phím. Script gameplay mới nên lấy input qua `InputService.Current`, ví dụ `InputService.Current.PlayerMove`, `PlayerJump`, `TetrisMove`, `Pause`.

## Cấu trúc project

```text
Assets/_Project/
├── Animations/       Animation của player và gameplay
├── Art/              Sprite và asset đồ họa
├── Audio/            Music và sound effect
├── Editor/           Công cụ dựng scene trong Unity Editor
├── Prefabs/          Prefab gameplay
├── Resources/        Config được tải runtime
├── Scenes/           Scene chính của project
├── Scripts/
│   ├── Bootstrap/    Khởi tạo và kết nối scene
│   ├── Core/         Hệ thống dùng chung
│   ├── Gameplay/     Board, tetromino và gameplay
│   ├── Player/       Player controller, config và health
│   └── UI/           HUD và preview
├── Tests/            EditMode và PlayMode tests
└── Tilemaps/         Tile và Tilemap của đấu trường
```

Các thư mục `Library`, `Temp`, `Logs`, `UserSettings` và file IDE được tạo tự động, không được commit.

## Kiến trúc Tetris hiện tại

```text
TetrominoSpawner
    ↓ lấy piece từ 7-bag
ActiveTetromino
    ↓ di chuyển, xoay và khóa
BlockBoard
    ↓ cập nhật dữ liệu
BoardModel
    ↓ phát hiện hàng đầy
GameSession + Score
    ↓ HUD / Pause / Game Over
```

- `BoardModel` chỉ quản lý dữ liệu lưới, không phụ thuộc scene.
- `BlockBoard` đồng bộ dữ liệu với block hiển thị và collider.
- `TetrominoSpawner` quản lý 7-bag, piece hiện tại và piece tiếp theo.
- `ActiveTetromino` xử lý input WASD, chuyển động từng ô và dùng solid collider để chặn player.
- `TetrisDemoBootstrap` kết nối scene, tự spawn player khi cần, tạo collider biên đấu trường, clamp player trong arena và mở Game Over khi player bị block đè mà không còn đường thoát.
- `GameSession` giữ trạng thái lượt chơi, thời gian sống sót, phase, tổng hàng đã xóa, điểm và kết quả cuối để HUD/menu dùng thống nhất.

## Quy trình làm việc nhóm

### Trước khi bắt đầu

```bash
git switch main
git pull
git switch -c feature/ten-chuc-nang
```

Ví dụ:

```bash
git switch -c feature/player-controller
git switch -c feature/drone-ai
git switch -c fix/row-clear-collision
```

### Khi hoàn thành một phần nhỏ

```bash
git add Assets Packages ProjectSettings README.md PROGRESS.md
git commit -m "feat: add player movement"
git push -u origin feature/player-controller
```

Quy ước commit:

| Prefix | Dùng khi |
|---|---|
| `feat:` | Thêm chức năng mới |
| `fix:` | Sửa lỗi |
| `refactor:` | Sắp xếp lại code nhưng không đổi hành vi |
| `test:` | Thêm hoặc sửa test |
| `docs:` | Cập nhật tài liệu |
| `chore:` | Cấu hình project hoặc công việc phụ |

### Quy tắc tránh conflict

- Không để hai người sửa cùng một scene hoặc prefab trong cùng thời điểm.
- Luôn commit cả file `.meta` đi kèm asset.
- Pull nhánh mới nhất trước khi bắt đầu công việc.
- Mỗi branch chỉ tập trung vào một chức năng hoặc một lỗi.
- Không đưa `Library`, `Temp`, `Logs`, build hoặc file IDE lên Git.
- Sau mỗi chức năng, cập nhật [PROGRESS.md](PROGRESS.md).
- Không merge nếu Console còn error hoặc gameplay chính không chạy.

## Kiểm thử

Trong Unity:

1. Mở **Window → General → Test Runner**.
2. Chọn **EditMode**.
3. Chọn **Run All**.

Trước khi merge, cần kiểm tra thêm:

- Scene mở không bị Missing Script hoặc Missing Reference.
- Tetromino preview trùng với piece sinh tiếp theo.
- Block không đi xuyên tường hoặc block đã khóa.
- Xóa hàng và dịch block đúng vị trí.
- Không có exception sau ít nhất 50 tetromino.

## Tài liệu

- [Tiến độ và công việc tiếp theo](PROGRESS.md)
- [Chi tiết cột mốc Tetris Core](Assets/_Project/TETRIS_CORE.md)

## Thành viên và phân công

Cập nhật bảng này khi nhóm chốt thành viên:

| Thành viên | Phụ trách |
|---|---|
| NguyenNgu2005 | Các phần đã sửa trong branch hiện tại: gameplay, player, input, HUD/session và test |
| Chưa phân công | Drone AI, dynamic events, pickup và power-up |
| Chưa phân công | Options, save data, art, animation và audio |

## License và asset bên ngoài

Hiện project sử dụng placeholder được tạo trong project. Khi thêm asset bên ngoài, cần ghi rõ tên asset, tác giả, nguồn và loại giấy phép tại mục này.
