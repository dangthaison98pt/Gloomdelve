# Brindlemoor: The Sunstone Hunt – Thiết kế

Platformer roguelike 2D pixel art. Brann rời làng Brindlemoor đang chìm trong mùa đông, leo qua núi hang sinh ngẫu nhiên để mang **Sunstone** từ đáy *The Sunken Vault* về. (Namespace trong code: Gloomdelve; trong code viên đá vẫn là `heartstone`.)

**Cốt truyện thật:** Sunstone không phải mặt trời. Nó giữ Brindlemoor mãi trong một ngày mùa đông. Mỗi lượt kết thúc (chết, bỏ cuộc hay mang đá về), Brann lại thức dậy trên giường vào đúng ngày đó, và chỉ mình Brann nhớ. Muốn thoát vòng lặp thì phải đập vỡ viên đá bằng cây búa ghép từ ba mảnh tìm được trên núi (mục 1, "Kết thúc thật"). Mạch truyện nằm trong `Scripts/Game/Story.cs`.

## 1. Cấu trúc lượt chơi
`RunPlan.Build` dựng lượt từ danh mục `GameConfig.stages`: mỗi khu là một khối (3 màn sinh ngẫu nhiên + 1 đấu trường boss). Mỗi lượt bốc `areasPerRun` = **4 khu** theo seed, khu nào cũng có thể mở đầu (`firstAreaFixed` = 0). Tên chặng đánh số theo vị trí trong lượt (1-1 … 4-4), nên "3-2" là màn 2 của khu thứ ba được bốc.

| Chặng | Nội dung |
|---|---|
| Làng **Brindlemoor** | Mọi lượt bắt đầu ở làng (trừ NEW RUN sau khi chết: vào thẳng 1-1). Không đồng hồ, không vũ khí, không đồ dùng. Lần đầu, cổng núi khoá tới khi xong nhiệm vụ "A Word with the Elder" |
| 4 khu × (3 màn + boss) | Màn sinh theo seed, được chứng minh qua được; đấu trường boss làm tay (`Levels/Boss_*.txt`), cửa ra khoá tới khi hạ boss |
| **The Last Camp** | Rương 3 món trang bị chưa có + shop 5 món |
| **The Sunken Vault** | Đại sảnh trống, không bẫy, không quái khác; trùm cuối **Eclipse King** (`Levels/FinalVault.txt`). Hạ nó thì nó rơi Sunstone; nhấc đá lên thì cổng mở (hầm không sập) |

Tổng 18 chặng. **Hướng dẫn:** game mới luôn bắt đầu bằng màn hướng dẫn (menu chính không còn nút TUTORIAL; menu tạm dừng có SKIP TUTORIAL). Màn làm tay, 3 bom 3 dây, chết thì làm lại. Cuối màn có rương chứa **Field Guide**, và cổng chỉ mở sau khi đã đọc sách (bỏ qua hướng dẫn thì vẫn được tặng sách). Xong thì sang làng.

**Thắng thường:** mang Sunstone ra cổng thì được +2000 vàng (`heartstoneBonusGold`). Sau đó là cảnh kết (`EndingScene`): làng về đêm, Brann đặt viên đá trước trưởng làng nhưng tuyết không tan, viên đá xoay và hút cả khung cảnh vào nó. Tiếp theo là bảng số liệu của lượt (một nút CONFIRM, có dòng báo có gì đó không ổn), rồi Brann thức dậy trên giường.

**Kết thúc thật:** cần cây búa **Stonebreaker** trong túi và Eclipse King đã chết. Khi đó banner gợi ý "Lift it - or take the hammer to it". Đập búa vào viên đá (đang nằm hay đang mang đều được) thì đá vỡ, đại sảnh ngập ánh sáng và làng thức dậy vào buổi sáng hôm sau (trời sáng, không tuyết). Màn kết quả ghi "THE WINTER IS OVER", sau đó là credits rồi về màn hình chính. Lượt tính là thắng nhưng không có thưởng 2000 vàng; save nhận key item `true_end`.

**Thức dậy:** sau mỗi lượt kết thúc, Brann thức dậy trên giường vào cùng một ngày. Lần chết đầu tiên của một save có một cảnh ngắn (chỉ xem một lần, `loopSeen`); các lần sau chỉ hiện một câu: sau khi thắng luôn là cùng một câu, sau khi chết thì câu xoay vòng.

**Chết:** màn kết quả (nguyên nhân, chặng tới được, vàng, thời gian, số quái giết, số màn, seed) với **NEW RUN** (seed mới, vào thẳng màn đầu) hoặc **BACK TO VILLAGE**. Màn kết quả không còn hiện kỷ lục. **Bỏ cuộc** (menu tạm dừng > GIVE UP, có hỏi lại): kết thúc lượt và thức dậy ở làng; tính là một lượt đã chơi nhưng không tính là một lần chết.

**Mất / giữ:** mất vàng, trang bị, các mảnh búa và tác dụng đã mua cho lượt đó (Lamp Oil, Heart Draught). Giữ lại bom, dây, đá, Flare, giấy còn trong túi (kho của save), ember, spark, dân làng đã cứu, key item (Ember Pouch, Field Guide, Map, bản chép, lời giải của trưởng làng…), cỡ túi đã nới, nhiệm vụ và số quái đã giết.

**Lưu:** 3 ô lưu (`slot1..3.json`), mỗi ô có làng, tiền, key item, nhiệm vụ và Field Guide riêng. Cài đặt (`settings.json`), tổng kết (`totals.json`) và kỷ lục Challenge (`challenge.json`) dùng chung cho cả game. Lượt đang chơi được lưu ở **đầu mỗi màn** (seed, vị trí, máu, vàng, đồ); tiếp tục thì chơi lại từ đầu màn đó. Dân làng được cứu, boss đã hạ và tiền nhặt được ghi ngay. Tutorial và Challenge không lưu vào ô.

**Challenge** (menu): đánh thẳng từng boss (8 boss khu + Eclipse King) với nhân vật mới. Chỉ mang ná và bom (10 máu, 5 bom, 10 viên đá, không dây, không Recall Charm); túi ở cỡ ban đầu dù trưởng làng đã nới bao nhiêu. Có Field Guide mở mọi trang. Không đọc hay ghi ô lưu nào (`SaveSystem.Apart`), không có thần chết, boss không rơi gì. Chết thì đánh lại ngay; thắng thì xem thời gian và máu đã mất, chọn FIGHT AGAIN / BOSS LIST. Thời gian tốt nhất và huy hiệu không mất máu (`[flawless]`) của từng boss lưu trong `challenge.json`.

**Records** (menu): tổng kết toàn game, cộng mọi ô lưu (`totals.json`): số lượt, số lần thắng, số lần chết, lần thắng nhanh nhất, số quái giết (không tính boss), số boss đã hạ; cột phải là thời gian tốt nhất Challenge của từng boss. Challenge không bao giờ được cộng vào tổng kết. Lần đầu chạy bản mới, các `records.json` cũ được cộng dồn vào file này.

### Mạch truyện: The Old Signs và cây búa (`Story.cs`, `Data/Quests`)
1. Màn 1 của mỗi khu có một **cột đá khắc chữ** (`RunePillar`) ở chỗ đi tới được. Xem cột lần đầu thì mở nhiệm vụ **"The Old Signs"**.
2. Mua **Sheet of Paper** ở Pip (tiệm lặt vặt). Có giấy trong túi thì chép được chữ trên cột, nhận key item **Copy of the Old Signs** (`rubbing`, mỗi lần chép tốn một tờ).
3. Mang bản chép cho trưởng làng Aldous đọc, nhận key item **The Elder's Reading** (`decoded`, giữ theo save).
4. Từ đó mỗi lượt giấu ba mảnh búa trong phòng phụ, ngoài đường chính:
   - **Cán:** một người lạ (`Stranger`) ở màn 2 của khu thứ 2. Nói chuyện thì ông ta trao cán rồi tan biến.
   - **Đầu búa:** trong rương ở màn 2 của khu thứ 3.
   - **Lõi:** ba linh hồn bị trói (`Ghost`), mỗi màn của khu thứ 4 có một. Giải thoát đủ ba thì họ hợp lại trao lõi (`RunState.ghostMask`).
5. Đủ ba mảnh thì búa thành đồ dùng (mục 4). Các mảnh mất khi lượt kết thúc. Người lạ và linh hồn không bị bom hay kiếm làm hại.

## 2. Các khu
Thứ tự trong danh mục: Mossroot, Sporegloom, Emberdeep, Frostdeep, Sunfall, Gearworks, Sandsunk, Geode. Mỗi khu là một gói trọn: quái riêng, cơ chế riêng, bẫy riêng, boss riêng và đồ giúp khu đó dễ hơn (shop khu đó luôn bày, không bắt buộc mua).

| Khu | Đường đi | Cơ chế & bẫy | Quái | Đồ nổi bật | Boss |
|---|---|---|---|---|---|
| **Mossroot Caverns** | 6×9 phòng, đi xuống | Vách bedrock 4 ô bắt buộc dùng **dây** (1–2/màn); chông, bẫy tên, thạch nhũ | rắn, dơi, nhện | – | Broodmother |
| **Sporegloom** | 6×9, đi xuống | Gần như tối (nguồn sáng là nấm phát sáng); **nấm nảy** (bật cao, không tính sát thương rơi); không có bẫy tên | puffshroom, lurecap, gloom moth, rắn, nhện, dơi | Spore Mask, Lantern | Rot Heart |
| **Emberdeep Mines** | 6×9, đi xuống | Tường đá vụn bắt buộc dùng **bom**; khối nghiền, lỗ phun lửa, chông, bẫy tên | cóc lửa, gã húc, dơi lửa | – | Forge Brute |
| **Frostdeep Hollows** | 11×5, đi ngang | **Sàn băng trơn** (`groundGrip` 0,3); chông, bẫy tên, thạch nhũ | chim cánh cụt, yêu tuyết, dơi băng, rắn, nhện | Ice Cleats, Fur Coat | Yeti King |
| **Sunfall Cliffs** (ngoài trời) | 6×8, **leo lên** (vào ở đáy, ra trên mặt đất) | Nhận **tàu lượn** khi vào khu; vực gai 6 ô bắt buộc lượn; bẫy leo: Rock Dropper, bệ gỗ mục | rắn, cóc, dơi, diều hâu (2/màn) | – | Storm Hawk |
| **Gearworks** | 11×5, đi ngang | **Băng chuyền**, lưỡi cưa trên ray, khối nghiền | sentry, drone, scrap beetle, dơi | Heavy Boots, Magnet Charm | Cogwyrm |
| **Sandsunk Tombs** | 6×9, đi xuống | **Cát lún** (chậm, đứng 2 s thì bị nuốt, nuốt bom), bẫy giáo theo nhịp | bọ hung (theo bầy), xác ướp, giun cát, dơi | Sand Striders, Warding Scarab | Dune Queen |
| **Shardlight Geode** | 6×9, đi xuống | **Pha lê cộng hưởng** (bị đánh thì ngân rồi vỡ thành mảnh cắt mọi thứ, mọc lại), thấu kính trên trần chiếu cột sáng theo nhịp; không có bẫy tên | shardling, glint, mimic (giả ngọc/bình/thùng), dơi | Dampener, Truth Lens | The Facet |

Cửa hàng: Mossroot, Emberdeep, Sunfall có shop chắc chắn ở màn 2 (40% ở màn 3); các khu sau có shop ở màn 1 và 50–60% ở màn 2, 3.

**Lồng dân làng:** **mọi lượt** có đúng một lồng (`Villagers.CaptiveChance` = 1), đặt trong phòng phụ của một màn (1–3) thuộc khu thứ 2, 3 hoặc 4, không bao giờ ở khu đầu; seed quyết định màn nào. Mở lồng là cứu một người dân còn lạc, được ghi vào save ngay cả khi lượt đó thất bại. Cứu đủ 6 người thì không còn lồng.

## 3. Boss
Luật chung (`Boss.cs`):
- Đòn của chính người chơi (kiếm, giẫm, đồ ném, đá ná) gây tối đa 3 mỗi cú.
- **Bom và bẫy gây 1 điểm**. Cùng một boss không bị bẫy làm hại lại trong 0,5 s.
- Chiêu bốc từ túi xáo trộn, không bao giờ cùng một chiêu hai lần liền, mỗi chiêu có dấu "!" báo trước. Khoảng nghỉ giữa các chiêu ngắn dần khi boss yếu đi.
- Giẫm đầu quá 2 lần trong 1,5 s thì bị phản đòn.
- Hạ boss: 10 cục vàng lớn, **1 ember + 5 spark** (`Villagers.EmbersPerBoss / SparksPerBoss`), cộng phần thưởng riêng (`rewardId`). Boss khu rơi **Heart Vessel** (+1 máu tối đa, hồi đầy); Eclipse King rơi Sunstone. Trong Challenge boss không rơi gì.
- Thanh máu boss hiện trên HUD.

| Boss | Khu | Máu | Chiêu |
|---|---|---|---|
| **Broodmother** | Mossroot | 24 | Bò trên trần tới đúng chỗ người chơi rồi rơi xuống (kẹt lại = cơ hội, sóng chấn hai bên, xé bọc kén thả nhện con); giăng tơ làm chậm; đẻ bọc trứng; phun độc từ trần |
| **Rot Heart** | Sporegloom | 24 | Treo trên trần, không di chuyển, không giẫm được, chạm không mất máu; với tới bằng nấm nảy và bệ. Phi tiêu bào tử, xúc tu quất thấp/cao, mưa bào tử (đảo trái/phải), gọi lurecap; còn 2/3 và 1/3 máu thì nửa số nấm sáng tắt |
| **Forge Brute** | Emberdeep | 28 | Lao húc (đâm tường đá nền thì choáng, đất thì phá xuyên); dậm sàn làm lửa trồi ở 5 chỗ; nhảy dậm 3 lần, mỗi lần ra sóng chấn |
| **Yeti King** | Frostdeep | 36 | Quạt cầu tuyết (đẩy lùi, đóng băng, không mất máu), trượt bụng (thấp, nhảy qua được; đâm tường thì choáng), cầu tuyết lăn to dần (chém 3 lần hoặc bom), gọi chim cánh cụt, nhảy vồ ra sóng băng, rung rơi băng nhũ. Dưới nửa máu: nghỉ ít hơn, 5 cầu tuyết mỗi loạt, trượt bật lại từ bức tường đầu tiên |
| **Storm Hawk** | Sunfall | 22 | Bổ nhào (trượt thì đậu đất một lúc), quạt lông vũ, thả 3 hòn đá (nhặt ném lại được), quét sát đất |
| **Cogwyrm** | Gearworks | 36 | Con rết bánh răng chạy trên đường ray quanh sảnh, lên tường và trần; chỉ đầu bị thương, mọi đốt là lưỡi cưa. Chạy dọc sàn (nhảy hoặc lướt qua), bổ đầu xuống từ trần và kẹt lại. Bắn hai quạt bu-lông, sau đó hạ đầu xuống tầm nhảy 0,6 s để nạp lại (cơ hội). Cuộn thành bánh xe nảy qua sảnh (không đánh được); cú nảy cuối đập nó vào tường, nằm rã dọc sàn, đầu chìa ra sảnh, choáng 3 s (cơ hội). Mỗi 1/4 máu rụng một đốt: chỉ rụng khi nó đã về ray trần, đốt phát sáng 0,6 s, rơi cách người chơi ít nhất 2,5 ô, nằm 0,4 s rồi lăn tới như lưỡi cưa; mỗi lần rụng thì nó nhanh hơn. Dưới nửa máu bổ hai lần liền và chạy sàn đi rồi về |
| **Dune Queen** | Sandsunk | 28 | Cả sàn là cát lún, 3 khối đá nhô lên làm chỗ đứng (vỡ thì mọc lại). Lặn rồi trồi lên dưới chân người chơi (húc khối đá thì mất 1 máu và kiệt sức = cơ hội), phun cát, gọi bọ hung; dưới cát thì không đánh được |
| **The Facet** | Geode | 40 | Pho tượng khổng lồ chiếm vách sau; đánh hai nắm đấm của nó. Vỏ nắm đấm đỡ kiếm, mảnh pha lê làm nứt vỏ, lúc nứt thì đòn vào nắm đấm trừ máu Facet. Đập xuống (trúng pha lê thì làm nó ngân), quét dọc sàn (đập vỡ pha lê, đẩy mảnh đi trước, đứng lên lưng nắm đấm được), 3 tia sáng từ viên ngọc trên trán (pha lê phản xạ như gương), mưa gai đá. Dưới nửa máu: hai nắm cùng đập, tia sáng bắn hai lần |
| **Eclipse King** | The Sunken Vault | 35 + 40 | Hai dạng, xem bên dưới. Không rơi Heart Vessel; rơi Sunstone |

### Eclipse King (`EclipseKing.cs`, `SunStake.cs`)
Lúc nó hở (bị kẹt ở dạng vỏ, mở ngực ở dạng lõi), đòn của chính người chơi tính gấp đôi (vẫn tối đa 3 mỗi cú).

**Dạng 1: vỏ đá (35 máu).** Đi về phía người chơi, nhanh hơn ×1,35 khi đã nứt.
- **Phun lửa:** luồng lửa thẳng từ khe ngực, thấp, xa 5,5 ô, cao 1,5 ô, 2 sát thương; nhảy qua được. Nếu người chơi ở quá xa thì nó nhảy thay vì phun.
- **Lao:** bốc cháy 0,6 s báo trước, rồi lao thành quả cầu lửa. Đâm tường thì sừng cắm vào tường, kẹt 2 s (cơ hội).
- **Nhảy:** khi đáp, hai bức tường ánh sáng (`sunwave`) chạy dọc sàn về hai phía. Tường quá cao để nhảy qua, kiếm không chặn được, chỉ lướt xuyên qua được. Tường chỉ gây sát thương khi đã ra khỏi thân nó.
- Còn ≤ 50% máu thì nứt: báo trước ngắn hơn, lao và tường sáng nhanh hơn.
- Hết máu thì quỳ 1,8 s, vỏ nổ tung ("ECLIPSE"), sảnh tối xuống, lõi bay lên với đầy máu.

**Dạng 2: lõi ánh sáng (40 máu).** Lơ lửng 3,2 ô trên sàn, ngoài tầm kiếm; chạm vào thân không mất máu. Có hai bàn tay.
- **Cột sáng:** trước khi gọi cột, lõi cắm 4 **cọc sáng** (`SunStake`) xuống sàn. Một đòn bất kỳ phá được cọc (kiếm, đồ ném, nổ; đạn của chính nó thì không), mỗi cọc vỡ trừ Eclipse King 2 máu. Cọc còn đứng khi cột sáng chạm tới thì nổ vòng sáng: 2 sát thương trong bán kính 2,3, không phá ô. Cột sáng rộng 2,6 ô, đi từ bức tường xa người chơi sang bên kia, 2 sát thương. Nó xuyên sàn gỗ và chui xuống hố bom, nên không có chỗ nấp, chỉ có lướt. Theo sau cột là mưa 8 tàn lửa.
- **Đập tay:** bàn tay phía người chơi lơ lửng trên đầu 0,7 s (đứng yên 0,25 s cuối), rồi đập xuống: 2 sát thương, kèm cột lửa lớn trồi lên từ sàn.
- **Vỗ tay:** hai tay quét dọc sàn từ hai bức tường (2 sát thương, nhảy qua), chỗ hai tay gặp nhau bắn ra vòng 16 tàn lửa.
- **Bùng sáng:** sóng dọc sàn cộng quạt 5 tàn lửa bắn lên.
- **Biến mất:** biến mất 0,7 s (không đánh được), hiện lại cạnh người chơi, phía sau lưng họ, tay đã giơ sẵn.
- Sau mỗi chiêu, lõi hạ xuống sàn, mở ngực 1,6 s (cơ hội). Dưới nửa máu, 50% số lần nó nối thêm một chiêu ngay.

**Hollow Warden** (`Warden.cs`) vẫn còn code, prefab và kịch bản kiểm thử, nhưng không màn nào dùng và không có trong Challenge.

## 4. Nhân vật & đồ
**Khởi đầu:** 10 máu (cũng là máu tối đa), 0 vàng. Đồ tiêu hao lấy từ **kho của save** (`Supplies.cs`): bom, dây, đá, Flare và giấy mà lượt trước còn lại khi kết thúc (chết, bỏ cuộc hay thắng đều giữ). Túi **không tự đổ đầy**; muốn thêm thì mua ở Pip bằng spark hoặc nhặt, mua trên núi. Lượt đầu tiên của một save được một túi đầy (5 bom, 5 dây, 10 đá). Màn hướng dẫn, Challenge và các lượt test tự động vẫn đổ đầy túi như cũ. Đồ dùng: ná (cầm sẵn trên tay), bom, dây, Recall Charm (`PlayerConfig.startingGear`). `startBombs` / `startRopes` (4) chỉ còn dùng cho các bài test, lượt chơi thật không dùng.

**Túi** (`Bags.cs`): bom 5, dây 5, viên đá 10, Ember Pouch 5. Trưởng làng Aldous nới mỗi túi +5, giá 1, rồi 2, rồi 3 ember, tối đa 3 lần (mỗi túi tối đa 20 / 20 / 25 / 20). Nới xong là vĩnh viễn; túi không được đổ thêm gì. Đồ nhặt chỉ lấy phần vừa túi, phần dư nằm lại dưới đất; túi đầy thì shop không bán món đó.

**Kiếm** (trường `whip*` trong code giữ tên cũ): 1 sát thương, vung 0,3 s, có hitbox phía sau lúc vung. Tempered Blade: +1 sát thương, tầm +0,45.

**Lướt** (`PlayerConfig > Dash`): 17 ô/s trong 0,2 s (~3,4 ô), không bị thương khi đang lướt và 0,12 s sau đó; tường chặn; trên không một lần. Thanh năng lượng 100, mỗi lần 30, hồi 60/s sau 0,5 s không lướt; thiếu thì thanh nhấp nháy đỏ.

**Đồ dùng** (nút Dùng, đổi bằng nút đổi đồ hoặc vòng chọn):
- **Bom**: ném, ↓ đặt, ↑ ném cao. Ngòi 2,5 s, sát thương 10, phá mọi ô trừ bedrock, kích nổ bom khác (giới hạn dây chuyền: 3 vụ nổ mỗi bước, 12 thế hệ, trần 600 thực thể).
- **Dây**: ném lên tối đa 7 ô, móc khi chạm trần hoặc có vách/mép cạnh đỉnh, không thì rơi lại thành cuộn; ↓ ở mép thì thả xuống. Dài 8 ô, sống 20 s (nhấp nháy 3 s cuối, không biến mất khi đang leo).
- **Tàu lượn**: nhận ở Sunfall; giữ nhảy khi rơi (rơi tối đa 1,9 ô/s), không tính sát thương rơi, không tốn gì.
- **Recall Charm**: về lại lối vào của màn, hồi 8 s, không mang được đồ nặng (tượng vàng, Sunstone).
- **Ná** (`Gear_Slingshot`): giữ nút Dùng để kéo dây ná, thả ra để bắn. Giữ ↑ khi thả thì bắn thẳng lên; đang cúi thì viên đá bay sát sàn. Viên đá bay thẳng khoảng 8 ô (16 ô/s trong 0,5 s) rồi rơi. Thứ đầu tiên nó trúng mất 1 máu; viên đá đập vỡ bình/thùng và làm bẫy tên bắn mất phát. Bắn xong viên đá biến mất, không nhặt lại được. Hồi 0,35 s. Bị choáng hoặc đổi đồ giữa chừng thì huỷ cú kéo, không tốn đá. Bình và thùng có thể rơi ra đá. **Pebble Pouch** (shop trên núi) mặc vào thì túi đá chứa thêm 10 viên.
- **Stonebreaker** (`Gear_Hammer`, `HammerGear.cs`): cây búa ghép từ ba mảnh (mục 1). Vung đập: 3 sát thương, tầm 1,7, lấy đà 0,13 s, nghỉ 0,5 s giữa hai lần, không bao giờ hết. Là thứ duy nhất đập vỡ được Sunstone.
- **Flare** (`Gear_Flare`, `FlareGear.cs`, `Flare.cs`): Bera bán 3 cây một lần, mua là có đồ dùng này cho lượt đó. Ném như bom (↓ đặt dưới chân, ↑ ném cao). Cháy 30 s với ánh sáng đỏ bán kính 7,5 ô, mờ dần trong 4 s cuối; không gây sát thương. Nhặt lên được và mang theo như đuốc.

**Máu:** tim (+1) từ bình (~5%), thùng (~18%), quái (5%); đầy máu thì không nhặt. Shop bày một trong ba: Heart Tonic +3 ($150), Healing Flask +5 ($250), Elixir of Dawn đầy máu ($500), tức 50 vàng mỗi tim (Elixir tính như 10 tim). Heart Vessel từ boss và Heart Draught ở làng tăng máu tối đa.

**Túi đồ** (I / Select): tab **WEAR** (ô feet, body, hand và 2 ô charm; chỉ món đang mặc mới có tác dụng; món mới tự mặc nếu ô trống), **ITEMS** (đồ dùng và số còn lại), **KEY ITEMS** (Ember Pouch, Field Guide, Map, bản chép chữ cổ, lời giải của trưởng làng; mục nào đọc được thì có nút Read). LB/RB (S/Tab) chuyển tab.

**Cửa hàng** (`ShopConfig`): kệ 5 món = 2 món tiêu hao khác nhau bốc từ bom, dây, đá ná (Pebble Sack: +5 đá, $100) và các đồ tiêu hao đã mở bản thiết kế (Lamp Oil, Flares, Heart Draught; chưa mở thì chỉ có ba món đầu), một món hồi máu (Heart Tonic 45%, Healing Flask 40%, Elixir of Dawn 15%), 2 trang bị (món nổi bật của khu, hoặc ngẫu nhiên trong số không bị khoá). Trang bị có bản thiết kế bị khoá trên núi tới khi mua (mục 5); những món mở sẵn thì không bao giờ bị khoá. Rương gỗ và rương ở The Last Camp cũng theo luật này. Trang bị chỉ mua một lần. Chủ shop đang hiền thì mua lại **tượng vàng** tại chỗ ("[enter] Sell", trả đúng giá trị vàng; không áp dụng cho Sunstone). Chủ shop: 2 lần bị đánh chỉ cảnh báo, lần 3 nổi giận; còn sống thì mọi shop sau đóng cửa và ông ta phục kích cùng 2 người anh em, chết thì các shop sau trống.

| Món | Giá | Ô | Tác dụng |
|---|---|---|---|
| Bomb Bag / Rope Pile | $350 / $250 | – | +3 bom / +3 dây |
| Lamp Oil | $200 | – | Thần chết tới muộn 120 s |
| Heart Draught | $400 | – | +1 máu tối đa và +1 máu cho lượt này (Maren bán ở làng) |
| Spring Boots | $900 | feet | Nhảy mạnh hơn 17% (mô tả trong game vẫn ghi ~40%) |
| Tempered Blade | $750 | hand | +1 sát thương, tầm xa hơn |
| Compass | $450 | charm | Dấu chỉ đường tới cửa ra / Sunstone |
| Blast Powder | $800 | charm | Bán kính nổ +0,9 |
| Ice Cleats / Fur Coat | $600 / $650 | feet / body | Không trượt trên băng / không bị đóng băng |
| Spore Mask / Lantern | $550 / $600 | charm | Mây bào tử vô hại / đèn xa hơn 60% |
| Heavy Boots / Magnet Charm | $600 / $650 | feet / charm | Băng chuyền không cuốn / kiếm xuyên khiên sentry |
| Sand Striders / Warding Scarab | $600 / $550 | feet / charm | Cát lún an toàn / xác ướp không lấy được vàng |
| Dampener / Truth Lens | $600 / $500 | charm | Mảnh pha lê đi xuyên qua / mimic hiện màu đỏ |
| Pebble Pouch | $450 | charm | Túi đá +10 viên khi đang mặc |
| Hobnail Boots | $700 | feet | Giẫm đầu gây 2 sát thương (`stompBonus`) |
| Iron Cap | $550 | body | Vật rơi trúng đầu (thạch nhũ, đá, bình rơi) bớt 1 sát thương; vật chỉ gây 1 thì không còn đau (`headGuard`) |
| Soft-Sole Boots | $600 | feet | Rơi 12 ô mới mất máu thay vì 9 (`fallBonus`) |
| Spyglass | $500 | charm | Giữ ↑/↓ nhìn xa gấp đôi (`lookBonus`) |
| Sheet of Paper | ($250) | – | Chỉ Pip ở làng bán; dùng để chép chữ trên cột đá |
| Map | ($500) | – | Chỉ Oswin ở làng bán; key item giữ mãi (mục 9) |
| Lost Letter | ($750) | – | Chỉ Oswin ở làng bán; key item giữ mãi. Vào màn có lồng dân làng thì có câu báo; bản đồ đánh dấu lồng bằng chấm hồng nhấp nháy |
| Flares | ($400) | – | Chỉ Bera ở làng bán; 3 cây Flare kèm đồ dùng |

Giá trong ngoặc là giá gốc để quy đổi sang tiền ở làng (mục 5).

## 5. Làng Brindlemoor & tiền tệ
Ba loại tiền (`Villagers.cs`):
- **Vàng:** nhặt trên núi (cục nhỏ 10, cục lớn 30, ngọc 100, tượng vàng 400 phải mang ra cửa hoặc bán cho chủ shop; khoảng 1500 mỗi màn). Chỉ tiêu ở shop trên núi, mất khi hết lượt.
- **Spark:** mỗi boss rơi 5. Không giới hạn, lưu ngay khi nhặt, không bao giờ mất. Dùng mua **đồ tiêu hao cho một lượt** ở tiệm của Pip.
- **Ember:** mỗi boss rơi 1, vào **Ember Pouch** (ban đầu chứa 5; nhặt khi túi đầy thì phần dư mất). Lưu ngay, không bao giờ mất. Dùng cho **những thứ lâu dài**: trưởng làng nới túi (1 / 2 / 3 ember), Map và Lost Letter của Oswin, và **bản thiết kế**.
- Chưa có Ember Pouch thì không nhặt được cả spark lẫn ember. Spark/ember boss đã rơi mà không nhặt thì mất.
- Giá ở làng = `max(1, round(giá vàng trên núi / 250))`. Giá và số tiền trong túi luôn ghi bằng số kèm hình đồng tiền, không ghi chữ.

**Bản thiết kế** (`Blueprints.cs`): dân làng không bán đồ cho một lượt nữa mà bán **bản thiết kế**. Mua một lần (bằng ember, giá theo công thức trên) là giữ mãi theo save (key item `bp:<id>`); từ đó shop trên núi mới bày món đó (mua bằng vàng), rương gỗ và rương ở trại mới có thể ra món đó. Chưa mua thì món đó không xuất hiện trên núi. Map, Lost Letter và giấy không phải bản thiết kế, vẫn mua trực tiếp. **Mở sẵn từ đầu** (không cần bản thiết kế, không có quầy): Compass, Iron Cap, Soft-Sole Boots, Pebble Pouch, bom, dây, đá, và món nhẹ hơn trong cặp đồ riêng của mỗi khu (Fur Coat, Spore Mask, Heavy Boots, Warding Scarab, Truth Lens); món mạnh hơn của mỗi khu (Ice Cleats, Lantern, Magnet Charm, Sand Striders, Dampener) cần bản thiết kế. Giá bản thiết kế đặt riêng ở `ShopOffer.blueprintPrice` (món mạnh dùng mọi khu 3–4, đồ riêng khu và Spyglass 2, đồ tiêu hao 1–2; mở hết tốn 31 ember). Pip không bán bản thiết kế (`VillagerDef.blueprints = false`). Túi đồ có mục **Blueprints** ở KEY ITEMS liệt kê những bản đã mở.

**Phố làng:** 8 ngôi nhà hai tầng liền nhau, 6 dân làng để cứu (mỗi lượt có một lồng). Nhà của dân chưa được cứu thì tối, khoá và không có người. Đứng cạnh một người thì lời nhắc hiện tên họ và các việc làm được (**Talk / Shop**, với trưởng làng là **Talk / Upgrade**); ↑/↓ chọn dòng, nút tương tác để làm. Làng không bán bom hay dây, vì lượt nào cũng bắt đầu với túi đầy. Shop là một màn hình danh sách (`VillageShopPanel`), game tạm dừng khi mở, mua xong lưu ngay; không còn bày hàng dưới sàn.

| Nhà | Chủ | Bán / làm |
|---|---|---|
| Lò rèn | Hilde the Smith | Bản thiết kế: Tempered Blade 4, Blast Powder 3, Hobnail Boots 3, Magnet Charm 2 ember |
| Xưởng dây | Tam the Ropemaker | Bản thiết kế: Spring Boots 4, Ice Cleats 2, Sand Striders 2 ember |
| Nhà thuốc | Maren the Healer | Bản thiết kế: Dampener 2 ember |
| Phòng bản đồ | Oswin the Mapmaker | Bản thiết kế: Spyglass 2 ember; **Map 2 ember**, **Lost Letter 3 ember** (mua trực tiếp) |
| Tiệm lặt vặt | Pip the Peddler | Đồ tiêu hao, bằng spark: Bomb Bag (+3) 1, Rope Pile (+3) 1, Pebble Sack (+5) 1, **Sheet of Paper** 1. Lamp Oil, Flares, Heart Draught: chưa mở thì Pip bán **bản thiết kế** (1 / 2 / 2 ember); mua xong thì chính dòng đó bán món đồ bằng spark (1 / 2 / 2) và món đó lên kệ núi. Bom, dây, đá, Flare, giấy vào kho, không mất khi chết; Lamp Oil và Heart Draught có tác dụng ngay, chỉ trong lượt đó. Tiệm nằm ngay cạnh nhà Brann (`HandcraftedLevel.StreetOrder`) |
| Nhà đèn | Bera the Lamplighter | Bản thiết kế: Lantern 2 ember |
| Hội trường | Trưởng làng **Aldous** | Nhiệm vụ; nới túi (Upgrade); chưa cứu đủ thì nói còn bao nhiêu người lạc |
| Nhà của Brann | – | Nơi mọi lượt bắt đầu; chuồng chó bên cạnh |

**Nhiệm vụ** (`QuestCatalog`, `Data/Quests`; bước kích hoạt bằng talk / enter / obtain / kill / free; phần thưởng là key item, ember, đồ hoặc vàng):
- **A Word with the Elder** (`elder_word`): tự bắt đầu, khoá cổng làng tới khi xong. Vào hội trường, nói chuyện với trưởng làng, nhận **Ember Pouch**.
- **The Old Signs** (`old_signs`): bắt đầu khi xem cột đá đầu tiên. Mua giấy, chép chữ, đưa trưởng làng đọc, nhận **The Elder's Reading** (mục 1).
- **Dấu chỉ nhiệm vụ** trên HUD dẫn tới thứ mà bước hiện tại cần (đi xuyên qua cửa), ưu tiên hơn mũi tên Compass.

**Làng sống động:**
- **Ngày và đêm** (`VillageClock`) theo giờ thật của máy, chốt một lần lúc vào làng: rạng sáng 5–6 giờ, ngày 7–16, hoàng hôn 17–18, đêm 19–4. Giờ đổi bầu trời, ánh sáng nền, độ sáng đèn nhân vật và hình phòng trong nhà. Thiết lập trong `Area0_Village` là ban đêm.
- **Dân phố** (`Townsfolk`, sinh bằng code): người lớn đi dạo, nói chuyện; trẻ con chơi đuổi bắt. Ban ngày 8 người lớn + 4 trẻ, rạng sáng/hoàng hôn 5 + 2, ban đêm 2 + 0.
- **Con chó** (`VillageDog`, `Kennel`): ngày thì chạy quanh trong 16 ô quanh chuồng, người chơi tới gần (4,5 ô) thì chạy lại ngồi cạnh; đêm thì ngủ trước chuồng.
- **Đồ vật để xem** (`Curio`): 35 món trong 8 phòng ("[enter] Look"), kể về chủ nhà và gợi ý về vòng lặp thời gian.

### Rương (`Chest.cs`)
- **Rương gỗ** (đai sắt, `lootchest`): mỗi màn sinh ngẫu nhiên của khu có 75% khả năng có một rương (`GameConfig.lootChestsPerLevel` = 1, `lootChestChance` = 0,75; theo seed màn), đặt trong phòng phụ ngoài đường chính, chỗ đi tới được (`Story.Spots`). Không có ở làng, hướng dẫn, đấu trường boss, trại, hầm cuối, Challenge. Mở ra thì bung 3 thứ (`lootContents`: cục vàng lớn, ngọc, túi bom, cuộn dây, đá, tim) và có thể kèm một trang bị chưa có nằm cạnh, lấy miễn phí. Tỉ lệ ra trang bị bắt đầu ở 25% (`lootGearChance`); mỗi rương không ra thì rương sau +10% (`lootGearStep`, đếm trong `RunState.lootMisses`, lưu cùng lượt), ra rồi thì về lại 25%. Như vậy không bao giờ quá 8 rương liền không có trang bị. Rương có viền sáng nhẹ và mũi tên gợi ý khi tới gần.
- **Rương xịn** (sơn đỏ, bọc vàng, khoá gắn ngọc): những rương đặc biệt, gồm rương 3 trang bị ở The Last Camp, rương Field Guide cuối hướng dẫn, rương trang bị ở cổng khu (tàu lượn) và rương đầu búa.

## 6. Hệ thống & sát thương
Mọi thực thể kế thừa `Entity` và chạy trong `World.FixedUpdate`. Quái chỉ gây sát thương khi đang ra đòn (`Enemy.Attacking`), chạm vào không mất máu. Bị thương: bật lùi, choáng 0,45 s, bất tử 1,2 s.

| Nguồn | Sát thương |
|---|---|
| Quái yếu (rắn, dơi, nhện, cóc, dơi băng…) | 1 |
| Dơi lửa, gã húc, diều hâu, chim cánh cụt | 2 |
| Chủ shop nổi giận | 3 |
| Đòn thân của boss | 2 (cú húc của Forge Brute 3) |
| Đạn boss / sóng chấn / đá, băng nhũ rơi | 1 / 2 / 2 |
| Bẫy tên, thạch nhũ | 2 |
| Khối nghiền | 3 |
| Lỗ phun lửa | 1 + hất lên |
| Chông | 2 + hất lên (vực gai cổng lượn vẫn chết ngay) |
| Rơi ≥ 9 ô | 2 + choáng 1 s (giẫm đầu quái sau cú rơi không tính) |
| Nổ bom | 10 |
| Thần chết (Reaper) | chết ngay, xuyên tường, không hạ được |

**Áp lực thời gian:** cảnh báo ở 9:00 (đồng hồ cam, banner), thần chết xuất hiện ở 10:00 (đồng hồ đỏ nhấp nháy), thêm một con mỗi 60 s. Không có ở làng, trại, đấu trường boss (kể cả Challenge) và hướng dẫn.

**Quái rơi thưởng:** mỗi con rơi một thứ: tim (`heartDropChance`) hoặc vàng (`goldDrop`). Quái do boss gọi ra không rơi gì.

## 7. Chuyển động (PlayerMotor – thuần C#, tất định)
`Scripts/Player/PlayerMotor.cs`, tham số trong `Data/PlayerConfig.asset` (mục *motor*). Bước mô phỏng cố định 1/60 s, va chạm AABB với lưới ô (không dùng Physics2D), nên **bộ kiểm tra màn và bot dùng đúng code này**.
- Hitbox 0,6×0,85 ô. Chạy 6,4 ô/s, đi chậm 3, bò 1,8, leo 4,2. Trọng lực 52 (×1,15 khi rơi), rơi tối đa 19.
- Nhảy v = 15,8 → ~2,3 ô; thả nút sớm nhân vận tốc 0,45. Coyote 0,08 s, bộ đệm nhảy 0,12 s.
- Bám mép tự động khi rơi sát tường, đầu trong cửa sổ quanh mép; ↑ trèo (0,22 s), nhảy bật lên, ↓ buông. Bò ra mép thì tự treo.
- Thang/dây: ↑ bám, đỉnh thang đứng được, nhảy để rời. ↓ + nhảy rơi xuyên sàn gỗ.

## 8. Sinh màn (`LevelGenerator`, `RoomTemplates.txt`, `LevelValidator`)
1. Seed lượt → seed từng màn; RNG xorshift tất định. Màn kế tiếp được sinh trên luồng nền trong lúc chơi màn hiện tại; màn đầu sinh sau màn hình tải.
2. Lưới phòng 10×8 ô mỗi phòng, kích thước lưới theo khu (6×9, 6×8, 11×5). Đường giải có thể đi xuống, ngang hoặc lên (`pathUp/Side/Down`, `pathAcross`, `pathStartBottom`). Chỉ đường giải và các nhánh là phòng, phần còn lại là đá đặc; phần lớn nhánh là lối rơi giả dẫn vào ngõ cụt.
3. Phòng đặc biệt: bắt đầu/thoát, cửa hàng, phòng tượng vàng, hầm kho báu, lồng dân làng, phòng chướng ngại `gate_<đồ>`. Sau khi dựng màn, `Story` đặt cột đá (màn 1 mỗi khu), người lạ, rương đầu búa và linh hồn vào phòng phụ ngoài đường chính.
4. 124 mẫu phòng (file văn bản) + lật ngang + ô xác suất + khe đánh dấu (quái, bẫy, kho báu, vật phẩm) + mạch vàng.
5. Bẫy/quái theo mật độ của khu, tăng dần theo màn; phòng phụ rủi ro và phần thưởng cao hơn.
6. Lối vào an toàn: không bẫy/quái/vật rơi gần lối vào, cửa hàng không có bẫy/quái.
7. **Chứng minh qua được:** `LevelValidator` chạy chính `PlayerMotor` với thư viện mẫu input và BFS trên các trạng thái nghỉ; cấm chạm chông và cú rơi gây sát thương. Màn có chướng ngại được kiểm tra hai chiều: có đồ thì qua, thiếu đồ thì không. Không đạt thì sinh lại (các attempt chạy song song). Thiếu đạn cho chướng ngại thì có kho tiếp tế cạnh lối vào.
8. Đường tìm được được bot phát lại trong game thật để xác nhận (xem TEST_REPORT).

## 9. Giao diện
- **HUD:** máu (hiện/tối đa, tim đập, đỏ khi thấp), thanh năng lượng lướt, vàng, ô đồ đang cầm (icon, số lượng, nút đổi và nút dùng), hàng biểu tượng đồ đang mặc, tên chặng + khu, đồng hồ màn + tổng, thanh máu boss, dấu Compass, dấu nhiệm vụ, gợi ý tương tác, banner. Ở làng chỉ còn góc máu, ember/sức chứa túi và spark.
- **Menu chính:** Start (chọn ô lưu, xoá ô có hỏi lại; game mới bắt đầu bằng màn hướng dẫn), Challenge, Settings, Records, Quit.
- **Settings:** âm lượng tổng/nhạc/hiệu ứng, toàn màn hình (F11), rung màn hình, **WINDOW SIZE** (1280×720, 1600×900, 1920×1080, 2560×1440, 3840×2160; chỉ hiện cỡ vừa màn hình), CONTROLS / KEYS.
- **Màn hình:** cửa sổ cỡ cố định 16:9, không kéo giãn được. Toàn màn hình luôn chạy đúng cỡ gốc của màn hình đang chứa cửa sổ. Màn hẹp hơn 16:9 (màn dọc, 16:10, 4:3) thì mọi camera và mọi canvas nằm trong khung 16:9 ở giữa, trên dưới để viền tối (`ScreenFrame`, `FramedCanvas`), nên bố cục và toạ độ chuột giống hệt màn ngang.
- **Tạm dừng** (theo nơi đang ở; seed và số liệu ở cuối bảng):
  - Trên núi: Resume, Settings, **Give Up** (hỏi lại), Main Menu.
  - Ở làng: Resume, Settings, Main Menu.
  - Hướng dẫn: Resume, **Skip Tutorial**, Settings, Main Menu.
  - Challenge: Resume, **Fight Again**, **Boss List** (mở ngay trong bảng tạm dừng, chọn boss khác mà không rời game; Back về trận), Settings, Main Menu.
- **Nút back của tay cầm** (B/○, A trên Switch) làm việc của Esc: đóng túi đồ, Field Guide, shop làng và bảng con trong menu tạm dừng; từ menu tạm dừng thì quay lại game; dùng được cả ở menu chính và bảng đổi phím.
- **Field Guide** (`Codex`, `CodexScreen`): key item, cuốn sách trong rương cuối màn hướng dẫn. Mở từ túi đồ (KEY ITEMS > Read); không có phím riêng. Game dừng khi mở; ↑/↓ lật trang (giữ để lật liên tục), ←/→ nhảy giữa các nơi; nút tạm dừng, nút túi đồ hoặc nút back để đóng. Có 10 trang nơi chốn, 31 trang sinh vật, 18 trang thông tin (bẫy, thần chết…). Trang nơi chốn và thông tin luôn đọc được; trang sinh vật trống cho tới khi giết được một con (đếm theo save, `killCounts`).
- **Map** (`MapScreen`): khi có Map trong túi, vùng quanh người chơi (6,5 ô) được vẽ lên tờ bản đồ khi đi qua; mỗi màn bắt đầu bằng tờ giấy tối. Giữ M / L1 thì bản đồ mở đè lên game (game vẫn chạy), thả ra thì gập lại. Không dùng được ở làng, khi tạm dừng, trong túi đồ, Field Guide, vòng chọn đồ hay khi đang hiện thẻ. Mỗi loại ô có màu riêng (khoảng trống, đá, bedrock, gỗ/thang, chông, mạch vàng); lối vào luôn được đánh dấu, lối ra chỉ khi đã thấy; người chơi là một chấm nhấp nháy.
- **Màn kết quả:** chết ("YOU DIED", nguyên nhân, chặng tới được, vàng, thời gian, số quái, số màn, seed; NEW RUN / BACK TO VILLAGE); thắng (một nút CONFIRM); đập vỡ đá ("THE WINTER IS OVER", sang credits); Challenge ("BOSS DEFEATED", thời gian, máu đã mất, kỷ lục, flawless; FIGHT AGAIN / BOSS LIST); hướng dẫn ("TUTORIAL COMPLETE", nhắc tới Field Guide; TO THE VILLAGE).
- Chữ UI là TextMesh Pro (EB Garamond). Token như `[jump]`, `[use]`, `[gear]`, `[flawless]` trong bất kỳ chữ UI nào được thay bằng hình nút theo thiết bị đang dùng (`GlyphText`, `Data/InputGlyphs.asset`).

## 10. Hình ảnh & âm thanh
Toàn bộ sprite, tile, icon, logo, âm thanh và nhạc sinh bằng code trong `Editor/ArtGenerator*.cs` và `AudioGenerator.cs`. Phong cách: pixel tô phẳng, mỗi mảng một màu, một viền bóng và vài mảng lớn; boss vẽ cùng quy tắc với quái thường. URP 2D: ánh sáng nền theo khu, đèn theo nhân vật, đuốc, đèn trên bom/lửa/ngọc, bloom và vignette (`Settings/PostFX.asset`), lớp đá parallax, hạt không khí theo khu.

## 11. Cấu trúc project
```
Assets/Game/
  Scripts/Core      cấu hình (ScriptableObject), RunState, SaveSystem, Villagers, Bags, Codex, QuestLog/QuestCatalog, ItemLog, ScreenFrame, âm thanh, animation
  Scripts/World     TileGrid, BodyPhysics, Entity, World, LevelRenderer
  Scripts/Player    PlayerMotor, PlayerController, PlayerInputReader, KeyBindings
  Scripts/Gear      GearBehaviour + bom, dây, tàu lượn, Recall Charm, ná (BuiltinGear), búa (HammerGear)
  Scripts/Enemies   mỗi loại quái một class (kể cả chủ shop, thần chết, mimic)
  Scripts/Bosses    Boss (khung chung), BossShot, mỗi boss một class
  Scripts/Items     Item, Bomb, Breakable, Arrow, Treasure, Pickup, PebbleShot, RopeProjectile, IceChunk
  Scripts/Traps     bẫy và cơ chế địa hình (băng chuyền, cát lún, pha lê, nấm nảy…)
  Scripts/Level     LevelGenerator, LevelValidator, RoomTemplates, HandcraftedLevel, cửa, shop, rương, lồng, cột đá, người lạ, linh hồn, nhà trong làng, dân phố, con chó, đồ vật để xem
  Scripts/Game      GameDirector, RunPlan, Story, EndingScene, VillageClock, LevelFactory, CameraFollow, AutoTest, ScenarioRunner, BotInput
  Scripts/UI        HUD, PauseMenu, SettingsPanel, KeyBindPanel, InventoryScreen, CodexScreen, MapScreen, VillageShopPanel, EquipmentWheel, ResultsScreen, MainMenuController…
  Scripts/FX        ánh sáng, hạt, chữ nổi, hiệu ứng
  Editor            ArtGenerator*, AudioGenerator, ProjectBuilder, UIBuilder, RoomTemplateEditor, GameTests
  Data              GameConfig, PlayerConfig, Area*, Gear_*, ShopConfig, QuestCatalog, Quests/*, TileSet_*, InputGlyphs, RoomTemplates.txt, Levels/*.txt
```
Chỉnh thông số: chọn asset trong `Assets/Game/Data` → Inspector. **Brindlemoor > Build Everything** sinh lại art/âm thanh/prefab/scene nhưng **giữ nguyên giá trị** của các config đã có.
