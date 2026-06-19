# Block Escape — Theo dõi tiến trình

**Cập nhật lần cuối:** 20/06/2026  
**Cột mốc hiện tại:** Hệ thống Tetris cốt lõi  
**Tiến độ tổng thể ước tính:** 25%

> Cập nhật file này sau mỗi chức năng hoàn thành, lỗi quan trọng được sửa hoặc lần kiểm thử gameplay.

## Tiến độ theo module

| Module | Tiến độ | Trạng thái |
|---|---:|---|
| Hệ thống Tetris | 90% | Đã triển khai, cần kiểm thử thủ công lần cuối |
| Điều khiển nhân vật | 0% | Chưa bắt đầu |
| Máu và sát thương | 0% | Chưa bắt đầu |
| Drone AI | 0% | Chưa bắt đầu |
| Sự kiện môi trường | 0% | Chưa bắt đầu |
| Vật phẩm và power-up | 0% | Chưa bắt đầu |
| HUD và luồng game | 20% | Mới có HUD của bản demo Tetris |
| Main Menu và Options | 0% | Chưa bắt đầu |
| Lưu dữ liệu và đổi phím | 0% | Chưa bắt đầu |
| Đồ họa, animation và âm thanh | 5% | Hiện đang dùng hình ảnh placeholder |
| Kiểm thử và bản build Windows | 20% | Đã có test cho board và kiểm tra compile |

## Đã hoàn thành

### Cấu trúc project

- [x] Tạo thư mục `Assets/_Project` có tổ chức rõ ràng.
- [x] Tạo assembly cho Runtime, Editor và EditMode Test.
- [x] Thêm file `.gitignore` dành cho Unity.
- [x] Tạo scene có Hierarchy rõ ràng, phù hợp trình bày trên lớp.
- [x] Tạo physics layer `World` và `FallingBlock`.
- [x] Thêm scene Tetris Demo vào Build Settings.

### Hệ thống Tetris cốt lõi

- [x] Xây dựng board 14×20 hoạt động theo lưới xác định.
- [x] Thêm bảy tetromino I, J, L, O, S, T và Z.
- [x] Thêm các trạng thái xoay cho từng tetromino.
- [x] Tạo bộ sinh ngẫu nhiên 7-bag có seed.
- [x] Tetromino sinh tại cột và rotation ban đầu ngẫu nhiên.
- [x] Thêm điều khiển tetromino bằng `A/D` để di chuyển, `W` để xoay và `S` để soft drop.
- [x] Bỏ cảnh báo 0,8 giây vì HUD đã hiển thị tetromino tiếp theo.
- [x] Chuyển chuyển động rơi thành từng bước, mỗi bước đúng một ô.
- [x] Thêm thời gian chờ trước khi block được khóa.
- [x] Chuyển block đã khóa thành các cell có collider tĩnh.
- [x] Thêm object pooling cho block cell.
- [x] Phát hiện hàng đầy.
- [x] Thêm hiệu ứng cảnh báo, xóa hàng và dịch các hàng phía trên.
- [x] Thêm hai hàng nguy hiểm và bộ đếm tràn board ba giây.
- [x] Thêm khu vực bên phải hiển thị tetromino tiếp theo trong 7-bag.

### Demo và trình bày

- [x] Tạo sẵn Main Camera, Game Manager và Tetris Systems trong scene.
- [x] Tạo Block Board và vùng chứa block runtime trong Hierarchy.
- [x] Tạo Arena, Grid Lines, Danger Line và Borders có thể quan sát trong Scene View.
- [x] Tạo sẵn HUD Canvas trong scene.
- [x] Hiển thị seed, số block đã sinh, số cell, hàng đã xóa và điểm.
- [x] Thêm phím `R` để reset và `P` để pause.
- [x] Thêm công cụ Editor để dựng lại scene trình bày.

### Kiểm tra kỹ thuật

- [x] Runtime assembly compile không có lỗi từ code của project.
- [x] Editor assembly compile không có lỗi từ code của project.
- [x] EditMode test assembly compile thành công.
- [x] Bộ kiểm tra BoardModel vượt qua các test về hàng, rotation, thông số board và 7-bag.
- [x] Sửa lỗi block đã khóa bị thiếu `SpriteRenderer`.
- [x] Sửa lỗi tetromino nháy tại giữa màn hình trong frame vừa được sinh.
- [ ] Kiểm tra thủ công khu vực hiển thị block tiếp theo sau lần dựng scene mới nhất.
- [ ] Kiểm tra chuyển động rơi từng ô với ít nhất 20 tetromino.
- [ ] Kiểm tra giữ `A/D`, xoay sát tường và soft drop bằng `S`.
- [ ] Chạy Unity Test Runner và lưu ảnh kết quả.

## Hierarchy hiện tại

```text
Main Camera
Game Manager
Tetris Systems
└── Block Board (14 x 20)
    └── Locked Block Cells (Runtime)
Arena Visuals
├── Board Background
├── Grid Lines
├── Danger Line (Overflow)
└── Arena Borders
User Interface
└── Tetris HUD Canvas
    ├── Game Title
    ├── Game Statistics
    ├── Game Status
    ├── Next Piece Preview
    └── Overflow Meter
```

## Công việc tiếp theo

Thực hiện theo thứ tự sau:

1. [ ] Kiểm thử hệ thống Tetris hiện tại và sửa các lỗi cản trở gameplay.
2. [ ] Tạo Tilemap cho nền, tường và platform cơ bản.
3. [ ] Xây dựng di chuyển, nhảy và kiểm tra tiếp đất cho nhân vật.
4. [ ] Thêm cúi người, thay đổi collider và kiểm tra khoảng trống khi đứng dậy.
5. [ ] Thêm HP, sát thương, knockback và iFrame.
6. [ ] Xử lý trường hợp block khóa đè lên nhân vật.
7. [ ] Kết nối trạng thái nhân vật với HUD.
8. [ ] Xây dựng các trạng thái của Drone AI.
9. [ ] Xây dựng sự kiện Cutter Sweep và Block Overdrive.
10. [ ] Thêm Score Crystal, Health Pack và Jump Boost.
11. [ ] Thêm Main Menu, Pause Menu và màn hình Game Over.
12. [ ] Thêm Options, lưu dữ liệu và đổi phím.
13. [ ] Thay placeholder bằng đồ họa, animation và âm thanh hoàn chỉnh.
14. [ ] Build và kiểm thử phiên bản Windows.

## Ghi chú và giới hạn hiện tại

- Các object logic như Game Manager và Spawner không có Renderer nên không xuất hiện thành hình trong Scene View. Có thể kiểm tra chúng qua Hierarchy và Inspector.
- HUD sử dụng Screen Space Overlay nên chủ yếu được quan sát trong Game View.
- Người chơi điều khiển tetromino bằng `WASD`; nhân vật platform sau này sẽ dùng các phím mũi tên để tránh xung đột.
- Cột và rotation ban đầu của tetromino được chọn ngẫu nhiên; game không còn tự chọn vị trí đặt block tốt nhất.
- Chọn **Block Escape → Build Classroom Tetris Scene** nếu cần tạo lại Hierarchy hoặc các reference trong scene.

## Điều kiện hoàn thành cột mốc Tetris Core

Cột mốc Tetris Core được xem là hoàn thành khi:

- [ ] Có thể sinh và khóa 50 tetromino mà không xảy ra exception hoặc soft-lock.
- [ ] Preview luôn trùng với tetromino được sinh tiếp theo.
- [ ] Mỗi tetromino rơi đúng một ô trong mỗi bước di chuyển.
- [ ] `A/D`, `W` và `S` điều khiển tetromino đúng và không cho phép đi xuyên board/block.
- [ ] Xóa chính xác một hoặc nhiều hàng đầy cùng lúc.
- [ ] Các hàng phía trên được dịch xuống đúng vị trí.
- [ ] Board overflow dừng sinh block và reset tạo một lượt chơi sạch.
- [ ] Mọi block đã khóa đều có Renderer và Collider.

## Nhật ký tiến trình

| Ngày | Thay đổi | Kết quả |
|---|---|---|
| 19/06/2026 | Lập kế hoạch và kiểm tra project Unity mới | Thống nhất phạm vi và kiến trúc |
| 19/06/2026 | Xây dựng BoardModel, tetromino và 7-bag | Quy tắc Tetris cốt lõi hoạt động |
| 19/06/2026 | Thêm rơi, khóa block, xóa hàng và overflow | Vòng lặp Tetris tự động hoạt động |
| 19/06/2026 | Thêm test và HUD demo | Compile và kiểm tra mô hình thành công |
| 19/06/2026 | Chuyển scene runtime thành Hierarchy có sẵn | Dễ kiểm tra và trình bày trên lớp |
| 20/06/2026 | Thêm preview block tiếp theo và chuyển động rơi từng ô | Chờ kiểm thử thủ công lần cuối |
| 20/06/2026 | Bỏ đặt block tự động và thêm điều khiển WASD | Tetromino do người chơi điều khiển |
| 20/06/2026 | Đặt vị trí spawn trước khi tạo visual và physics | Không còn nháy block tại tâm màn hình |

## Cách cập nhật file này

1. Thay đổi ngày trong mục `Cập nhật lần cuối`.
2. Đánh dấu công việc hoàn thành bằng `[x]`.
3. Cập nhật phần trăm của module liên quan.
4. Ghi lỗi hoặc giới hạn mới trong mục **Ghi chú và giới hạn hiện tại**.
5. Thêm một dòng ngắn gọn vào bảng **Nhật ký tiến trình**.
