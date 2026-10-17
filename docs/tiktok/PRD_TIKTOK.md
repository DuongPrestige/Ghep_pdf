# PRD bổ sung — Tạo file in theo đơn TikTok Shop

- **Phiên bản:** 0.2 (draft)
- **Ngày cập nhật:** 2026-10-16 (bổ sung TT-10: đơn có ghi chú không ghép tự động)
- **Phụ thuộc:** `PRD.md` (BR-01, BR-02, BR-07, BR-08 vẫn áp dụng).

> **Thay đổi từ v0.1:** TikTok không cấp quyền dùng Open API, nên nguồn đơn chuyển sang **file xuất đơn từ Seller Center (CSV)**. Chrome extension là phương án sau, dùng chung cơ chế import và lọc trùng. Bổ sung loại xử lý SKU "Bỏ qua" và quy tắc chống trùng đơn.

---

## 1. Bối cảnh

Shop bán nhiều loại sản phẩm in: card bo góc, móc khóa, nhãn vở, photobooth, polaroid… Riêng **card bo góc 1 mặt** có file in làm sẵn trong kho PDF, mỗi trang A4 chứa 9 card. Hiện người dùng phải đọc từng đơn, tìm file rồi ghép thủ công, và tự ghi "ĐÃ IN …" vào Seller Note trên TikTok để khỏi in trùng.

Tính năng này nhận danh sách đơn từ Seller Center, tự khớp các dòng card bo góc với file trong kho, ghép thành một file PDF in duy nhất, và ghi nhớ những đơn đã in để **lần import sau không bị trùng**.

## 2. Mục tiêu

- Import file xuất đơn "Chờ vận chuyển" của nhiều shop.
- **Chống trùng:** import lại hoặc import file chồng lấn không tạo đơn trùng; đơn đã in không bị đưa vào lô in mới.
- Khớp dòng hàng card bo góc với file kho theo idol và số trang; bỏ qua các sản phẩm không thuộc phạm vi.
- Tạo một file PDF in theo lô, đúng thứ tự, không giảm chất lượng.
- Lưu lịch sử in: đơn nào, dòng nào, dùng file kho nào.

## 3. Ngoài phạm vi (giai đoạn này)

- Card 2 mặt, sản phẩm "in theo yêu cầu (gửi ảnh cho shop)", và các sản phẩm không phải card bo góc. Các loại này được phân loại để làm thủ công hoặc bỏ qua.
- Ghi ngược Seller Note hoặc trạng thái lên TikTok.
- Tự động in (BR-07).
- Gọi TikTok Open API. Mã spike API được giữ lại trong `spikes/TikTokApiSpike`, phòng khi sau này được cấp quyền.

## 4. Khái niệm

| Khái niệm | Mô tả |
|---|---|
| **Shop** | Một TikTok Shop. Người dùng tự khai báo tên shop trong app. Hiện có 2 shop. |
| **File xuất đơn** | File CSV xuất từ Seller Center → Đơn hàng → Xuất. Mỗi dòng là một dòng hàng (một SKU trong một đơn). |
| **SKU** | Biến thể sản phẩm, có `SKU ID` duy nhất. Ví dụ `20 CARD, 1 Mặt`. |
| **Loại xử lý SKU** | `Ghép tự động`, `Thủ công` hoặc `Bỏ qua` (xem TT-03). |
| **Mã idol** | Mã ngắn dùng trong tên file kho, ví dụ `JACK`, `IVAN`, `CONGB`, `24AT`. |
| **File kho** | File PDF đặt tên theo TT-01. |
| **Lô import** | Một lần import file xuất đơn. |
| **Lô in** | Một lần tạo file in từ nhiều đơn. Kết quả là một file PDF. |

## 5. Quy tắc nghiệp vụ

### TT-01 — Quy ước tên file kho

```text
<MÃ IDOL> - <N> PAGE - <dd.MM>.pdf      Ví dụ: JACK - 3 PAGE - 13.10.pdf · 24AT - 3 PAGE - 13.10.pdf
```

- Mã idol so khớp không phân biệt hoa thường, bỏ dấu và khoảng trắng thừa.
- Hai file khác nhau có cùng idol, số tờ và ngày được phép thêm hậu tố ` (2)`, ` (3)`… trước `.pdf`, ví dụ `JACK - 3 PAGE - 13.10 (2).pdf`.
- Tên file không có năm. Nếu ngày `dd.MM` của năm hiện tại lớn hơn hôm nay thì tính là năm trước.
- File sai quy ước, hoặc có số trang thực tế khác N, bị đánh dấu không hợp lệ và không được dùng.

### TT-02 — Quy đổi số card sang số trang

`số trang = ceil(số card / 9)`. Ví dụ: 20 card → 3 trang · 30 → 4 · 40 → 5 · 100 → 12.

### TT-03 — Cấu hình SKU

- Đơn được khớp theo **SKU ID**. Mỗi SKU có một **loại xử lý**:

| Loại | Ý nghĩa | Ví dụ |
|---|---|---|
| **Ghép tự động** | Card bo góc 1 mặt, có file trong kho. Cần thêm: mã idol, số card. | `30 CARD, 1 Mặt` của Cody |
| **Thủ công** | Card nhưng không ghép tự động được: 2 mặt, in theo yêu cầu… | `20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)` |
| **Bỏ qua** | Không thuộc quy trình in card | móc khóa, nhãn vở, photobooth, polaroid, card A4 |

- SKU **lần đầu xuất hiện** trong file import được tự thêm vào danh sách với trạng thái *Chưa cấu hình*, kèm gợi ý:
  - Tên sản phẩm **không chứa "BO GÓC", "THẺ GÓC" hay "PHOTOCARD"** → gợi ý *Bỏ qua* (móc khóa, nhãn vở, photobooth/PTB, polaroid…).
  - Variation chứa `YÊU CẦU` hoặc sản phẩm bắt đầu bằng `IN THEO YÊU CẦU` → *Thủ công*. Variation chỉ ghi `<số> CARD` (không ghi số mặt) → coi là 1 mặt.
  - Không đọc được số card (`<số> CARD`) → *Thủ công*.
  - Variation chứa `2 MẶT` hoặc `THEO YÊU CẦU` → *Thủ công*.
  - Variation có `<số> CARD` và `1 MẶT` → *Ghép tự động*, số card lấy từ variation.
  - Mã idol: lấy đoạn variation sau dấu phẩy cuối (nếu đó là tên idol); nếu không có thì lấy từ đoạn tên sản phẩm chứa "BO GÓC", bỏ các từ chung (CARD, BO, GÓC…). Kết quả được đối chiếu với bảng alias.
- Người dùng xác nhận hoặc sửa gợi ý. Chỉ SKU *đã xác nhận* mới được dùng để tạo file in.
- Có thể cấu hình hàng loạt theo sản phẩm, ví dụ đặt mọi SKU của "[MÓC KHÓA …]" thành *Bỏ qua*.
- Bảng **alias idol** cũng được áp dụng khi khớp SKU đã xác nhận với file kho, nên thêm alias là sửa được cả SKU cũ.
- Bảng **alias idol** quy các cách viết về một mã. Ví dụ `CongB`, `Công B` → `CONGB`; `Vỹ Vỹ`, `VĨ VĨ` → `VYVY`; `COMBO 24 ANH TRAI` → `24AT`.

### TT-04 — Chọn file kho và xoay vòng

- Ứng viên cho một dòng hàng là các file kho hợp lệ cùng mã idol và cùng số trang, sắp từ mới nhất đến cũ nhất.
- **Mỗi đơn luôn lấy file mới nhất** (cập nhật 2026-10-17). Ví dụ 3 đơn Đình Bắc 20 card đều in `DINH BAC - 3 PAGE - 31.07`. Chỉ khi **một đơn** cần nhiều bộ cùng idol và số trang (số lượng > 1), các bộ sau mới lấy lần lượt file cũ hơn để khách nhận hình khác nhau; hết file thì quay lại file mới nhất.

- **Luôn lấy ảnh mới nhất** (cập nhật 2026-10-17, công tắc trong Cài đặt, mặc định bật; quy tắc này thay cho việc chọn file đúng số trang ở trên): ghép bộ từ các file của idol theo ngày **mới nhất → cũ dần**, không quan tâm số trang từng file, đến khi đủ N trang; file cuối chỉ lấy số trang đầu còn thiếu. Ví dụ Đình Bắc 20 card (3 trang) = 3 trang đầu của `DINH BAC - 5 PAGE - 24.09` (dù có `DINH BAC - 3 PAGE - 31.07`); Jack 50 card (6 trang) = `JACK - 1 PAGE - 13.09` + `JACK - 1 PAGE - 06.09` + `JACK - 4 PAGE - 31.08`; mọi đơn cùng loại đều in cùng bộ này. Tắt công tắc thì chỉ dùng file đúng số trang (mới nhất trước).
- **Xem trước file in** (thêm 2026-10-17): nút "Xem trước file in" cạnh "Tạo file in" mở cửa sổ toàn màn hình liệt kê mọi trang sẽ in của các đơn đang tick, đúng thứ tự file in (đánh số trang, chia theo đơn, gồm trang trắng giữa đơn). Mỗi đơn có "Đổi file" (mở cửa sổ chọn file bên dưới) và "Bỏ khỏi lô" (bỏ tick); xem trước tự cập nhật sau mỗi thay đổi; nút "Tạo file in" trong cửa sổ chạy đúng luồng tạo file in thường.
- **Xem trước và tự chọn file** (thêm 2026-10-17): ô *File in* của mỗi đơn (Sẵn sàng / Thiếu file / Đã tạo file in) có nút **"Xem / chọn file"**, mở cửa sổ (bố cục giống trình ghép PDF gốc): bên trái là mọi file trong kho của idol, mỗi file một dòng ảnh các trang (lọc được theo tên bất kỳ, file không còn trên ổ đĩa bị ẩn kèm nhắc "Quét kho"); **bấm vào trang để chọn / bỏ chọn**, trang đã chọn có số thứ tự in; nút "Lấy cả file"; nút **"Thêm file PDF ngoài…"** chọn PDF bất kỳ trên máy (không cần nằm trong kho), file ngoài luôn nằm đầu danh sách (nền vàng) và được lưu kèm số trang (`OrderFileOverride.FilePageCount`) để tạo file in. Bên phải là các trang đã chọn của bộ đang chọn theo thứ tự in (đổi thứ tự, bỏ trang, "Bỏ hết"). Đơn nhiều bộ (nhiều dòng hàng hoặc số lượng > 1) chọn bộ ở thanh trên. Mỗi bộ được chọn nhiều hơn hoặc ít hơn số trang theo số card (ví dụ đơn 30 card in 5 tờ thay vì 4); khi lệch app chỉ cảnh báo màu cam, chỉ chặn lưu khi một bộ chưa có trang nào hoặc có trang của file không còn trên ổ đĩa. Trang tự chọn được lưu theo đơn thành các đoạn trang liên tiếp (bảng `OrderFileOverride`: file, trang bắt đầu, số trang); lịch sử in lưu cả trang bắt đầu (`PrintBatchItem.FirstPage`), thay cho chọn tự động khi tạo file in, hiện chữ "tự chọn" trong ô *File in*; đơn *Thiếu file* có file tự chọn thì thành *Sẵn sàng*. Nút **"Dùng lại chọn tự động"** xóa lựa chọn tay. Nếu file đã chọn không còn trong kho, đơn chuyển *Thiếu file*. Bộ thứ 2 trong cùng một đơn lấy tiếp các file sau đó. Lịch sử in lưu mỗi file của bộ là một dòng, kèm số trang đã dùng khi chỉ lấy một phần (`PrintBatchItem.PagesUsed`).

### TT-05 — Số lượng lớn hơn 1

Dòng hàng có `Quantity = N` được in **N bộ khác nhau**, tức N lần lấy file liên tiếp. Nếu số file ứng viên ít hơn N, app vẫn tạo nhưng gắn **cảnh báo "trùng hình"**.

### TT-06 — Thứ tự trong file in

Các đơn sắp **đúng thứ tự dòng trong file CSV** (cập nhật 2026-10-17; trước đó là `Created Time`, đơn cũ trước), áp dụng cho danh sách đơn, file in và file xuất danh sách. Đơn từ nhiều lần import: lần import cũ trước, trong một lần theo dòng CSV; đơn trùng dòng (dữ liệu cũ chưa có số dòng) sắp theo `Created Time` rồi mã đơn. Tab *Đơn hàng* cho phép bôi đen mã đơn để Ctrl+C, và nút **Copy mã đơn** copy mã các đơn đang tick (không tick đơn nào thì copy mọi đơn đang hiển thị), mỗi mã một dòng. Trong một đơn, các dòng hàng theo thứ tự trong file; trong một dòng, các bộ từ 1 đến N.

### TT-07 — Trang phân cách

Trước mỗi **đơn phức tạp** (có từ 2 dòng *Ghép tự động* trở lên, hoặc có dòng số lượng lớn hơn 1), app chèn một trang phân cách A4 **trắng hoàn toàn** (cập nhật 2026-10-17 để tiết kiệm mực; trước đó in mã đơn, shop, người mua, danh sách món, số tờ). Tùy chọn theo lô: *Không chèn* / *Chỉ đơn phức tạp* (mặc định) / *Mọi đơn*.

### TT-08 — Phân loại đơn

Phân loại dựa trên các dòng hàng **không phải** *Bỏ qua*:

| Nhóm | Điều kiện |
|---|---|
| **Sẵn sàng** | Có ít nhất 1 dòng *Ghép tự động* và mọi dòng (trừ dòng *Bỏ qua*) đều khớp được file. |
| **Cần cấu hình** | Có SKU chưa cấu hình hoặc chưa xác nhận. |
| **Thiếu file** | Kho không có file hợp lệ cho (idol, số trang). |
| **Thủ công** | Có dòng *Thủ công*. |
| **Có ghi chú** | Đơn có `Buyer Message` hoặc `Seller Note` (xem TT-10). |
| **Không cần in card** | Mọi dòng đều là *Bỏ qua*. Mặc định ẩn. |
| **Đã tạo file in** | Đã có trong một lô in thành công, hoặc Seller Note chứa "ĐÃ IN". |

- Thứ tự xét khi một đơn thỏa nhiều điều kiện: *Đã tạo file in* → *Không cần in card* → *Có ghi chú* → *Cần cấu hình* → *Thủ công* → *Thiếu file* → *Sẵn sàng*.
- Dòng *Bỏ qua* **không chặn** đơn. Ví dụ đơn có card Ivan 1 mặt và móc khóa: phần card vẫn được ghép.
- Dòng *Thủ công* hoặc lỗi **chặn cả đơn**, để tránh in nửa đơn rồi đóng gói thiếu.
- Một đơn bị chặn không ảnh hưởng đến các đơn khác.

### TT-09 — Chống trùng khi import

- **Khóa đơn** là `Order ID`; **khóa dòng hàng** là `Order ID + SKU ID`. Trước khi so sánh, bỏ khoảng trắng và tab ở hai đầu giá trị.
- Import một file:
  - Đơn **mới**: thêm vào.
  - Đơn **đã có**: cập nhật trạng thái, Buyer Message, Seller Note, số lượng. **Không** tạo bản ghi mới và **không** làm mất dấu "đã in".
  - Trong cùng một file, nếu có hai dòng trùng `Order ID + SKU ID`, chỉ giữ một dòng và ghi cảnh báo.
- Đơn **đã tạo file in** vẫn hiển thị ở nhóm *Đã tạo file in* và **không** được chọn vào lô in mới, trừ khi người dùng chủ động chọn *In lại*.
- Nếu đơn đã in mà số lượng hoặc SKU thay đổi so với lúc in, app gắn cảnh báo **"Đơn đã thay đổi sau khi in"** để người dùng kiểm tra.
- Đơn có trong lần import trước nhưng **không còn** trong file mới (đã giao hoặc đã hủy) thì chuyển sang *Không còn chờ vận chuyển* và ẩn khỏi danh sách. Quy tắc này **chỉ áp dụng cho cùng shop**, và chỉ khi người dùng chọn chế độ *File đầy đủ* lúc import (mặc định). Với chế độ *File bổ sung* (một phần), app không ẩn đơn nào.
- Chỉ nhận dòng có `Order Status` là *Cần vận chuyển* / *Chờ vận chuyển* (`AWAITING_SHIPMENT`). Dòng ở trạng thái khác được bỏ qua và đếm trong báo cáo import.
- Sau mỗi lần import, app hiện báo cáo: X đơn mới, Y đơn cập nhật, Z đơn đã in trước đó, W dòng bị bỏ qua.
- **Xóa dữ liệu import** (thêm 2026-10-17, tab *Lịch sử*, luôn hỏi xác nhận trước): *Xóa lần import* xóa lần import đang chọn cùng các đơn **chỉ có** trong lần đó; đơn có cả trong lần import khác được giữ. *Xóa toàn bộ đơn* xóa mọi đơn và mọi lần import. Cả hai **giữ** cấu hình SKU, alias, kho PDF và lịch sử lô in. Hộp xác nhận cảnh báo số đơn đã tạo file in, vì import lại thì các đơn đó hiện là chưa in.

### TT-10 — Ghi chú của khách và Seller Note

- App đọc và hiển thị `Buyer Message` (ghi chú của khách) và `Seller Note`.
- Hai công tắc trong Cài đặt (cập nhật 2026-10-17, đang **tắt** theo yêu cầu người dùng): *Seller Note "ĐÃ IN" = đã in* và *Đơn có ghi chú thì không ghép tự động*. Khi tắt cả hai, mọi đơn được ghép bất kể ghi chú. Ghi chú chỉ chứa khoảng trắng hoặc tab được coi là trống.
- **Seller Note chứa "ĐÃ IN"** (không phân biệt hoa thường và dấu): đơn được coi là **đã in thủ công**, xếp vào *Đã tạo file in* với nguồn "Seller Note". Tùy chọn này mặc định bật.
- **Có ghi chú khác** (Buyer Message hoặc Seller Note không trống): đơn **không được ghép tự động**. Đơn vào nhóm *Có ghi chú*, hiển thị đầy đủ nội dung ghi chú để người dùng xử lý thủ công.
  - Ví dụ: khách nhắn "cho em ảnh khác", hoặc shop ghi "dã có qhung".
  - Quy tắc này chỉ áp dụng cho đơn có dòng card (không phải *Bỏ qua*). Đơn toàn món *Bỏ qua* vẫn ở *Không cần in card*.
- Sau khi đọc ghi chú, người dùng có thể bấm **"Đã xem – cho phép ghép"** cho từng đơn. Đơn khi đó được phân loại như bình thường (*Sẵn sàng*, *Thiếu file*…). Nếu một lần import sau cho thấy **nội dung ghi chú đã thay đổi**, quyền cho phép này tự hủy và đơn quay lại *Có ghi chú*.

### TT-11 — Đánh dấu và lịch sử in

- Xuất file thành công thì đánh dấu các đơn trong lô, kèm thời gian và mã lô.
- Lịch sử lưu theo chuỗi: lô in → đơn → dòng hàng → bộ thứ mấy → file kho nào.
- Người dùng có thể **bỏ đánh dấu** một đơn hoặc **tạo lại** file in của một lô cũ.
- Xuất thất bại hoặc bị hủy thì không đánh dấu đơn nào.
- Sau khi in, app cho phép **copy danh sách mã đơn** để người dùng dán ghi chú lên Seller Center nếu muốn.

### TT-12 — Xuất danh sách đơn

- Tab *Đơn hàng* có ô chọn phạm vi và nút **"Xuất danh sách CSV"**; tab *Lịch sử* có nút **"Xuất DS đơn của lô"** cho lô in đang chọn. File CSV mở được bằng Excel, giúp chủ shop biết đơn nào đã làm file in.
- Phạm vi:
  - *Đơn đã làm file in*: mọi đơn đang ở nhóm *Đã tạo file in* (lô in hoặc Seller Note), kể cả đơn không còn chờ vận chuyển.
  - *Đơn chưa làm file in* (thêm 2026-10-17): đơn còn chờ vận chuyển và chưa ở nhóm *Đã tạo file in* (sẵn sàng, thủ công, thiếu file, cần cấu hình…).
  - *Tất cả đơn đang chờ (đánh dấu đã làm)*: mọi đơn còn chờ vận chuyển, đã in hay chưa.
  - *Lần import gần nhất*: các đơn có trong lần import mới nhất.
  - *Đơn của một lô in* (từ tab *Lịch sử*).
- Mỗi **dòng hàng** là một dòng CSV (giống file xuất Seller Center); các cột của đơn được lặp lại. Cột theo thứ tự: Order ID, Shop, Thời gian đặt, Đã làm file in (Có/Không), Nguồn đánh dấu (Lô in / Seller Note), Lô in, Thời gian in, Trạng thái xử lý (cùng nhãn với tab *Đơn hàng*, theo cài đặt hiện tại), Lý do, SKU ID, Sản phẩm, Phân loại, Số lượng, File kho đã dùng (kèm "lấy N/M trang đầu" khi chỉ in một phần file), Đơn đã thay đổi sau khi in, Còn chờ vận chuyển, Buyer Username, Ghi chú khách, Seller Note.
- Order ID và SKU ID có ký tự tab ở cuối (như Seller Center) để Excel giữ nguyên dạng chữ. File UTF-8 có BOM, xuống dòng CRLF, sắp theo thứ tự dòng trong file CSV đã import.
- Không xuất tên, số điện thoại, địa chỉ người nhận. Không có đơn nào thì báo "Không có đơn nào để xuất." và không tạo file. Ghi lỗi giữa chừng thì không để lại file dở.
- Với *Đơn của một lô in*, các cột Đã làm file in, Nguồn đánh dấu, Lô in, Thời gian in và File kho đã dùng lấy theo chính lô đó (kể cả đơn sau này đã bỏ đánh dấu hoặc in lại ở lô khác); Trạng thái xử lý và Lý do theo phân loại hiện tại.
- Ô chữ tự do (ghi chú, tên sản phẩm, ...) bắt đầu bằng `=`, `+`, `-` hoặc `@` được thêm dấu nháy đơn `'` ở đầu để Excel hiển thị đúng chữ, không hiểu là công thức.

## 6. Luồng chính

1. **Cấu hình một lần:** khai báo shop, chọn thư mục kho PDF, thư mục xuất.
2. Trên Seller Center: lọc *Chờ vận chuyển* → **Xuất** → tải file CSV.
3. Trong app: **Import đơn** → chọn shop → chọn file (hoặc kéo thả) → xem báo cáo import (TT-09).
4. Nếu có SKU mới: vào tab *Cấu hình SKU* để xác nhận gợi ý.
5. Tab *Đơn hàng*: các đơn *Sẵn sàng* được chọn sẵn. Chọn kiểu trang phân cách rồi bấm **Tạo file in**.
6. App ghép file, hiện tiến trình, có thể hủy. Xong thì mở bằng Foxit và đánh dấu đơn.

## 7. Ví dụ bắt buộc

Kho có các file:

```text
IVAN - 3 PAGE - 15.10.pdf   IVAN - 3 PAGE - 10.10.pdf   CODY - 4 PAGE - 14.10.pdf
```

File import gồm:

| Order ID | Dòng hàng | Loại SKU |
|---|---|---|
| O1 | Cody `30 CARD, 1 Mặt` x1 | Ghép tự động |
| O2 | Ivan `20 CARD, 1 Mặt` x2; Móc khóa `Spotify, Ivan` x1 | Ghép tự động; Bỏ qua |
| O3 | Ivan `20 CARD, 2 MẶT` x1; Móc khóa album mini x1 | Thủ công; Bỏ qua |
| O4 | Nhãn vở CORTIS x1 | Bỏ qua |
| O5 | Cody `30 CARD, 1 Mặt` x1, Buyer Message "cho em ảnh khác nhé" | Ghép tự động |
| O6 | Ivan `20 CARD, 1 Mặt` x1, Seller Note "ĐÃ IN IVAN mini" | Ghép tự động |

Kết quả:

```text
O1: CODY-4P-14.10                                        (4 tờ)
[Phân cách O2]
O2: IVAN-3P-15.10, IVAN-3P-10.10                         (6 tờ + 1 tờ phân cách; móc khóa bỏ qua)
O3 → "Thủ công"   O4 → "Không cần in card"   O5 → "Có ghi chú"   O6 → "Đã tạo file in" (Seller Note)
```

Tổng file in: 11 trang. O1 và O2 được đánh dấu đã in. Import lại **cùng file** cho kết quả: 0 đơn mới, 6 đơn cập nhật; O1, O2, O6 vẫn ở *Đã tạo file in* và không vào lô in mới.

## 8. Tiêu chí nghiệm thu

- **AC-T01:** Import đúng file xuất thực tế của Seller Center: đọc được UTF-8 có BOM, bỏ tab thừa, đọc được ô có dấu phẩy và ngoặc kép, đọc ngày dạng `dd/MM/yyyy HH:mm:ss`.
- **AC-T02:** Import cùng một file 2 lần không tạo đơn hay dòng hàng trùng. Đơn đã in không vào lô in mới.
- **AC-T03:** Phân loại đúng theo TT-08 và TT-10. Ví dụ ở mục 7 cho đúng thứ tự trang. Đơn có ghi chú không bao giờ được ghép tự động nếu chưa bấm "Đã xem – cho phép ghép".
- **AC-T04:** SKU mới tự xuất hiện kèm gợi ý đúng cho các mẫu variation trong file mẫu ngày 2026-10-16.
- **AC-T05:** File in giữ nguyên page size và chất lượng.
- **AC-T06:** Xuất lỗi hoặc hủy thì không đánh dấu đơn nào. Thành công thì lịch sử ghi đủ.
- **AC-T07:** App **không lưu** các cột người nhận, số điện thoại, địa chỉ vào database hay log.

## 9. Câu hỏi mở

- **Q1 (đã chốt 2026-10-16):** Polaroid và photobooth (PTB) hiện tại là *Bỏ qua*. Nếu sau này có file kho cho các loại này, cần mở rộng TT-01/TT-02 theo loại sản phẩm.
- **Q2 (đã chốt 2026-10-16):** Đơn có lẫn card *Thủ công* (ví dụ 2 mặt) và card *Ghép tự động* thì **chặn cả đơn**, xếp vào nhóm *Thủ công* (TT-08).
- **Q3 (phase sau):** Chrome extension đọc đơn trực tiếp từ Seller Center và gửi vào app, dùng chung TT-09.
