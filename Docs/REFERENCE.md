# Tham chiếu: Spelunky 2 → Gloomdelve

Gloomdelve là game **tự làm**: tên, nhân vật, hình ảnh (vẽ bằng code), âm thanh/nhạc (tổng hợp bằng code), màn chơi và mã nguồn đều là sản phẩm mới. Spelunky 2 chỉ là tham chiếu về **cảm giác điều khiển, cấu trúc sinh màn và chiều sâu tương tác**. Không sao chép tên, nhân vật, hình ảnh, âm thanh, câu chữ hay màn chơi của Spelunky.

## Nguồn đã tra cứu (11/2026)

| Nguồn | Nội dung dùng | Trạng thái |
|---|---|---|
| mejoress.com – "Spelunky 2 Controls – PC, Xbox & PlayStation" (qua tìm kiếm web) | Phím mặc định PC: Z nhảy, X roi, C bom, D dây, A vào cửa/mua, Shift chạy, Esc dừng; mặc định là auto-run | Đã đọc tóm tắt từ kết quả tìm kiếm |
| tinysubversions.com/spelunkyGen (Darius Kazemi, "Spelunky Generator Lessons") | Lưới 4×4 phòng; loại phòng 0/1/2/3; đi đường bằng xúc xắc 1–5 (trái/phải/xuống); chạm biên thì đi xuống; phòng đi xuống thành loại 2, phòng nhận thành loại 3; phòng ngoài đường là loại 0 | **Đã đọc trực tiếp** |
| Game Maker's Toolkit – "How and Why Spelunky Makes its Own Levels" (bản phụ đề amara.org, qua tìm kiếm) | Mẫu phòng làm tay + ô xác suất; mỗi loại phòng có nhiều mẫu | Đã đọc tóm tắt |
| Spelunky Fandom wiki (Spelunky 2: Bomb) – qua kết quả tìm kiếm | Bom: khởi đầu 4 quả, nổ sau ~2,5 s, bán kính ~2 ô, 10 sát thương | Tóm tắt từ tìm kiếm (trang wiki trả về HTTP 402 khi tải trực tiếp) |
| Twinfinite "how to kill the ghost", tạp chí Edge "The making of Spelunky 2" (qua tìm kiếm) | Hồn ma xuất hiện ở mốc **3 phút** trong một màn; có cảnh báo trước | Tóm tắt từ tìm kiếm |
| GamePur "How to avoid arrow traps", Destructoid/Looper guides (qua tìm kiếm) | Rơi vào chông là chết; mũi tên trúng mất 2 máu và đẩy lùi; mỗi bẫy chỉ có 1 mũi tên | Tóm tắt từ tìm kiếm; một số chi tiết có thể thuộc bản HD |

**Hạn chế:** wiki Fandom chặn tải trực tiếp (HTTP 402) nên các con số lấy từ phần tóm tắt của công cụ tìm kiếm; không xem trực tiếp video gameplay. Những gì không xác minh được được ghi là *suy luận* bên dưới.

## Kết luận đã xác minh (từ nguồn)

- Sinh màn theo **lưới phòng + đường giải** (loại 0–3), đặt phòng bắt đầu ở hàng trên, phòng thoát ở hàng dưới; phòng làm tay có ô ngẫu nhiên.
- Bố cục phím PC (Z/X/C/D/A/Shift) và việc mặc định **tự chạy** (giữ nút để đi chậm).
- Bom: 4 quả khởi đầu, ngòi ~2,5 s, bán kính ~2 ô, phá địa hình.
- Chông: rơi xuống là chết.
- Bẫy tên: kích hoạt khi có vật di chuyển trước mặt, 1 mũi tên, 2 sát thương.
- Áp lực thời gian: hồn ma ở mốc 3 phút, có cảnh báo.

## Suy luận / quyết định thiết kế riêng (chưa xác minh với nguồn)

| Hạng mục | Lựa chọn trong Gloomdelve | Lý do |
|---|---|---|
| Độ cao nhảy | ~2,3–2,4 ô; với bám mép tới 3 ô | Cảm giác "nhảy lên 2 ô, bám mép lên 3 ô" quen thuộc của thể loại |
| Coyote time / bộ đệm nhảy | 0,08 s / 0,12 s | Yêu cầu của đề bài; giá trị thường dùng cho platformer chính xác |
| Bám mép | Tự động khi rơi sát mép, đang hướng về tường | Suy luận từ trải nghiệm thể loại |
| Bò xuống mép → treo | Có | Kỹ thuật an toàn đặc trưng của thể loại |
| Sát thương rơi | ≥ 9 ô: −1 máu + choáng 1 s | Suy luận; bộ kiểm tra màn không bao giờ đòi hỏi cú rơi gây thương |
| Cảnh báo hồn ma | 2:30, xuất hiện 3:00, lặp mỗi 60 s, chạm là chết | Mốc 3:00 có nguồn; mốc cảnh báo 2:30 lấy từ các bản cũ |
| Lưới phòng | 4×4 phòng 10×8 ô cho cả hai khu | Theo nguồn (4×4), kích thước phòng là suy luận |
| Thêm loại phòng 4 (cả 4 hướng) | Có | Cần khi đường đi rơi qua hai phòng liên tiếp |
| Cửa hàng | Chủ tiệm nổi giận khi bị tấn công, đồ thành miễn phí | Suy luận về chiều sâu tương tác của thể loại |
| Tượng vàng | Mang ra cửa được +1200 vàng; vật nặng (chậm hơn) | Rủi ro/phần thưởng kiểu thể loại |

## Những gì cố ý **không** làm giống
- Không dùng tên khu vực, quái, vật phẩm, nhân vật hay câu thoại của Spelunky.
- Không có nhiều người chơi/online (ngoài phạm vi bản đầu).
- Hình ảnh và âm thanh hoàn toàn tự sinh, phong cách riêng.
