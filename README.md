# Block Escape

Block Escape là game 2D kết hợp **Tetris**, **platform** và **sinh tồn**, được phát triển bằng Unity cho final project PRU213.

Người chơi sẽ điều khiển tetromino để tạo địa hình, sau đó điều khiển một nhân vật bên trong đấu trường để né block, tận dụng địa hình và sống sót qua các phase ngày càng khó.

> Project đang trong giai đoạn phát triển. Xem checklist và công việc tiếp theo tại [PROGRESS.md](PROGRESS.md).

## Trạng thái hiện tại

**Cột mốc:** Tetris Core  
**Tiến độ tổng thể ước tính:** 25%

Đã có:

- Board Tetris 14×20 và hai hàng nguy hiểm.
- Bảy loại tetromino cùng bộ sinh 7-bag.
- Block rơi từng ô theo nhịp Tetris.
- Điều khiển block bằng WASD.
- Preview tetromino tiếp theo.
- Khóa block, phát hiện/xóa nhiều hàng và dịch địa hình.
- Board overflow, pause, reset và HUD demo.
- Scene có Hierarchy rõ ràng để kiểm tra và trình bày.
- EditMode tests cho các quy tắc của board.

Player platform, Drone AI, event, pickup, menu hoàn chỉnh, save data, art và audio chưa được triển khai.

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
4. Mở scene `Assets/_Project/Scenes/TetrisDemo.unity`.
5. Nhấn **Play**.

Nếu scene bị mất reference hoặc Hierarchy chưa đúng, chọn:

```text
Block Escape → Build Classroom Tetris Scene
```

## Điều khiển hiện tại

| Phím | Chức năng |
|---|---|
| `A / D` | Di chuyển tetromino trái/phải |
| `W` | Xoay tetromino theo chiều kim đồng hồ |
| `S` | Soft drop |
| `P` | Pause/Resume |
| `R` | Reset lượt chơi với cùng seed |

Nhân vật platform sau này sẽ sử dụng các phím mũi tên để không xung đột với điều khiển Tetris.

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
Row Clear + HUD
```

- `BoardModel` chỉ quản lý dữ liệu lưới, không phụ thuộc scene.
- `BlockBoard` đồng bộ dữ liệu với block hiển thị và collider.
- `TetrominoSpawner` quản lý 7-bag, piece hiện tại và piece tiếp theo.
- `ActiveTetromino` xử lý input WASD và chuyển động từng ô.
- `TetrisDemoBootstrap` kết nối các object đã được tạo sẵn trong scene.

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
| Chưa phân công | Tetris và hệ thống gameplay |
| Chưa phân công | Player, health và animation |
| Chưa phân công | UI, save và scene flow |
| Chưa phân công | Art, audio và kiểm thử |

## License và asset bên ngoài

Hiện project sử dụng placeholder được tạo trong project. Khi thêm asset bên ngoài, cần ghi rõ tên asset, tác giả, nguồn và loại giấy phép tại mục này.
