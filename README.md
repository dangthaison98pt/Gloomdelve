# Gloomdelve

Platformer roguelike 2D pixel art lấy cảm hứng từ cảm giác chơi của Spelunky 2: hang động sinh ngẫu nhiên theo seed, roi/bom/dây, nhặt ném đồ vật, bẫy và chuỗi tương tác vật lý. Nội dung (tên, nhân vật, hình ảnh, âm thanh, màn chơi) hoàn toàn tự tạo bằng code.

**Mục tiêu:** đi qua 2 khu hang và 1 khu ngoài trời (mỗi khu 3 màn sinh ngẫu nhiên) tới *The Sunken Vault*, lấy **Heartstone** và mang nó về cổng mặt đất.


## Chơi ngay
`Builds/Windows/Gloomdelve.exe` (Windows 64-bit). Cửa sổ 1280×720, F11 bật/tắt toàn màn hình.

## Phiên bản & mở project
- **Unity 6000.3.12f1** (đã cài tại `D:\Unity\6000.3.12f1`), URP 17.3, Input System 1.19 (chế độ *Input System Package (New)*).
- Unity Hub → *Add project from disk* → chọn thư mục `E:\Spelunky 2`.
- Scene khởi đầu: `Assets/Game/Scenes/MainMenu.unity` (Play). Có thể mở thẳng `Assets/Game/Scenes/Game.unity` để chơi ngay một lượt (đối tượng **GameDirector** có mục *Debug*: seed cố định, màn bắt đầu, tutorial, god mode).
- Menu **Gloomdelve** trên thanh menu Editor:
  - *Build Everything* – sinh lại art, âm thanh, tile, prefab, scene, cấu hình URP/Input (giá trị trong các config đã có được giữ nguyên).
  - *Build Windows (Builds/Windows)*.
  - *Tests > Logic Tests*, *Tests > Seed Sweep (120 seeds)* (báo cáo kiểm thử dùng 150 seed qua dòng lệnh `-seeds 150`).

## Điều khiển
| Phím | Hành động |
|---|---|
| ← → | Di chuyển (giữ Shift: đi chậm) |
| Z / Space | Nhảy (giữ lâu nhảy cao hơn) |
| X | Roi · khi cầm đồ: ném (↑+X ném cao) |
| ↓ + X | Nhặt / đặt đồ |
| C | **Dùng trang bị đang chọn**: bom (ném; ↓+C đặt; ↑+C ném cao) hoặc dây (ném lên, chỉ móc vào trần/vách bên cạnh, tự biến mất sau 20 s; ↓+C ở mép: thả dây xuống) |
| Giữ D | **Cửa sổ trang bị**: game dừng, ←/→ chọn, thả D để trang bị (bấm nhanh D: đổi về món vừa dùng trước đó) |
| A hoặc ↑ | Vào cửa / mua đồ |
| ↑ ↓ | Leo thang, dây; giữ khi đứng yên để nhìn lên/xuống |
| ↓ | Cúi, bò; bò ra mép để treo mép |
| ↓ + nhảy | Rơi xuyên sàn gỗ |
| Esc | Tạm dừng (tiếp tục, điều khiển, âm lượng, lượt mới, chơi lại seed, menu, thoát) |

Bám mép là tự động khi rơi sát mép; ↑ hoặc nhảy để trèo, ↓ để buông.

**Tay cầm:** cần trái/D-pad di chuyển · A/✕ nhảy · X/□ roi, nhặt, ném · B/○ dùng trang bị · **giữ R1, gạt trái/phải để chọn trang bị, thả R1 để trang bị** · Y/△ vào cửa, mua · LT/L2 đi chậm · Start/Options tạm dừng.

**Trang bị & giải đố địa hình:** bom, dây và tàu lượn (nhận khi tới khu ngoài trời Sunfall Cliffs: trang bị rồi giữ nút nhảy khi đang rơi để lượn; vực gai rộng 6 ô bắt buộc phải lượn). Mossroot có vách đá cứng cao 4 ô (phải dùng dây), Emberdeep có tường đá vụn bịt đường (phải dùng bom). Mỗi màn có 1–2 chướng ngại như vậy trên đường chính; thiếu đạn thì có kho tiếp tế cạnh lối vào.

## Build
Editor: **Gloomdelve > Build Windows**. Hoặc dòng lệnh:
```
"D:\Unity\6000.3.12f1\Editor\Unity.exe" -batchmode -projectPath "E:\Spelunky 2" -executeMethod Gloomdelve.EditorTools.ProjectBuilder.BuildWindows -logFile Logs\build.log
```
Kết quả: `Builds/Windows/Gloomdelve.exe`.

## Tinh chỉnh
Tất cả nằm trong `Assets/Game/Data`:
- `PlayerConfig` – tốc độ, nhảy, coyote/bộ đệm, bám mép, roi, ném, sát thương rơi, máu/bom/dây khởi đầu.
- `Area1_Mossroot`, `Area2_Emberdeep`, `Area4_Sunfall`, `Area3_Vault` (màn cuối) – kẻ địch, bẫy, mật độ, kho báu, cửa hàng, nhạc, màu nền.
- `GameConfig` – danh sách màn, áp lực thời gian (2:30 cảnh báo / 3:00 hồn ma), giới hạn nổ dây chuyền, sinh màn.
- `ShopConfig` – giá và hiệu ứng đồ bán.
- `RoomTemplates.txt` – mẫu phòng (văn bản, có chú thích ký hiệu). `Levels/Tutorial.txt`, `Levels/FinalVault.txt` – màn làm tay.
- Prefab trong `Assets/Game/Prefabs` (thông số từng quái/bẫy/vật phẩm).

Seed: mỗi lượt chơi tự lấy seed ngẫu nhiên (menu chính không cho chọn seed); muốn chơi lại đúng màn đó thì dùng nút chơi lại seed ở màn kết quả; seed hiện ở menu tạm dừng và màn kết quả; mọi seed được ghi vào `run_log.txt` (thư mục `%USERPROFILE%\AppData\LocalLow\Gloomdelve Team\Gloomdelve`, cùng `settings.json`, `records.json`).

## Tài liệu
`PROJECT_STATUS.md` · `Docs/DESIGN.md` · `Docs/REFERENCE.md` · `Docs/TEST_REPORT.md` · `Docs/Screenshots/`
