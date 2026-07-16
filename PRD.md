# PRD — PDF Page Composer

## 1. Thông tin tài liệu

- **Tên sản phẩm:** PDF Page Composer
- **Loại sản phẩm:** Ứng dụng desktop Windows
- **Mục đích chính:** Chọn trực quan các trang từ nhiều file PDF, sắp xếp và nhân bản trang hoặc nhóm trang theo yêu cầu, sau đó xuất thành một file PDF tổng để mở và in bằng Foxit PDF Reader.
- **Phiên bản tài liệu:** 1.0
- **Ngày tạo:** 2026-07-16
- **Đối tượng sử dụng tài liệu:** Product Owner, Business Analyst, UI/UX Designer, Developer, Tester.

---

## 2. Bối cảnh và vấn đề

Người dùng thường phải xử lý nhiều file PDF trong cùng một phiên làm việc. Mỗi file có thể chứa nhiều trang nhưng chỉ một số trang cần được in.

Nhu cầu thực tế không chỉ là chọn trang đơn lẻ, mà còn bao gồm:

- Chọn trang từ nhiều file PDF khác nhau.
- Xem trực quan toàn bộ trang của tất cả file đã import.
- Chọn trang bằng thao tác click thay vì nhập thủ công số trang.
- Xem preview lớn và điều chỉnh kích thước thumbnail.
- Thêm cùng một trang hoặc cùng một nhóm trang nhiều lần.
- Kiểm soát chính xác thứ tự trang trong file PDF đầu ra.
- Xuất một file PDF tổng rồi mở bằng Foxit PDF Reader để sử dụng cấu hình in màu, khay giấy, khổ giấy và các thiết lập in đã có.

Ví dụ:

- File A: lấy trang 3, 4.
- File B: lấy trang 1–3 một lần, trang 4–5 một lần, sau đó lấy lại trang 1–3 một lần.

File đầu ra phải có thứ tự:

```text
A3, A4, B1, B2, B3, B4, B5, B1, B2, B3
```

Tổng cộng 10 trang.

---

## 3. Mục tiêu sản phẩm

### 3.1. Mục tiêu chính

Xây dựng một ứng dụng desktop Windows giúp người dùng tạo file PDF tổng theo cách trực quan, nhanh và hạn chế sai sót.

### 3.2. Giá trị mang lại

- Giảm thời gian mở từng file PDF và chọn trang thủ công khi in.
- Giảm sai sót do nhập nhầm số trang.
- Hỗ trợ quy trình in phức tạp có nhân bản trang hoặc nhóm trang.
- Giữ nguyên chất lượng PDF gốc.
- Cho phép tiếp tục dùng Foxit PDF Reader để in với cấu hình hiện tại.

### 3.3. Chỉ số thành công

- Người dùng có thể import tối thiểu 10 file PDF trong một phiên.
- Tất cả trang được hiển thị trực quan dưới dạng thumbnail.
- Người dùng có thể tạo file đầu ra chính xác mà không cần nhập tay từng số trang.
- File xuất giữ nguyên chất lượng nội dung PDF.
- Không làm thay đổi hoặc mất thiết lập in trong Foxit Reader.
- Thời gian thao tác giảm ít nhất 50% so với quy trình mở và in từng file.

---

## 4. Phạm vi sản phẩm

## 4.1. Trong phạm vi MVP

- Import nhiều file PDF.
- Kéo thả file PDF từ Windows Explorer.
- Hiển thị tất cả file và tất cả trang trên một vùng cuộn.
- Hiển thị thumbnail từng trang.
- Click để chọn hoặc bỏ chọn trang.
- Chọn nhiều trang liên tiếp hoặc không liên tiếp.
- Zoom kích thước thumbnail.
- Mở preview lớn của một trang.
- Thêm các trang đã chọn vào danh sách đầu ra.
- Một trang nguồn có thể xuất hiện nhiều lần trong đầu ra.
- Nhân bản trang hoặc nhóm trang.
- Kéo thả để thay đổi thứ tự đầu ra.
- Xóa trang hoặc nhóm trang khỏi đầu ra.
- Hiển thị tổng số trang đầu ra.
- Xuất thành một file PDF mới.
- Mở file vừa xuất bằng Foxit PDF Reader.
- Undo/Redo các thao tác chính.
- Lưu tạm phiên làm việc để phục hồi khi ứng dụng đóng bất thường.

## 4.2. Ngoài phạm vi MVP

- Chỉnh sửa nội dung PDF.
- Chèn chữ, hình ảnh, chữ ký hoặc watermark.
- OCR tài liệu scan.
- In trực tiếp từ ứng dụng.
- Đồng bộ cloud.
- Quản lý tài khoản người dùng.
- Hỗ trợ macOS hoặc Linux.
- Chuyển đổi PDF sang Word, Excel hoặc ảnh.
- Thay thế Foxit Reader trong vai trò phần mềm in.

---

## 5. Người dùng mục tiêu

### 5.1. Người dùng chính

Nhân viên văn phòng thường xuyên xử lý và in nhiều file PDF.

### 5.2. Đặc điểm

- Sử dụng Windows.
- Đã quen với Foxit PDF Reader.
- Không muốn thao tác kỹ thuật phức tạp.
- Cần thao tác nhanh bằng chuột.
- Có thể xử lý từ vài chục đến hàng trăm trang mỗi phiên.
- Có nhu cầu lấy lại cùng một trang hoặc nhóm trang nhiều lần.

---

## 6. Khái niệm nghiệp vụ

### 6.1. File nguồn

File PDF được người dùng import vào ứng dụng.

### 6.2. Trang nguồn

Một trang cụ thể thuộc file PDF nguồn.

Ví dụ:

```text
File B — Trang 3
```

### 6.3. Trạng thái được chọn

Trang đang được người dùng đánh dấu trên khu vực thumbnail để chuẩn bị thêm vào file đầu ra.

### 6.4. Mục đầu ra

Một bản sao logic của trang nguồn đã được thêm vào danh sách đầu ra.

Một trang nguồn có thể tạo ra nhiều mục đầu ra.

### 6.5. Nhóm đầu ra

Một tập hợp các trang được thêm vào đầu ra trong cùng một thao tác và giữ nguyên thứ tự chọn.

Ví dụ:

```text
B1, B2, B3
```

### 6.6. Nhân bản nhóm

Tạo thêm một bản sao của toàn bộ nhóm, giữ nguyên thứ tự bên trong.

Ví dụ:

```text
B1, B2, B3
```

nhân bản hai lần sẽ thành:

```text
B1, B2, B3, B1, B2, B3
```

### 6.7. File đầu ra

File PDF mới được tạo từ danh sách các mục đầu ra theo đúng thứ tự hiện tại.

---

## 7. Quy tắc nghiệp vụ

### BR-01 — Giữ nguyên chất lượng

Ứng dụng phải sao chép trực tiếp các page object từ file nguồn sang file đầu ra, không rasterize thành ảnh trừ khi bắt buộc.

### BR-02 — Tách biệt nguồn và đầu ra

Việc chọn hoặc bỏ chọn thumbnail không được tự động xóa các trang đã có trong danh sách đầu ra.

### BR-03 — Cho phép trùng lặp

Một trang nguồn có thể xuất hiện không giới hạn số lần trong file đầu ra.

### BR-04 — Thứ tự đầu ra là nguồn sự thật

File PDF xuất phải được tạo theo đúng thứ tự hiển thị trong khay đầu ra.

### BR-05 — Thứ tự chọn trang

Khi thêm nhiều trang cùng lúc, mặc định sắp xếp theo:

1. Thứ tự file trong vùng nguồn.
2. Số trang tăng dần trong từng file.

Có thể cho phép người dùng thay đổi thứ tự sau khi thêm vào đầu ra.

### BR-06 — Nhân bản nhóm

Nhân bản nhóm phải sao chép toàn bộ trang trong nhóm và giữ nguyên thứ tự.

### BR-07 — Không tự động in

Ứng dụng chỉ tạo file PDF và mở bằng Foxit Reader. Foxit Reader chịu trách nhiệm in.

### BR-08 — Không thay đổi file nguồn

Ứng dụng không được sửa file PDF gốc.

### BR-09 — Xử lý file trùng tên

Ứng dụng phải phân biệt file bằng đường dẫn đầy đủ hoặc định danh nội bộ, không chỉ bằng tên file.

### BR-10 — File có mật khẩu

File PDF được mã hóa phải được phát hiện và yêu cầu mật khẩu trước khi render hoặc xuất.

---

## 8. Luồng nghiệp vụ chính

## 8.1. Import file

1. Người dùng bấm **Thêm PDF** hoặc kéo thả file vào ứng dụng.
2. Ứng dụng kiểm tra định dạng.
3. Ứng dụng đọc metadata:
   - Tên file.
   - Đường dẫn.
   - Số trang.
   - Kích thước file.
   - Trạng thái mã hóa.
4. Ứng dụng tạo thumbnail nền.
5. Tất cả file được hiển thị nối tiếp theo chiều dọc.

## 8.2. Chọn trang

1. Người dùng click vào thumbnail.
2. Trang chuyển sang trạng thái đã chọn.
3. Click lại để bỏ chọn.
4. Người dùng có thể:
   - Ctrl + Click để chọn rời rạc.
   - Shift + Click để chọn một dải.
   - Kéo chuột để chọn vùng.
   - Chọn tất cả trong một file.
   - Bỏ chọn tất cả trong một file.

## 8.3. Thêm vào đầu ra

1. Người dùng chọn một hoặc nhiều trang.
2. Bấm **Thêm vào đầu ra**.
3. Ứng dụng tạo một nhóm đầu ra.
4. Nhóm được thêm vào cuối khay đầu ra.
5. Hệ thống cập nhật tổng số trang.

## 8.4. Nhân bản trang hoặc nhóm

1. Người dùng chọn một mục hoặc một nhóm trong khay đầu ra.
2. Bấm **Nhân bản** hoặc nhập số lượng bản.
3. Ứng dụng chèn bản sao:
   - Ngay sau mục gốc.
   - Hoặc tại cuối danh sách, tùy lựa chọn.
4. Thứ tự bên trong nhóm phải được giữ nguyên.

## 8.5. Sắp xếp đầu ra

1. Người dùng kéo mục hoặc nhóm.
2. Ứng dụng hiển thị vị trí thả.
3. Khi thả, danh sách được cập nhật.
4. Số thứ tự trang đầu ra được tính lại.

## 8.6. Preview

1. Người dùng nhấp đúp hoặc bấm biểu tượng preview.
2. Ứng dụng mở cửa sổ preview lớn.
3. Hỗ trợ:
   - Trang trước.
   - Trang sau.
   - Fit Width.
   - Fit Height.
   - Zoom.
   - Rotate tạm thời để xem.
4. Preview không thay đổi file nguồn.

## 8.7. Xuất PDF

1. Người dùng bấm **Xuất PDF**.
2. Ứng dụng kiểm tra danh sách đầu ra.
3. Người dùng chọn:
   - Tên file.
   - Thư mục lưu.
4. Ứng dụng tạo PDF theo đúng thứ tự.
5. Sau khi hoàn tất, hiển thị:
   - Mở thư mục.
   - Mở bằng Foxit Reader.
6. Nếu Foxit không tồn tại, dùng ứng dụng PDF mặc định.

---

## 9. Ví dụ nghiệp vụ bắt buộc

### 9.1. Dữ liệu nguồn

- File A: chọn trang 3, 4.
- File B: chọn trang 1–3.
- File B: chọn trang 4–5.
- File B: chọn lại trang 1–3.

### 9.2. Đầu ra kỳ vọng

```text
01 — File A — Trang 3
02 — File A — Trang 4
03 — File B — Trang 1
04 — File B — Trang 2
05 — File B — Trang 3
06 — File B — Trang 4
07 — File B — Trang 5
08 — File B — Trang 1
09 — File B — Trang 2
10 — File B — Trang 3
```

### 9.3. Kết quả

- File đầu ra có đúng 10 trang.
- Chất lượng nội dung không giảm.
- Mở được bằng Foxit Reader.
- Người dùng có thể in bằng cấu hình Foxit hiện tại.

---

## 10. Yêu cầu giao diện

## 10.1. Bố cục tổng thể

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Toolbar                                                                      │
│ Thêm PDF | Xóa file | Zoom | Chọn tất cả | Bỏ chọn | Undo | Redo | Xuất   │
├──────────────────────────────────────────────────┬───────────────────────────┤
│                                                  │ KHAY ĐẦU RA               │
│ TẤT CẢ FILE VÀ TẤT CẢ TRANG                      │                           │
│                                                  │ 01 A.pdf — Trang 3       │
│ A.pdf                                            │ 02 A.pdf — Trang 4       │
│ [1] [2] [✓3] [✓4] [5] [6]                      │ 03 B.pdf — Trang 1       │
│                                                  │ ...                       │
│ B.pdf                                            │                           │
│ [✓1] [✓2] [✓3] [✓4] [✓5]                       │ [Nhân bản] [Xóa]        │
│                                                  │                           │
│ C.pdf                                            │ Tổng: 10 trang           │
│ [1] [2] [3] [4] [5] ...                         │                           │
├──────────────────────────────────────────────────┴───────────────────────────┤
│ Đang chọn: 3 trang | File: 10 | Trang nguồn: 128 | Đầu ra: 10 trang       │
└──────────────────────────────────────────────────────────────────────────────┘
```

## 10.2. Tỷ lệ khu vực

- Vùng file nguồn: 70–75%.
- Khay đầu ra: 25–30%.
- Người dùng có thể kéo thay đổi kích thước hai vùng.
- Khay đầu ra có thể thu gọn.

## 10.3. Hiển thị file nguồn

Mỗi file là một section gồm:

- Tên file.
- Số trang.
- Số trang đang được chọn.
- Số trang đã được thêm vào đầu ra.
- Nút chọn tất cả.
- Nút bỏ chọn.
- Nút thu gọn.
- Nút xóa file khỏi phiên.

## 10.4. Thumbnail trang

Mỗi thumbnail gồm:

- Hình preview.
- Số trang.
- Checkbox hoặc dấu tick.
- Viền trạng thái.
- Biểu tượng preview khi hover.
- Badge hiển thị số lần trang đã xuất hiện trong đầu ra.

## 10.5. Trạng thái thumbnail

- Mặc định.
- Hover.
- Đã chọn.
- Đã thêm vào đầu ra.
- Đã chọn và đã thêm.
- Đang render.
- Lỗi render.
- File bị khóa.

## 10.6. Zoom thumbnail

- Slider zoom.
- Các preset:
  - Nhỏ.
  - Vừa.
  - Lớn.
- Phím tắt Ctrl + con lăn chuột.
- Zoom không làm mất vị trí cuộn hiện tại.

## 10.7. Khay đầu ra

Hỗ trợ hai chế độ hiển thị:

### Chế độ danh sách

```text
01 — A.pdf — Trang 3
02 — A.pdf — Trang 4
03 — B.pdf — Trang 1
```

### Chế độ thumbnail

Hiển thị thumbnail nhỏ theo thứ tự PDF đầu ra.

Người dùng có thể chuyển đổi giữa hai chế độ.

## 10.8. Preview lớn

- Mở dạng modal hoặc panel nổi.
- Không làm mất lựa chọn hiện tại.
- Có zoom, fit, chuyển trang.
- Có thể chọn hoặc bỏ chọn trang ngay trong preview.

---

## 11. Yêu cầu chức năng chi tiết

### FR-01 — Import nhiều PDF

- Chọn nhiều file cùng lúc.
- Kéo thả nhiều file.
- Không giới hạn cứng số lượng file trong nghiệp vụ.
- MVP phải hỗ trợ tốt ít nhất 50 file.

### FR-02 — Hiển thị toàn bộ trang

- Tất cả file và tất cả trang phải tồn tại trong cùng một vùng cuộn.
- Cho phép thu gọn từng file.
- Có mini navigation để nhảy nhanh tới file.

### FR-03 — Render thumbnail

- Render nền theo ưu tiên vùng đang nhìn thấy.
- Cache thumbnail.
- Không render toàn bộ độ phân giải cao ngay khi import.

### FR-04 — Chọn trang

- Click để chọn.
- Ctrl + Click.
- Shift + Click.
- Chọn theo vùng kéo.
- Chọn tất cả theo file.
- Bỏ chọn tất cả.

### FR-05 — Thêm vào đầu ra

- Thêm trang đã chọn vào cuối danh sách.
- Tạo group cho mỗi lần thêm.
- Giữ metadata file nguồn và số trang.

### FR-06 — Nhân bản

- Nhân bản một trang.
- Nhân bản nhiều trang.
- Nhân bản một group.
- Cho nhập số lượng từ 1 đến 999.
- Cho chọn vị trí chèn:
  - Sau bản gốc.
  - Cuối danh sách.

### FR-07 — Sắp xếp

- Kéo thả trang.
- Kéo thả group.
- Di chuyển lên đầu.
- Di chuyển xuống cuối.

### FR-08 — Xóa

- Xóa một trang đầu ra.
- Xóa nhiều trang.
- Xóa group.
- Xóa toàn bộ đầu ra.

### FR-09 — Undo/Redo

Áp dụng cho:

- Thêm vào đầu ra.
- Xóa.
- Nhân bản.
- Sắp xếp.
- Chọn hoặc bỏ chọn hàng loạt.

### FR-10 — Xuất PDF

- Không giảm chất lượng.
- Giữ kích thước trang gốc.
- Hỗ trợ trang có kích thước khác nhau.
- Hỗ trợ portrait và landscape trong cùng một file.
- Không tự động scale trang.

### FR-11 — Mở bằng Foxit

- Tự phát hiện Foxit Reader.
- Cho phép người dùng cấu hình đường dẫn Foxit thủ công.
- Sau khi xuất có nút **Mở bằng Foxit Reader**.
- Nếu không có Foxit, mở bằng ứng dụng mặc định.

### FR-12 — Lưu phiên

Lưu:

- Danh sách file nguồn.
- Thứ tự file.
- Trạng thái thu gọn.
- Kích thước thumbnail.
- Danh sách trang đầu ra.
- Group.
- Thứ tự đầu ra.

### FR-13 — Tìm kiếm và điều hướng

- Tìm file theo tên.
- Nhảy tới file.
- Nhảy tới số trang trong file.
- Lọc:
  - Tất cả.
  - Đã chọn.
  - Đã thêm vào đầu ra.
  - Chưa chọn.

### FR-14 — Thống kê

Hiển thị:

- Tổng số file.
- Tổng số trang nguồn.
- Tổng số trang đang chọn.
- Tổng số trang đầu ra.
- Dung lượng ước tính file xuất.

---

## 12. Yêu cầu phi chức năng

## 12.1. Hiệu năng

- Import 10 file, tổng 500 trang: giao diện phản hồi trong vòng 2 giây.
- Thumbnail bắt đầu xuất hiện trong vòng 1 giây.
- Không khóa UI khi render.
- Cuộn mượt ở mức tối thiểu 30 FPS trong điều kiện thông thường.
- Xuất 500 trang mà không vượt mức RAM không kiểm soát.

## 12.2. Khả năng mở rộng

- Thiết kế hướng tới 50–100 file.
- Có thể xử lý tổng 2.000–5.000 trang bằng virtualization và lazy loading.

## 12.3. Độ ổn định

- Không làm hỏng file nguồn.
- Nếu xuất lỗi, file tạm phải được xóa.
- Tự động lưu trạng thái định kỳ.
- Có log lỗi cục bộ.

## 12.4. Bảo mật và riêng tư

- Xử lý hoàn toàn offline.
- Không tải PDF lên server.
- Không gửi dữ liệu phân tích nội dung.
- Không lưu mật khẩu PDF dạng plain text.

## 12.5. Khả năng sử dụng

- Giao diện tiếng Việt.
- Có tooltip cho chức năng chính.
- Phím tắt nhất quán với Windows.
- Thao tác chính phải thực hiện được bằng chuột.

## 12.6. Tương thích

- Windows 10 64-bit.
- Windows 11 64-bit.
- Màn hình từ 1366×768 trở lên.
- Hỗ trợ màn hình DPI cao.

---

## 13. Phím tắt đề xuất

| Phím tắt | Chức năng |
|---|---|
| Ctrl + O | Thêm file PDF |
| Ctrl + Shift + O | Thêm thư mục PDF |
| Ctrl + A | Chọn tất cả trang trong phạm vi hiện tại |
| Ctrl + Shift + A | Bỏ chọn tất cả |
| Ctrl + Enter | Thêm trang đã chọn vào đầu ra |
| Ctrl + D | Nhân bản mục hoặc group |
| Delete | Xóa khỏi đầu ra |
| Ctrl + Z | Undo |
| Ctrl + Y | Redo |
| Ctrl + S | Lưu project |
| Ctrl + Shift + S | Lưu project thành |
| Ctrl + E | Xuất PDF |
| Enter | Mở preview |
| Esc | Đóng preview |
| Ctrl + con lăn | Zoom thumbnail |

---

## 14. Xử lý lỗi

### 14.1. File không hợp lệ

Hiển thị tên file và lý do:

- Không phải PDF.
- File bị hỏng.
- Không có quyền đọc.
- File đã bị di chuyển.

### 14.2. File có mật khẩu

- Hiển thị hộp nhập mật khẩu.
- Cho phép bỏ qua file.
- Không lưu mật khẩu sau khi đóng phiên trừ khi người dùng bật tùy chọn.

### 14.3. Thiếu file nguồn khi mở project

- Đánh dấu file bị thiếu.
- Cho phép chọn lại đường dẫn.
- Không xóa các mục đầu ra liên quan trước khi người dùng xác nhận.

### 14.4. Không thể mở Foxit

- Hiển thị nút chọn đường dẫn Foxit.
- Cho phép mở bằng ứng dụng mặc định.

### 14.5. Không đủ dung lượng

- Kiểm tra dung lượng đĩa trước khi xuất.
- Cảnh báo và dừng xuất an toàn.

---

## 15. Tech stack đề xuất

## 15.1. Nền tảng

- **Hệ điều hành:** Windows 10/11 64-bit.
- **Framework:** .NET 8 hoặc .NET 10 LTS tại thời điểm triển khai.
- **Ngôn ngữ:** C#.

## 15.2. UI Framework

### Lựa chọn khuyến nghị: WPF

Lý do:

- Ổn định và trưởng thành.
- Hỗ trợ data binding tốt.
- Phù hợp ứng dụng desktop nghiệp vụ.
- Hỗ trợ virtualization.
- Dễ triển khai drag-and-drop phức tạp.
- Dễ xử lý multi-panel, thumbnail grid và preview.
- Hệ sinh thái thư viện lớn.

### Kiến trúc UI

- MVVM.
- CommunityToolkit.Mvvm.
- Dependency Injection bằng Microsoft.Extensions.DependencyInjection.

### Thư viện UI tùy chọn

- MaterialDesignInXaml hoặc MahApps.Metro.
- Có thể xây custom theme để giao diện gọn và tập trung vào nội dung PDF.

## 15.3. PDF Rendering

### Khuyến nghị

- PDFium thông qua một wrapper .NET phù hợp.
- Có thể sử dụng PdfiumViewer hoặc một binding được duy trì tốt.

Nhiệm vụ:

- Đọc số trang.
- Render thumbnail.
- Render preview lớn.
- Đọc page size và rotation.

## 15.4. PDF Composition

### Khuyến nghị ưu tiên

- PdfSharpCore hoặc PDFsharp cho tác vụ copy/import page.
- Có thể đánh giá iText 8 nếu cần chức năng PDF nâng cao.

### Lưu ý giấy phép

- iText dùng mô hình AGPL/commercial, cần đánh giá pháp lý trước khi dùng.
- Ưu tiên thư viện có giấy phép phù hợp với mục đích phân phối sản phẩm.

### Yêu cầu kỹ thuật

- Import page object trực tiếp.
- Không chuyển trang thành ảnh.
- Giữ kích thước trang.
- Giữ vector, font và hình ảnh gốc tối đa có thể.

## 15.5. Drag and Drop

- GongSolutions.WPF.DragDrop hoặc custom behavior.
- Hỗ trợ drag:
  - File nguồn.
  - Group đầu ra.
  - Trang đầu ra.
  - Nhiều mục cùng lúc.

## 15.6. Virtualization và rendering

- VirtualizingStackPanel.
- ItemsControl hoặc ListView có virtualization.
- Phân đoạn theo file.
- Thumbnail render bất đồng bộ.
- CancellationToken để hủy render ngoài viewport.
- MemoryCache hoặc cache file tạm.

## 15.7. Lưu project

### Định dạng

JSON, ví dụ:

```json
{
  "version": 1,
  "sourceFiles": [],
  "outputGroups": [],
  "uiState": {}
}
```

### Dữ liệu cần lưu

- Đường dẫn file.
- Hash hoặc fingerprint.
- Số trang.
- Danh sách group.
- Danh sách mục đầu ra.
- Thứ tự.
- Zoom.
- Trạng thái thu gọn.
- Vị trí cuộn gần nhất.

## 15.8. Logging

- Serilog.
- Ghi log local.
- Rolling file theo ngày.
- Không ghi nội dung PDF hoặc mật khẩu.

## 15.9. Testing

- xUnit.
- FluentAssertions.
- Moq hoặc NSubstitute.
- Test nghiệp vụ:
  - Group.
  - Nhân bản.
  - Sắp xếp.
  - Tổng số trang.
  - Xuất thứ tự chính xác.
- UI test có thể dùng FlaUI.

## 15.10. Đóng gói và cài đặt

- MSIX hoặc Inno Setup.
- Có bản self-contained x64.
- Không yêu cầu người dùng cài .NET riêng.
- Có tùy chọn tạo shortcut desktop.
- Có cấu hình đường dẫn Foxit.

---

## 16. Kiến trúc hệ thống đề xuất

```text
PDFPageComposer.sln

src/
├── PDFPageComposer.App
│   ├── Views
│   ├── ViewModels
│   ├── Controls
│   ├── Behaviors
│   ├── Themes
│   └── App.xaml
│
├── PDFPageComposer.Application
│   ├── UseCases
│   ├── Services
│   ├── Commands
│   ├── DTOs
│   └── Interfaces
│
├── PDFPageComposer.Domain
│   ├── Entities
│   ├── ValueObjects
│   ├── Rules
│   └── Events
│
├── PDFPageComposer.Infrastructure
│   ├── PdfRendering
│   ├── PdfComposition
│   ├── Persistence
│   ├── FileSystem
│   ├── FoxitIntegration
│   └── Logging
│
└── PDFPageComposer.Tests
    ├── Unit
    ├── Integration
    └── UI
```

---

## 17. Mô hình dữ liệu đề xuất

### SourcePdfFile

```text
Id
FilePath
DisplayName
PageCount
FileSize
Fingerprint
IsEncrypted
IsMissing
IsCollapsed
```

### SourcePdfPage

```text
Id
SourceFileId
PageNumber
Width
Height
Rotation
ThumbnailState
IsSelected
```

### OutputGroup

```text
Id
Name
CreatedAt
Items
IsCollapsed
```

### OutputPageItem

```text
Id
GroupId
SourceFileId
SourcePageNumber
OutputIndex
```

### ProjectState

```text
Version
SourceFiles
OutputGroups
ThumbnailZoom
PanelWidth
LastExportPath
FoxitExecutablePath
```

---

## 18. Trạng thái và hành vi quan trọng

### 18.1. Trang đã chọn nhưng chưa thêm

- Viền màu nổi bật.
- Dấu tick.
- Không xuất hiện trong file đầu ra.

### 18.2. Trang đã thêm vào đầu ra

- Có badge số lần xuất hiện.
- Ví dụ `×3`.

### 18.3. Trang đang render

- Hiển thị skeleton.
- Không khóa thao tác với phần khác.

### 18.4. Trang lỗi

- Hiển thị placeholder lỗi.
- Có nút thử lại.

### 18.5. File đang được xử lý

- Hiển thị tiến độ thumbnail.
- Cho phép người dùng tiếp tục thao tác với file đã sẵn sàng.

---

## 19. MVP ưu tiên theo giai đoạn

## Giai đoạn 1 — Core PDF workflow

- Import nhiều PDF.
- Hiển thị tất cả file và trang.
- Thumbnail lazy loading.
- Click chọn trang.
- Thêm trang vào đầu ra.
- Khay đầu ra.
- Kéo thả sắp xếp.
- Xuất PDF.
- Mở bằng Foxit.

## Giai đoạn 2 — Nhân bản và thao tác nâng cao

- Group.
- Nhân bản group.
- Nhập số lượng bản.
- Multi-select trong đầu ra.
- Undo/Redo.
- Preview lớn.

## Giai đoạn 3 — Khả năng phục hồi và tối ưu

- Lưu project.
- Auto-save.
- Cache thumbnail.
- Phục hồi file bị thiếu.
- Tối ưu hàng nghìn trang.

## Giai đoạn 4 — Trải nghiệm nâng cao

- Filter.
- Search.
- Chế độ thumbnail/list cho đầu ra.
- Dark mode.
- Cấu hình phím tắt.
- Lưu preset UI.

---

## 20. Tiêu chí nghiệm thu MVP

### AC-01

Import được ít nhất 10 file PDF trong một lần.

### AC-02

Tất cả file và trang xuất hiện trong cùng một vùng cuộn.

### AC-03

Click thumbnail thay đổi chính xác trạng thái chọn.

### AC-04

Zoom thumbnail hoạt động mà không mất lựa chọn.

### AC-05

Preview lớn mở đúng file và đúng trang.

### AC-06

Thêm cùng một trang nhiều lần vào đầu ra.

### AC-07

Nhân bản group `B1, B2, B3` tạo đúng:

```text
B1, B2, B3, B1, B2, B3
```

### AC-08

Kéo thả thay đổi thứ tự đầu ra chính xác.

### AC-09

Ví dụ nghiệp vụ 10 trang trong mục 9 xuất đúng thứ tự.

### AC-10

File đầu ra mở được bằng Foxit PDF Reader.

### AC-11

Chất lượng text, hình ảnh và vector không bị giảm đáng kể so với file nguồn.

### AC-12

Không có thay đổi nào trên file PDF gốc.

---

## 21. Rủi ro kỹ thuật

### 21.1. RAM cao khi hiển thị nhiều thumbnail

Giải pháp:

- Lazy loading.
- Virtualization.
- Thumbnail resolution thấp.
- Giới hạn cache theo dung lượng.

### 21.2. PDF có cấu trúc phức tạp

Giải pháp:

- Test với nhiều nguồn PDF.
- Có fallback library.
- Log rõ lỗi export.

### 21.3. Giấy phép thư viện PDF

Giải pháp:

- Đánh giá giấy phép trước khi phát triển.
- Tránh dùng AGPL nếu sản phẩm không mở mã nguồn và chưa mua license.

### 21.4. File nguồn bị thay đổi sau khi import

Giải pháp:

- Dùng fingerprint.
- Kiểm tra lại trước khi xuất.
- Cảnh báo người dùng nếu file thay đổi.

### 21.5. Danh sách hàng nghìn trang đầu ra

Giải pháp:

- Virtualize khay đầu ra.
- Không giữ bitmap preview độ phân giải cao trong RAM.

---

## 22. Quyết định sản phẩm

- Ứng dụng là desktop Windows, không phải web.
- Hiển thị toàn bộ file và toàn bộ trang trong một màn hình cuộn.
- Thumbnail là cách thao tác chính.
- Khay đầu ra là nguồn sự thật cho file xuất.
- Trang nguồn có thể xuất hiện nhiều lần.
- Nhân bản group là nghiệp vụ cốt lõi.
- Không in trực tiếp.
- File sau khi tạo được mở bằng Foxit PDF Reader.
- Ưu tiên xử lý offline và bảo toàn chất lượng PDF.

---

## 23. Tên sản phẩm gợi ý

- PDF Page Composer
- PDF Print Composer
- PDF Batch Page Builder
- PDF Page Collector
- PDF Print Set Builder

Tên tạm dùng trong tài liệu này: **PDF Page Composer**.
