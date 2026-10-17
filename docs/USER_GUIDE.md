# Hướng Dẫn Sử Dụng Nhanh

Áp dụng cho phiên bản 1.3.1. Ứng dụng mở ở chế độ toàn màn hình.

## Phần 1 — Ghép lẻ PDF

### Mở file PDF

1. Bấm **Thêm PDF** để chọn một hoặc nhiều file PDF.
2. Hoặc kéo thả file PDF từ Windows Explorer vào cửa sổ ứng dụng.
3. File đã có trong danh sách (cùng đường dẫn hoặc cùng nội dung) sẽ không được thêm lần nữa.
4. Nếu file lỗi, thiếu quyền đọc hoặc cần mật khẩu, ứng dụng báo lỗi cho từng file và tiếp tục xử lý các file còn lại.

### Tìm và chọn trang

- Ô **Tìm** lọc theo tên file, không phân biệt dấu và hoa thường: gõ `dinh bac` sẽ ra `ĐÌNH BẮC.pdf`.
- Click một thumbnail để chọn hoặc bỏ chọn trang. Giữ **Ctrl** để chọn từng trang rời rạc, **Shift** để chọn một dải trang.
- Dùng **Chọn tất cả** hoặc **Bỏ chọn** ở header file để thao tác nhanh theo file; **Thu gọn** để ẩn các trang của file.
- Dùng thanh Zoom hoặc **Ctrl + con lăn** để đổi kích thước thumbnail.
- Thumbnail chỉ được tải khi cuộn tới, nên mở nhiều file vẫn nhẹ.

### Xem lớn

- Nháy đúp thumbnail để xem lớn; có **◀ Trước / Sau ▶**, **Vừa rộng**, **Vừa cao**, zoom +/−.
- Bấm **Đóng** hoặc **Esc** để đóng.

### Thêm trang vào đầu ra

1. Chọn các trang cần ghép.
2. Bấm **Thêm vào đầu ra** hoặc nhấn phím **Enter**.
3. Mỗi lần thêm tạo một nhóm mới (có màu riêng) ở cuối **Khay đầu ra**. Một trang nguồn có thể xuất hiện nhiều lần.

### Sắp xếp đầu ra

- Thứ tự trong Khay đầu ra là thứ tự trang trong file PDF xuất.
- Kéo thả trang để đổi vị trí: thả vào nửa trái của trang đích là đứng trước, nửa phải là đứng sau. Kéo tên nhóm để chuyển cả nhóm.
- **Nhân bản** thêm bản sao trang, **Nhân nhóm** nhân bản cả nhóm, **Xóa** bỏ trang.
- **Hoàn tác / Làm lại** cho các thao tác chính.

### Xem trước đầu ra

- Bấm **Xem trước đầu ra** để xem mọi trang sẽ xuất. **Lưới tất cả trang** xem dạng lưới, **Xem 1 trang** xem lớn trang đang chọn.
- Trong màn xem trước có thể kéo thả đổi vị trí, nhân bản, đưa lên trước / ra sau, hoặc xóa trang.

### File nguồn bị thiếu

- Khi mở lại phiên, file nguồn không còn ở đường dẫn cũ sẽ được đánh dấu thiếu.
- Bấm **Chọn lại** ở header file để chọn lại file. Ứng dụng chỉ chấp nhận khi file mới khớp số trang và nội dung với file cũ.
- Khay đầu ra không tự xóa trang khi file nguồn bị thiếu.

### Xuất PDF

1. Kiểm tra lại thứ tự trang.
2. Bấm **Xuất PDF**. Ứng dụng không ghi đè PDF nguồn và không rasterize trang.
3. Sau khi xuất, file được mở bằng Foxit PDF Reader nếu có, hoặc bằng trình xem mặc định của Windows.

### Tự lưu

- Ứng dụng tự lưu phiên làm việc. Nếu lần trước đóng bất thường, khi mở lại có nút **Phục hồi** hoặc **Bỏ qua** ở thanh trạng thái.

## Phần 2 — Đơn TikTok

Bấm **Đơn TikTok** trên thanh công cụ để mở cửa sổ đơn hàng.

### Cài đặt lần đầu

1. Tab **Cài đặt**: thêm shop, chọn **Thư mục kho PDF** và thư mục lưu file in.
2. Bấm **Quét kho** để đọc các file trong kho. Tên file theo mẫu `TÊN IDOL - 5 PAGE - 24.09.pdf`.
3. Tab **Cấu hình SKU**: kiểm tra các SKU mới, bấm **Xác nhận gợi ý đã chọn**; sản phẩm không cần in (móc khóa, polaroid…) đặt **Bỏ qua**.

### Quy trình hằng ngày

1. Xuất file CSV đơn hàng từ TikTok Seller Center.
2. Bấm **Import đơn** (hoặc kéo thả file .csv vào cửa sổ), chọn shop và chế độ:
   - **File đầy đủ**: đơn của shop không còn trong file sẽ bị ẩn.
   - **Bổ sung**: không ẩn đơn nào.
3. Đơn đã làm file in ở lần trước tự được lọc ra, không bị in lại khi import file CSV mới.
4. Tab **Đơn hàng** hiện đơn theo đúng thứ tự file CSV. Nháy đúp hoặc bấm nút copy để copy mã đơn.
5. Bấm **Xem trước file in** để xem mọi trang sẽ in: **Đổi file** để chọn lại trang cho đơn, **Bỏ khỏi lô** để không in đơn đó lần này, **Copy mã tất cả đơn**.
6. Bấm **Tạo file in**. File được mở bằng Foxit để in.
7. Dùng **Xuất CSV** với phạm vi "Đơn chưa làm file in" để lấy danh sách mã đơn còn lại.

### Cách chọn file ảnh

- Mặc định bật **Luôn lấy ảnh mới nhất**: mỗi bộ card lấy từ file mới nhất của idol, thiếu trang thì lấy tiếp file cũ hơn.
- File cuối chỉ lấy số trang đầu còn thiếu. Ví dụ Đình Bắc 20 card (3 tờ), file mới nhất có 5 trang → lấy 3 trang đầu của file đó.
- Đơn mua nhiều bộ: bộ 2, bộ 3… lùi dần sang file cũ hơn để ảnh không trùng.

### Tự chọn file cho một đơn

- Ở cột **File in**, bấm nút xem để mở **Xem và tự chọn file**.
- Bên trái là các file của idol; bấm vào trang để chọn, hoặc **Lấy cả file**. Bấm **Thêm file PDF ngoài…** để lấy trang từ file bất kỳ trên máy.
- Có thể chọn nhiều hoặc ít trang hơn số card (ví dụ đơn 30 card bình thường 4 tờ, muốn in 5 tờ); ứng dụng chỉ cảnh báo.
- Bấm **Lưu lựa chọn**. **Dùng lại chọn tự động** để bỏ lựa chọn tay.
- Nếu file kho đã bị xóa khỏi ổ đĩa, bấm **Quét kho** rồi chọn lại.

### Trang phân cách

- Giữa các đơn phức tạp có một trang trắng để dễ tách đơn. Đổi ở **Trang trắng giữa đơn** (không chèn / chỉ đơn phức tạp / mọi đơn).

### Xóa dữ liệu

- Tab **Lịch sử**: **Xóa lần import** để xóa đơn của một file CSV đã import; **Xóa toàn bộ đơn** để làm sạch.
- **Bỏ đánh dấu** một lô in để các đơn đó được in lại.

## Cập nhật phần mềm

- Bấm **Check update** trên thanh công cụ. Nếu có bản mới, ứng dụng tự tải, tự đóng, cài đè và mở lại.
- Nếu vừa phát hành mà máy báo đang dùng bản mới nhất, đợi khoảng 5 phút rồi bấm lại.
- Nên đặt ứng dụng trong thư mục có quyền ghi (ví dụ `D:\Apps\PDFPageComposer`), không đặt trong `Program Files`.
