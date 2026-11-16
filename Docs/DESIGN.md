# Gloomdelve – Thiết kế

Platformer roguelike 2D pixel art: thám hiểm hang động sinh ngẫu nhiên, mang **Heartstone** từ đáy *The Sunken Vault* về cổng mặt đất.

## 1. Cấu trúc lượt chơi
| Chặng | Nội dung |
|---|---|
| Hướng dẫn (menu → TUTORIAL) | Màn làm tay 96×18, 9 biển chỉ dẫn: di chuyển, nhảy, bám mép, thang, roi/giẫm, nhặt/ném, bom, dây, chông + bẫy tên, cửa ra |
| Khu 1 – Mossroot Caverns (1-1, 1-2, 1-3) | Đất rêu; rắn, dơi, nhện; chông, bẫy tên, thạch nhũ rơi |
| Khu 2 – Emberdeep Mines (2-1, 2-2, 2-3) | Đá than hồng; cóc lửa nhảy, gã chuột chũi lao húc, dơi lửa; chông obsidian, bẫy tên, **khối nghiền** (rơi + chu kỳ), **lỗ phun lửa** (chu kỳ) |
| Khu 3 – Sunfall Cliffs (3-1, 3-2, 3-3) — **ngoài trời** | Bầu trời hoàng hôn (gradient + núi xa + đồi gần parallax + mây trôi), đồi cỏ, cây, dây leo (thang), bụi gai (chông), hang dưới vách đá; rắn, cóc, dơi trong hang, **diều hâu** (lượn vòng, bổ nhào khi thấy người chơi phía dưới, rồi bay lên); nhận **tàu lượn** khi bắt đầu khu; chướng ngại: vực gai rộng 6 ô |
| Màn cuối – The Sunken Vault | Làm tay 40×34. Xuống đáy lấy Heartstone (cổng bị khóa đến khi nhấc nó). Nhấc lên → hầm sập (đá rơi liên tục), mang nó leo thang phía đông về cổng → **CHIẾN THẮNG** |

Cửa hàng: chắc chắn ở màn 2 mỗi khu, 40% ở màn 3. Chết = hết lượt → màn kết quả (nguyên nhân chết, vàng, thời gian, seed, kỷ lục) → **R** chơi lượt mới / chơi lại đúng seed / menu.

## 2. Điều khiển (bàn phím; tay cầm cũng được)
| Phím | Hành động |
|---|---|
| ← → | Di chuyển (mặc định chạy; giữ **Shift** để đi chậm) |
| Z / Space | Nhảy – giữ lâu nhảy cao hơn |
| X | Roi; khi đang cầm đồ: ném (↑+X ném cao) |
| ↓ + X | Nhặt đồ / đặt đồ xuống |
| C | Dùng trang bị đang chọn (bom: ném, ↓+C đặt, ↑+C ném cao · dây: xem dưới) |
| Giữ D (tay cầm: giữ R1) | Cửa sổ trang bị: game dừng, ←/→ (gạt cần) chọn, thả để trang bị; bấm nhanh = đổi về món vừa dùng trước đó |
| (trang bị Dây) | Ném dây lên (↓+C khi đứng mép: thả dây xuống vách). Dây chỉ móc khi chạm trần hoặc có vách/mép đá cạnh đỉnh dây; không bám được thì rơi lại thành cuộn dây nhặt lại được. Dây móc tồn tại 20 s (nhấp nháy 3 s cuối, không biến mất khi đang leo) |
| A hoặc ↑ | Vào cửa, mua đồ |
| ↑/↓ | Leo thang/dây; giữ khi đứng yên để nhìn lên/xuống |
| ↓ | Cúi/bò; bò ra mép → tự treo mép |
| ↓ + nhảy | Rơi xuyên sàn gỗ |
| Esc / P | Tạm dừng · F11 toàn màn hình |

## 2b. Hệ trang bị & giải đố theo địa hình
- `EquipmentDef` (ScriptableObject, `Data/Gear_Bomb.asset`, `Data/Gear_Rope.asset`): id, tên, mô tả, icon, kiểu (`GearKind`). Danh sách trong `GameConfig.equipment`; người chơi khởi đầu với `PlayerConfig.startingGear` (bom, dây). Đạn lưu trong `RunState` (bombs, ropes). Thêm trang bị mới = thêm một `GearKind` + hành vi trong `PlayerController.UseGear` + một asset.
- Một nút **Dùng** (C / B) chỉ dùng món đang trang bị. **Cửa sổ trang bị** (`EquipmentWheel`): giữ D / R1 quá 0,25 s → game dừng hẳn, hiện các ô trang bị (icon, số đạn, mô tả, địa hình phù hợp), gạt trái/phải chọn, thả nút để trang bị; bấm nhanh = đổi qua lại giữa món hiện tại và món dùng trước đó (chưa có món trước thì sang món kế tiếp), nên vừa chọn trên cửa sổ xong chỉ cần **1** lần bấm là về món cũ; nhấn hơi lâu làm cửa sổ bật lên nhưng không chọn gì mà thả trước 0,4 s vẫn tính là bấm nhanh; giữ lâu không chọn thì đóng, giữ nguyên món. HUD có ô "đang trang bị" (icon, tên, số đạn — đỏ khi hết).
- `AreaConfig.requiredGear` + `gatesByLevel` (mặc định 1, 1, 2): bộ sinh màn biến 1–2 phòng ngang trên đường chính thành **phòng chướng ngại**:
  - Mossroot (`gate_rope`): vách **bedrock** cao 4 ô (tầm với tối đa không trang bị là 3), 2 ô hở dưới trần; ném dây sát vách (ô `R`) → dây móc dưới trần → leo, bước sang đỉnh vách, nhảy xuống bên kia.
  - Emberdeep (`gate_bomb`): **đá vụn** (ô mới `Rubble`, hình đống đá nứt) 2×3 bịt đường, bedrock phía trên/dưới; 1 quả bom đặt sát mở lối cao 2 ô.
  - Biển gợi ý (`!`) đặt sẵn cạnh chướng ngại; banner đầu màn báo số chướng ngại và trang bị cần.
- Validator kiểm tra **hai chiều**: dựng "lưới đã dùng trang bị" (đá vụn bị xoá; dây được móc đúng chỗ mà dây thật sẽ móc — dùng chung hàm `RopeProjectile.RiseStep/SimulateThrow/ComputeCells`) → phải tới được cửa ra (và cửa hàng); trên lưới gốc (không trang bị) → **không được** tới cửa ra, nếu tới được thì loại và sinh lại.
- Chống kẹt: nếu đầu màn người chơi có ít đạn hơn số chướng ngại, một kho tiếp tế (túi bom / cuộn dây) xuất hiện cạnh lối vào.

## 2c. Khu ngoài trời (Sunfall Cliffs)
- `AreaConfig.outdoor`: thêm `skyRows` (7) hàng trời trống phía trên lưới phòng, bỏ viền trên và viền hai bên phía trên mặt đất (ngoài bản đồ vẫn là tường vô hình). Hàng phòng trên cùng chỉ dùng mẫu gắn tag `surface` (mở lên trời); các hàng dưới dùng mẫu hang thường (tag `under` chỉ cho hàng dưới).
- `TileSet.outdoor`: nền đất tối chỉ vẽ dưới mặt đất (làm mượt theo 2 cột lân cận để giếng hẹp vẫn là "trong lòng đất"); phía trên là `SkyBackdrop` (con của camera, chỉ bật ở khu ngoài trời).
- Cây (`Scenery`, 2 kiểu) mọc ngẫu nhiên trên mặt đất (`treeChance`); diều hâu đặt ở khoảng trời (`airEnemies`, `airEnemiesPerLevel`), không gần lối vào.
- **Tàu lượn** (`GearKind.Glider`, `Data/Gear_Glider.asset`): `AreaConfig.grantGear = glider` trao cho người chơi khi vào khu. Trang bị rồi giữ nút nhảy khi đang rơi → tốc độ rơi tối đa `MotorStats.glideFallSpeed` (1,9 ô/s), vẫn điều khiển ngang; lượn không tính sát thương rơi; không tốn đạn.
- Chướng ngại `gate_glider`: hố gai rộng 6 ô (ô `~`, bộ sinh đặt bedrock bên dưới), nhảy chạy đà tối đa ~4 ô. Validator thêm các mẫu input "giữ nhảy suốt cú rơi" khi có tàu lượn; vẫn kiểm tra hai chiều (có tàu lượn qua được, không có thì không).
- Nhạc riêng `area3` (giọng trưởng, nhẹ).

## 2d. Đọc được ngay đâu là đồ nhặt được
- Lớp trang trí (cỏ, nấm, đầu lâu, sỏi, rễ...) được làm tối/nhạt (`Decor` tilemap màu 0.55) để luôn lùi ra sau.
- `Highlight` (gắn trên prefab đồ vật): quầng sáng mềm nhấp nháy phía sau sprite, màu theo loại (trắng: đá/bình/thùng/mũi tên, cam: bom, vàng: vàng/tượng, xanh: ngọc, đỏ hồng: tim/Heartstone); đồ tự nhặt khi chạm (vàng, ngọc, tim, túi bom, cuộn dây) còn nhún lên xuống; đồ phải nhặt bằng ↓+X có mũi tên trắng nảy phía trên khi người chơi đứng sát. Quầng sáng tắt khi đang cầm. Màu/độ sáng chỉnh trong component trên prefab.

- Hiệu ứng nhún của đồ tự nhặt chỉ nhô lên trên vị trí nằm (0 → +0,12 ô), không bao giờ lún xuống đất.

- Hình móc dây theo chỗ bám (`RopeProjectile.ClassifyHold`): trần phía trên → móc lên trần; vách ngang đỉnh dây → dây vắt qua mép, móc cắm trên mặt đất; vách cao hơn đỉnh dây → chốt đóng vào vách; thả dây từ mép (↓ + dây) → móc cắm trên mép chỗ đang đứng. Chỉ thay đổi hình, luật móc giữ nguyên.

## 3. Chuyển động (PlayerMotor – thuần C#, tất định)
`Assets/Game/Scripts/Player/PlayerMotor.cs`, tham số trong `Data/PlayerConfig.asset` (mục *motor*).
- Bước mô phỏng cố định 1/60 s; va chạm AABB với lưới ô (không dùng Physics2D) – chính xác theo pixel, tất định, nên **bộ kiểm tra màn và bot dùng đúng code này**.
- Hitbox 0,6×0,85 ô (lọt khe 1 ô). Chạy 6,4 ô/s, đi chậm 3, bò 1,8. Trọng lực 52 (×1,15 khi rơi), rơi tối đa 19.
- Nhảy v=15,8 → ~2,3 ô; thả nút sớm nhân vận tốc 0,45. Coyote 0,08 s, bộ đệm nhảy 0,12 s, đổi hướng trên đất tăng tốc ×2.
- Bám mép tự động khi đang rơi, sát tường, đầu ở trong cửa sổ [mép−0,38; mép+0,15]. Treo: ↑ trèo lên (0,22 s), nhảy để bật lên, ↓ buông.
- Thang/dây: ↑ bám (căn giữa cột), đỉnh thang là bề mặt đứng được; nhảy rời thang.
- Rơi ≥ 9 ô: −1 máu, choáng 1 s.

## 4. Hệ thống & quy tắc tương tác
Tất cả thực thể kế thừa `Entity` (pos = đáy giữa hitbox) và chạy trong `World.FixedUpdate`.

**Sát thương** (`DamageInfo`: lượng, loại, nguồn, lực đẩy, mô tả → hiện “-2 Shot by an arrow trap” và nguyên nhân chết):
| Nguồn | Sát thương | Ghi chú |
|---|---|---|
| Kiếm | 1 (Tempered Blade: 2, tầm +0,45) | Có hitbox phía sau lúc vung |
| Giẫm đầu | 1 + nảy (giữ nhảy nảy cao) | Rắn, dơi, nhện, cóc, gã húc, chủ tiệm khi giận |
| Vật bị ném / rơi nhanh | 1 (mũi tên nhanh: 2) | Khi tốc độ ≥ ngưỡng; trúng cả người chơi (kể cả đồ mình ném lên rơi xuống) |
| Chạm kẻ địch | 1 + bật lùi + choáng 0,45 s + bất tử 1,2 s | Gã húc đẩy mạnh ×1,6 |
| Bẫy tên | 2 | 1 mũi/bẫy, kích hoạt bởi bất kỳ thứ gì đang di chuyển trong tầm 7 ô |
| Thạch nhũ / đá sập | 2 | Rung 0,45 s trước khi rơi (báo hiệu) |
| Khối nghiền | 3 | Cảnh báo rung 0,35 s, nghiền, nâng lên lại (chu kỳ) |
| Lỗ phun lửa | 1 + hất lên | Nhỏ lửa báo trước 0,75 s; châm ngòi bom; đốt vỡ bình |
| Chông | chết | Chỉ khi rơi vào; đi ngang qua an toàn |
| Nổ bom | 10 | Bỏ qua thời gian bất tử |
| Thần chết (Reaper) | chết | Xuyên tường, không thể bị hạ |

**Bom:** ngòi 2,5 s (nhấp nháy nhanh khi < 0,8 s), bán kính 2,3 ô (Blast Powder +0,9). Phá mọi ô trừ bedrock (khối đá tối có đinh tán — khác biệt rõ về hình), ô có vàng/ngọc rơi ra chiến lợi phẩm, mạnh vỡ bình/thùng, đẩy vật thể, làm kẻ địch/người chơi chết, **kích nổ bom khác** (ngòi rút còn 0,12–0,3 s).
**Giới hạn dây chuyền:** tối đa 3 vụ nổ xử lý mỗi bước (phần còn lại đợi bước sau), mỗi bom mang “thế hệ”; quá 12 thế hệ bom tự tắt; trần 600 thực thể. Kiểm thử: 37 bom nổ dây chuyền kết thúc gọn.

**Dây:** bay tối đa 7 ô; móc khi chạm trần, hoặc khi dừng mà bên cạnh/ngay trên đỉnh có ô đặc; không thì rơi xuống thành cuộn 1 dây. Dây móc dài tối đa 8 ô, sống `PlayerConfig.ropeLifetime` (20 s), nhấp nháy `blinkTime` (3 s) rồi biến mất; bom cắt đứt dây.

**Vật thể:** đá, bình (vỡ → vàng/ngọc/kẻ địch/**trái tim** ~5%/không gì), thùng (vỡ → túi bom/dây/vàng/ngọc/**trái tim** ~18%), mũi tên (nhặt được), bom đang cháy (nhặt & ném lại được), tượng vàng/Heartstone (nặng: chậm 20%). Mọi thứ đang bay đều kích bẫy tên.

**Kẻ địch (≥4 hành vi khác nhau):** Rắn (tuần tra, quay đầu ở tường/mép/chông) · Dơi (treo trần, thấy người thì lao đuổi) · Nhện (phục kích trên trần, rơi khi đi qua bên dưới, rồi nhảy đuổi) · Cóc lửa (nhảy vòng cung về phía người chơi) · Gã húc (3 máu, thấy cùng tầng thì gồng 0,4 s rồi lao, đâm tường thì choáng) · Hồn ma (áp lực thời gian) · Chủ tiệm (6 máu): lần bị người chơi đánh thứ 1 và 2 chỉ cảnh báo ("HEY! WATCH IT!", "LAST WARNING!" + thông báo), không mất máu, không phản công; lần thứ 3 mới nổi giận: mất máu, đuổi đánh, đồ trong tiệm thành miễn phí. Roi, đồ ném, bom và nổ trúng hàng hóa đều tính là một lần đánh; một cú vung/một vụ nổ chỉ tính 1 lần. Số lần cảnh báo chỉnh ở `ShopConfig.warningsBeforeAngry`.

## 5. Sinh màn (`LevelGenerator`, `RoomTemplates.txt`, `LevelValidator`)
1. Seed lượt → seed từng màn `Hash(runSeed, stage)`; RNG xorshift tất định.
2. Đường giải trong lưới 4×4 phòng 10×8: xúc xắc 1–5 (trái/phải/xuống), không quay lại; loại phòng 1 (trái-phải), 2 (+đáy), 3 (+đỉnh), 4 (cả 4); phòng ngoài đường loại 0.
3. Phòng đặc biệt: bắt đầu/thoát (có cửa `D`), cửa hàng (bên cạnh đường, lật theo hướng), phòng tượng vàng (bẫy bao quanh), hầm kho báu (kín, cần bom).
4. Mẫu phòng (43 mẫu, file văn bản sửa được) + lật ngang ngẫu nhiên + ô xác suất `?` + khe đánh dấu (kẻ địch, bẫy, kho báu, vật phẩm) + mạch vàng/ngọc trong đất.
5. Bẫy/kẻ địch theo mật độ của khu (AreaConfig), tăng 15%/màn; **phòng phụ ×1,4 rủi ro và nhiều kho báu hơn** (rủi ro tương xứng phần thưởng).
6. Lượt an toàn: không chông/bẫy tên/lỗ lửa trong bán kính 4 ô quanh lối vào, không bẫy tên nào nhắm vào lối vào, không kẻ địch trong 6 ô, không vật rơi phía trên lối vào; cửa hàng không có bẫy/quái.
7. **Chứng minh vượt qua được:** `LevelValidator` chạy chính `PlayerMotor` với thư viện mẫu input (đi bộ, đi chậm, 21 kiểu nhảy với độ giữ/đà chạy/trễ khác nhau, bò xuống treo mép, leo/xuống thang, nhảy khỏi thang, trèo/nhảy/buông mép, rơi xuyên sàn) và BFS trên các trạng thái nghỉ. Cấm chạm chông và cú rơi gây sát thương. Không dùng bom/dây. Phải tới được cửa ra (và cửa hàng nếu có). Không đạt → sinh lại với attempt kế tiếp. Không chỉ xét ô trống liền nhau.
8. Đường đi tìm được được bot phát lại trong game thật để xác nhận (xem TEST_REPORT).

## 6. Tiến trình & phần thưởng
- Vàng: cục nhỏ 50, cục lớn 150, ngọc 300, tượng vàng 1200 (phải mang ra cửa), thưởng Heartstone 5000.
- Cửa hàng (`ShopConfig.asset`): Bomb Bag +3 ($350), Rope Pile +3 ($250), Heart Tonic +1 máu ($500) + 2 nâng cấp ngẫu nhiên (chủ tiệm cảnh báo 2 lần trước khi nổi giận) trong: **Spring Boots** (nhảy cao ~40%), **Iron Whip** (2 sát thương, xa hơn), **Compass** (điểm đánh dấu waypoint chỉ cửa ra/Heartstone, xem dưới), **Blast Powder** (nổ to hơn).
- HUD: máu, bom, dây, vàng, khu–màn, thời gian màn + tổng, nâng cấp, gợi ý tương tác, waypoint.
- Waypoint (nâng cấp Compass, `HUD.UpdateWaypoint`): huy hiệu tròn kiểu UI hiện đại (vẽ mượt, không pixel) có icon cửa (hoặc viên ngọc khi mục tiêu là Heartstone), viền màu nhấn (xanh bạc hà = cửa ra, hồng = Heartstone), vòng sóng lan tỏa, số mét bên dưới. Cửa ra trong màn hình → huy hiệu lơ lửng phía trên cửa với mũi nhọn chỉ xuống; ngoài màn hình → bám mép màn hình (chừa vùng HUD trên/dưới) kèm mũi tên chevron chỉ hướng. Chuyển trạng thái mượt, mờ đi khi đứng sát cửa, ẩn khi tạm dừng / mở cửa sổ trang bị / hết màn.
- Áp lực thời gian (`GameConfig`): cảnh báo 4:30 (đồng hồ chuyển cam + banner + âm), thần chết xuất hiện 5:00 (đồng hồ đỏ nhấp nháy, nhạc trầm xuống), thêm 1 con mỗi 60 s. Tắt ở màn hướng dẫn.
- Lưu: `settings.json`, `records.json`, `run_log.txt` (seed mỗi màn) trong `%USERPROFILE%\AppData\LocalLow\Gloomdelve Team\Gloomdelve`.

## 7. Cấu trúc project
```
Assets/Game/
  Scripts/Core      cấu hình (ScriptableObject), RunState, lưu, âm thanh, animation
  Scripts/World     TileGrid, BodyPhysics, Entity, World, LevelRenderer
  Scripts/Player    PlayerMotor, PlayerController, PlayerInputReader
  Scripts/Enemies   Snake, Bat, Spider, Toad, Brute, Wraith, Shopkeeper
  Scripts/Items     Item, Bomb, Breakable, Arrow, Treasure, Pickup, RopeProjectile
  Scripts/Traps     ArrowTrap, Stalactite, Crusher, FlameVent (chông nằm trong lưới ô)
  Scripts/Level     LevelGenerator, LevelValidator, RoomTemplates, HandcraftedLevel, Door, ShopItem, Sign
  Scripts/Game      GameDirector, CameraFollow, AutoTest, ScenarioRunner, BotInput
  Scripts/UI        HUD, PauseMenu, SettingsPanel, ResultsScreen, TransitionScreen, MainMenuController
  Editor            ArtGenerator, AudioGenerator, ProjectBuilder, UIBuilder, GameTests
  Data              GameConfig, PlayerConfig, Area1/2/3, ShopConfig, TileSet_*, SfxLibrary, RoomTemplates.txt, Levels/*.txt
  Art, Audio, Tiles, Animations, Prefabs, Scenes, Settings (URP, Input Actions), Resources/GameServices.prefab
```
Chỉnh thông số: chọn asset trong `Assets/Game/Data` → Inspector. **Gloomdelve > Build Everything** sinh lại art/âm thanh/prefab/scene nhưng **giữ nguyên giá trị** của các config đã tồn tại.
