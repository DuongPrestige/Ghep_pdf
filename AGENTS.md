# Quy tắc làm việc với PDF Page Composer

Tài liệu này là quy tắc bắt buộc khi AI agent (Codex, Claude) hoặc dev phát triển dự án. Mục lục tài liệu: `docs/README.md`. Dự án dùng cá nhân; ưu tiên giải pháp rõ ràng, nhỏ gọn và đủ dùng.

## Thứ tự ưu tiên tài liệu

Tài liệu chia theo module: `docs/composer/` (Ghép lẻ PDF), `docs/tiktok/` (Đơn TikTok), `docs/update/` (Cập nhật và phát hành). Trong một module:

1. `docs/<module>/PRD.md`
2. `docs/<module>/UI_SPEC.md`
3. `docs/<module>/TECH_SPEC.md`
4. `docs/<module>/TASKS.md`
5. `AGENTS.md`, `docs/ARCHITECTURE.md`

Module `update` không có `PRD.md` và `UI_SPEC.md` riêng; dùng `docs/update/TECH_SPEC.md` và `docs/update/RELEASE.md`.

Nếu có mâu thuẫn, ưu tiên tài liệu có thứ tự cao hơn. Không tự ý thay đổi nghiệp vụ trong `PRD.md` của module. Quy tắc BR-01, BR-02, BR-07, BR-08 của `docs/composer/PRD.md` áp dụng cho mọi module. Tài liệu trong `docs/archive/` chỉ để tham khảo.

## Tech stack bắt buộc

- C#, .NET 10, WPF, MVVM.
- `CommunityToolkit.Mvvm` cho MVVM.
- `PDFiumCore` để render thumbnail và preview.
- `PDFsharp` để ghép trang PDF.
- `Microsoft.Data.Sqlite` cho dữ liệu module Đơn TikTok.
- Kéo thả tự viết bằng sự kiện WPF trong code-behind (không dùng thư viện kéo thả).
- Serilog để ghi log cục bộ.
- `System.Text.Json` để lưu settings và project.

Chi tiết và lý do: `docs/ARCHITECTURE.md`.

## Quy tắc kiến trúc

- Không đặt business logic trong View hoặc code-behind; code-behind chỉ xử lý hành vi UI thật sự cần thiết.
- ViewModel quản lý trạng thái giao diện và command, không trực tiếp thao tác filesystem hoặc thư viện PDF.
- Service xử lý PDF, filesystem, export và Foxit integration.
- Model/domain entity không phụ thuộc WPF.
- Dùng Dependency Injection và ưu tiên interface cho service quan trọng.
- Mỗi module giữ ranh giới của mình (xem `docs/ARCHITECTURE.md`): module TikTok không thêm state hoặc command vào `MainViewModel`.
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
- Thumbnail phải lazy load (`RenderWhenVisibleBehavior` hoặc `LoadedCommandBehavior`) và danh sách lớn phải dùng virtualization.
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

1. Xác định module; đọc task đang làm trong `docs/<module>/TASKS.md`. Task mới lấy mã ở dòng "Mã task tiếp theo" của `docs/README.md`.
2. Đọc phần liên quan trong `docs/<module>/PRD.md`.
3. Đọc `UI_SPEC.md`, `TECH_SPEC.md` của module và `docs/ARCHITECTURE.md` nếu đụng phần dùng chung.
4. Xác định acceptance criteria và dependency.
5. Chỉ triển khai đúng phạm vi task; chỉ một task được `In Progress`.

Sau khi hoàn thành:

1. Build project.
2. Chạy test liên quan nếu đã có.
3. Sửa warning hợp lý.
4. Cập nhật checkbox và trạng thái task trong `docs/<module>/TASKS.md`.
5. Ghi `Completed`, `Files changed`, `Verification`, `Notes` dưới task.
6. Cập nhật tài liệu bị ảnh hưởng theo bảng "Quy tắc bảo trì" trong `docs/README.md`, trong cùng commit với code.
7. Không tự động bắt đầu task tiếp theo nếu chưa được yêu cầu.
