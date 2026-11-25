# Báo cáo kiểm thử – Brindlemoor: The Sunstone Hunt (Gloomdelve) – cập nhật 25/11/2026 (số liệu chạy thật mới nhất: 19/11)

## Số liệu mới nhất
Game hiện có 8 khu (bốc 4 mỗi lượt), 24 màn sinh ngẫu nhiên trong danh mục, 9 boss (trùm cuối Eclipse King), làng Brindlemoor, 3 ô lưu. Mỗi dòng dưới đây là một lần chạy thật; file gốc trong `Docs/TestResults/` (kịch bản chạy lẻ ghi vào `%USERPROFILE%\AppData\LocalLow\Brindlemoor Team\Brindlemoor\autotest\`).

| Kiểm tra | Kết quả | Ngày |
|---|---|---|
| Test logic (`logic_tests.txt`) | **65/65 PASS** | 25/11 |
| Quét seed (`seed_sweep.txt`), seed 1–20 × 24 màn | **480 màn, 0 màn không có đường, 0 vấn đề an toàn lối vào**, 287 lần sinh lại (tối đa 14 attempt/màn), trung bình 197 ms/màn; 180 màn có chướng ngại bắt buộc (219 cổng: 70 dây, 71 bom, 78 lượn), mỗi cổng được chứng minh hai chiều; 19 màn có lồng dân làng. Ghi chú: seed 12 màn 4-1 không đặt được shop bắt buộc | 19/11 |
| Kịch bản trong build, chạy đủ (`scenario_report.txt`) | **79/79 PASS** | 18/11 |
| Kịch bản đổi phím (`-scenarioFilter "keyboard keys"`) sau khi đổi phím mặc định | **1/1 PASS** | 19/11 |
| Kịch bản chạy lẻ sau khi sửa shop làng và thêm hàng mới (`-scenarioFilter "new wares,village,map:,long fall,stomp,stalactite,story,shop"`) | **16/16 PASS** | 25/11 |
| Bot chơi trọn 3 lượt (`autotest_report.txt`, seed 201–203) | **54/54 chặng PASS**, 3/3 lượt tới VICTORY, 0 rò rỉ trạng thái (bot không đánh boss) | 17/11 |

Hiện có **103 kịch bản** trong `ScenarioRunner` (25/11). Lần chạy đủ gần nhất (18/11) có 79, nên chưa có lần chạy đủ 103/103 trên bản hiện tại. Các thay đổi từ 19/11 tới 24/11 chưa có số liệu kiểm thử nào ghi vào file này. Bot chưa chạy lại từ khi có làng, ô lưu, Sandsunk, Geode, Cogwyrm, ná, Eclipse King và kết thúc thật; bot cũng chưa đi mạch truyện cây búa.

Switch dòng lệnh hiện có: `-scenarios`, `-scenarioFilter a,b`, `-autotest`, `-autotestSeed`, `-autotestOut`, `-autotestShots`, `-saveDir`, `-loadingShot dir`, `-toggleShot`, `-shotEvery N dir`, `-bossTest N`, `-tutorialTest`, `-villageTest H`, `-villageX x`; khi chạy kiểm thử, game giữ cỡ màn từ `-screen-width / -screen-height / -screen-fullscreen` của Unity.

Các mục bên dưới là báo cáo chi tiết của các đợt trước (giữ lại làm lịch sử, số liệu theo phiên bản lúc đó).

---

## Lịch sử (16–17/11)

> **Cập nhật 17/11 (thêm khu nấm tối Sporegloom, 15 màn sinh ngẫu nhiên mỗi lượt):** test logic 34/34; quét 150 seed = **2250 màn, 0 màn không có đường, 0 vấn đề an toàn, 568 lần sinh lại**; kịch bản trong build **65/65** (4 kịch bản mới: nấm nảy, puffshroom + mặt nạ, gloom moth, lurecap); bot chơi trọn 3 lượt (seed 101-103) **63/63 chặng**.
>
> **Cập nhật 16/11 (thêm khu băng Frostdeep Hollows, 12 màn sinh ngẫu nhiên):** quét 150 seed = **1800 màn, 0 lỗi, 0 vấn đề an toàn, 456 lần sinh lại (sau khi khu Sunfall chuyển sang kiểu leo lên)**; bot chơi trọn 3 lượt = **54/54 chặng** (18 chặng/lượt: 12 màn, 4 đấu trường boss, trại, hầm cuối; bot không đánh boss); 52/52 kịch bản (thêm quái băng, trang bị, boss, rương, chủ shop ghi thù, bẫy leo); 34/34 test logic. Các bảng bên dưới là số liệu của đợt trước (9 màn/lượt, chưa có khu băng); khu băng không có chướng ngại bắt buộc theo trang bị.

Môi trường: Windows 11, Unity 6000.3.12f1, URP 17.3, Input System 1.19. Tất cả kết quả dưới đây là từ các lần chạy **thật**; file kết quả gốc nằm trong `Docs/TestResults/`.

## 1. Biên dịch & build
| Kiểm tra | Kết quả |
|---|---|
| Biên dịch toàn bộ script (runtime + editor) ở batch mode | ✅ 0 lỗi |
| `Brindlemoor > Build Everything` (art, âm thanh, asset, prefab, 2 scene) | ✅ 397 sprite, 42 SFX, 4 nhạc |
| Build Windows x64 → `Builds/Windows/Brindlemoor.exe` | ✅ Succeeded, 94 MB, 0 lỗi |
| Chạy bản build (cửa sổ 1280×720) | ✅ chạy được; 0 exception trong `player.log` của các lần chạy tự động |

## 2. Test logic (Editor) – `logic_tests.txt`: **34/34 PASS** (16/11)
Độ cao nhảy (2,27 ô) · nhảy chạm nhẹ thấp hơn (0,99 ô) · coyote time · bộ đệm nhảy · nhảy lên bậc 2 ô · bám mép tường 3 ô · trèo lên từ mép · leo lên/xuống thang · bò ra mép → treo · rơi vào chông chết / đi ngang an toàn · báo độ cao rơi · ↓+nhảy xuyên sàn gỗ · validator: hành lang phẳng qua được / tường chặn / hố 5 ô không nhảy qua / tường 4 ô không leo / tường 3 ô leo được nhờ bám mép · nhảy khỏi thang sát trần không chui vào trần (hồi quy) · nhảy chạm sàn gỗ từ dưới không bị bật lên trên (hồi quy) · lượt mới không giữ trạng thái cũ (kể cả nâng cấp và bộ đếm tùy biến) · hiệu ứng mua đồ + chỉ số cộng thêm · nâng cấp chỉ áp dụng một lần · tài nguyên tùy biến · mọi asset trang bị đều có hành vi · mẫu phòng phủ mọi loại · file mẫu phòng đọc → ghi lại không đổi · kiểm tra quy tắc: 60 phòng có sẵn không lỗi (17 cảnh báo, liệt kê trong `room_lint.txt`), bắt được phòng thiếu lối xuống và ký hiệu lạ · ép một mẫu phòng vào bộ sinh màn · ký hiệu tùy biến sinh đúng prefab · cùng seed → cùng màn.

## 3. Quét seed tự động – `seed_sweep.txt`
| Chỉ số | Kết quả |
|---|---|
| Seed | 1 … 150 (×9 màn sinh ngẫu nhiên = **1350 màn**) |
| Màn không có đường đi hợp lệ | **0** |
| Lỗi an toàn lối vào / vị trí spawn | **0** (không bẫy/quái gần lối vào, không bẫy tên nhắm lối vào, không vật rơi trên lối vào, không spawn trong tường) |
| Sinh lại do validator loại (attempt bị loại) | 467 lần trên 1350 màn, tối đa 8 attempt/màn (318 exit unreachable, 145 exit + shop unreachable, 4 shop unreachable), đều được sinh lại thành công |
| **Chướng ngại bắt buộc theo trang bị** | **1350/1350 màn** có chướng ngại (1522 chướng ngại: 508 vách dây, 520 tường đá vụn, 494 vực gai cần tàu lượn); mỗi màn được chứng minh *qua được khi có* trang bị và *không qua được khi thiếu* |
| Thời gian sinh + kiểm tra (2 lần tìm đường) | trung bình 82,3 ms/màn, tối đa 337 ms |
| So với trước khi tái cấu trúc (14/11) | `seed_sweep.txt` **giống hệt** từng dòng (trừ thời gian chạy): cùng 467 lần sinh lại, cùng thống kê mẫu phòng |
| Cửa hàng | 602 cửa hàng được đặt (450 màn bắt buộc có cửa hàng) |
| Phòng tượng vàng | 475 |
| Màn cuối | validator OK, kế hoạch bot 882 frame |
| Màn hướng dẫn | phân tích OK, 9 biển |

**Seed lỗi và cách sửa:** các attempt bị loại được ghi đầy đủ (seed/màn/số attempt) trong `seed_sweep.txt`. Nguyên nhân chung là tổ hợp phòng tạo bức tường > 3 ô hoặc hố rơi quá cao; cơ chế sửa là validator loại và sinh lại (tất định theo seed), sau đó bot xác nhận lại trong game. Lần chạy đầu phát hiện 1 test logic sai (khối 1 ô quá hẹp nên nhân vật nhảy qua) — đã sửa test. Khi thêm chướng ngại, lần quét đầu có 1203 attempt bị loại vì đường giải không có phòng ngang để đặt chướng ngại (thời gian tối đa 516 ms) — đã sửa: bộ sinh đi lại đường giải trong cùng attempt.

## 4. Bot chơi trong bản build thật – `autotest_report.txt`
`Brindlemoor.exe -autotest N -autotestSeed S`: bot phát lại input mà validator tìm được qua **PlayerController thật** (ở màn có chướng ngại, dây được móc sẵn đúng vị trí validator tính và đá vụn được phá trước — việc ném dây/đặt bom thật được kiểm tra riêng bằng kịch bản) (god mode để quái/bẫy không đẩy lệch đường), tự vào cửa, nhặt Heartstone ở màn cuối và mang ra cổng.
| Lần chạy | Kết quả |
|---|---|
| Seed 101–103 (16/11, sau khi tái cấu trúc, đủ 3 khu + màn cuối) — file `autotest_report.txt` hiện lưu | 30/30 màn PASS, 3/3 lượt **thắng**, lượt mới sạch trạng thái, 0 rò rỉ |
| Seed 1101 (14/11) | 10/10 màn PASS, thắng — *file đã bị ghi đè* |
| Các lần chạy trước khi có khu ngoài trời (7 màn/lượt): seed 1–2, 101–106, 7, 300–301 | 77/77 màn PASS, 11/11 lượt thắng — *file kết quả gốc đã bị ghi đè, không còn lưu* |

## 5. Kịch bản tương tác trong game – `scenario_report.txt`: **65/65 PASS** (chạy lại 17/11 trên bản build có khu Sporegloom)
`Brindlemoor.exe -scenarios` dựng đấu trường nhỏ trong scene Game thật, điều khiển nhân vật bằng input kịch bản rồi kiểm tra kết quả:
roi giết rắn đang lao tới · giẫm đầu rắn · nhặt đá ném chết rắn · bom phá đất nhưng không phá bedrock, người chơi chạy thoát · dây chuyền 37 bom kết thúc trong giới hạn · ném đá kích bẫy tên, mũi tên gây 2 sát thương · thạch nhũ rơi giết rắn · rơi vào chông chết (đúng nguyên nhân) · ném dây, leo, lên mép cao · móc dây bám đúng chỗ giữ: vắt qua mép vách, chốt đóng vào vách, móc lên trần (ảnh `Docs/Screenshots/rope_lip.png`, `rope_piton.png`) · dây ném vào khoảng không không móc được và rơi lại thành cuộn dây · dây móc biến mất khi hết thời gian · dây không biến mất khi đang leo · cửa sổ trang bị dừng game, chọn rồi thả thì trang bị đúng món, bấm nhanh đổi món · đổi trang bị bằng tay cầm ảo qua Input System (giữ R1 + gạt phải → dây; 1 lần bấm → về bom; bấm 0,3 s vẫn đổi; giữ lâu không chọn → giữ nguyên) · nút Dùng chỉ dùng món đang trang bị · vách 4 ô không qua được nếu không có dây · ném dây sát vách thì leo qua được · 1 quả bom phá tường đá vụn, bedrock còn nguyên, đi qua được · bám mép và trèo (controller thật) · leo thang · leo thang sát trần rồi nhảy nhiều lần vẫn ở trong bản đồ, không lọt vào khối nào · lượn qua vực gai rộng 6 ô · không có tàu lượn thì rơi vào vực · lượn từ trên cao xuống không mất máu · diều hâu bổ nhào trúng người chơi · trái tim hồi 1 máu · mua túi bom · chủ tiệm: 2 lần bị quất roi chỉ cảnh báo, không mất máu; lần 3 nổi giận, mất máu, đồ miễn phí · khối nghiền gây 3 · lỗ lửa đốt người chơi và châm nổ bom · roi đập vỡ bình · rơi cao mất máu + choáng · menu tạm dừng đóng băng mô phỏng · bảng cài đặt lưu âm lượng · màn kết quả sau khi chết · hồn ma áp lực thời gian xuất hiện và chạm là chết · mang tượng vàng ra cửa được +1200 · cửa ra kết thúc màn · waypoint: không có Compass thì ẩn, cửa ở xa thì bám mép phải + chevron, lại gần thì lơ lửng ngay trên cửa + mũi nhọn (ảnh `Docs/Screenshots/waypoint_edge.png`, `waypoint_onscreen.png`).

## 5b. Lỗi người chơi báo và đã sửa
- **Nhảy khi đang trèo thang sát trần → chui qua đất/bedrock ra ngoài bản đồ.** Nguyên nhân: bước dò mặt đất kéo nhân vật lên đứng trên ô thang trên cùng (hoặc sàn gỗ) khi chân còn nằm sâu trong ô đó, đẩy thân vào trần; mỗi lần nhảy lại bị đẩy lên thêm. Sửa: chỉ bám đất khi chân sát đúng mặt ô; thêm lớp tự gỡ khi lỡ kẹt trong khối. Đã thêm test logic + kịch bản trong game; quét 150 seed và bot chạy lại đều sạch.
- **Khung thời gian góc phải bị lệch.** Viền khung vẽ kiểu nổi khối nên phóng to trông như hai khung chồng lệch; các dòng chữ neo theo góc nên dòng TOTAL lòi khỏi khung. Sửa: viền đối xứng bốn cạnh cho mọi khung UI, các dòng neo theo tâm; nới ô trang bị. Kiểm tra bằng ảnh chụp từ bản build.
- **Nhãn giá cửa hàng hiện "FREE" dù chủ tiệm không giận, và nhãn các món dính nhau** (phát hiện khi xem ảnh). Sửa: nhãn cập nhật khi trạng thái chủ tiệm thay đổi, nhãn xen kẽ cao/thấp; kịch bản cửa hàng kiểm tra thêm nhãn "$350" khi bình thường và "FREE" khi chủ tiệm giận (32/32 PASS).

## 6. Kiểm tra hình ảnh
Đã xem ảnh chụp thật từ bản build: menu chính, bảng điều khiển, HUD + gameplay khu 1/khu 2/màn cuối, cài đặt, màn kết quả (`Docs/Screenshots`). Đã sửa sau khi xem: viền panel/nút 9-slice quá dày, tiêu đề đè phụ đề, hình trang trí đè nút, banner hiện xuyên menu, banner lặp tên khu, cột số liệu lệch.

## 7. Chưa kiểm chứng / giới hạn
- **Room Template Editor**: phần đọc/ghi file, kiểm tra quy tắc và chạy thử qua bộ sinh màn có test tự động; giao diện mới được kiểm tra bằng một ảnh chụp cửa sổ thật (`Docs/Screenshots/room_editor.png`). Thao tác vẽ bằng chuột, nút Lưu và nút **Chơi thử** chưa có ai bấm thử bằng tay.
- Các điểm mở rộng mới (trang bị mới, ký hiệu tùy biến trong màn làm tay, nâng cấp tự thêm) được test ở mức logic; chưa có nội dung mới thật nào được thêm qua chúng.
- **Chưa có người chơi thật bằng bàn phím.** Môi trường làm việc không cho phép tôi điều khiển game bằng tay; “chơi thử” được thay bằng bot phát lại input thật qua controller thật + kịch bản tương tác + xem ảnh chụp. Cảm giác tay (độ nhạy, độ khó, nhịp độ) cần bạn chơi thử và phản hồi.
- Màn hướng dẫn không được bot chơi trọn (cần bom và dây); các thao tác trong đó được kiểm tra riêng bằng kịch bản. Bố cục đã kiểm tra bằng phân tích + ASCII map.
- Âm thanh/nhạc chỉ được kiểm tra là tạo file và phát không lỗi; chưa nghe đánh giá chất lượng.
- Bot chạy god mode nên không kiểm tra độ khó thực tế khi có quái/bẫy (độ khó cần tinh chỉnh qua chơi thật).
- Chưa thử trên máy khác, độ phân giải khác 1280×720 (UI dùng CanvasScaler 1920×1080 nên co giãn được), hay tay cầm thật.

## 8. Cách chạy lại
```
Unity -batchmode -projectPath "<project>" -executeMethod Gloomdelve.EditorTools.GameTests.BatchAll -seeds 150 -logFile Logs/tests.log
Unity -batchmode -projectPath "<project>" -executeMethod Gloomdelve.EditorTools.GameTests.BatchLogic -logFile Logs/tests_logic.log
Builds/Windows/Brindlemoor.exe -scenarios -autotestOut <thư mục>
Builds/Windows/Brindlemoor.exe -scenarios -scenarioFilter "từ1,từ2"      (chỉ các kịch bản có tên chứa các từ đó)
Builds/Windows/Brindlemoor.exe -autotest 6 -autotestSeed 101 -autotestOut <thư mục> [-autotestShots]
Builds/Windows/Brindlemoor.exe -bossTest N [-shotEvery N <thư mục>]      (vào thẳng boss thứ N của danh sách Challenge)
```
Chạy bản build để kiểm thử luôn ở cửa sổ nhỏ (`-screen-fullscreen 0 -screen-width 1280 -screen-height 720`; game tự ép cửa sổ khi có cờ kiểm thử).
Hoặc trong Editor: menu **Brindlemoor > Tests**.
