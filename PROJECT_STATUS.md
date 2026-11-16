# PROJECT_STATUS – Gloomdelve

Cập nhật: 14/11/2026 · Phiên bản 0.1.0 · Unity 6000.3.12f1

## Đã xong
- Unity project tại `E:\Spelunky 2` (thư mục ban đầu trống), URP (Universal Renderer, sprite unlit), New Input System, 2 scene dựng sẵn, build settings.
- Chuyển động platformer tất định: chạy/đi chậm, nhảy biến thiên, coyote time, bộ đệm nhảy, cúi/bò, bám mép tự động + trèo, bò ra mép để treo, thang & dây, rơi xuyên sàn gỗ, sát thương rơi.
- Roi (đánh sau/trước), giẫm đầu, nhặt/mang/đặt/ném, bom (ném/đặt/nhặt lại, phá địa hình, dây chuyền có giới hạn), dây (ném lên/thả xuống mép), máu, bật lùi, choáng, bất tử ngắn, chết, nguyên nhân chết rõ ràng.
- 7 loại kẻ địch (rắn, dơi, dơi lửa, nhện, cóc lửa, gã húc, chủ tiệm) + hồn ma áp lực thời gian.
- Bẫy: chông, bẫy tên, thạch nhũ rơi, khối nghiền (rơi + chu kỳ), lỗ phun lửa (chu kỳ); đá sập ở màn cuối.
- Vật thể: đá, bình, thùng, mũi tên, bom, tượng vàng, Heartstone; vàng, ngọc, túi bom, cuộn dây; mạch vàng/ngọc trong đất.
- Sinh màn theo seed: lưới phòng + đường giải + 43 mẫu phòng (văn bản sửa được), phòng cửa hàng / tượng vàng / hầm kho báu, rủi ro–phần thưởng theo phòng phụ, lượt an toàn lối vào, **validator dùng chính bộ chuyển động của nhân vật**.
- 2 khu (Mossroot, Emberdeep) khác nhau về hình ảnh, bẫy, quái, nhạc; 3 màn mỗi khu; màn cuối làm tay với điều kiện thắng; màn hướng dẫn 9 biển.
- Cửa hàng: máu, bom, dây + 4 nâng cấp (Spring Boots, Iron Whip, Compass, Blast Powder); chủ tiệm cảnh báo 2 lần rồi mới nổi giận.
- UI: menu chính, hướng dẫn điều khiển, cài đặt (âm lượng tổng/nhạc/hiệu ứng, toàn màn hình, rung màn hình), tạm dừng, HUD đầy đủ, thẻ chuyển màn, màn kết quả (R chơi lại nhanh, chơi lại seed), kỷ lục.
- Lưu cài đặt + kỷ lục (JSON), ghi log seed.
- Art pixel 397 sprite + 42 SFX + 4 nhạc, tất cả sinh bằng code (Editor).
- Kiểm thử: 23 test logic, quét 150 seed (900 màn, 0 lỗi), 23 kịch bản tương tác trong build, bot chơi trọn 9 lượt (63 màn) trong build, kiểm tra không rò rỉ trạng thái giữa lượt. Build Windows chạy được.
- Tài liệu: README, DESIGN, REFERENCE, TEST_REPORT.

## Thay đổi theo phản hồi (14/11)
- Dây: chỉ móc khi chạm trần hoặc có vách/mép bên cạnh; không bám được thì rơi lại thành cuộn dây nhặt lại được; dây móc biến mất sau 20 s (nhấp nháy báo trước, giữ nguyên khi đang leo). Biển hướng dẫn số 8 cập nhật. Thêm 3 kịch bản kiểm thử (26/26 PASS).
- Chủ tiệm: 2 lần đầu bị đánh chỉ cảnh báo (không mất máu, không phản công), lần 3 mới nổi giận; ném bom trúng hàng cũng tính là một lần đánh (`ShopConfig.warningsBeforeAngry`). Kịch bản kiểm thử cập nhật, 26/26 PASS.

- **Hệ trang bị + giải đố theo địa hình** (14/11): bom và dây thành 2 trang bị (`EquipmentDef`); nút Dùng (C / B) dùng món đang trang bị; giữ D / R1 mở cửa sổ trang bị (game dừng, trái/phải chọn, thả để trang bị, bấm nhanh để đổi). Mossroot có vách bedrock 4 ô bắt buộc dùng dây, Emberdeep có tường đá vụn bắt buộc dùng bom (1–2 chướng ngại/màn); validator chứng minh có trang bị thì qua, thiếu thì không; kho tiếp tế chống kẹt. Đổi phím tay cầm: Y/△ vào cửa/mua (R1 dành cho cửa sổ trang bị). Kiểm thử: 900/900 màn có chướng ngại bắt buộc, 31/31 kịch bản, bot qua toàn bộ.

- Sửa lỗi nhảy khi đang trèo thang sát trần làm nhân vật chui ra ngoài bản đồ (bước dò mặt đất kéo nhân vật vào trần); thêm cơ chế tự gỡ khi kẹt trong khối và 3 test hồi quy (25/25 logic, 32/32 kịch bản).

- Sửa khung thời gian góc phải bị lệch (viền khung UI vẽ lại đối xứng, dòng chữ neo theo tâm) và nhãn giá cửa hàng hiện sai "FREE" / dính nhau.

- **Khu ngoài trời Sunfall Cliffs (thử nghiệm, 14/11)**: 3 màn 3-1..3-3 giữa Emberdeep và màn cuối; bầu trời parallax, cây, dây leo, bụi gai, diều hâu (kẻ địch mới), nhạc mới; trang bị mới **tàu lượn** (nhận khi vào khu, giữ nhảy khi rơi để lượn); chướng ngại bắt buộc: vực gai 6 ô. Lỗi tìm thấy khi kiểm thử và đã sửa: tag mẫu phòng `gate_glide` ≠ `gate_glider` (mọi màn ngoài trời bị loại), bot giữ tàu lượn khi sang màn cuối (lệch đường), nền trời lộ trong giếng, cột viền nhô lên trời.
- Thùng/bình có thể rơi ra trái tim (+1 máu): thùng ~18%, bình ~5%.

- Đồ nhặt được nổi bật so với đồ trang trí: decor làm tối, đồ vật có quầng sáng nhấp nháy theo màu loại, đồ tự nhặt nhún lên xuống, mũi tên gợi ý ↓+X khi đứng gần (kịch bản kiểm tra + ảnh `Docs/Screenshots/collectibles.png`).

- Chỉ đường đến cổng (nâng cấp Compass) làm lại theo phong cách hiện đại: waypoint tròn vẽ mượt lơ lửng trên cửa ra, bám mép màn hình + chevron khi cửa ở ngoài tầm nhìn, số mét, màu hồng + icon ngọc khi mục tiêu là Heartstone (39/39 kịch bản).

- Sửa lỗi vừa giữ R1/D chọn trang bị xong phải bấm 2 lần mới đổi: bấm nhanh giờ đổi về món dùng trước đó (thay vì món kế tiếp — ở khu có 3 trang bị phải bấm 2 lần), ngưỡng giữ 0,18 → 0,25 s, cái bấm hơi lâu làm cửa sổ bật lên mà không chọn gì vẫn tính là bấm nhanh. Kịch bản mới bấm nút thật qua tay cầm ảo (40/40).

- Sửa đồ tự nhặt (vàng, ngọc, tim...) nhún lún xuống đất: hiệu ứng nhún giờ chỉ nhô lên trên vị trí nằm; kịch bản kiểm tra thêm điều kiện sprite không bao giờ thấp hơn vị trí nằm. Kịch bản bấm nút tay cầm ảo được làm cho không phụ thuộc cửa sổ có focus (chạy 2 lần liền 40/40).

- Móc dây trông như lơ lửng giữa không trung khi bám nhờ vách bên cạnh: giờ móc vẽ đúng chỗ bám (vắt qua mép vách / chốt đóng vào vách / móc trần / cắm trên mép khi thả xuống). Kịch bản mới kiểm tra cả 3 kiểu (41/41).

- Menu chính bỏ ô nhập seed (lượt nào cũng seed ngẫu nhiên; vẫn chơi lại được seed từ màn kết quả) và bỏ nút thử TRY OUTDOOR; menu còn 6 nút.

## Đang làm
- Không có hạng mục dở dang.

## Còn lại / đề xuất tiếp theo
- Khu ngoài trời hiện là bản thử: cần bạn chơi và góp ý (độ khó vực gai, số diều hâu, giao diện tàu lượn, có nên có gió/thời tiết...).
- **Chơi thử bằng tay** để tinh chỉnh cảm giác và độ khó (chưa có người chơi thật — xem TEST_REPORT §7).
- Tinh chỉnh mật độ quái/bẫy theo phản hồi; cân bằng giá cửa hàng.
- Âm thanh: đánh giá bằng tai, có thể thay bằng âm thanh do sound designer làm.
- Tùy chọn đổi phím trong game (hiện đổi được trong `GameInput.inputactions`).
- Font pixel cho UI (đang dùng font hệ thống LegacyRuntime của Unity).

## Lỗi đã biết
- Cột thông tin dùng font proportional nên không thẳng hàng tuyệt đối (đã đổi sang dạng “Nhãn: giá trị”).
- Giữ nút ↑ ở cạnh thang ngay cạnh cửa: ↑ ưu tiên leo thang (bộ sinh màn không đặt thang ở cửa).
- Bot kiểm thử chạy god mode, nên độ khó thực tế chưa đo.

## Ghi chú quyết định
- Đề bài nhắc “Big Project” nhưng thư mục làm việc được chỉ định là `Spelunky 2` (trống) và `E:\Big Project` là một dự án khác → làm toàn bộ trong `E:\Spelunky 2`, không đụng `Big Project`.
- Tên game “Gloomdelve”; chữ trong game bằng tiếng Anh, tài liệu tiếng Việt.
- Va chạm tự viết theo lưới ô thay cho Physics2D để chuyển động tất định → validator, bot và game dùng chung một code.

## Cách tiếp tục
1. Đọc file này + `Docs/TEST_REPORT.md`.
2. Mở project, chạy **Gloomdelve > Tests > Logic Tests** và **Seed Sweep** để xác nhận trạng thái.
3. Sửa code/config → **Gloomdelve > Build Everything** (nếu đổi art/prefab/scene) → **Build Windows** → chạy `Gloomdelve.exe -scenarios` và `-autotest 3`.
