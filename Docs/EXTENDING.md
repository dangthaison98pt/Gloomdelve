# Hướng dẫn mở rộng – Gloomdelve

Tài liệu này chỉ ra **phải đụng vào đâu** khi thêm nội dung mới. Nguyên tắc chung của code:

- **Dữ liệu nằm trong asset** (`Assets/Game/Data`), hành vi nằm trong class nhỏ đăng ký theo **id chuỗi**. Thêm thứ mới = thêm asset + (nếu cần) một class, không sửa `PlayerController`, HUD hay bộ sinh màn.
- **Mọi màn sinh ra đều được `LevelValidator` chứng minh là qua được**, nên nội dung mới sai chỉ khiến màn bị sinh lại chứ không tạo ra màn kẹt.
- Sau khi sửa: **Gloomdelve > Tests > Logic Tests** và **Seed Sweep**; nếu đổi prefab/art/scene thì chạy **Gloomdelve > Build Everything** trước.

| Muốn thêm | Cần code? | Làm ở đâu |
|---|---|---|
| Mẫu phòng mới | Không | Tool **Gloomdelve > Room Template Editor** |
| Đặt một prefab bất kỳ vào mẫu phòng | Không | `GameConfig > Custom Markers` |
| Nâng cấp chỉ cộng chỉ số | Không | `ShopConfig > Offers` |
| Nâng cấp có hành vi riêng | Vài dòng | `ShopConfig` + `Run.HasUpgrade("id")` |
| Vật phẩm nhặt (pickup) mới | Không (chỉ prefab) | Prefab `Pickup` + `PrefabRegistry` |
| Trang bị mới (như bom/dây/tàu lượn) | 1 class | `Scripts/Gear` |
| Kẻ địch / bẫy mới | 1 class | `Scripts/Enemies`, `Scripts/Traps` + `PrefabRegistry` |
| Khu / màn mới | Không | `AreaConfig` + `GameConfig > Stages` |

---

## 1. Mẫu phòng – tool cho Game Designer

Mở **Gloomdelve > Room Template Editor**. Tool đọc/ghi thẳng `Assets/Game/Data/RoomTemplates.txt` (file văn bản vẫn là nguồn duy nhất, sửa tay vẫn được).

![Room Template Editor](Screenshots/room_editor.png)

**Bố cục cửa sổ**

- **Cột trái** – danh sách phòng, ô tìm theo tên, lọc theo loại và tag. Chấm màu: xanh = hợp lệ, vàng = có cảnh báo, đỏ = có lỗi.
- **Giữa** – lưới 10×8. Chuột trái vẽ ký hiệu đang chọn, chuột phải lấy ký hiệu ở ô đó. **Vạch xanh ở mép lưới** cho biết chỗ bắt buộc phải hở theo loại phòng đã tick. Bên dưới là bảng ký hiệu (rê chuột để xem giải thích), nút Lật ngang / Xoá trắng / Hoàn tác.
- **Cột phải** – tên, weight, loại phòng, tag, khu, ghi chú; kết quả **kiểm tra quy tắc** cập nhật ngay khi vẽ; phần **thử trong bộ sinh màn**.

**Quy trình làm một phòng mới**

1. `+ Phòng mới` (hoặc `Nhân bản` một phòng gần giống).
2. Tick **loại phòng**: `1` trái-phải, `2` có lối xuống, `3` có lối lên, `4` bốn phía, `0` phòng phụ ngoài đường chính. Một phòng tick được nhiều loại.
3. Vẽ. Giữ các vạch xanh hở.
4. Sửa hết lỗi đỏ trong mục *Kiểm tra quy tắc*.
5. **Chạy thử** (30 seed): tool ép phòng này vào mọi chỗ phù hợp rồi cho validator chứng minh từng màn; báo cáo cho biết phòng được dùng bao nhiêu lần, có làm bộ sinh màn phải sinh lại nhiều hơn không.
6. **Xem màn** để nhìn bản đồ thu nhỏ (khung vàng = phòng đang thử), **▶ Chơi thử màn này** để vào Play ngay trong màn đó (cùng seed với bản xem trước).
7. **Lưu**.

**Ý nghĩa ký hiệu** – xem bảng ngay trong tool (hoặc `RoomLegend.cs`). Lưu ý các ký hiệu bẫy/quái/kho báu là *vị trí có thể xuất hiện*: xác suất thật do `AreaConfig` của khu quyết định, nên cùng một phòng sẽ khác nhau giữa các khu.

**Tag đặc biệt**: `start`, `exit` (cần đúng 1 cửa `D`), `shop` (cần `K` và `S`, hở bên phải), `idol` (cần `O`), `vault`, `surface`/`under` (khu ngoài trời), `gate_<id trang bị>` (phòng chướng ngại bắt buộc dùng trang bị đó).

### Đặt prefab mới vào phòng mà không cần code

`GameConfig.asset > Custom Markers > +`:

| Trường | Ý nghĩa |
|---|---|
| Symbol | 1 ký tự chưa dùng (ví dụ `M`) |
| Label | Tên hiện trong bảng ký hiệu của tool |
| Spawn Id | id trong `PrefabRegistry` (ví dụ `crate`, `heart`, hoặc prefab mới của bạn) |
| Chance | Xác suất xuất hiện |
| Placement | `OnFloor` (cần sàn), `UnderCeiling` (cần trần), `Anywhere` |
| Tile | Ô địa hình ghi vào chỗ đó (thường `Empty`) |

Ký hiệu mới xuất hiện ngay trong tool (đóng/mở lại cửa sổ hoặc bấm *Tải lại*), dùng được cả trong `Levels/Tutorial.txt` và `Levels/FinalVault.txt`.

---

## 2. Nâng cấp (bán trong cửa hàng)

`ShopConfig.asset > Offers > +`, tick **Is Upgrade**.

- **Chỉ cộng chỉ số** → điền `Modifiers`, không cần code:
  `jumpBonus` (0.17 = nhảy mạnh hơn 17%), `whipDamageBonus`, `whipRangeBonus`, `blastRadiusBonus`. Các nâng cấp cộng dồn với nhau.
- **Có hành vi riêng** → trong code hỏi `RunSession.Current.HasUpgrade("id_cua_ban")`. Ví dụ có sẵn: la bàn (`HUD.cs`), hình roi sắt (`PlayerController.cs`).
- **Cần một chỉ số mới** (ví dụ tốc độ chạy): thêm 1 trường vào `StatModifiers` + 1 dòng trong `StatModifiers.Add` (`Configs.cs`), rồi đọc `Run.stats.<trường>` ở nơi dùng.

Cửa hàng tự chọn ngẫu nhiên 2 nâng cấp mỗi lần, HUD tự hiện tên nâng cấp đã mua, lượt mới tự xoá sạch.

Món tiêu hao (không tick Is Upgrade): điền `Resource` + `Amount`. Resource có sẵn: `gold`, `health`, `bombs`, `ropes`; id khác được lưu thành bộ đếm riêng (`RunState.GetResource / AddResource`), dùng làm đạn cho trang bị mới.

## 3. Vật phẩm nhặt (pickup)

Prefab có component `Pickup`: `Resource` (như trên), `Value`, `Label` (chữ hiện khi nhặt, ví dụ `ARROWS`), `Sfx`. Thêm một dòng vào `PrefabRegistry.asset` (id + prefab). Các dòng thêm tay được giữ lại khi chạy *Build Everything*.

Để đặt trong phòng: dùng Custom Marker (mục 1). Để nó rơi ra từ thùng: thêm id vào danh sách `contents` của `crate` trong `ProjectBuilder.CreatePrefabs` (prefab có sẵn như Crate được sinh lại mỗi lần *Build Everything*, nên sửa trực tiếp trên prefab đó sẽ bị mất).

## 4. Trang bị mới (gear)

Ví dụ thêm "cuốc" đào đất:

1. **Hành vi** – tạo `Scripts/Gear/PickaxeGear.cs`:
   ```csharp
   public class PickaxeGear : GearBehaviour
   {
       public override int Ammo(RunState run) => run.GetResource("pickaxe");   // -1 nếu dùng vô hạn
       public override void Use(PlayerController p, FrameInput fi)
       {
           var run = RunSession.Current;
           if (run.GetResource("pickaxe") <= 0) { Sfx.Play("error", p.pos); return; }
           int x = Mathf.FloorToInt(p.pos.x) + p.facing, y = Mathf.FloorToInt(p.pos.y + 0.5f);
           if (!p.World.Grid.IsDestructible(x, y)) return;
           p.World.DestroyTile(x, y, true);
           run.AddResource("pickaxe", -1);
       }
   }
   ```
2. **Đăng ký** – thêm 1 dòng trong `GearRegistry` (`GearBehaviour.cs`): `Register("pickaxe", new PickaxeGear());`
3. **Dữ liệu** – *Create > Gloomdelve > Equipment*, đặt `id = pickaxe`, tên, mô tả, icon; kéo vào `GameConfig > Equipment`.
4. **Cho người chơi** – thêm id vào `PlayerConfig > Starting Gear`, hoặc `AreaConfig > Grant Gear` để nhận khi vào khu.

Vòng chọn trang bị, HUD, số đạn tự hoạt động. Các hook khác của `GearBehaviour`:

- `ModifyMotor` – đổi cách di chuyển khi đang trang bị (tàu lượn dùng cái này); khi đó đặt `AffectsMovement => true`.
- `ApplyToValidator` – nếu trang bị cho phép vượt địa hình mà bình thường không qua được, khai báo để validator biết.

**Chướng ngại bắt buộc cho trang bị mới**: đặt `AreaConfig > Required Gear = pickaxe`, làm các phòng tag `gate_pickaxe` trong tool, điền `Gate Banner` / `Gate Sign` / `Supply Pickup Id` trong asset trang bị. Validator sẽ loại mọi màn mà không cần trang bị vẫn qua được. Nếu chướng ngại là loại địa hình mới, dạy validator cách trang bị xử lý nó trong `LevelValidator.GearGrid`.

## 5. Kẻ địch / bẫy mới

1. Class kế thừa `Enemy` (hoặc `Entity` cho bẫy) – xem `Snake.cs` (32 dòng) làm mẫu. Ghi đè `Tick(dt)`.
2. Bẫy: ghi đè `IsHazard => true` (và `IsCeilingHazard => true` nếu rơi từ trần) để bộ sinh màn tự giữ nó xa lối vào, ngoài cửa hàng. Kẻ địch kế thừa `Enemy` đã là hazard sẵn.
3. Tạo prefab, thêm vào `PrefabRegistry.asset`.
4. Cho xuất hiện: thêm id vào `AreaConfig > Ground Enemies / Ceiling Enemies / Air Enemies` (kèm trọng số), hoặc đặt tay bằng Custom Marker.

Prefab có sẵn trong dự án được sinh bằng code trong `ProjectBuilder.CreatePrefabs` (để art và thông số tái tạo được). Prefab bạn tự tạo trong Editor không bị đụng tới.

## 6. Khu / màn mới

1. *Create > Gloomdelve > Area Config*: bộ tile, nhạc, danh sách quái, mật độ bẫy, trang bị bắt buộc...
2. Thêm các dòng vào `GameConfig > Stages` (danh sách này không còn bị ghi đè khi *Build Everything*).
3. Trong tool mẫu phòng, tick khu mới cho các phòng riêng của khu (không tick khu nào = dùng chung).
4. Chạy *Seed Sweep* – báo cáo cho biết khu mới có sinh màn ổn không.

---

## Bản đồ code (rút gọn)

| Thư mục | Nội dung |
|---|---|
| `Scripts/Core` | Config (ScriptableObject), `RunState` (toàn bộ trạng thái một lượt chơi), lưu file, âm thanh |
| `Scripts/Gear` | `GearBehaviour` + các trang bị |
| `Scripts/Player` | `PlayerMotor` (chuyển động tất định, dùng chung cho game + validator + bot), `PlayerController` (hành động, sát thương) |
| `Scripts/World` | `TileGrid`, va chạm, `World` (vòng mô phỏng, nổ, spawn), `Entity` |
| `Scripts/Level` | `RoomTemplates` (đọc/ghi mẫu phòng), `RoomLegend` + `RoomLint` (ký hiệu, quy tắc), `LevelGenerator`, `LevelValidator` |
| `Scripts/Enemies`, `Traps`, `Items` | Mỗi loại một class nhỏ |
| `Scripts/Game` | `GameDirector` (luồng một màn), bot và kịch bản kiểm thử |
| `Editor` | `ProjectBuilder` (sinh asset), `RoomTemplateEditor` (tool GD), `GameTests` |
