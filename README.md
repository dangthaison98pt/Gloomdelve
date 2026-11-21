# Brindlemoor: The Sunstone Hunt

Platformer roguelike 2D pixel art, cảm giác chơi lấy cảm hứng từ Spelunky 2: núi hang sinh ngẫu nhiên theo seed, kiếm/bom/dây, nhặt ném đồ vật, bẫy và chuỗi tương tác vật lý. Nội dung (tên, nhân vật, hình ảnh, âm thanh, màn chơi) hoàn toàn tự tạo bằng code. File .exe và thư mục lưu dùng tên **Brindlemoor**; namespace trong code vẫn là `Gloomdelve`.

**Cốt truyện:** mùa đông không chịu rời làng Brindlemoor. Brann lên núi tìm **Sunstone** (đá mặt trời) mang về sưởi ấm làng.

**Một lượt chơi:** thức dậy ở làng Brindlemoor → qua cổng núi → **4 khu bốc ngẫu nhiên** trong 8 khu (mỗi khu 3 màn sinh ngẫu nhiên + 1 đấu trường boss) → *The Last Camp* → *The Sunken Vault*: hạ Hollow Warden, nhấc Sunstone, mang ra cổng. Chết là mất hết đồ của lượt, chỉ giữ lại ember, dân làng đã cứu, vật phẩm chính và kỷ lục.

Chi tiết thiết kế: **`Docs/DESIGN.md`**.

## Chơi ngay
`Builds/Windows/Brindlemoor.exe` (Windows 64-bit). Cửa sổ 1280×720, F11 (hoặc F4) bật/tắt toàn màn hình. Có 3 ô lưu; lượt đang chơi được lưu ở đầu mỗi màn.

## Phiên bản & mở project
- **Unity 6000.3.12f1** (cài tại `D:\Unity\6000.3.12f1`), URP 17.3 (2D Renderer), Input System 1.19, TextMesh Pro (font EB Garamond, SIL OFL, trong `Assets/Game/Fonts`).
- Unity Hub → *Add project from disk* → chọn thư mục `E:\Spelunky 2`.
- Scene khởi đầu: `Assets/Game/Scenes/MainMenu.unity`. Có thể mở thẳng `Assets/Game/Scenes/Game.unity` để chơi ngay (đối tượng **GameDirector** có mục *Debug*: seed cố định, màn bắt đầu, tutorial, god mode).
- Menu **Brindlemoor** trên thanh menu Editor:
  - *Build Everything*: sinh lại art, âm thanh, tile, prefab, scene, cấu hình URP/Input. Giá trị đã chỉnh trong các config được giữ nguyên.
  - *Build Windows (Builds/Windows)*.
  - *Room Template Editor*: tool xếp mẫu phòng (vẽ lưới, kiểm tra quy tắc, thử trong bộ sinh màn, chơi thử).
  - *Tests > Logic Tests*, *Tests > Seed Sweep*.

## Điều khiển
Bàn phím (mặc định; đổi được ở **Settings > CONTROLS / KEYS**, chỉ phím khác mặc định mới được lưu):

| Phím | Hành động |
|---|---|
| ← → | Di chuyển (mặc định chạy) |
| ↑ / ↓ | Leo thang/dây, cúi/bò; giữ khi đứng yên để nhìn lên/xuống |
| Space | Nhảy (giữ lâu nhảy cao hơn) · ↓ + Space: rơi xuyên sàn gỗ |
| Q | Chém kiếm · khi cầm đồ: ném (↑ + Q ném cao) · ↓ + Q: nhặt / đặt đồ |
| Shift | Lướt (tốn năng lượng, xem dưới) |
| W | Dùng đồ đang cầm: bom (ném; ↓ + W đặt; ↑ + W ném cao), dây (ném lên; ↓ + W ở mép: thả xuống), Recall Charm |
| Tab | Bấm nhanh: sang món kế tiếp · giữ: vòng chọn đồ (game dừng, ←/→ chọn, thả để cầm) |
| E (hoặc ↑) | Vào cửa, mua đồ, nói chuyện, mở lồng |
| S | Đi chậm |
| I | Túi đồ (WEAR / ITEMS / KEY ITEMS) |
| Esc / P | Tạm dừng (Resume, Settings, Main Menu) |

Bám mép là tự động khi rơi sát mép; ↑ hoặc nhảy để trèo, ↓ để buông. **Lướt:** ngang 3,4 ô trong 0,2 s, xuyên qua quái và đạn (không bị đánh trúng khi đang lướt), tường chặn lại, trên không lướt được một lần. Mỗi lần tốn 30 trên thanh năng lượng 100 (thanh dưới máu và vàng trên HUD); ngừng lướt 0,5 s thì thanh tự hồi.

**Tay cầm** (cố định): cần trái / D-pad di chuyển · Y/△ nhảy · X/□ chém, nhặt, ném · B/○ vào cửa, mua (có thứ trong tầm thì ưu tiên, không thì lướt) · RT/R2 dùng đồ · RB/R1 đổi đồ (giữ: vòng chọn) · LB/L1 đi chậm · Select/Share túi đồ · Start/Options tạm dừng. Hình nút trong mọi chữ hướng dẫn tự đổi theo thiết bị đang dùng (bàn phím, Xbox, PlayStation, Switch).

## Build
Editor: **Brindlemoor > Build Windows**. Hoặc dòng lệnh:
```
"D:\Unity\6000.3.12f1\Editor\Unity.exe" -batchmode -quit -projectPath "E:\Spelunky 2" -executeMethod Gloomdelve.EditorTools.ProjectBuilder.BuildWindows -logFile Logs\build.log
```
Kết quả: `Builds/Windows/Brindlemoor.exe`. Đổi art/prefab/scene thì chạy *Build Everything* (`ProjectBuilder.BatchBuildEverything`) trước.

## Tinh chỉnh
Tất cả nằm trong `Assets/Game/Data`:
- `PlayerConfig`: tốc độ, nhảy, coyote/bộ đệm, bám mép, kiếm, lướt, ném, sát thương rơi/chông, máu/bom/dây và đồ khởi đầu.
- `Gear_Bomb`, `Gear_Rope`, `Gear_Glider`, `Gear_Recall`: tên, mô tả, câu thông báo chướng ngại, kho tiếp tế của từng món dùng được.
- `Area0_Village` (làng), `Area1_Mossroot`, `Area2_Emberdeep`, `Area4_Sunfall`, `Area5_Frost`, `Area6_Sporegloom`, `Area7_Gearworks`, `Area8_Sandsunk`, `Area9_Geode`, `Area3_Vault` (trại và hầm cuối): kẻ địch, bẫy, cơ chế riêng, mật độ, kiểu đường đi, cửa hàng, ánh sáng, nhạc.
- `GameConfig`: danh mục chặng (`stages`), số khu mỗi lượt (`areasPerRun`), áp lực thời gian (9:00 cảnh báo / 10:00 thần chết), giới hạn nổ dây chuyền, sinh màn, ký hiệu tùy biến.
- `ShopConfig`: giá, tài nguyên, ô mặc và chỉ số cộng thêm (`modifiers`) của từng món.
- `RoomTemplates.txt`: 124 mẫu phòng (sửa bằng **Room Template Editor** hoặc sửa tay). `Levels/*.txt`: màn làm tay (Tutorial, Village, Camp, FinalVault, các đấu trường `Boss_*`).
- Prefab trong `Assets/Game/Prefabs` (thông số từng quái/boss/bẫy/vật phẩm).

Seed: mỗi lượt tự lấy seed ngẫu nhiên; seed hiện ở menu tạm dừng và màn kết quả, mọi seed được ghi vào `run_log.txt`. Thư mục lưu: `%USERPROFILE%\AppData\LocalLow\Brindlemoor Team\Brindlemoor` (`settings.json`, `slot1..3.json`, `records.json`, `run_log.txt`). Save cũ ở `LocalLow\Gloomdelve Team\Gloomdelve` được tự chép sang lần đầu mở bản mới (thư mục cũ giữ nguyên làm bản sao lưu).

## Mở rộng game
Thêm mẫu phòng, nâng cấp, vật phẩm, đồ dùng, kẻ địch, boss, khu mới: xem **`Docs/EXTENDING.md`**.

## Tài liệu
`PROJECT_STATUS.md` · `Docs/DESIGN.md` · `Docs/EXTENDING.md` · `Docs/REFERENCE.md` · `Docs/TEST_REPORT.md` · `Docs/Screenshots/`
