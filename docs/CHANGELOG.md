# Lịch sử phiên bản

Mỗi phiên bản là một GitHub Release `vX.Y.Z` có file `PDFPageComposer-vX.Y.Z.zip`. Máy người dùng nhận bản mới qua nút **Check update** (xem `docs/update/`).

## 1.3.3 — 2026-10-18

- Đơn TikTok: đơn có nhiều bộ card được tách theo từng bộ trong **Xem trước file in**, mỗi bộ có nút **Đổi file bộ này**.
- Hộp **Xem / chọn file**: các bộ card hiện thành nút **Bộ 1**, **Bộ 2**… kèm tên sản phẩm, bấm để chọn bộ cần sửa.

## 1.3.2 — 2026-10-18

- Đơn TikTok: ô **Tìm mã đơn / sản phẩm** ở tab Đơn hàng (tìm một phần mã đơn hoặc tên sản phẩm, phân loại, không cần dấu; tìm trong mọi nhóm).
- Tài liệu được tách theo module (`docs/composer`, `docs/tiktok`, `docs/update`).

## 1.3.1 — 2026-10-17

- Logo mới cho file exe, thanh taskbar và mọi cửa sổ.

## 1.3.0 — 2026-10-17

**Đơn TikTok (module mới)**

- Import file CSV đơn hàng từ TikTok Seller Center, nhiều shop; chống trùng, đơn đã tạo file in không bị in lại khi import file mới.
- Cấu hình SKU có gợi ý tự động và alias idol; quét kho PDF theo tên `IDOL - N PAGE - dd.MM.pdf`.
- Tự chọn ảnh mới nhất của idol cho đủ số trang; tự chọn file / trang bằng tay, thêm PDF ngoài kho, in nhiều hoặc ít trang hơn số card.
- Xem trước file in, bỏ đơn khỏi lô, tạo file in, trang trắng giữa đơn phức tạp.
- Lịch sử lô import và lô in, bỏ đánh dấu, tạo lại, xóa lần import, xuất danh sách đơn ra CSV (gồm "Đơn chưa làm file in").
- Hiển thị đơn theo đúng thứ tự file CSV, copy mã đơn bằng nút hoặc nháy đúp.

**Ghép lẻ**

- Giao diện mới, mở toàn màn hình, chữ lớn hơn; khay đầu ra dạng lưới.
- Kéo thả trang (đặt trước / sau), màn xem trước đầu ra mới.
- Phím Enter thêm trang đang chọn vào đầu ra.
- Tìm file không dấu; không thêm file đã có.
- Thumbnail chỉ render khi cuộn tới, nhẹ hơn khi mở nhiều file.

## 1.2.0 — 2026-07-28

- Mỗi nhóm trong khay đầu ra có màu riêng.
- Nút nhân bản cả nhóm; bỏ chọn trang nguồn sau khi thêm vào đầu ra; hiện số trang đang chọn trên nút thêm.

## 1.1.0 — 2026-07-17

- Sửa nhanh trang trong màn xem trước đầu ra (nhân bản, di chuyển, xóa).
- Check update đọc sẵn `latest.json` trên GitHub, không cần tự cấu hình link.

## 1.0.0 — 2026-07-17

- Bản đầu tiên: import nhiều PDF, thumbnail, chọn trang, khay đầu ra theo nhóm, nhân bản, sắp xếp, hoàn tác, xuất PDF và mở bằng Foxit, tự lưu phục hồi, nút **Check update**.
