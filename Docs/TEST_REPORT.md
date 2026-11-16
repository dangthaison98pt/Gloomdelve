# Báo cáo kiểm thử – Gloomdelve v0.1.0 (14/11/2026)

Môi trường: Windows 11, Unity 6000.3.12f1, URP 17.3, Input System 1.19. Tất cả kết quả dưới đây là từ các lần chạy **thật**; file kết quả gốc nằm trong `Docs/TestResults/`.

## 1. Biên dịch & build
| Kiểm tra | Kết quả |
|---|---|
| Biên dịch toàn bộ script (runtime + editor) ở batch mode | ✅ 0 lỗi |
| `Gloomdelve > Build Everything` (art, âm thanh, asset, prefab, 2 scene) | ✅ 397 sprite, 42 SFX, 4 nhạc |
| Build Windows x64 → `Builds/Windows/Gloomdelve.exe` | ✅ Succeeded, 94 MB, 0 lỗi |
| Chạy bản build (cửa sổ 1280×720) | ✅ chạy được; 0 exception trong `player.log` của các lần chạy tự động |

## 2. Test logic (Editor) – `logic_tests.txt`: **25/25 PASS**
Độ cao nhảy (2,27 ô) · nhảy chạm nhẹ thấp hơn (0,99 ô) · coyote time · bộ đệm nhảy · nhảy lên bậc 2 ô · bám mép tường 3 ô · trèo lên từ mép · leo lên/xuống thang · bò ra mép → treo · rơi vào chông chết / đi ngang an toàn · báo độ cao rơi · ↓+nhảy xuyên sàn gỗ · validator: hành lang phẳng qua được / tường chặn / hố 5 ô không nhảy qua / tường 4 ô không leo / tường 3 ô leo được nhờ bám mép · nhảy khỏi thang sát trần không chui vào trần (hồi quy) · nhảy chạm sàn gỗ từ dưới không bị bật lên trên (hồi quy) · lượt mới không giữ trạng thái cũ · hiệu ứng mua đồ · mẫu phòng phủ mọi loại · cùng seed → cùng màn.

## 3. Quét seed tự động – `seed_sweep.txt`
| Chỉ số | Kết quả |
|---|---|
| Seed | 1 … 150 (×9 màn sinh ngẫu nhiên = **1350 màn**) |
| Màn không có đường đi hợp lệ | **0** |
| Lỗi an toàn lối vào / vị trí spawn | **0** (không bẫy/quái gần lối vào, không bẫy tên nhắm lối vào, không vật rơi trên lối vào, không spawn trong tường) |
| Sinh lại do validator loại (attempt bị loại) | 467 lần trên 1350 màn, tối đa 8 attempt/màn (318 exit unreachable, 145 exit + shop unreachable, 4 shop unreachable), đều được sinh lại thành công |
| **Chướng ngại bắt buộc theo trang bị** | **1350/1350 màn** có chướng ngại (1522 chướng ngại: 508 vách dây, 520 tường đá vụn, 494 vực gai cần tàu lượn); mỗi màn được chứng minh *qua được khi có* trang bị và *không qua được khi thiếu* |
| Thời gian sinh + kiểm tra (2 lần tìm đường) | trung bình 77,8 ms/màn, tối đa 289 ms |
| Cửa hàng | 602 cửa hàng được đặt (450 màn bắt buộc có cửa hàng) |
| Phòng tượng vàng | 475 |
| Màn cuối | validator OK, kế hoạch bot 882 frame |
| Màn hướng dẫn | phân tích OK, 9 biển |

**Seed lỗi và cách sửa:** các attempt bị loại được ghi đầy đủ (seed/màn/số attempt) trong `seed_sweep.txt`. Nguyên nhân chung là tổ hợp phòng tạo bức tường > 3 ô hoặc hố rơi quá cao; cơ chế sửa là validator loại và sinh lại (tất định theo seed), sau đó bot xác nhận lại trong game. Lần chạy đầu phát hiện 1 test logic sai (khối 1 ô quá hẹp nên nhân vật nhảy qua) — đã sửa test. Khi thêm chướng ngại, lần quét đầu có 1203 attempt bị loại vì đường giải không có phòng ngang để đặt chướng ngại (thời gian tối đa 516 ms) — đã sửa: bộ sinh đi lại đường giải trong cùng attempt.

## 4. Bot chơi trong bản build thật – `autotest_report.txt`
`Gloomdelve.exe -autotest N -autotestSeed S`: bot phát lại input mà validator tìm được qua **PlayerController thật** (ở màn có chướng ngại, dây được móc sẵn đúng vị trí validator tính và đá vụn được phá trước — việc ném dây/đặt bom thật được kiểm tra riêng bằng kịch bản) (god mode để quái/bẫy không đẩy lệch đường), tự vào cửa, nhặt Heartstone ở màn cuối và mang ra cổng.
| Lần chạy | Kết quả |
|---|---|
| Seed 1101 (lần chạy mới nhất, 12:22 ngày 14/11, đủ 3 khu + màn cuối) — file `autotest_report.txt` hiện lưu | 10/10 màn PASS, **thắng**, lượt mới sạch trạng thái, 0 rò rỉ |
| Các lần chạy trước khi có khu ngoài trời (7 màn/lượt): seed 1–2, 101–106, 7, 300–301 | 77/77 màn PASS, 11/11 lượt thắng — *file kết quả gốc đã bị ghi đè, không còn lưu* |

## 5. Kịch bản tương tác trong game – `scenario_report.txt`: **41/41 PASS** (14/11, thêm kiểm tra hình móc dây bám mép/vách/trần)
`Gloomdelve.exe -scenarios` dựng đấu trường nhỏ trong scene Game thật, điều khiển nhân vật bằng input kịch bản rồi kiểm tra kết quả:
roi giết rắn đang lao tới · giẫm đầu rắn · nhặt đá ném chết rắn · bom phá đất nhưng không phá bedrock, người chơi chạy thoát · dây chuyền 37 bom kết thúc trong giới hạn · ném đá kích bẫy tên, mũi tên gây 2 sát thương · thạch nhũ rơi giết rắn · rơi vào chông chết (đúng nguyên nhân) · ném dây, leo, lên mép cao · móc dây bám đúng chỗ giữ: vắt qua mép vách, chốt đóng vào vách, móc lên trần (ảnh `Docs/Screenshots/rope_lip.png`, `rope_piton.png`) · dây ném vào khoảng không không móc được và rơi lại thành cuộn dây · dây móc biến mất khi hết thời gian · dây không biến mất khi đang leo · cửa sổ trang bị dừng game, chọn rồi thả thì trang bị đúng món, bấm nhanh đổi món · đổi trang bị bằng tay cầm ảo qua Input System (giữ R1 + gạt phải → dây; 1 lần bấm → về bom; bấm 0,3 s vẫn đổi; giữ lâu không chọn → giữ nguyên) · nút Dùng chỉ dùng món đang trang bị · vách 4 ô không qua được nếu không có dây · ném dây sát vách thì leo qua được · 1 quả bom phá tường đá vụn, bedrock còn nguyên, đi qua được · bám mép và trèo (controller thật) · leo thang · leo thang sát trần rồi nhảy nhiều lần vẫn ở trong bản đồ, không lọt vào khối nào · lượn qua vực gai rộng 6 ô · không có tàu lượn thì rơi vào vực · lượn từ trên cao xuống không mất máu · diều hâu bổ nhào trúng người chơi · trái tim hồi 1 máu · mua túi bom · chủ tiệm: 2 lần bị quất roi chỉ cảnh báo, không mất máu; lần 3 nổi giận, mất máu, đồ miễn phí · khối nghiền gây 3 · lỗ lửa đốt người chơi và châm nổ bom · roi đập vỡ bình · rơi cao mất máu + choáng · menu tạm dừng đóng băng mô phỏng · bảng cài đặt lưu âm lượng · màn kết quả sau khi chết · hồn ma áp lực thời gian xuất hiện và chạm là chết · mang tượng vàng ra cửa được +1200 · cửa ra kết thúc màn · waypoint: không có Compass thì ẩn, cửa ở xa thì bám mép phải + chevron, lại gần thì lơ lửng ngay trên cửa + mũi nhọn (ảnh `Docs/Screenshots/waypoint_edge.png`, `waypoint_onscreen.png`).

## 5b. Lỗi người chơi báo và đã sửa
- **Nhảy khi đang trèo thang sát trần → chui qua đất/bedrock ra ngoài bản đồ.** Nguyên nhân: bước dò mặt đất kéo nhân vật lên đứng trên ô thang trên cùng (hoặc sàn gỗ) khi chân còn nằm sâu trong ô đó, đẩy thân vào trần; mỗi lần nhảy lại bị đẩy lên thêm. Sửa: chỉ bám đất khi chân sát đúng mặt ô; thêm lớp tự gỡ khi lỡ kẹt trong khối. Đã thêm test logic + kịch bản trong game; quét 150 seed và bot chạy lại đều sạch.
- **Khung thời gian góc phải bị lệch.** Viền khung vẽ kiểu nổi khối nên phóng to trông như hai khung chồng lệch; các dòng chữ neo theo góc nên dòng TOTAL lòi khỏi khung. Sửa: viền đối xứng bốn cạnh cho mọi khung UI, các dòng neo theo tâm; nới ô trang bị. Kiểm tra bằng ảnh chụp từ bản build.
- **Nhãn giá cửa hàng hiện "FREE" dù chủ tiệm không giận, và nhãn các món dính nhau** (phát hiện khi xem ảnh). Sửa: nhãn cập nhật khi trạng thái chủ tiệm thay đổi, nhãn xen kẽ cao/thấp; kịch bản cửa hàng kiểm tra thêm nhãn "$350" khi bình thường và "FREE" khi chủ tiệm giận (32/32 PASS).

## 6. Kiểm tra hình ảnh
Đã xem ảnh chụp thật từ bản build: menu chính, bảng điều khiển, HUD + gameplay khu 1/khu 2/màn cuối, cài đặt, màn kết quả (`Docs/Screenshots`). Đã sửa sau khi xem: viền panel/nút 9-slice quá dày, tiêu đề đè phụ đề, hình trang trí đè nút, banner hiện xuyên menu, banner lặp tên khu, cột số liệu lệch.

## 7. Chưa kiểm chứng / giới hạn
- **Test logic, quét seed và bot chạy lần cuối lúc 12:20–12:22 ngày 14/11**; sau đó còn sửa `RopeProjectile`, `RunState`, `EquipmentWheel`, `HUD`, `Highlight`, `MainMenuController`, `UIUtil` và chỉ bộ kịch bản (mục 5) được chạy lại lúc 21:47 trên bản build mới nhất. Ba bộ kia cần chạy lại để xác nhận.
- **Chưa có người chơi thật bằng bàn phím.** Môi trường làm việc không cho phép tôi điều khiển game bằng tay; “chơi thử” được thay bằng bot phát lại input thật qua controller thật + kịch bản tương tác + xem ảnh chụp. Cảm giác tay (độ nhạy, độ khó, nhịp độ) cần bạn chơi thử và phản hồi.
- Màn hướng dẫn không được bot chơi trọn (cần bom và dây); các thao tác trong đó được kiểm tra riêng bằng kịch bản. Bố cục đã kiểm tra bằng phân tích + ASCII map.
- Âm thanh/nhạc chỉ được kiểm tra là tạo file và phát không lỗi; chưa nghe đánh giá chất lượng.
- Bot chạy god mode nên không kiểm tra độ khó thực tế khi có quái/bẫy (độ khó cần tinh chỉnh qua chơi thật).
- Chưa thử trên máy khác, độ phân giải khác 1280×720 (UI dùng CanvasScaler 1920×1080 nên co giãn được), hay tay cầm thật.

## 8. Cách chạy lại
```
Unity -batchmode -projectPath "<project>" -executeMethod Gloomdelve.EditorTools.GameTests.BatchAll -seeds 150 -logFile Logs/tests.log
Builds/Windows/Gloomdelve.exe -scenarios -autotestOut <thư mục>
Builds/Windows/Gloomdelve.exe -autotest 6 -autotestSeed 101 -autotestOut <thư mục> [-autotestShots]
```
Hoặc trong Editor: menu **Gloomdelve > Tests**.
