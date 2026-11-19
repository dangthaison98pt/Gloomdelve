# Brindlemoor: The Sunstone Hunt – Thiết kế

Platformer roguelike 2D pixel art. Brann rời làng Brindlemoor đang chìm trong mùa đông, leo qua núi hang sinh ngẫu nhiên để mang **Sunstone** từ đáy *The Sunken Vault* về. (Tên nội bộ: Gloomdelve; trong code viên đá vẫn là `heartstone`.)

## 1. Cấu trúc lượt chơi
`RunPlan.Build` dựng lượt từ danh mục `GameConfig.stages`: mỗi khu là một khối (3 màn sinh ngẫu nhiên + 1 đấu trường boss). Mỗi lượt bốc `areasPerRun` = **4 khu** theo seed, khu nào cũng có thể mở đầu (`firstAreaFixed` = 0). Tên chặng đánh số theo vị trí trong lượt (1-1 … 4-4), nên "3-2" là màn 2 của khu thứ ba được bốc.

| Chặng | Nội dung |
|---|---|
| Làng **Brindlemoor** | Mọi lượt bắt đầu ở làng (trừ NEW RUN sau khi chết: vào thẳng 1-1). Không đồng hồ, không vũ khí, không đồ dùng. Lần đầu cổng núi khoá tới khi đã gặp trưởng làng |
| 4 khu × (3 màn + boss) | Màn sinh theo seed, được chứng minh qua được; đấu trường boss làm tay (`Levels/Boss_*.txt`), cửa ra khoá tới khi hạ boss |
| **The Last Camp** | Rương 3 món trang bị chưa có + shop 5 món |
| **The Sunken Vault** | Hạ **Hollow Warden** mới nhấc được Sunstone; nhấc lên thì hầm sập và cửa mở; mang Sunstone ra cổng = **thắng** (+2000 vàng) |

Tổng 18 chặng. **Hướng dẫn** (menu > TUTORIAL): màn làm tay 9 biển, 3 bom 3 dây, chết thì làm lại; xong thì có nút START A RUN.

**Chết hoặc thắng:** màn kết quả (chặng tới được, seed, kỷ lục) với **NEW RUN** (seed mới, vào thẳng màn đầu) hoặc **BACK TO VILLAGE** (thức dậy trên giường). Mất mọi thứ của lượt; giữ lại ember, dân làng đã cứu, vật phẩm chính (Ember Pouch), kỷ lục.

**Lưu:** 3 ô lưu (`slot1..3.json`), mỗi ô có làng, ember, vật phẩm chính và kỷ lục riêng; cài đặt dùng chung. Lượt đang chơi được lưu ở **đầu mỗi màn** (seed, vị trí, máu, vàng, đồ); tiếp tục thì chơi lại từ đầu màn đó. Dân làng được cứu và boss đã hạ ghi ngay, không gặp lại lần hai. Tutorial và Challenge không lưu.

**Challenge** (menu): đánh thẳng từng boss (8 boss khu + Warden) với nhân vật mới; chết thì đánh lại ngay, thắng thì xem thời gian và máu đã mất, FIGHT AGAIN / BOSS LIST. Lưu thời gian tốt nhất và huy hiệu không mất máu (`[flawless]`) từng boss.

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

**Lồng dân làng:** khoảng nửa số lượt có một lồng, đặt trong phòng phụ của một màn thuộc khu thứ 2, 3 hoặc 4 (không bao giờ ở khu đầu). Mở lồng là cứu một người dân còn lạc; cứu đủ thì không còn lồng.

## 3. Boss
Luật chung (`Boss.cs`): đòn của người chơi gây tối đa 3 mỗi cú; **bom và bẫy chỉ gây nửa điểm**. Chiêu bốc từ túi xáo trộn, không bao giờ cùng một chiêu hai lần liền, mỗi chiêu có dấu "!" báo trước. Khoảng nghỉ giữa các chiêu ngắn dần khi boss yếu đi. Giẫm đầu quá 2 lần liền thì bị phản đòn. Hạ boss khu: 10 cục vàng, 3 ember, 1 **Heart Vessel** (+1 máu tối đa, hồi đầy). Thanh máu boss trên HUD.

| Boss | Khu | Máu | Chiêu |
|---|---|---|---|
| **Broodmother** | Mossroot | 14 | Bò trên trần tới đúng chỗ người chơi rồi rơi xuống (kẹt lại = cơ hội, sóng chấn hai bên, xé bọc kén thả nhện con); giăng tơ làm chậm; đẻ bọc trứng; phun độc từ trần |
| **Rot Heart** | Sporegloom | 14 | Treo trên trần, không di chuyển, không giẫm được, chạm không mất máu; với tới bằng nấm nảy và bệ. Phi tiêu bào tử, xúc tu quất thấp/cao, mưa bào tử (đảo trái/phải), gọi lurecap; còn 2/3 và 1/3 máu thì nửa số nấm sáng tắt |
| **Forge Brute** | Emberdeep | 16 | Lao húc (đâm tường đá nền thì choáng, đất thì phá xuyên); dậm sàn làm lửa trồi ở 5 chỗ; nhảy dậm 3 lần, mỗi lần ra sóng chấn |
| **Yeti King** | Frostdeep | 30 | Quạt cầu tuyết (đẩy lùi, đóng băng, không mất máu), trượt bụng (thấp, nhảy qua được; đâm tường thì choáng), cầu tuyết lăn to dần (chém 3 lần hoặc bom), gọi chim cánh cụt, nhảy vồ ra sóng băng, rung rơi băng nhũ. Dưới nửa máu: nghỉ ít hơn, 5 cầu tuyết mỗi loạt, trượt bật lại từ bức tường đầu tiên |
| **Storm Hawk** | Sunfall | 12 | Bổ nhào (trượt thì đậu đất một lúc), quạt lông vũ, thả 3 hòn đá (nhặt ném lại được), quét sát đất |
| **Cogwyrm** | Gearworks | 30 | Con rết bánh răng chạy trên đường ray quanh sảnh, lên tường và trần; chỉ đầu bị thương, mọi đốt là lưỡi cưa. Chạy dọc sàn (nhảy hoặc lướt qua), bổ đầu xuống từ trần và kẹt lại, bắn bu-lông, cuộn thành bánh xe nảy qua sảnh (không đánh được). Mỗi 1/4 máu rụng một đốt thành lưỡi cưa rời và nhanh hơn; dưới nửa máu bổ hai lần liền và chạy sàn đi rồi về |
| **Dune Queen** | Sandsunk | 16 | Cả sàn là cát lún, 3 khối đá nhô lên làm chỗ đứng (vỡ thì mọc lại). Lặn rồi trồi lên dưới chân người chơi (húc khối đá thì mất 1 máu và kiệt sức = cơ hội), phun cát, gọi bọ hung; dưới cát thì không đánh được |
| **The Facet** | Geode | 40 | Pho tượng khổng lồ chiếm vách sau; đánh hai nắm đấm của nó. Vỏ nắm đấm đỡ kiếm, mảnh pha lê làm nứt vỏ, lúc nứt thì đòn vào nắm đấm trừ máu Facet. Đập xuống (trúng pha lê thì làm nó ngân), quét dọc sàn (đập vỡ pha lê, đẩy mảnh đi trước, đứng lên lưng nắm đấm được), 3 tia sáng từ viên ngọc trên trán (pha lê phản xạ như gương), mưa gai đá. Dưới nửa máu: hai nắm cùng đập, tia sáng bắn hai lần |
| **Hollow Warden** | The Sunken Vault | 24 | Trôi theo người chơi; quạt đạn, mưa đạn, lao tới, dịch chuyển sang phía bên kia. Dưới nửa máu: nhanh hơn, 5 đạn mỗi quạt, vòng 8 đạn. Không rơi Heart Vessel; còn sống thì không nhấc được Sunstone |

## 4. Nhân vật & đồ
**Khởi đầu:** 10 máu (cũng là máu tối đa), 4 bom, 4 dây, 0 vàng; đồ dùng: bom, dây, Recall Charm.

**Kiếm** (trường `whip*` trong code giữ tên cũ): 1 sát thương, vung 0,3 s, có hitbox phía sau lúc vung. Tempered Blade: +1 sát thương, tầm +0,45.

**Lướt** (`PlayerConfig > Dash`): 17 ô/s trong 0,2 s (~3,4 ô), không bị thương khi đang lướt và 0,12 s sau đó; tường chặn; trên không một lần. Thanh năng lượng 100, mỗi lần 30, hồi 60/s sau 0,5 s không lướt; thiếu thì thanh nhấp nháy đỏ.

**Đồ dùng** (nút Dùng, đổi bằng nút đổi đồ hoặc vòng chọn):
- **Bom**: ném, ↓ đặt, ↑ ném cao. Ngòi 2,5 s, sát thương 10, phá mọi ô trừ bedrock, kích nổ bom khác (giới hạn dây chuyền: 3 vụ nổ mỗi bước, 12 thế hệ, trần 600 thực thể).
- **Dây**: ném lên tối đa 7 ô, móc khi chạm trần hoặc có vách/mép cạnh đỉnh, không thì rơi lại thành cuộn; ↓ ở mép thì thả xuống. Dài 8 ô, sống 20 s (nhấp nháy 3 s cuối, không biến mất khi đang leo).
- **Tàu lượn**: nhận ở Sunfall; giữ nhảy khi rơi (rơi tối đa 1,9 ô/s), không tính sát thương rơi, không tốn gì.
- **Recall Charm**: về lại lối vào của màn, hồi 8 s, không mang được đồ nặng (tượng vàng, Sunstone).

**Máu:** tim (+1) từ bình (~5%), thùng (~18%), quái (5%); đầy máu thì không nhặt. Shop bày một trong ba: Heart Tonic +1 ($100), Healing Flask +3 ($250), Elixir of Dawn đầy máu ($550). Heart Vessel từ boss và Heart Draught ở làng tăng máu tối đa.

**Túi đồ** (I / Select): tab **WEAR** (ô feet, body, hand và 2 ô charm; chỉ món đang mặc mới có tác dụng; món mới tự mặc nếu ô trống), **ITEMS** (đồ dùng và số còn lại), **KEY ITEMS** (Ember Pouch).

**Cửa hàng** (`ShopConfig`): kệ 5 món = bom, dây, một món hồi máu, 2 món nổi bật của khu (hoặc ngẫu nhiên). Trang bị chỉ mua một lần. Chủ shop: 2 lần bị đánh chỉ cảnh báo, lần 3 nổi giận; còn sống thì mọi shop sau đóng cửa và ông ta phục kích cùng 2 người anh em, chết thì các shop sau trống.

| Món | Giá | Ô | Tác dụng |
|---|---|---|---|
| Bomb Bag / Rope Pile | $350 / $250 | – | +3 bom / +3 dây |
| Lamp Oil | $200 | – | Thần chết tới muộn 120 s |
| Spring Boots | $900 | feet | Nhảy mạnh hơn 17% |
| Tempered Blade | $750 | hand | +1 sát thương, tầm xa hơn |
| Compass | $450 | charm | Dấu chỉ đường tới cửa ra / Sunstone |
| Blast Powder | $800 | charm | Bán kính nổ +0,9 |
| Ice Cleats / Fur Coat | $600 / $650 | feet / body | Không trượt trên băng / không bị đóng băng |
| Spore Mask / Lantern | $550 / $600 | charm | Mây bào tử vô hại / đèn xa hơn 60% |
| Heavy Boots / Magnet Charm | $600 / $650 | feet / charm | Băng chuyền không cuốn / kiếm xuyên khiên sentry |
| Sand Striders / Warding Scarab | $600 / $550 | feet / charm | Cát lún an toàn / xác ướp không lấy được vàng |
| Dampener / Truth Lens | $600 / $500 | charm | Mảnh pha lê đi xuyên qua / mimic hiện màu đỏ |

## 5. Làng Brindlemoor & tiền tệ
- **Vàng**: nhặt trên núi (cục nhỏ 10, cục lớn 30, ngọc 100, tượng vàng 400 phải mang ra cửa; khoảng 1500 mỗi màn), tiêu ở shop trên núi, mất khi hết lượt.
- **Ember**: chỉ boss rơi (3, trùm cuối 6), vào thẳng **Ember Pouch** (chứa 5) và lưu ngay; chưa có túi thì mất. Dùng để mua đồ của dân làng.
- **Phố làng**: 8 ngôi nhà hai tầng liền nhau: lò rèn (Hilde: Tempered Blade, Blast Powder), xưởng dây (Tam: dây, Spring Boots), nhà thuốc (Maren: Heart Draught), phòng bản đồ (Oswin: Compass), tiệm đồ lặt vặt (Pip: 2 nâng cấp ngẫu nhiên, rẻ hơn), nhà đèn (Bera: Lamp Oil, bom), hội trường (trưởng làng **Aldous**: lần đầu gặp trao Ember Pouch) và nhà của Brann. Nhà của dân chưa được cứu thì tối và khoá. Giá đồ của dân ~1 ember mỗi 250 vàng giá trên núi (tối thiểu 1). Trong nhà có 35 đồ vật để xem, kể về chủ nhà và mùa đông này.

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
3. Phòng đặc biệt: bắt đầu/thoát, cửa hàng, phòng tượng vàng, hầm kho báu, lồng dân làng, phòng chướng ngại `gate_<đồ>`.
4. 124 mẫu phòng (file văn bản) + lật ngang + ô xác suất + khe đánh dấu (quái, bẫy, kho báu, vật phẩm) + mạch vàng.
5. Bẫy/quái theo mật độ của khu, tăng dần theo màn; phòng phụ rủi ro và phần thưởng cao hơn.
6. Lối vào an toàn: không bẫy/quái/vật rơi gần lối vào, cửa hàng không có bẫy/quái.
7. **Chứng minh qua được:** `LevelValidator` chạy chính `PlayerMotor` với thư viện mẫu input và BFS trên các trạng thái nghỉ; cấm chạm chông và cú rơi gây sát thương. Màn có chướng ngại được kiểm tra hai chiều: có đồ thì qua, thiếu đồ thì không. Không đạt thì sinh lại (các attempt chạy song song). Thiếu đạn cho chướng ngại thì có kho tiếp tế cạnh lối vào.
8. Đường tìm được được bot phát lại trong game thật để xác nhận (xem TEST_REPORT).

## 9. Giao diện
- **HUD:** máu (hiện/tối đa, tim đập, đỏ khi thấp), thanh năng lượng lướt, vàng (ở làng: ember/sức chứa túi), ô đồ đang cầm (icon, số lượng, nút đổi và nút dùng), hàng biểu tượng đồ đang mặc, tên chặng + khu, đồng hồ màn + tổng, thanh máu boss, dấu Compass, gợi ý tương tác, banner. Ở làng chỉ còn góc máu và ember.
- **Menu chính:** Start (chọn ô lưu, xoá ô có hỏi lại), Tutorial, Challenge, Settings (âm lượng, toàn màn hình, rung màn hình, CONTROLS / KEYS), Records, Quit. **Tạm dừng:** Resume, Settings, Main Menu; seed ở cuối bảng.
- Chữ UI là TextMesh Pro (EB Garamond). Token như `[jump]`, `[use]`, `[gear]`, `[flawless]` trong bất kỳ chữ UI nào được thay bằng hình nút theo thiết bị đang dùng (`GlyphText`, `Data/InputGlyphs.asset`).

## 10. Hình ảnh & âm thanh
Toàn bộ sprite, tile, icon, logo, âm thanh và nhạc sinh bằng code trong `Editor/ArtGenerator*.cs` và `AudioGenerator.cs`. Phong cách: pixel tô phẳng, mỗi mảng một màu, một viền bóng và vài mảng lớn; boss vẽ cùng quy tắc với quái thường. URP 2D: ánh sáng nền theo khu, đèn theo nhân vật, đuốc, đèn trên bom/lửa/ngọc, bloom và vignette (`Settings/PostFX.asset`), lớp đá parallax, hạt không khí theo khu.

## 11. Cấu trúc project
```
Assets/Game/
  Scripts/Core      cấu hình (ScriptableObject), RunState, SaveSystem, Villagers, âm thanh, animation
  Scripts/World     TileGrid, BodyPhysics, Entity, World, LevelRenderer
  Scripts/Player    PlayerMotor, PlayerController, PlayerInputReader, KeyBindings
  Scripts/Gear      GearBehaviour + bom, dây, tàu lượn, Recall Charm
  Scripts/Enemies   mỗi loại quái một class (kể cả chủ shop, thần chết, mimic)
  Scripts/Bosses    Boss (khung chung), BossShot, mỗi boss một class
  Scripts/Items     Item, Bomb, Breakable, Arrow, Treasure, Pickup, RopeProjectile, IceChunk
  Scripts/Traps     bẫy và cơ chế địa hình (băng chuyền, cát lún, pha lê, nấm nảy…)
  Scripts/Level     LevelGenerator, LevelValidator, RoomTemplates, HandcraftedLevel, cửa, shop, rương, lồng, nhà trong làng
  Scripts/Game      GameDirector, RunPlan, LevelFactory, CameraFollow, AutoTest, ScenarioRunner, BotInput
  Scripts/UI        HUD, PauseMenu, SettingsPanel, KeyBindPanel, InventoryScreen, EquipmentWheel, ResultsScreen, MainMenuController…
  Scripts/FX        ánh sáng, hạt, chữ nổi, hiệu ứng
  Editor            ArtGenerator*, AudioGenerator, ProjectBuilder, UIBuilder, RoomTemplateEditor, GameTests
  Data              GameConfig, PlayerConfig, Area*, Gear_*, ShopConfig, TileSet_*, InputGlyphs, RoomTemplates.txt, Levels/*.txt
```
Chỉnh thông số: chọn asset trong `Assets/Game/Data` → Inspector. **Gloomdelve > Build Everything** sinh lại art/âm thanh/prefab/scene nhưng **giữ nguyên giá trị** của các config đã có.
