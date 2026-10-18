# Đặc tả sản phẩm MVP

## 1. Yêu cầu chức năng

### FR-01 — Import PDF

- Chọn hoặc kéo thả nhiều file cùng lúc; hỗ trợ tốt ít nhất 50 file.
- Đọc tên, đường dẫn, dung lượng, số trang, encryption và fingerprint.
- Báo rõ file không phải PDF, hỏng, không có quyền đọc hoặc đã di chuyển.
- PDF có mật khẩu phải cho nhập hoặc bỏ qua.

### FR-02 — Duyệt nguồn và thumbnail

- Tất cả file/trang nằm trong cùng vùng cuộn; từng file có thể thu gọn.
- Section file hiển thị tên, tổng trang, số đang chọn, số đã thêm và các thao tác chọn/bỏ chọn/xóa.
- Thumbnail có preview, số trang, trạng thái chọn, nút preview và badge số lần xuất hiện trong đầu ra.
- Render nền ưu tiên viewport, cache, lazy loading; có loading/error/retry/locked states.
- Zoom bằng slider, preset và Ctrl + con lăn, không làm mất selection/vị trí cuộn.

### FR-03 — Chọn và điều hướng

- Click, Ctrl+Click, Shift+Click, kéo vùng, chọn/bỏ tất cả theo file/phạm vi.
- Tìm file theo tên; nhảy tới file hoặc trang.
- Lọc tất cả/đã chọn/đã thêm/chưa chọn.

### FR-04 — Preview

- Mở đúng file/trang bằng nhấp đúp, biểu tượng hoặc Enter.
- Hỗ trợ trước/sau, zoom, Fit Width, Fit Height và rotate chỉ để xem.
- Có thể chọn/bỏ chọn trong preview; đóng preview không mất selection.

### FR-05 — Xây dựng đầu ra

- Thêm selection vào cuối khay và tạo một group cho mỗi lần thêm.
- Lưu tham chiếu file nguồn và số trang cho từng item.
- Một trang nguồn được thêm nhiều lần.
- Hiển thị tổng trang và số thứ tự phẳng liên tục.

### FR-06 — Nhân bản

- Nhân bản một/nhiều trang hoặc cả group.
- Số lượng bản từ 1–999.
- Chèn ngay sau bản gốc hoặc cuối danh sách.
- Duplicate group giữ tuyệt đối thứ tự nội bộ.

### FR-07 — Sắp xếp và xóa

- Kéo thả trang, nhiều mục hoặc group; hiển thị vị trí thả.
- Hỗ trợ đưa lên đầu/xuống cuối.
- Xóa một/nhiều trang, group hoặc toàn bộ đầu ra.

### FR-08 — Undo/Redo

Áp dụng cho thêm đầu ra, xóa, nhân bản, sắp xếp và chọn/bỏ chọn hàng loạt.

### FR-09 — Lưu và phục hồi project

- Lưu nguồn/thứ tự, fingerprint, trạng thái thu gọn, zoom/vị trí cuộn, group và thứ tự đầu ra.
- Auto-save định kỳ và đề nghị phục hồi sau khi đóng bất thường.
- Khi nguồn thiếu, đánh dấu và cho relink; không tự xóa item liên quan.

### FR-10 — Export và mở viewer

- Xuất đúng thứ tự khay, không giảm chất lượng, không scale và giữ kích thước trang.
- Hỗ trợ mixed page size, portrait và landscape.
- Kiểm tra nguồn, quyền ghi và dung lượng; dừng an toàn khi lỗi.
- Phát hiện Foxit, cho cấu hình đường dẫn; fallback sang ứng dụng PDF mặc định.
- Không tự động in.

### FR-11 — Thống kê

Hiển thị tổng file, tổng trang nguồn, trang đang chọn, trang đầu ra và dung lượng xuất ước tính.

## 2. Giao diện

- Toolbar: Thêm PDF, Xóa file, Zoom, Chọn/Bỏ chọn, Undo, Redo, Xuất.
- Split view có thể resize: nguồn 70–75%, khay đầu ra 25–30%; khay có thể thu gọn.
- Khay hỗ trợ list mode (`01 — A.pdf — Trang 3`) và thumbnail mode.
- Status bar hiển thị selection, tổng file, tổng trang nguồn và đầu ra.
- UI tiếng Việt, tooltip cho chức năng chính, high-DPI và tối thiểu 1366×768.

## 3. Phím tắt

| Phím | Chức năng |
|---|---|
| Ctrl+O | Thêm PDF |
| Ctrl+Shift+O | Thêm thư mục PDF |
| Ctrl+A / Ctrl+Shift+A | Chọn / bỏ chọn tất cả trong phạm vi |
| Ctrl+Enter | Thêm selection vào đầu ra |
| Ctrl+D | Nhân bản item/group |
| Delete | Xóa khỏi đầu ra |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+S / Ctrl+Shift+S | Lưu / Lưu project thành |
| Ctrl+E | Xuất PDF |
| Enter / Esc | Mở / đóng preview |
| Ctrl+con lăn | Zoom thumbnail |

## 4. Yêu cầu phi chức năng

### Hiệu năng

- Import 10 file/500 trang: UI phản hồi trong 2 giây.
- Thumbnail đầu tiên bắt đầu xuất hiện trong 1 giây.
- Render không khóa UI; cuộn mục tiêu tối thiểu 30 FPS trong điều kiện thông thường.
- Export 500 trang không tăng RAM mất kiểm soát.
- Thiết kế mở rộng tới 50–100 file và 2.000–5.000 trang.

### Độ ổn định và an toàn

- Không làm hỏng/thay đổi nguồn; dọn file tạm sau export lỗi.
- Auto-save, log lỗi cục bộ và phục hồi phiên.
- Hoàn toàn offline; không upload hoặc phân tích nội dung từ xa.
- Không log nội dung PDF/mật khẩu; không lưu mật khẩu plain text.

### Tương thích và sử dụng

- Windows 10/11 64-bit, màn hình từ 1366×768, hỗ trợ DPI cao.
- Thao tác chính dùng được bằng chuột; phím tắt nhất quán với Windows.

## 5. Tiêu chí nghiệm thu

- **AC-01:** Import ít nhất 10 PDF trong một lần.
- **AC-02:** Mọi file/trang xuất hiện trong cùng vùng cuộn.
- **AC-03:** Click thumbnail đổi đúng trạng thái chọn.
- **AC-04:** Zoom không làm mất selection.
- **AC-05:** Preview mở đúng file và trang.
- **AC-06:** Cùng trang được thêm nhiều lần.
- **AC-07:** Duplicate `B1, B2, B3` tạo `B1, B2, B3, B1, B2, B3`.
- **AC-08:** Kéo thả cập nhật đúng thứ tự đầu ra.
- **AC-09:** Ví dụ chuẩn xuất đúng `A3, A4, B1, B2, B3, B4, B5, B1, B2, B3`.
- **AC-10:** File xuất mở được bằng Foxit Reader.
- **AC-11:** Text, ảnh và vector không giảm chất lượng đáng kể.
- **AC-12:** File nguồn không có bất kỳ thay đổi nào.

## 6. Kịch bản lỗi bắt buộc

- File không hợp lệ: nêu tên và lý do.
- File mã hóa: nhập mật khẩu hoặc bỏ qua.
- Nguồn thiếu khi mở project: đánh dấu, relink, giữ item cho tới khi người dùng xác nhận.
- Không mở được Foxit: chọn executable hoặc mở bằng viewer mặc định.
- Thiếu dung lượng/quyền ghi: cảnh báo và dừng export an toàn.
- Nguồn đổi sau import: phát hiện bằng fingerprint và yêu cầu quyết định trước export.
