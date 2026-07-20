# Block Escape — Tiến độ và hướng dẫn triển khai chung

- **Cập nhật lần cuối:** 20/07/2026
- **Cột mốc hiện tại:** Gameplay tích hợp playable
- **Tiến độ tổng thể ước tính:** 61%
- **Unity bắt buộc:** `6000.4.4f1`
- **Luồng chạy hiện tại:** `MainMenu.unity` → `TetrisDemo.unity`; `ArenaSandbox.unity` dùng để playtest vật lý player
- **Branch tích hợp đang chờ merge:** `feature/pickups-powerups` tại `5659406`

> Đây là nguồn thông tin chung của cả team. Thành viên phải đọc phần **Quyết định kỹ thuật**, task mình nhận và **Checklist trước khi merge** trước khi bắt đầu code.

## 1. Cách sử dụng tài liệu

Mỗi task có mã riêng, ví dụ `PLAYER-01`. Khi nhận task, thành viên phải:

1. Ghi tên mình vào cột **Phụ trách**.
2. Đổi trạng thái từ `Chưa làm` thành `Đang làm`.
3. Tạo branch đúng tên được đề xuất.
4. Chỉ sửa các file thuộc phạm vi task; muốn sửa file dùng chung phải báo người phụ trách tích hợp.
5. Hoàn thành toàn bộ tiêu chí nghiệm thu trước khi tạo Pull Request.
6. Cập nhật phần trăm module và thêm một dòng vào **Nhật ký tiến trình**.

Quy ước trạng thái:

| Trạng thái | Ý nghĩa |
|---|---|
| `Chưa làm` | Chưa có người nhận hoặc chưa bắt đầu |
| `Đang làm` | Đã có branch và người phụ trách |
| `Chờ review` | Code xong, đã tự test, đang chờ thành viên khác kiểm tra |
| `Bị chặn` | Không thể tiếp tục vì phụ thuộc task khác hoặc lỗi chưa giải quyết |
| `Hoàn thành` | Đã merge vào `main` và chạy ổn trong scene tích hợp |

## 2. Quyết định kỹ thuật bắt buộc

Không tự ý thay đổi các quyết định sau trong branch cá nhân. Nếu cần đổi, phải thảo luận với cả nhóm và ghi quyết định mới vào tài liệu này trước khi code.

| Hạng mục | Quyết định thống nhất |
|---|---|
| Engine | Unity `6000.4.4f1` |
| Nền tảng | Windows x86-64, ưu tiên màn hình 16:9 |
| Render | URP 2D, pixel-art, PPU thống nhất 16 |
| Vật lý | Unity Physics 2D; player dùng Dynamic Rigidbody2D, block đang rơi dùng Kinematic Rigidbody2D |
| Block đã khóa | Dữ liệu nằm trong `BoardModel`, hình/collider nằm trong pool của `BlockBoard` |
| Địa hình tĩnh | Dùng Tilemap; không đưa tetromino động vào Tilemap |
| Input Tetris | `A/D` di chuyển, `W` xoay, `S` soft drop |
| Input player | Mũi tên trái/phải di chuyển, mũi tên lên nhảy, mũi tên xuống cúi |
| Pause | `Esc` mở/đóng Pause Menu; `R` mở xác nhận reset |
| UI | uGUI Canvas; TextMeshPro chỉ dùng khi cả nhóm đã import và thống nhất font |
| Cấu hình | Giá trị cân bằng đặt trong ScriptableObject, không rải magic number trong nhiều script |
| Giao tiếp hệ thống | Dùng C# event và reference Inspector; không dùng `FindObjectOfType` mỗi frame |
| Lưu dữ liệu | JSON có version trong `Application.persistentDataPath` |
| Scene final | `Bootstrap`, `MainMenu`, `Gameplay`; `TetrisDemo` chỉ là scene kiểm thử Tetris |
| Code namespace | `BlockEscape.Core`, `BlockEscape.Tetris`, `BlockEscape.Player`, `BlockEscape.AI`, `BlockEscape.Events`, `BlockEscape.UI` |
| Ngôn ngữ code | Tên class/biến/comment kỹ thuật bằng tiếng Anh; tài liệu và nội dung UI có thể dùng tiếng Việt |

### Quy tắc code

- Một file public class chính, tên file trùng tên class.
- Field private dùng `_camelCase`; property/method/class dùng `PascalCase`.
- Field hiển thị Inspector dùng `[SerializeField] private`, không đổi thành `public` chỉ để kéo reference.
- Không tạo singleton mới nếu chưa được ghi trong kiến trúc chung.
- Không gọi `GetComponent`, `Find` hoặc cấp phát `new List` liên tục trong `Update/FixedUpdate`.
- Mọi subscription event phải được gỡ trong `OnDisable` hoặc `OnDestroy` nếu publisher có thể sống lâu hơn subscriber.
- Không sửa trực tiếp file trong `Library`, `Temp`, `Logs`, `UserSettings` hoặc file `.csproj` do Unity sinh.
- Luôn commit file `.meta` đi cùng asset.

## 3. Phân chia file và tránh conflict

### File dùng chung — chỉ người tích hợp sửa

Các file sau dễ conflict. Thành viên khác không sửa nếu chưa báo trước:

- `ProjectSettings/TagManager.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/_Project/Scenes/Gameplay.unity`
- `Assets/_Project/Scenes/MainMenu.unity`
- `Assets/_Project/Scenes/Bootstrap.unity`
- `README.md`
- `PROGRESS.md`

`TetrisDemo.unity` được tạo bởi `BlockEscapeTetrisSetup.cs`. Không sửa Hierarchy của scene này bằng tay vì công cụ dựng scene có thể ghi đè thay đổi. Muốn đổi Tetris Demo phải sửa Editor builder hoặc component nguồn.

### Phạm vi theo vai trò

| Vai trò | Thư mục chính | Không tự ý sửa |
|---|---|---|
| Tích hợp/Gameplay Lead | `Core`, scene final, `Resources`, ProjectSettings | Art nguồn của thành viên khác |
| Tetris | `Scripts/Gameplay`, `Scripts/Tetris`, Tetris tests | Player, AI, menu final |
| Player | `Scripts/Player`, `Prefabs/Player`, Player sandbox | BoardModel và Tetris scene builder |
| AI/Event | `Scripts/Bootstrap/TetrisDemoBootstrap.cs`, `Scripts/Gameplay/TetrominoSpawner.cs` | Player controller và menu |
| UI/System | `Scripts/UI`, `Scripts/Core/Save`, UI prefabs | Tetris rules và Player physics |
| Art/Audio/QA | `Resources/Art`, `Resources/Audio`, test checklist | Gameplay code khi chưa trao đổi |

Nếu nhóm có hai người: người 1 nhận Tetris + Player + tích hợp; người 2 nhận AI/Event + UI + Art/QA.

## 4. Tiến độ theo module

| Module | Tiến độ | Trạng thái | Phụ trách |
|---|---:|---|---|
| Tetris Core | 96% | Lần chạy gần nhất EditMode 84/84 xanh; test mới compile nhưng cần chạy lại Test Runner, còn playtest 50 piece | NguyenNgu2005 |
| Chuẩn hóa Input System | 98% | Có rebind 8 action, reset mặc định, JSON persistence và compile test; còn kiểm thử Play Mode đổi scene | NguyenNgu2005 |
| Tilemap và đấu trường | 55% | Có Arena prefab, sandbox scene, player thật tại spawn và test collider/support; còn playtest vật lý | NguyenNgu2005 |
| Player Controller | 79% | Có movement, jump cao hơn, crouch, gravity config, prefab, sandbox integration, runtime spawn trong TetrisDemo và clamp trong biên arena; còn playtest cảm giác điều khiển | NguyenNgu2005 |
| Block tương tác với player | 57% | Falling block pre-check vị trí kế tiếp; block đang rơi dùng trigger sensor để không bị physics đẩy ngang; mọi chạm không-crush với block đang rơi chỉ bật player xuống; locked block collider được bù kín khe để player đi trên bề mặt đỡ nhảy; meteor có thể phá block đã khóa theo bán kính; crush trừ tim, tìm điểm respawn trống và cho bất tử 3 giây | NguyenNgu2005 |
| Máu và sát thương | 66% | Có `DamageInfo`, `IDamageable`, `PlayerHealth`, reset health, invulnerability public API, crush/enemy/hazard damage và HP 0 kết thúc run trong TetrisDemo; còn cần cân bằng damage/iFrame qua playtest | NguyenNgu2005 |
| Game Session và scoring | 72% | Có countdown, pause state thống nhất, score/phase/result và Near Miss +50 | NguyenNgu2005 |
| Drone AI | 52% | Drone gốc tồn tại phía trên vạch đỏ, bám X player và bắn đạn xuống; mỗi 6 giây thả clone telegraph rồi dash gây Enemy damage | NguyenNgu2005 |
| Dynamic Events | 70% | Có Event Director runtime, Block Overdrive, Cutter Sweep dùng rocket pixel-art với exhaust/burst và có thể né bằng crouch, cùng Meteor Shower phá block bán kính 2; chờ Play Mode test để cân bằng lịch event | NguyenNgu2005 |
| Pickup và power-up | 86% | Có ba pickup pixel-art với aura/burst riêng, weighted 45/35/20, spawn đầu 12–18 giây, lần sau 18–28 giây, Kinematic Rigidbody2D, rơi lại khi mất mặt đỡ và tồn tại 10–15 giây sau khi chạm đất; Jump Boost tint player khi hoạt động; chờ Play Mode test | NguyenNgu2005 |
| HUD và game flow | 78% | Countdown/Playing/Paused dùng cùng GameSession; HUD, Pause/Game Over và tutorial đã nối; còn QA nhiều độ phân giải | NguyenNgu2005 |
| Main Menu, Options và Save | 90% | Có Bootstrap builder, first-run tutorial, rebind/reset persistence, audio live preview/rollback và display settings; chờ Play Mode QA | NguyenNgu2005 |
| Art, animation và audio | 92% | Có pixel-art/VFX, BGM theo menu/gameplay và SFX cho UI, pickup, hồi máu, player damage, drone laser, rocket Cutter, meteor, Game Over; còn thiếu row clear/block/Near Miss SFX | NguyenNgu2005 |
| Test và Windows build | 72% | Runtime/test/editor compile 0 warning/0 error; EditMode 84/84 và Windows x64 build là kết quả trước đợt audio/polish mới, cần chạy lại trước khi nộp | NguyenNgu2005 |

## 5. Phần đã hoàn thành

### Cấu trúc project

- [x] Tạo `Assets/_Project` và các assembly Runtime, Editor, Test.
- [x] Thêm `.gitignore` dành cho Unity.
- [x] Tạo Tetris Demo có Hierarchy rõ ràng.
- [x] Thêm layer `World` và `FallingBlock`.
- [x] Thêm README và tài liệu tiến độ tại root repository.

### Tetris Core

- [x] Board 14×20 hoạt động theo lưới xác định.
- [x] Bảy tetromino I, J, L, O, S, T, Z và rotation.
- [x] Bộ sinh ngẫu nhiên 7-bag có seed.
- [x] Cột và rotation ban đầu ngẫu nhiên.
- [x] Điều khiển `A/D/W/S`, giữ phím ngang và wall kick đơn giản.
- [x] Block rơi từng bước đúng một ô.
- [x] Preview tetromino tiếp theo.
- [x] Ghost piece trong suốt hiển thị vị trí khóa dự kiến phía dưới.
- [x] Lock delay, collider tĩnh và object pooling.
- [x] Phát hiện/xóa nhiều hàng và dịch block phía trên.
- [x] Hai danger row và overflow timer ba giây.
- [x] Reset, pause và HUD thống kê.
- [x] Pause Menu bằng `Esc`, nút Resume và Reset có hộp xác nhận.
- [x] Pause Menu hiển thị số block, hàng đã xóa, điểm và seed của lượt hiện tại.
- [x] Sửa lỗi thiếu SpriteRenderer và nháy block tại tâm màn hình.

### Input System

- [x] Thu gọn `InputSystem_Actions.inputactions` thành ba map `Tetris`, `Player`, `System`.
- [x] WASD chỉ điều khiển Tetris; phím mũi tên được dành riêng cho player.
- [x] Tạo một `InputService` persistent quản lý toàn bộ action map.
- [x] `ActiveTetromino` không còn đọc `Keyboard.current` trực tiếp.
- [x] Pause và hộp xác nhận reset vô hiệu hóa input gameplay nhưng vẫn giữ map `System`.
- [x] Scene builder tạo object `Input Service (Persistent)` và gắn action asset trong Inspector.
- [x] EditMode test xác nhận binding và việc bật/tắt map gameplay không tắt map `System`.

### Scene flow và Game Over

- [x] Tạo scene `MainMenu` với nút Bắt đầu và Thoát game.
- [x] Nút Bắt đầu tải `TetrisDemo`; Build Settings đặt Main Menu trước Gameplay.
- [x] Board overflow mở bảng tổng kết thay vì chỉ hiện dòng trạng thái.
- [x] Tổng kết hiển thị nguyên nhân thua, block đã thả, hàng đã xóa, điểm và seed.
- [x] Game Over có nút Chơi lại và Main Menu.
- [x] Pause Menu có nút trở về Main Menu và yêu cầu xác nhận trước khi rời lượt.
- [x] `GameSession` quản lý trạng thái lượt chơi, thời gian sống sót, điểm theo giây và điểm xóa hàng.
- [x] HUD, Pause Menu và Game Over summary hiển thị score/thời gian từ cùng một session.

### Tilemap và đấu trường

- [x] Tạo `Assets/_Project/Prefabs/Arena/Arena.prefab` với `Grid`, bốn Tilemap và spawn points.
- [x] Tạo `Assets/_Project/Scenes/Sandbox/ArenaSandbox.unity` để kiểm tra đấu trường độc lập.
- [x] Ground/Platform dùng `TilemapCollider2D`, `CompositeCollider2D` và Static `Rigidbody2D`.
- [x] Obstacle dùng trigger collider riêng; Decoration Tilemap không có collider.
- [x] Thêm layer mục tiêu `Player`, `Enemy`, `Hazard`, `Pickup`, `Sensor` vào `TagManager`.
- [x] Test xác nhận Player Spawn có ground support bên dưới trên layer `World`.
- [x] ArenaSandbox có `InputService` và player thật được instantiate từ `Player.prefab` tại Player Spawn.

### Player Controller

- [x] Tạo `PlayerConfig` với thông số mặc định đã thống nhất; `jumpVelocity = 12.5` để nhảy cao hơn.
- [x] Tạo `PlayerController` đọc input trong `Update` và áp vận tốc Rigidbody trong `FixedUpdate`.
- [x] Tạo `Assets/_Project/Prefabs/Player/Player.prefab` với Rigidbody2D, CapsuleCollider2D, Visual và Ground Check.
- [x] Gravity của player được đưa vào `PlayerConfig` và đặt mặc định `gravityScale = 3` để rơi vừa hơn.
- [x] Ground check dùng layer `World`, không dựa vào tag.
- [x] Thêm EditMode test xác nhận config mặc định và cấu trúc prefab.
- [x] Thêm crouch bằng Down Arrow, đổi collider theo `PlayerConfig` và giữ crouch khi thiếu headroom.
- [x] Gắn `PlayerHealth` vào prefab và nối SpriteRenderer để nhấp nháy iFrame.
- [x] Đưa `Player.prefab` vào `ArenaSandbox` để playtest bằng Left/Right/Up/Down Arrow.
- [x] TetrisDemo builder/runtime spawn `Player.prefab` trong đấu trường tại vùng giữa-dưới, không dùng platform tạm.
- [x] Viền trái/phải/đáy của đấu trường có collider layer `World`, player rơi xuống đáy và không lọt ra ngoài.
- [x] `TetrisDemoBootstrap` tự spawn player khi Play nếu scene chưa có player.
- [x] `BlockBoard` phát hiện locked cell overlap player và phát event crush.
- [x] TetrisDemo chuyển Game Over với lý do player bị block đè.
- [x] Falling tetromino dùng trigger `BoxCollider2D` sensor và overlap logic để bắt va chạm mà không để physics đẩy player ngang.
- [x] Player và collider block/biên arena dùng frictionless material để hạn chế dính tường.
- [x] TetrisDemo clamp player trong biên board nếu physics bị ép vượt khỏi arena.
- [x] Active/locked block chỉ phát crush damage khi player bị đè và không còn đường thoát ngang.
- [x] Crush không kết thúc ngay nếu player còn tim; player mất 1 tim rồi respawn ở điểm trống cao hơn block đã khóa cao nhất 5 đơn vị Y.
- [x] Sau respawn crush, player bất tử 3 giây, nhấp nháy trong lúc bất tử và hết hiệu ứng khi hết bất tử.
- [x] Khi mới respawn crush, player đứng lơ lửng 0.75 giây trước khi gravity kéo rơi xuống.
- [x] Player nhảy đụng mặt dưới falling block sẽ bị chặn bởi collider, không bị tính là crush.
- [x] Falling tetromino kiểm tra player tại vị trí kế tiếp trước khi move/rotate để tránh xuyên qua hoặc đẩy player văng ngang khi soft drop.
- [x] Player đang nhảy lên vào block đang xuống sẽ bị bounce xuống, block vẫn tiếp tục rơi.
- [x] Player chạm block đang rơi nhưng chưa bị kẹt sẽ chỉ bị bật xuống; block vẫn tiếp tục rơi và không đẩy player ngang sang khe trống.
- [x] Falling block dùng trigger sensor + overlap logic thay cho physics solid contact để Unity không tự tách player sang hai bên.
- [x] Player cố tình đu/chèn cạnh block đã khóa sẽ được đẩy ngang nhẹ thay vì bị safety probe văng lên theo vòng lặp.
- [x] Nếu player bị kẹt collider trong block/world, TetrisDemo tự dò điểm trống gần nhất để giải kẹt.
- [x] Crush Game Over do falling block chỉ phát sau khi block đã apply vị trí xuống, không chết sớm ở vị trí dự đoán.
- [x] Falling block tiếp tục rơi khi player đứng cạnh/đứng dưới nhưng còn đường thoát, không còn bị kẹt giữa không trung.

### Máu và sát thương

- [x] Tạo contract dùng chung `DamageInfo`, `DamageType` và `IDamageable`.
- [x] Tạo `PlayerHealth` với Max HP, iFrame, knockback, heal và event `Died`.
- [x] `PlayerHealth.ResetHealth()` hồi HP và trạng thái death/iFrame khi restart run.
- [x] `PlayerHealth.StartInvulnerability(seconds)` cho phép bật bất tử/nhấp nháy theo thời lượng.
- [x] TetrisDemo dùng crush damage trừ tim, respawn nếu còn tim và lắng nghe `PlayerHealth.Died` để HP 0 mở Game Over.
- [x] iFrame chặn damage liên tục và trả alpha SpriteRenderer về 1 khi kết thúc/disable/death.
- [x] Thêm EditMode test cho damage, knockback, iFrame và death chỉ phát một lần.

### Drone AI

- [x] Thêm runtime drone cho `TetrisDemo`, tự tạo khi Play, không cần sửa scene thủ công.
- [x] Drone hoạt động từ phase 1, bám X player phía trên vạch đỏ và dừng khi Pause/GameOver.
- [x] Drone gốc tồn tại vĩnh viễn trong lượt chơi; falling block không thể phá hoặc làm respawn.
- [x] Mỗi 6 giây drone thả clone; clone telegraph rồi dash gây `Enemy` damage và knockback qua `IDamageable`.
- [x] Drone bắn đạn xuống; đạn chạm `World`/`FallingBlock` tạo vụ nổ nhỏ.

### Dynamic Events

- [x] Thêm runtime Event Director làm nơi duy nhất lập lịch event trong `TetrisDemo`.
- [x] Block Overdrive chạy từ phase 1 với interval ngắn hơn, tint tím và tăng tốc 3 tetromino tiếp theo, hiện số piece còn lại trên HUD rồi trả màu/tốc độ phase hiện tại.
- [x] Meteor Shower chạy từ phase 1, báo đường bay bằng vệt đỏ, spawn chậm hơn từ sát mép trong trái/phải nửa trên arena, rơi vào X ngẫu nhiên với Y dựa trên block cao nhất, gây Hazard damage khi trúng player và phá block đã khóa bán kính 2 cell sau 0.5 giây nhấp nháy.
- [x] Event schedule dùng seed của spawner để tái hiện interval theo phase.
- [x] Cutter Sweep dùng rocket pixel-art bay ngang với exhaust pulse/burst, bám theo hàng của player từ row 1 trong 1.2 giây cảnh báo, sau đó khóa hàng, gây Hazard damage rồi gọi `BlockBoard.ForceClearRow`.

## 6. Kiến trúc và contract dùng chung

### Luồng Tetris hiện tại

```text
SevenBag
  ↓ next piece
TetrominoSpawner
  ↓ spawn
ActiveTetromino
  ↓ move / rotate / lock
BlockBoard
  ↓ occupancy
BoardModel
  ↓ full rows
Row Clear + HUD events
```

Không viết script mới truy cập trực tiếp mảng cell của `BoardModel`. Dùng API của `BlockBoard/BoardModel` hoặc đề xuất thêm method rõ nghĩa kèm test.

Các event hiện có của `BlockBoard`:

- `PieceLocked(TetrominoKind)`
- `RowsCleared(int[])`
- `OverflowChanged(bool, float)`
- `Overflowed()`

Các event hiện có của `TetrominoSpawner`:

- `PieceSpawned(TetrominoKind)`
- `NextPieceChanged(TetrominoKind)`

### Contract sát thương phải dùng chung

Task HEALTH-01 phải tạo đúng contract sau; AI, hazard và block không tự gọi method riêng của player:

```csharp
public interface IDamageable
{
    bool TakeDamage(DamageInfo damage);
}

public readonly struct DamageInfo
{
    public int Amount { get; }
    public Vector2 Knockback { get; }
    public GameObject Source { get; }
    public DamageType Type { get; }
}
```

`DamageType`: `Impact`, `Enemy`, `Hazard`, `Crush`. `TakeDamage` trả về `true` nếu damage thực sự được nhận; trả về `false` trong iFrame hoặc khi đã chết.

### Contract Game Session

Chỉ `GameSession` được chuyển trạng thái toàn game:

```text
Countdown → Playing ↔ Paused → GameOver
```

- Tetris, AI, event và score chỉ chạy khi state là `Playing`.
- UI gửi yêu cầu Pause/Restart; bootstrap đồng bộ `GameSession`, input map và `Time.timeScale` tại một chỗ.
- `GameSession` phát event `StateChanged`, `PhaseChanged` và `RunEnded`.

### Layer và collision matrix mục tiêu

| Layer | Va chạm cần thiết |
|---|---|
| Player | World, FallingBlock, Enemy, Hazard, Pickup |
| World | Player, FallingBlock |
| FallingBlock | World, Player, Enemy |
| Enemy | Player, FallingBlock |
| Hazard | Player |
| Pickup | Player, dùng trigger |
| Sensor | World, dùng query/trigger, không tạo lực |

Người tích hợp thêm layer còn thiếu một lần trong `TagManager`, sau đó cả nhóm dùng layer theo tên; không dùng số layer viết trực tiếp trong code.

## 7. Backlog triển khai chi tiết

Các task phải được làm theo dependency. Không bắt đầu task khi dependency chưa `Hoàn thành`, trừ khi làm trong sandbox scene và không cần contract chưa có.

### TETRIS-01 — Kiểm thử và khóa Tetris Core

- **Dependency:** Không
- **Branch:** `test/tetris-stability`
- **File chính:** `Scripts/Gameplay`, `Tests/EditMode`

Các bước:

1. Chạy liên tục ít nhất 50 tetromino với seed cố định.
2. Kiểm tra preview luôn trùng piece tiếp theo.
3. Thử giữ `A/D`, đổi hướng nhanh, xoay sát hai tường và xoay trên block.
4. Kiểm tra ghost piece cập nhật đúng sau mỗi lần di chuyển và xoay.
5. Soft drop bằng `S` đến khi chạm block; không được xuyên hoặc khóa hai lần.
6. Tạo tình huống xóa 1, 2, 3 và 4 hàng.
7. Để block vượt danger row và kiểm tra overflow đúng ba giây.
8. Reset khi đang rơi, đang clear hàng và sau overflow.
9. Bổ sung test cho bug tìm được trước khi sửa nếu có thể tái hiện bằng `BoardModel`.

Nghiệm thu:

- [ ] Không có exception, MissingComponent hoặc soft-lock sau 50 piece.
- [ ] Mỗi lần bấm/giữ phím chỉ thay đổi vị trí theo cell hợp lệ.
- [ ] Preview đúng 100%.
- [ ] Ghost luôn chỉ đúng vị trí khóa cuối cùng và không có collider.
- [ ] Row clear và compaction không sai occupancy.
- [x] EditMode Test Runner 84/84 xanh trên code hiện tại.

### INPUT-01 — Chuẩn hóa Input System

- **Dependency:** TETRIS-01
- **Branch:** `feature/input-actions`
- **File chính:** `Assets/InputSystem_Actions.inputactions`, `Scripts/Core/InputService.cs`

Mục tiêu: mọi gameplay mới dùng Input System thống nhất, không để mỗi script tự đọc phím theo cách khác.

Action Map cần tạo:

| Map | Action | Binding mặc định |
|---|---|---|
| Tetris | Move | 1D Axis: `A/D` |
| Tetris | Rotate | `W` |
| Tetris | SoftDrop | `S` |
| Player | Move | 1D Axis: Left/Right Arrow |
| Player | Jump | Up Arrow |
| Player | Crouch | Down Arrow |
| System | Pause | Escape |
| System | ResetRun | `R` |

Các bước:

1. Tạo ba Action Map trong asset hiện tại.
2. Tạo `InputService` sở hữu action asset và bật/tắt map theo game state.
3. Chuyển `ActiveTetromino` từ `Keyboard.current` sang action của `InputService` nhưng giữ nguyên hành vi repeat.
4. Chỉ bật map Tetris và Player khi game `Playing`; map System luôn bật.
5. Chưa làm UI rebind trong task này, nhưng không hard-code phím ở script mới.

Trạng thái hiện tại: toàn bộ năm bước đã được triển khai; ngày 22/06/2026 đã bổ sung test cho `InputService` và chạy EditMode Test Runner 15/15 pass. Cần playtest trong scene để xác nhận cảm giác điều khiển và kiểm tra duplicate service khi đổi scene trước khi đổi task thành hoàn thành.

Nghiệm thu:

- [x] WASD vẫn điều khiển Tetris như trước.
- [x] Phím mũi tên không tác động tetromino.
- [x] Pause vô hiệu hóa input gameplay.
- [ ] Không có hai `InputService` sau khi đổi scene.

### LEVEL-01 — Tilemap và đấu trường tĩnh

- **Dependency:** TETRIS-01
- **Branch:** `feature/arena-tilemap`
- **Scene làm việc:** tạo `Assets/_Project/Scenes/Sandbox/ArenaSandbox.unity`

Hierarchy prefab mục tiêu:

```text
Arena
├── Grid
│   ├── Ground Tilemap
│   ├── Platform Tilemap
│   ├── Obstacle Tilemap
│   └── Decoration Tilemap
├── Spawn Points
│   ├── Player Spawn
│   ├── Drone Left
│   └── Drone Right
└── Kill Zone
```

Quy tắc:

- Ground/Platform dùng `TilemapCollider2D` kết hợp `CompositeCollider2D` và Static Rigidbody2D.
- Obstacle gây damage dùng trigger riêng, không gắn logic damage vào tile trang trí.
- Không đặt platform tĩnh chắn toàn bộ chiều ngang vùng Tetris vì sẽ phá quy tắc rơi/xóa hàng.
- Board Tetris giữ kích thước 14×20 và origin thống nhất với `BlockBoard`.

Trạng thái hiện tại: đã có builder `BlockEscapeArenaSetup`, prefab `Arena`, scene `ArenaSandbox`, tile assets và EditMode test kiểm tra cấu trúc/collider/support dưới Player Spawn. Cần mở Play Mode để xác nhận player placeholder đứng không rung và falling block tương tác vật lý đúng với terrain tĩnh.

Nghiệm thu:

- [ ] Player placeholder có thể đứng trên ground/platform mà không rung.
- [ ] Falling block không xuyên terrain tĩnh.
- [x] Decoration không có collider thừa.
- [x] Prefab Arena hoạt động độc lập trong sandbox.

### PLAYER-01 — Di chuyển và nhảy

- **Dependency:** INPUT-01, LEVEL-01
- **Branch:** `feature/player-controller`
- **File tạo:** `Scripts/Player/PlayerController.cs`, `PlayerConfig.cs`
- **Prefab:** `Prefabs/Player/Player.prefab`

Component prefab:

```text
Player
├── Rigidbody2D (Dynamic, Freeze Rotation Z)
├── CapsuleCollider2D hoặc BoxCollider2D
├── PlayerController
├── PlayerHealth (thêm ở HEALTH-01)
├── PlayerPresentation
├── Visual
│   └── SpriteRenderer
└── Ground Check
```

Thông số mặc định trong `PlayerConfig`:

- Move speed: `7 units/s`.
- Jump velocity: `11`.
- Coyote time: `0.10s`.
- Jump buffer: `0.12s`.
- Max fall speed: `18 units/s`.
- Variable jump: nhả Up sớm sẽ giảm vận tốc Y dương còn 50%.

Quy tắc implementation:

- Đọc input trong `Update`, áp vận tốc Rigidbody trong `FixedUpdate`.
- Ground check dùng `Physics2D.OverlapBox` hoặc cast với layer `World`; không dựa vào tag.
- Không gán `transform.position` mỗi frame cho Dynamic Rigidbody2D.
- Player không điều khiển được khi GameSession không ở `Playing`.

Trạng thái hiện tại: đã có `PlayerConfig`, `PlayerController`, prefab `Player`, player thật trong `ArenaSandbox`, `TetrisDemoBootstrap` tự spawn player khi Play nếu scene thiếu player và gravity được chỉnh qua config. Cần playtest cảm giác di chuyển, coyote time, jump buffer và tương tác trên block tĩnh.

Nghiệm thu:

- [ ] Left/Right Arrow di chuyển, Up Arrow nhảy.
- [ ] Coyote time và jump buffer hoạt động.
- [ ] Không nhảy vô hạn hoặc dính tường.
- [ ] Đứng ổn định trên static block cell.
- [ ] Không thay đổi input WASD của Tetris.

### PLAYER-02 — Cúi người

- **Dependency:** PLAYER-01
- **Branch:** `feature/player-crouch`

Các bước:

1. Down Arrow chuyển sang crouch.
2. Lưu size/offset collider đứng và crouch trong `PlayerConfig`.
3. Trước khi đứng, dùng overlap/cast kiểm tra vùng đầu với layer `World`.
4. Nếu bị chặn, giữ crouch dù người chơi đã nhả phím.
5. `PlayerPresentation` đọc trạng thái crouch để đổi pose nhưng vẫn giữ chân sprite sát mặt đất.

Trạng thái hiện tại: đã triển khai trong `PlayerController`, cập nhật `PlayerConfig.asset`, prefab vẫn dùng collider đứng mặc định và test xác nhận default crouch config. Cần Play Mode để kiểm tra cảm giác cúi, headroom với block thấp và hazard cao ngang đầu.

Nghiệm thu:

- [x] Collider đổi đúng và không lún sàn.
- [x] Không đứng xuyên block thấp.
- [x] Crouch có tác dụng né hazard cao ngang đầu.

### HEALTH-01 — Máu, damage, knockback và iFrame

- **Dependency:** PLAYER-01
- **Branch:** `feature/player-health`
- **File tạo:** `Core/DamageInfo.cs`, `Core/IDamageable.cs`, `Player/PlayerHealth.cs`

Thông số:

- Max HP: `3`.
- iFrame: `1.2s`.
- Damage thông thường: `1 HP`.
- Trong iFrame, `TakeDamage` trả về false và không cộng dồn knockback.
- Khi HP về 0, phát event `Died`; không tự load scene trong `PlayerHealth`.

Visual iFrame: nhấp nháy SpriteRenderer mỗi `0.1s`; khi kết thúc phải trả alpha về 1.

Trạng thái hiện tại: đã có contract `DamageInfo`/`DamageType`/`IDamageable`, `PlayerHealth`, hook trên prefab và test logic cho damage, knockback, iFrame và death. Cần tích hợp với hazard/AI/block trong các task sau.

Nghiệm thu:

- [x] Damage liên tục chỉ trừ máu một lần trong 1.2 giây.
- [x] Knockback dùng Rigidbody2D, không teleport.
- [x] HP không nhỏ hơn 0 hoặc lớn hơn max.
- [x] Death chỉ phát một lần.

### INTEGRATION-01 — Block tương tác với player

- **Dependency:** PLAYER-02, HEALTH-01, TETRIS-01
- **Branch:** `feature/player-block-interaction`

Quy tắc:

- Falling tetromino chạm player: `1 Impact damage` và knockback.
- Khi piece sắp lock, kiểm tra bounds của bốn cell với player collider.
- Nếu overlap, tìm ô thoát theo thứ tự: trái, phải, trên, xa hơn tối đa hai cell.
- Chỉ teleport player trong tình huống giải kẹt lúc lock; mọi knockback khác dùng physics.
- Không có ô thoát: gửi `Crush` damage kết thúc lượt chơi.

Nghiệm thu:

- [ ] Player không bị kẹt vĩnh viễn bên trong locked block.
- [ ] Không nhận damage nhiều lần trong cùng một piece nhờ iFrame.
- [ ] Row clear khi player đứng phía trên không làm player xuyên block.

### SESSION-01 — Game state, score và difficulty phase

- **Dependency:** INPUT-01, HEALTH-01
- **Countdown đã triển khai tại:** `feature/pickups-powerups`
- **Branch tiếp theo:** `feature/session-pause-flow`
- **File hiện tại:** `Core/InputService.cs` (`GameSession`, `ScoreService`), `Gameplay/TetrisBalanceConfig.cs`

State:

```text
Countdown (3s) → Playing ↔ Paused → GameOver
```

Score thống nhất:

| Hành động | Điểm |
|---|---:|
| Sống sót mỗi giây | 10 |
| Xóa 1 hàng | 250 |
| Xóa 2 hàng cùng lúc | 600 |
| Xóa 3 hàng cùng lúc | 1000 |
| Xóa 4 hàng cùng lúc | 1500 |
| Score Crystal | 100 |
| Block phá hủy drone | 300 |

Near miss +50 được tính một lần khi piece khóa trong phạm vi một cell quanh player mà piece đó không gây damage/crush.

Difficulty:

| Phase | Thời gian | Tốc độ rơi piece mới | Drone/Event |
|---|---:|---:|---|
| 1 | 0–<45s | 2 cell/s | Bật từ phase 1 |
| 2 | 45–<90s | 2.35 cell/s | Theo config runtime |
| 3 | 90–<135s | 2.7 cell/s | Theo config runtime |
| 4+ | Mỗi 45s | +0.35 cell/s, tối đa 5 | Theo config runtime |

Spawner hiện dùng initial delay `0.5s` và delay giữa hai piece `0.6s`.

Nghiệm thu:

- [x] Countdown 3 giây khóa player/Tetris/drone/event/pickup và chạy lại sạch khi reset.
- [x] Restart/resume không tạo spawn coroutine hoặc active tetromino trùng lặp.
- [ ] Pause chỉ được quản lý tại GameSession.
- [x] Timer/score không tăng khi Pause hoặc GameOver.
- [x] Điểm xóa hàng chỉ được cộng khi session đang `Playing`.
- [x] HP 0 hoặc overflow đều tạo `RunResult` và GameOver.
- [x] Phase đổi đúng mốc và cập nhật config của spawner.
- [x] Phase cập nhật drone và event director khi AI/Event được triển khai.

### AI-01 — Drone AI

- **Dependency:** HEALTH-01, SESSION-01, LEVEL-01
- **Branch:** `feature/drone-ai`
- **File tạo:** `AI/DroneController.cs`, `AI/DroneConfig.cs`
- **Prefab:** `Prefabs/Enemies/Drone.prefab`

State của drone gốc:

```text
Disabled → Patrol
```

Thông số ban đầu:

- Chỉ một drone gốc hoạt động và luôn nằm trên vạch đỏ.
- Drone gốc bám player theo trục X, bắn laser xuống và không nhận damage từ FallingBlock.
- Mỗi `6s` tạo clone; clone telegraph `0.6s`, dash về vị trí player với tốc độ `13` trong tối đa `1.2s` rồi biến mất.
- Clone dash gây `1 Enemy damage` và knockback.

Nghiệm thu:

- [x] Mỗi state có entry/exit rõ ràng, không xử lý tất cả bằng nhiều bool rời rạc.
- [x] Warning cho người chơi đủ thời gian phản ứng.
- [x] Drone dừng hoàn toàn khi Pause/GameOver.
- [x] Không có hơn một drone.

### EVENT-01 — Event Director, Cutter và Overdrive

- **Dependency:** SESSION-01, AI-01
- **Branch:** `feature/dynamic-events`

`EventDirector` là nơi duy nhất lập lịch event. Event không tự sinh ngẫu nhiên độc lập.

`Cutter Sweep`:

1. Chọn hàng có block, không chọn hàng ground.
2. Hiện warning `1.2s`.
3. Cutter bay ngang, gây `1 Hazard damage` nếu chạm player.
4. Gọi API của `BlockBoard` để clear hàng; không tự sửa mảng BoardModel.

`Block Overdrive`:

1. Đánh dấu ba tetromino tiếp theo.
2. Tăng tốc độ rơi theo multiplier trong config.
3. Đổi màu visual để người chơi nhận biết.
4. Sau ba piece phải trả đúng tốc độ phase hiện tại.

Event không chạy khi row clear, Countdown, Pause hoặc GameOver.

Nghiệm thu:

- [x] Hai event không chạy đồng thời.
- [x] Seed cố định tái hiện được thứ tự event.
- [x] Không làm hỏng next-piece preview hoặc difficulty speed.

### ITEM-01 — Pickup và power-up

- **Dependency:** HEALTH-01, SESSION-01, LEVEL-01
- **Branch triển khai:** `feature/pickups-powerups`

Ba loại:

| Item | Hiệu ứng |
|---|---|
| Score Crystal | +100 điểm |
| Health Pack | +1 HP, không vượt max |
| Jump Boost | +20% jump velocity trong 8 giây |

Quy tắc spawn:

- Tối đa hai pickup tồn tại.
- Pickup đầu xuất hiện ngẫu nhiên sau `12–18s`; các lần sau cách nhau `18–28s` khi pool chưa đầy.
- Trọng số mặc định: Score Crystal `45`, Jump Boost `35`, Health Pack `20`.
- Spawn từ mép trên và rơi xuống mặt block/platform cao nhất có hai ô trống phía trên.
- Pickup dùng Kinematic Rigidbody2D; nếu mặt đỡ bị phá thì retarget xuống mặt đỡ mới hoặc floor.
- Pickup tồn tại `10–15s` sau khi chạm đất.
- Health Pack không spawn khi player đầy HP và chỉ biến mất nếu `TryHeal(1)` thành công.
- Pickup dùng trigger layer `Pickup`; effect chỉ áp dụng một lần rồi trả object về pool.

Nghiệm thu:

- [x] Item không spawn trong block hoặc ngoài arena.
- [x] Timer Jump Boost refresh rõ ràng nếu nhặt thêm, không nhân multiplier nhiều lần.
- [x] Item biến mất đúng một lần khi nhặt; Health Pack đầy máu vẫn còn.

### UI-01 — HUD, Pause và Game Over

- **Dependency:** SESSION-01, HEALTH-01
- **Branch:** `feature/gameplay-ui`

HUD hiển thị:

- HP hiện tại/max.
- Score.
- Survival time định dạng `mm:ss`.
- Phase và thời gian đến phase kế tiếp.
- Next tetromino.
- Icon/timer power-up đang hoạt động.

- Pause menu: Resume, Restart, Options, Main Menu.
- Game Over: score, survival time, phase, nguyên nhân chết, Restart, Main Menu.

UI subscribe event; không đọc toàn bộ gameplay state trong `Update`. Chỉ timer có thể cập nhật mỗi frame từ một presenter.

Nghiệm thu:

- [ ] UI đúng ở 1280×720 và 1920×1080.
- [ ] Không click xuyên panel.
- [ ] Pause menu vẫn nhận input khi `Time.timeScale = 0`.
- [ ] Restart không tạo duplicate service/event subscription.

### SYSTEM-01 — Bootstrap, Main Menu, Options và Save

- **Dependency:** SESSION-01, UI-01, INPUT-01
- **Branch:** `feature/app-flow`
- **Hiện có:** Bootstrap builder → MainMenu → TetrisDemo, Options, first-run tutorial, JSON save v1, high score/best time, display/audio settings và input rebind persistence.
- **Còn thiếu:** Play Mode QA sau khi Unity license hoạt động lại.

Scene mục tiêu:

1. `Bootstrap`: tạo `AppServices`, sau đó load MainMenu.
2. `MainMenu`: Start, Options, High Score, Best Time, Exit.
3. `Gameplay`: arena, Tetris, player, AI, HUD.

Options hiện có:

- Master/Music/SFX volume.
- Fullscreen, resolution, VSync.

Options input:

- [x] Rebind Move Left/Right, Jump, Crouch và Tetris actions.
- [x] Reset binding mặc định.

JSON save version 1 hiện có:

- `highScore`.
- `bestSurvivalTime`.
- Audio/display settings.

Save hiện lưu thêm input binding override JSON và `hasSeenTutorial`.

Nếu file thiếu/hỏng: ghi warning, dùng default, không crash. Chỉ SaveService đọc/ghi file.

Nghiệm thu:

- [ ] Mở game đi qua Bootstrap → MainMenu → Gameplay.
- [x] High score/best time chỉ cập nhật khi tốt hơn.
- [x] Audio/display settings còn sau khi mở lại game.
- [x] Input rebind được serialize/restore qua SaveService; còn xác nhận thủ công sau khi mở lại build.
- [x] Save thiếu/hỏng dùng default và giữ file corrupt để chẩn đoán.
- [x] Exit chỉ gọi `Application.Quit` trong build.

### ART-01 — Art, animation, VFX và audio

- **Dependency:** PLAYER-02, AI-01, UI-01
- **Branch:** `feature/presentation`

Quy chuẩn:

- PPU 16, Filter Mode Point, Compression None cho pixel sprite quan trọng.
- Palette sáng, mỗi tetromino giữ màu hiện tại để dễ nhận biết.
- `PlayerPresentation` tạo phản hồi Idle, Run, Jump/Fall, Crouch, Hurt và Death bằng code; không gắn Animator rỗng.
- VFX không chứa gameplay collider.
- Audio chia Music/SFX; không phát AudioSource mới mỗi frame.
- BGM_02 loop ở Main Menu; BGM_01/03/04 random trong gameplay và fade khi chuyển. SFX hiện có: UI click, pickup, hồi máu, player damage, laser lúc drone bắn, rocket Cutter, meteor và Game Over; còn thiếu row clear, block move/rotate/lock và Near Miss.
- Asset ngoài phải ghi nguồn và license trong README.

Nghiệm thu:

- [x] Builder ép pixel sprite quan trọng dùng PPU 16, Point và Uncompressed.
- [ ] Kiểm tra presentation transition trong Play Mode.
- [x] Có feedback rõ cho damage, row clear, warning và Game Over.

### QA-01 — Test, build và demo

- **Dependency:** Tất cả module P0
- **Branch:** `test/final-qa`

Automated tests tối thiểu:

- Board bounds, lock, 1–4 row clear, compaction, overflow.
- 7-bag tạo đủ bảy loại trước khi lặp.
- iFrame chặn damage liên tục.
- HP không vượt max.
- Pause dừng gameplay.
- Save fallback khi JSON hỏng.
- Drone chuyển state đúng.
- Pause không cộng survival time.
- Input override lưu/restore/reset đúng.
- Near Miss chỉ raise khi piece được đánh dấu hợp lệ.
- Crouch né Cutter damage.

Manual test:

- Chơi liên tục 15 phút không exception/soft-lock.
- Test 1280×720 và 1920×1080.
- Test restart 10 lần và đổi scene 10 lần.
- Build Windows chạy trên máy không cài Unity.
- Console không có error trước khi quay demo.

## 8. Thứ tự merge đề xuất

```text
TETRIS-01
   ├── INPUT-01 ── PLAYER-01 ── PLAYER-02 ── HEALTH-01
   └── LEVEL-01 ─────────────────────┘
                         ↓
                  INTEGRATION-01
                         ↓
                    SESSION-01
                 ┌───────┼────────┐
               AI-01   ITEM-01   UI-01
                 ↓                 ↓
              EVENT-01         SYSTEM-01
                 └───────┬─────────┘
                       ART-01
                         ↓
                       QA-01
```

Branch phụ thuộc phải rebase/merge `main` mới nhất sau khi dependency được merge, rồi mới tiếp tục tích hợp.

## 9. Checklist trước khi tạo Pull Request

- [ ] Pull/merge `main` mới nhất vào branch và giải quyết conflict.
- [ ] Chỉ có file thuộc task; không có thay đổi ngẫu nhiên từ Unity.
- [ ] Không commit `Library`, `Temp`, `Logs`, build hoặc file IDE.
- [ ] Không có Console error, Missing Script hoặc Missing Reference.
- [ ] Đã test scene sandbox và scene tích hợp liên quan.
- [ ] Đã thêm/sửa test cho logic quan trọng.
- [ ] Đã cập nhật `PROGRESS.md` nếu task thay đổi trạng thái hoặc contract.
- [ ] Commit message dùng đúng prefix `feat/fix/refactor/test/docs/chore`.
- [ ] PR mô tả: đã làm gì, cách test, ảnh/video nếu là UI/gameplay.
- [ ] Có ít nhất một thành viên khác review trước khi merge.

## 10. Điều kiện hoàn thành cột mốc hiện tại

Tetris Core chỉ chuyển từ 90% thành 100% khi:

- [ ] 50 tetromino spawn/lock không exception hoặc soft-lock.
- [ ] Preview luôn trùng piece tiếp theo.
- [ ] Ghost piece cập nhật đúng khi di chuyển, xoay và soft drop.
- [ ] Mỗi bước rơi đúng một cell.
- [ ] `A/D/W/S` không cho đi xuyên board hoặc locked block.
- [ ] Xóa đúng 1–4 hàng và compaction đúng.
- [ ] Overflow dừng spawn; reset tạo run sạch.
- [x] EditMode tests 84/84 xanh toàn bộ trên code hiện tại.
- [ ] Hai thành viên đã playtest độc lập.

## 11. Ghi chú và giới hạn hiện tại

- `TetrisDemo` là scene demo được Editor builder tạo lại; không dùng làm scene final.
- HUD dùng Screen Space Overlay nên chủ yếu hiện trong Game View.
- Game hiện mới có Tetris, chưa có player platform.
- `ActiveTetromino` và Pause/Reset đều đọc action từ `InputService`; script gameplay mới không được đọc phím trực tiếp.
- BoardModel là nguồn dữ liệu thật; SpriteRenderer/Collider chỉ là phần hiển thị vật lý.
- Các giá trị phase/player trong tài liệu là default đã thống nhất nhưng vẫn có thể cân bằng qua ScriptableObject sau playtest.

## 12. Nhật ký tiến trình

| Ngày | Task | Thay đổi | Kết quả |
|---|---|---|---|
| 19/06/2026 | Khởi tạo | Lập kế hoạch và kiểm tra project | Thống nhất phạm vi và kiến trúc |
| 19/06/2026 | Tetris Core | BoardModel, tetromino và 7-bag | Quy tắc lưới hoạt động |
| 19/06/2026 | Tetris Core | Rơi, lock, row clear và overflow | Vòng lặp Tetris hoạt động |
| 19/06/2026 | Demo | HUD và scene Hierarchy | Dễ kiểm tra và trình bày |
| 20/06/2026 | Tetris Input | Preview, rơi từng ô và WASD | Player điều khiển tetromino |
| 20/06/2026 | Bugfix | Sửa renderer và nháy tại tâm | Runtime ổn định hơn |
| 20/06/2026 | Documentation | README và PROGRESS tại root | Team theo dõi trên GitHub |
| 20/06/2026 | Documentation | Chi tiết hóa task và contract | Thống nhất cách triển khai giữa thành viên |
| 20/06/2026 | Tetris UX | Thêm ghost piece tại vị trí rơi | Người chơi nhìn trước vị trí khóa block |
| 20/06/2026 | Bugfix | Tách ghost khỏi transform của Rigidbody và giảm alpha | Ghost không còn giật khi giữ soft drop |
| 20/06/2026 | Tetris UI | Thêm Pause Menu và xác nhận reset | Tránh reset nhầm và có thể tiếp tục bằng nút/ESC |
| 20/06/2026 | Input System | Tạo ba action map và `InputService` persistent | Tetris, player và system dùng binding tách biệt |
| 20/06/2026 | Tetris UI | Thêm thống kê lượt chơi trong Pause Menu | Dễ theo dõi và trình bày khi demo |
| 20/06/2026 | Bugfix UI | Khôi phục reference từ scene cũ trước khi tạo HUD fallback | Không còn HUD/Status chồng lên nhau |
| 20/06/2026 | Scene Flow | Thêm Main Menu và Game Over summary | Có vòng lặp Start → Gameplay → Kết quả → Restart/Menu |
| 20/06/2026 | Bugfix UI Input | Thay `AssignDefaultActions` bằng UI action asset riêng | Builder tạo liên tiếp hai scene không còn exception |
| 20/06/2026 | Pause Flow | Thêm xác nhận trước khi trở về Main Menu | Tránh người chơi làm mất lượt do bấm nhầm |
| 22/06/2026 | Tetris/Input stability | Bổ sung test `InputService`, làm sạch warning preview và chạy EditMode Test Runner | 15/15 test pass, build 0 warning/0 error |
| 23/06/2026 | LEVEL-01 | Tạo Arena prefab, ArenaSandbox, tile assets và test cấu trúc/collider/support | EditMode Test Runner 19/19 pass |
| 23/06/2026 | PLAYER-01 | Tạo PlayerConfig, PlayerController, Player prefab và test cấu trúc | EditMode Test Runner 21/21 pass |
| 23/06/2026 | PLAYER-02/HEALTH-01 | Thêm crouch collider/headroom, damage contract, PlayerHealth, prefab hook và test logic | Chưa chạy lại Test Runner do Unity Licensing local lỗi kết nối |
| 23/06/2026 | PLAYER sandbox | Thay placeholder bằng `Player.prefab`, thêm `InputService` vào ArenaSandbox và test scene có player thật | Sẵn sàng mở `Assets/_Project/Scenes/Sandbox/ArenaSandbox.unity` để playtest |
| 23/06/2026 | PLAYER demo hook | TetrisDemo builder tạo platform test và instantiate `Player.prefab` cạnh board | Đã có hook khi rebuild scene |
| 23/06/2026 | PLAYER visible in demo | TetrisDemo runtime tự tạo player/platform nếu scene thiếu player | Bấm Play trong TetrisDemo là thấy nhân vật cạnh board |
| 23/06/2026 | PLAYER crush game over | Locked block overlap player sẽ phát `PlayerCrushed` và mở Game Over | Có EditMode test cho crush detection |
| 23/06/2026 | Falling block collision | Đổi collider của active tetromino từ trigger sang solid để chặn player khi đang rơi | Có EditMode test khóa collider không trigger |
| 29/06/2026 | Block-player collision bugfix | Thêm frictionless physics material, clamp player trong arena và active falling crush event | Sửa lỗi bám tường/văng khỏi đấu trường khi block đè; đã thêm EditMode test logic |
| 29/06/2026 | Crush escape rule | Chỉ Game Over khi player bị block đè mà không còn đường thoát ngang; nhảy đụng block chỉ bị chặn | Đã thêm EditMode test cho side escape và jump contact |
| 29/06/2026 | Soft drop collision guard | Falling block pre-check vị trí kế tiếp trước khi move/rotate vào player | Chặn lỗi soft drop xuyên qua player hoặc đẩy player văng ngang |
| 29/06/2026 | Jump bounce under block | Bounce player xuống khi nhảy vào block đang rơi, và chỉ crush sau khi block đã xuống | Tránh block bị đứng yên hoặc Game Over quá sớm |
| 29/06/2026 | Game Session scoring | Thêm `GameSession`/`ScoreService`, survival score, row score và nối HUD/Pause/Game Over | Score và thời gian sống sót thống nhất trong TetrisDemo |
| 29/06/2026 | Player gravity + session phase | Đưa gravity vào `PlayerConfig`, thêm HP death Game Over và phase tăng tốc piece mới | Build runtime/test/editor xanh, session sẵn sàng hơn cho AI/Event/Pickup |
| 29/06/2026 | Gravity/hearts/block fall bugfix | Tăng gravity lên 4, đổi HUD HP sang 3 tim và cho block tiếp tục rơi khi player còn đường thoát | Sửa lỗi block đứng giữa không trung cạnh/dưới player |
| 29/06/2026 | Crush life respawn | Crush trừ 1 tim và respawn player cao hơn block đã khóa cao nhất 5Y nếu còn tim | Chỉ hết 3 tim mới Game Over do hết máu |
| 29/06/2026 | Respawn safety + iFrame | Respawn tìm điểm trống quanh giữa arena và bật bất tử 3 giây có nhấp nháy | Tránh hồi sinh kẹt trong block và tránh mất tim liên tục |
| 30/06/2026 | Respawn hover + gravity 5 | Tăng gravity lên 5 và giữ player lơ lửng 0.75 giây sau respawn crush | Respawn dễ nhìn hơn, tránh vừa hồi sinh đã rơi ngay vào cụm block |
| 30/06/2026 | Side climb release + jump tune | Tăng `jumpVelocity` lên 12.5 và tách player ngang khi leo/chạm cạnh falling block | Tránh bị block đang rơi kéo xuống, nhảy cao hơn để leo block dễ hơn |
| 30/06/2026 | Player gravity tune | Giữ `jumpVelocity = 12.5`, hạ `gravityScale` xuống 2 | Player rơi nhẹ hơn theo yêu cầu playtest |
| 30/06/2026 | Drone AI + Event Director | Thêm runtime drone phase 2, Enemy damage và Block Overdrive event | AI/Event có lát cắt playable đầu tiên trong TetrisDemo |
| 30/06/2026 | Player gravity retune | Giữ `jumpVelocity = 12.5`, chỉnh `gravityScale` lên 3 | Player rơi nhanh hơn bản gravity 2 nhưng vẫn nhẹ hơn bản 5 |
| 30/06/2026 | Early events + drone bullets | Drone/event bật từ phase 1, event interval ngắn hơn, drone bắn đạn xuống và đạn nổ nhỏ khi chạm block/world | Người chơi thấy event sớm hơn và drone có hazard rõ ràng hơn |
| 30/06/2026 | Player unstuck safety | TetrisDemo tự dò điểm trống khi player bị kẹt collider trong block/world | Giảm lỗi nhân vật kẹt trong cụm block như ảnh playtest |
| 30/06/2026 | Meteor event + locked block side fix | Thêm Meteor Shower có cảnh báo đường bay, thiên thạch bay chéo từ hai cạnh nửa trên arena, phá block bán kính 2 sau khi nhấp nháy 0.5s; chỉnh unstuck để đẩy ngang khi player chèn cạnh block đã khóa | Event nhìn rõ hơn, tác động được địa hình và tránh vòng lặp văng khi cố tình đu vào block khóa |
| 30/06/2026 | Meteor path + block seam tune | Giảm tốc meteor, cho bay từ sát mép trong trái/phải nửa trên arena qua toàn chiều ngang; bù collider block để bề mặt liền hơn | Meteor dễ quan sát hơn và player đi qua khe giữa block ít bị nhảy |
| 30/06/2026 | Falling block bounce priority | Nếu player đã bị bounce xuống khi nhảy vào block đang rơi thì bỏ qua side-release ngang trong cùng frame; falling block collider giảm nhẹ so với locked block | Tránh trường hợp đáng lẽ bật xuống nhưng bị hất ngang/văng khỏi arena |
| 30/06/2026 | Meteor stack-aware targeting | Meteor spawn từ sát mép trong hai vách nửa trên, chọn X rơi ngẫu nhiên trong arena và Y rơi theo block đã khóa cao nhất; đường bay ép đủ xa nếu target quá gần | Tránh đập vào tường bên và tạo đường bay bất ngờ hơn |
| 30/06/2026 | Meteor contact explosion + crush priority | Kéo warning meteor tới dưới sàn và chỉ nổ khi chạm player/block/sàn; falling block rơi từ trên vào player đứng yên sẽ crush thay vì side-release ngang | Tránh meteor nổ trên không và tránh player bị đẩy văng khi đáng lẽ bị đè |
| 30/06/2026 | Falling block actual-contact crush | Pre-check của falling block không raise crush nữa; block phải apply vào vị trí thật rồi actual contact mới crush, còn player nhảy lên sẽ bounce và bỏ qua crush frame đó | Tránh chết sớm khi block chưa rơi tới và khôi phục cảm giác nhảy chạm block bị bật xuống |
| 30/06/2026 | Falling block crush probe tune | Tách solid collider dùng để chặn/bounce khỏi vùng kiểm tra crush nhỏ hơn và yêu cầu tiếp xúc Y thật với đáy block | Tránh chết khi block nhìn như chưa rơi hết nhưng collider phụ đã overlap |
| 30/06/2026 | Meteor radius + side-release clamp | Giảm bán kính phá block của meteor xuống 2 cell; side-release chỉ chạy khi player đang đi lên, block rơi nhanh sẽ kiểm crush thật thay vì đẩy ngang player đang đứng | Giảm vùng tác động meteor và giảm lỗi player bị hất khỏi arena |
| 30/06/2026 | Jump-to-block bounce window | PlayerController lưu short jump window để falling block vẫn bounce khi physics đã kéo velocity Y về gần 0 lúc chạm collider | Tránh player vừa nhảy vào block đang rơi nhưng bị tính chết thay vì bật xuống |
| 30/06/2026 | Grounded step jitter clamp | PlayerController triệt tiêu vận tốc Y nhỏ khi đang grounded và không chủ động jump | Giảm hiện tượng nảy khi bước từ block cao xuống block thấp |
| 30/06/2026 | Falling block downward-only deflect | Bỏ side-release của falling block; overlap không-crush chỉ đặt player xuống dưới/velocity Y âm, còn crush chỉ xảy ra khi probe nhỏ xác nhận bị kẹp và hết đường thoát | Tránh văng ngang khỏi arena nhưng vẫn giữ luật không chạy kịp thì mất tim |
| 30/06/2026 | Falling block trigger sensor | Đổi active falling block sang trigger sensor và bootstrap unstuck bỏ qua `FallingBlock` overlap | Chặn nguồn physics/unstuck tự đẩy player ra hai bên; falling block vẫn xử lý bounce/crush bằng overlap logic |
| 19/07/2026 | ITEM-01/EVENT-01 | Thêm pickup/power-up, Cutter bám hàng player, Overdrive feedback và Meteor Shower hoàn chỉnh | Gameplay tích hợp có đủ ba pickup và ba dynamic event để playtest |
| 19/07/2026 | SESSION-01 | Thêm countdown 3 giây trước mỗi run và khóa gameplay tới khi chuyển sang Playing | Reset/restart chạy lại countdown sạch |
| 19/07/2026 | Spawn regression | Dừng spawn coroutine cũ, làm `StartSpawning` idempotent và dọn active piece mồ côi khi restart | Không còn nhiều tetromino rơi đồng thời hoặc spawn trong countdown |
| 20/07/2026 | SESSION-01/Reset lifecycle | Chặn điểm xóa hàng ngoài `Playing`; `ResetBoard` dừng toàn bộ coroutine row-clear/phá block của run cũ | Build xanh và 3/3 regression test mục tiêu pass |
| 20/07/2026 | TEST/crush lifecycle | Dọn object an toàn trong EditMode, sửa test khởi tạo Player/Drone, hoàn nguyên collision matrix và yêu cầu đường thoát crush liên tục | Build 0 warning/0 error; EditMode Test Runner 60/60 pass |
| 20/07/2026 | ITEM-01/HEALTH-01 | Cân bằng nhịp spawn/lifetime, thêm weighted pickup, Kinematic Rigidbody2D, retarget khi mất mặt đỡ và `TryHeal` trước khi thu Health Pack | Build 0 warning/0 error; EditMode Test Runner 64/64 pass |
| 20/07/2026 | TEST/ITEM-01/HEALTH-01 | Bổ sung regression test cho pool/capacity/reset/trigger/support của pickup và các trường hợp biên của `TryHeal`, `HealthChanged`, reset/death | Thêm 16 test case; EditMode Test Runner 80/80 pass |
| 20/07/2026 | SYSTEM-01/ART-01/QA-01 | Thêm Bootstrap, tutorial, input rebind persistence, Near Miss, crouch né Cutter, pixel-art/presentation/VFX và builder Windows | Runtime/test/editor 0 warning/0 error; EditMode 84/84 pass; Windows x64 build và smoke startup thành công |
| 20/07/2026 | ITEM-01/ART-01 | Thêm pixel-art riêng cho Score Crystal, Health Pack và Jump Boost; aura pulse, burst đúng màu và tint player khi Jump Boost hoạt động | Có regression test xác nhận ba sprite load được qua Resources |
| 20/07/2026 | AUDIO-01/EVENT-01 | Gắn laser SFX vào thời điểm drone bắn; đổi Cutter thành rocket pixel-art bay ngang với exhaust pulse và burst | Giữ nguyên hitbox/crouch contract, bổ sung regression test cho sprite resource |
| 20/07/2026 | AUDIO-01/direct Play | TetrisDemo tự tạo audio service khi chạy trực tiếp, không còn phụ thuộc phải đi qua Bootstrap/Main Menu | BGM và SFX hoạt động trong cả app flow lẫn Play trực tiếp từ gameplay scene |
| 20/07/2026 | AUDIO-01/lifecycle | MainMenu tự tạo audio service khi Play trực tiếp; gameplay BGM pause/resume theo GameSession và tự bỏ pause khi đổi scene | Không làm câm UI click SFX, không mang trạng thái pause về Main Menu |
| 20/07/2026 | AUDIO-01/Game Over | Dừng gameplay BGM khi GameSession chuyển sang GameOver; Restart và đổi scene tự khôi phục nhạc | Không còn nhạc gameplay chạy phía sau màn hình tổng kết |
| 20/07/2026 | AUDIO-01/SFX | Thêm `rocket.ogg` khi Cutter bắt đầu quét và `player_hurt.mp3` khi PlayerHealth chấp nhận damage | i-frame chặn đòn không phát nhầm hurt SFX; cập nhật asset regression test |
| 20/07/2026 | AUDIO-01/settings | Ba volume slider áp dụng tức thời qua SaveService; Apply lưu, Back/ESC hoàn tác preview | Có regression test cho Master/Music/SFX data và AudioListener volume |
| 20/07/2026 | AUDIO-01/Game Over SFX | Thêm `game_over.mp3` khi GameSession chuyển sang GameOver, đồng thời dừng BGM | SFX dùng source riêng nên vẫn phát trên màn tổng kết |
| 20/07/2026 | UI/Animator polish | Rút HUD gameplay còn Time/Phase/Rows/Score và boost đang chạy; ẩn tên chữ thừa dưới preview, reset status khi event kết thúc và bỏ Animator rỗng khỏi player | Runtime/test/editor build 0 warning/0 error; không còn nguồn gọi `Animator.SetBool` gây spam Console |
| 20/07/2026 | UI menu theme | Đồng bộ Main Menu, tutorial, Pause, Options, Rebind, xác nhận và Game Over theo theme neon/pixel xanh đen | Nút tối có viền cyan, màu cảnh báo cam/tím và hover/pressed rõ hơn |
| 20/07/2026 | Difficulty balance | Rút phase còn 30 giây, tăng fall speed 2.5→6.5, giảm spawn delay còn 0.35 giây và overflow grace còn 1.75 giây | Tăng áp lực từ board trước khi thay đổi Drone, pickup hoặc event |
| 20/07/2026 | Drone tracking/clone dash | Drone gốc bất tử, bám X phía trên vạch đỏ và thả clone mỗi 6 giây; clone telegraph rồi dash về player | Falling block không còn phá drone; giữ dash pressure mà không kéo drone gốc khỏi vị trí bắn |
| 20/07/2026 | TEST/full project | Thêm 5 exhaustive suite cho board/tetromino, core/save/input, player/combat, runtime event/pickup và UI/asset integration; sửa overflow khi `TryHeal(int.MaxValue)` | Thêm 607 test case; EditMode Test Runner 687/687 pass |

## 13. Cách cập nhật file này

Khi bắt đầu task:

1. Điền tên tại cột **Phụ trách**.
2. Đổi trạng thái thành `Đang làm`.
3. Ghi tên branch bên cạnh task nếu khác đề xuất.

Khi tạo PR:

1. Đổi trạng thái thành `Chờ review`.
2. Đánh dấu các tiêu chí nghiệm thu đã đạt.
3. Thêm dòng nhật ký gồm ngày, mã task, thay đổi và kết quả.

Sau khi merge:

1. Người tích hợp đổi trạng thái thành `Hoàn thành`.
2. Cập nhật phần trăm module và tiến độ tổng thể.
3. Ghi dependency tiếp theo đã được mở khóa.
4. Nếu contract thay đổi, cập nhật cả phần kiến trúc trước khi giao task mới.
