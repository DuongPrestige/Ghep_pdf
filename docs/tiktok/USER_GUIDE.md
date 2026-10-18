# Hướng dẫn sử dụng — Đơn TikTok

Áp dụng cho phiên bản 1.3.2.

Bấm **Đơn TikTok** trên thanh công cụ để mở cửa sổ đơn hàng.

## Cài đặt lần đầu

1. Tab **Cài đặt**: thêm shop, chọn **Thư mục kho PDF** và thư mục lưu file in.
2. Bấm **Quét kho** để đọc các file trong kho. Tên file theo mẫu `TÊN IDOL - 5 PAGE - 24.09.pdf`.
3. Tab **Cấu hình SKU**: kiểm tra các SKU mới, bấm **Xác nhận gợi ý đã chọn**; sản phẩm không cần in (móc khóa, polaroid…) đặt **Bỏ qua**.

## Quy trình hằng ngày

1. Xuất file CSV đơn hàng từ TikTok Seller Center.
2. Bấm **Import đơn** (hoặc kéo thả file .csv vào cửa sổ), chọn shop và chế độ:
   - **File đầy đủ**: đơn của shop không còn trong file sẽ bị ẩn.
   - **Bổ sung**: không ẩn đơn nào.
3. Đơn đã làm file in ở lần trước tự được lọc ra, không bị in lại khi import file CSV mới.
4. Tab **Đơn hàng** hiện đơn theo đúng thứ tự file CSV. Nháy đúp hoặc bấm nút copy để copy mã đơn. Muốn tìm một đơn, gõ mã đơn (một phần cũng được) hoặc tên sản phẩm vào ô **Tìm mã đơn / sản phẩm**, không cần dấu; khi đang tìm, đơn ở mọi nhóm đều hiện.
5. Bấm **Xem trước file in** để xem mọi trang sẽ in: **Đổi file** để chọn lại trang cho đơn, **Bỏ khỏi lô** để không in đơn đó lần này, **Copy mã tất cả đơn**.
6. Bấm **Tạo file in**. File được mở bằng Foxit để in.
7. Dùng **Xuất CSV** với phạm vi "Đơn chưa làm file in" để lấy danh sách mã đơn còn lại.

## Cách chọn file ảnh

- Mặc định bật **Luôn lấy ảnh mới nhất**: mỗi bộ card lấy từ file mới nhất của idol, thiếu trang thì lấy tiếp file cũ hơn.
- File cuối chỉ lấy số trang đầu còn thiếu. Ví dụ Đình Bắc 20 card (3 tờ), file mới nhất có 5 trang → lấy 3 trang đầu của file đó.
- Đơn mua nhiều bộ: bộ 2, bộ 3… lùi dần sang file cũ hơn để ảnh không trùng.

## Tự chọn file cho một đơn

- Ở cột **File in**, bấm nút xem để mở **Xem và tự chọn file**.
- Bên trái là các file của idol; bấm vào trang để chọn, hoặc **Lấy cả file**. Bấm **Thêm file PDF ngoài…** để lấy trang từ file bất kỳ trên máy.
- Có thể chọn nhiều hoặc ít trang hơn số card (ví dụ đơn 30 card bình thường 4 tờ, muốn in 5 tờ); ứng dụng chỉ cảnh báo.
- Bấm **Lưu lựa chọn**. **Dùng lại chọn tự động** để bỏ lựa chọn tay.
- Nếu file kho đã bị xóa khỏi ổ đĩa, bấm **Quét kho** rồi chọn lại.

## Trang phân cách

- Giữa các đơn phức tạp có một trang trắng để dễ tách đơn. Đổi ở **Trang trắng giữa đơn** (không chèn / chỉ đơn phức tạp / mọi đơn).

## Xóa dữ liệu

- Tab **Lịch sử**: **Xóa lần import** để xóa đơn của một file CSV đã import; **Xóa toàn bộ đơn** để làm sạch (cấu hình SKU, alias và kho PDF vẫn giữ nguyên).
- Chọn lô in, chọn đơn trong lô rồi bấm **Bỏ đánh dấu** để đơn đó quay lại danh sách chưa in.
