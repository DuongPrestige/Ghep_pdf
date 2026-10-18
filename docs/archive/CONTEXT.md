# Bối cảnh sản phẩm

## Tổng quan

**PDF Page Composer** là ứng dụng desktop Windows, xử lý hoàn toàn offline. Người dùng chọn trực quan các trang từ nhiều file PDF, có thể thêm lại cùng trang hoặc cùng nhóm nhiều lần, kiểm soát chính xác thứ tự rồi xuất một PDF tổng để mở và in bằng Foxit PDF Reader.

Đối tượng chính là nhân viên văn phòng quen dùng Windows/Foxit, thường xử lý từ vài chục đến hàng trăm trang và muốn thao tác nhanh bằng chuột thay vì nhập số trang thủ công.

## Vấn đề cần giải quyết

Quy trình mở từng PDF và chọn trang khi in tốn thời gian, dễ nhập nhầm và khó biểu diễn việc lặp lại một nhóm trang. Sản phẩm phải cho thấy tất cả file/trang trong một vùng cuộn, cho phép chọn bằng thumbnail và xây dựng một danh sách đầu ra độc lập.

Ví dụ chuẩn:

- File A: trang 3, 4.
- File B: trang 1–3; sau đó 4–5; sau đó lại 1–3.
- Đầu ra: `A3, A4, B1, B2, B3, B4, B5, B1, B2, B3` (10 trang).

## Mục tiêu và giá trị

- Giảm ít nhất 50% thời gian so với mở/in từng file.
- Giảm sai sót do nhập tay số trang.
- Hỗ trợ nhân bản trang/nhóm và sắp thứ tự phức tạp.
- Giữ tối đa chất lượng vector, font và hình ảnh của PDF nguồn.
- Tiếp tục dùng Foxit cho màu, khay giấy, khổ giấy và cấu hình in hiện có.

## Thuật ngữ

- **File nguồn:** PDF được import, nhận dạng bằng ID nội bộ/đường dẫn đầy đủ.
- **Trang nguồn:** một trang cụ thể thuộc file nguồn.
- **Trạng thái chọn:** đánh dấu tạm trên thumbnail để chuẩn bị thêm; không phải đầu ra.
- **Mục đầu ra:** bản sao logic tham chiếu tới một trang nguồn; một trang nguồn có thể có nhiều mục.
- **Nhóm đầu ra:** các mục được thêm trong cùng một thao tác, giữ thứ tự nội bộ.
- **Nhân bản nhóm:** sao chép toàn bộ nhóm theo đúng thứ tự.
- **Khay đầu ra:** danh sách chuẩn quyết định chính xác nội dung và thứ tự PDF xuất.
- **Project/phiên:** nguồn, đầu ra và trạng thái UI có thể lưu/phục hồi.

## Trong phạm vi MVP

- Import/chọn kéo thả nhiều PDF; phát hiện PDF lỗi, mã hóa hoặc bị thiếu.
- Hiển thị mọi file/trang trong một vùng cuộn; thumbnail nền, zoom và preview lớn.
- Chọn đơn, rời rạc, theo dải, theo vùng và theo file.
- Thêm lựa chọn thành nhóm đầu ra; cho phép trùng lặp không giới hạn nghiệp vụ.
- Nhân bản trang/nhóm, nhập số lượng, chọn vị trí chèn.
- Sắp xếp bằng kéo thả, xóa, thống kê tổng trang và Undo/Redo.
- Lưu project, tự lưu phục hồi và relink file nguồn bị thiếu.
- Xuất PDF mới, mở bằng Foxit hoặc ứng dụng PDF mặc định.
- Tìm kiếm, điều hướng và lọc theo trạng thái.

## Ngoài phạm vi MVP

- Chỉnh sửa nội dung, chèn chữ/hình/chữ ký/watermark hoặc OCR.
- In trực tiếp hoặc thay thế Foxit.
- Cloud, tài khoản, telemetry nội dung.
- macOS/Linux; chuyển PDF sang Word, Excel hoặc ảnh.

## Quyết định sản phẩm đã chốt

- Desktop Windows, không phải web; giao diện tiếng Việt.
- Thumbnail là cách thao tác chính; mọi file/trang nằm trên cùng màn hình cuộn.
- Khay đầu ra là nguồn sự thật; selection và output là hai trạng thái riêng.
- Trang nguồn được phép lặp; nhân bản nhóm là nghiệp vụ cốt lõi.
- Ứng dụng chỉ tạo/mở PDF, Foxit chịu trách nhiệm in.
- Ưu tiên offline, bảo toàn chất lượng và không sửa nguồn.

## Lộ trình

1. Core: import, lazy thumbnail, chọn, khay đầu ra, sắp xếp, export, Foxit.
2. Nâng cao: group, duplicate, multi-select đầu ra, Undo/Redo, preview.
3. Phục hồi/tối ưu: project, auto-save, cache, relink, hàng nghìn trang.
4. Trải nghiệm: filter/search, hai chế độ khay, dark mode, phím tắt/preset.
