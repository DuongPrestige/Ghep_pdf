# Quy tắc làm việc với PDF Page Composer

Tài liệu này là quy tắc bắt buộc khi Codex phát triển dự án. Dự án dùng cá nhân; ưu tiên giải pháp rõ ràng, nhỏ gọn và đủ dùng.

## Thứ tự ưu tiên tài liệu

1. `PRD.md`
2. `docs/UI_SPEC.md`
3. `docs/TECH_SPEC.md`
4. `docs/TASKS.md`
5. `AGENTS.md`

Nếu có mâu thuẫn, ưu tiên tài liệu có thứ tự cao hơn. Không tự ý thay đổi nghiệp vụ trong `PRD.md`.

## Tech stack bắt buộc

- C#, .NET 10, WPF, MVVM.
- `CommunityToolkit.Mvvm` cho MVVM.
- PDFium hoặc wrapper phù hợp để render thumbnail và preview.
- PDFsharp hoặc thư viện có giấy phép phù hợp để ghép trang PDF.
- `GongSolutions.WPF.DragDrop` khi cần kéo thả.
- Serilog để ghi log cục bộ.
- `System.Text.Json` để lưu settings và project.

## Quy tắc kiến trúc

- Không đặt business logic trong View hoặc code-behind; code-behind chỉ xử lý hành vi UI thật sự cần thiết.
- ViewModel quản lý trạng thái giao diện và command, không trực tiếp thao tác filesystem hoặc thư viện PDF.
- Service xử lý PDF, filesystem, export và Foxit integration.
- Model/domain entity không phụ thuộc WPF.
- Dùng Dependency Injection và ưu tiên interface cho service quan trọng.
- Không over-engineering: bắt đầu với App và Tests; chỉ tách thêm project khi có nhu cầu rõ ràng.
- Không tạo abstraction khi chưa có ít nhất một nhu cầu thực tế.

## Quy tắc nghiệp vụ bắt buộc

- Không sửa hoặc ghi đè PDF nguồn.
- Output Tray là nguồn sự thật; file xuất phải giữ đúng thứ tự trong đó.
- Một trang nguồn có thể xuất hiện nhiều lần trong đầu ra.
- Nhân bản group phải giữ nguyên thứ tự trang.
- Khi xuất, ưu tiên copy page object trực tiếp; không rasterize nếu không bắt buộc.
- Không làm mất chất lượng PDF; giữ page size, rotation, trang ngang/dọc và kích thước khác nhau.
- Foxit PDF Reader chỉ mở file sau khi xuất; ứng dụng không tự động in.

## Quy tắc hiệu năng

- Không render đồng bộ trên UI thread.
- Thumbnail phải lazy load và danh sách lớn phải dùng virtualization.
- Tác vụ render không còn cần thiết phải có thể hủy.
- Cache bitmap phải có giới hạn; không giữ bitmap độ phân giải cao không giới hạn trong RAM.
- Không đọc toàn bộ PDF vào memory nếu không cần.
- Tác vụ lâu phải có loading/progress và không khóa UI.

## Quy tắc code

- Bật nullable reference types.
- Dùng `async`/`await` đúng chỗ; không dùng `async void` ngoài event handler.
- Không nuốt exception hoặc dùng `catch { }` rỗng.
- Không hard-code đường dẫn Foxit hoặc đường dẫn người dùng.
- Tách class khi trách nhiệm không còn rõ; xem xét tách ở khoảng 300–400 dòng.
- Class, method và property dùng tiếng Anh; nội dung giao diện có thể dùng tiếng Việt.
- Không thêm package trước khi kiểm tra giấy phép, tình trạng duy trì và tương thích .NET 10.
- Không ghi nội dung PDF hoặc mật khẩu vào log; không lưu mật khẩu dạng plain text.

## Quy tắc thực hiện task

Trước khi code:

1. Đọc task đang làm trong `docs/TASKS.md`.
2. Đọc phần liên quan trong `PRD.md`.
3. Đọc UI/technical specification liên quan.
4. Xác định acceptance criteria và dependency.
5. Chỉ triển khai đúng phạm vi task; chỉ một task được `In Progress`.

Sau khi hoàn thành:

1. Build project.
2. Chạy test liên quan nếu đã có.
3. Sửa warning hợp lý.
4. Cập nhật checkbox và trạng thái task.
5. Ghi `Completed`, `Files changed`, `Verification`, `Notes` dưới task.
6. Không tự động bắt đầu task tiếp theo nếu chưa được yêu cầu.
