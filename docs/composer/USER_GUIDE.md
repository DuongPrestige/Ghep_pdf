# Hướng dẫn sử dụng — Ghép lẻ PDF

Áp dụng cho phiên bản 1.4.0. Đây là màn hình chính khi mở ứng dụng (mở ở chế độ toàn màn hình).

## Mở file PDF

1. Bấm **Thêm PDF** để chọn một hoặc nhiều file PDF.
2. Hoặc kéo thả file PDF từ Windows Explorer vào cửa sổ ứng dụng.
3. File đã có trong danh sách (cùng đường dẫn hoặc cùng nội dung) sẽ không được thêm lần nữa.
4. Nếu file lỗi, thiếu quyền đọc hoặc cần mật khẩu, ứng dụng báo lỗi cho từng file và tiếp tục xử lý các file còn lại.

## Tìm và chọn trang

- Ô **Tìm** lọc theo tên file, không phân biệt dấu và hoa thường: gõ `dinh bac` sẽ ra `ĐÌNH BẮC.pdf`.
- Click một thumbnail để chọn hoặc bỏ chọn trang. Giữ **Ctrl** để chọn từng trang rời rạc, **Shift** để chọn một dải trang.
- Dùng **Chọn tất cả** hoặc **Bỏ chọn** ở header file để thao tác nhanh theo file; **Thu gọn** để ẩn các trang của file.
- Dùng thanh Zoom hoặc **Ctrl + con lăn** để đổi kích thước thumbnail.
- Thumbnail chỉ được tải khi cuộn tới, nên mở nhiều file vẫn nhẹ.

## Xem lớn

- Nháy đúp thumbnail để xem lớn; có **◀ Trước / Sau ▶**, **Vừa rộng**, **Vừa cao**, zoom +/−.
- Bấm **Đóng** hoặc **Esc** để đóng.

## Thêm trang vào đầu ra

1. Chọn các trang cần ghép.
2. Bấm **Thêm vào đầu ra** hoặc nhấn phím **Enter**.
3. Mỗi lần thêm tạo một nhóm mới (có màu riêng) ở cuối **Khay đầu ra**. Một trang nguồn có thể xuất hiện nhiều lần.

## Sắp xếp đầu ra

- Thứ tự trong Khay đầu ra là thứ tự trang trong file PDF xuất.
- Kéo thả trang để đổi vị trí: thả vào nửa trái của trang đích là đứng trước, nửa phải là đứng sau. Kéo tên nhóm để chuyển cả nhóm.
- **Nhân bản** thêm bản sao trang, **Nhân nhóm** nhân bản cả nhóm, **Xóa** bỏ trang.
- **Hoàn tác / Làm lại** cho các thao tác chính.

## Xem trước đầu ra

- Bấm **Xem trước đầu ra** để xem mọi trang sẽ xuất. **Lưới tất cả trang** xem dạng lưới, **Xem 1 trang** xem lớn trang đang chọn.
- Trong màn xem trước có thể kéo thả đổi vị trí, nhân bản, đưa lên trước / ra sau, hoặc xóa trang.

## Ghép 2 mặt

Dùng khi in card 2 mặt: in xấp giấy mặt 1, lật xấp giấy rồi in mặt 2.

1. Bấm **Ghép 2 mặt** trên thanh công cụ. Khay bên phải đổi thành **Khay 2 mặt** (khay đầu ra thường vẫn được giữ, bấm lại nút để quay về).
2. Chọn các trang ảnh của đơn thứ nhất, bấm **Thêm mặt 1** (hoặc Enter).
3. Chọn trang chữ ký, bấm **Thêm mặt 2** (hoặc Enter). Chỉ chọn 1 trang thì app tự lặp cho bằng số trang mặt 1.
4. Làm tiếp đơn thứ hai, thứ ba… theo cùng cách.
5. Bấm **Xuất PDF** và đặt tên file. File gồm mặt 1 của mọi cặp trước, sau đó mặt 2 cùng thứ tự. Ví dụ 13 tờ: trang 1–13 là mặt 1, trang 14–26 là mặt 2. Thanh trạng thái ghi rõ khoảng trang của từng mặt để in.

Lưu ý:

- Số tròn trên mỗi trang là số tờ; trang cùng số ở cột Mặt 1 và Mặt 2 sẽ in chung một tờ.
- Cặp thiếu mặt 2 hoặc hai mặt khác số trang có viền đỏ và chưa xuất được.
- Sửa một mặt bằng nút **Thay** của mặt đó: nếu đang chọn trang thì thay ngay; nếu chưa chọn, nút thành **Hủy thay** (màu cam) và nút trên thanh công cụ thành **Thay mặt X · cặp N** — chọn trang rồi bấm nút đó hoặc Enter. Bấm **Hủy thay** để thôi.
- Nút **Thêm** cạnh **Thay** dùng giống hệt, nhưng **thêm trang vào cuối** mặt đó và giữ các trang đang có (nút toolbar thành **Thêm vào mặt X · cặp N**, bấm **Hủy thêm** để thôi). Mặt 2 đang trống mà thêm 1 trang thì trang đó tự lặp cho đủ số trang mặt 1.
- **▲ / ▼** đổi thứ tự cặp, **Nhân bản** thêm bản sao cả cặp, **Xóa** bỏ cả cặp. **Hoàn tác / Làm lại** dùng được như bình thường.

## File nguồn bị thiếu

- Khi mở lại phiên, file nguồn không còn ở đường dẫn cũ sẽ được đánh dấu thiếu.
- Bấm **Chọn lại** ở header file để chọn lại file. Ứng dụng chỉ chấp nhận khi file mới khớp số trang và nội dung với file cũ.
- Khay đầu ra không tự xóa trang khi file nguồn bị thiếu.

## Xuất PDF

1. Kiểm tra lại thứ tự trang.
2. Bấm **Xuất PDF**. Ứng dụng không ghi đè PDF nguồn và không rasterize trang.
3. Sau khi xuất, file được mở bằng Foxit PDF Reader nếu có, hoặc bằng trình xem mặc định của Windows.

## Tự lưu

- Ứng dụng tự lưu phiên làm việc. Nếu lần trước đóng bất thường, khi mở lại có nút **Phục hồi** hoặc **Bỏ qua** ở thanh trạng thái.
