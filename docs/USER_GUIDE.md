# Hướng Dẫn Sử Dụng Nhanh

## Mở file PDF

1. Bấm **Them PDF** để chọn một hoặc nhiều file PDF.
2. Hoặc kéo thả file PDF từ Windows Explorer vào cửa sổ ứng dụng.
3. Nếu file lỗi, thiếu quyền đọc hoặc cần mật khẩu, ứng dụng sẽ báo lỗi cho từng file và tiếp tục xử lý các file còn lại.

## Xem và chọn trang

- Trang PDF hiển thị trong Source Workspace theo từng file.
- Click một thumbnail để chọn hoặc bỏ chọn trang.
- Giữ **Ctrl** để chọn/bỏ chọn từng trang rời rạc.
- Giữ **Shift** để chọn một dải trang trong cùng file.
- Dùng **Chon tat ca** hoặc **Bo chon** ở header file để thao tác nhanh theo file.
- Dùng slider Zoom hoặc **Ctrl + con lăn** để đổi kích thước thumbnail.

## Preview lớn

- Bấm **Xem**, double click thumbnail hoặc nhấn **Enter** khi thumbnail đang focus để mở preview.
- Trong preview có thể đi **Truoc/Sau**, **Fit Width**, **Fit Height**, zoom bằng nút +/- hoặc **Ctrl + con lăn**.
- Bấm **Chon/bo chon** để đổi selection của chính trang đang preview.
- Bấm **Dong** hoặc **Esc** để đóng preview.

## Thêm trang vào đầu ra

1. Chọn các trang cần ghép.
2. Bấm **Them vao dau ra**.
3. Mỗi lần thêm sẽ tạo một group mới ở cuối Output Tray.
4. Trang nguồn có thể xuất hiện nhiều lần trong đầu ra.

## Sắp xếp và nhân bản đầu ra

- Trong Output Tray, thứ tự item là thứ tự trang trong file PDF xuất.
- Dùng **+** để nhân bản một item.
- Dùng **G+** để nhân bản cả group.
- Dùng **^/v** để di chuyển item lên/xuống.
- Dùng **G^/Gv** để di chuyển group lên/xuống.
- Có thể kéo thả item hoặc group để đổi vị trí.
- Dùng **x** để xóa item, **Gx** để xóa group, **G-** để thu gọn/mở group.
- Dùng **Undo/Redo** để hoàn tác hoặc làm lại các thao tác chính.

## File nguồn bị thiếu

- Khi mở/phục hồi phiên, file nguồn không còn ở đường dẫn cũ sẽ được đánh dấu thiếu.
- Bấm **Relink** ở header file để chọn lại file nguồn.
- Ứng dụng chỉ chấp nhận relink khi file mới khớp số trang và fingerprint với file đã import.
- Output Tray không tự xóa item khi file nguồn bị thiếu.

## Xuất PDF và mở bằng Foxit

1. Kiểm tra lại thứ tự trang trong Output Tray.
2. Bấm **Xuat PDF**.
3. Ứng dụng không ghi đè PDF nguồn.
4. File xuất giữ page object gốc, không rasterize trang.
5. Sau khi xuất, ứng dụng có thể mở file bằng Foxit PDF Reader nếu tìm thấy hoặc dùng viewer mặc định của Windows.

## Auto-save

- Ứng dụng tự lưu recovery cục bộ khi phiên làm việc thay đổi.
- Nếu lần trước ứng dụng đóng bất thường, khi mở lại sẽ có tùy chọn **Phuc hoi** hoặc **Bo qua** ở thanh trạng thái.
