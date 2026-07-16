# Kế hoạch và trạng thái công việc

## Tổng quan trạng thái

| Trạng thái | Số lượng |
|---|---:|
| Backlog | 51 |
| In Progress | 0 |
| Blocked | 0 |
| Done | 0 |

Chỉ tối đa một task được ở trạng thái **In Progress**. `[ ]` là chưa hoàn thành, `[x]` là hoàn thành; trạng thái chi tiết ghi ngay dưới task khi bắt đầu hoặc bị chặn.

Khi hoàn thành, bổ sung:

```text
Completed: YYYY-MM-DD
Files changed: ...
Verification: ...
Notes: ...
```

## Phase 0 — Project setup

### - [ ] TASK-001 — Khởi tạo solution và WPF project

- **Mục tiêu:** Tạo solution .NET 10 và `src/PDFPageComposer.App` chạy được cửa sổ WPF mặc định.
- **Dependency:** Không.
- **Acceptance criteria:** Solution đúng cấu trúc; App target `net10.0-windows`, bật WPF và nullable; `dotnet build` thành công.

### - [ ] TASK-002 — Tạo test project

- **Mục tiêu:** Tạo `tests/PDFPageComposer.Tests` và tham chiếu App.
- **Dependency:** TASK-001.
- **Acceptance criteria:** xUnit test mẫu chạy thành công bằng `dotnet test`.

### - [ ] TASK-003 — Thiết lập repository cơ bản

- **Mục tiêu:** Thêm `.gitignore` phù hợp .NET/WPF và giữ cấu trúc tài liệu hiện tại.
- **Dependency:** TASK-001.
- **Acceptance criteria:** Không track `bin/`, `obj/`, output publish, log hoặc cache local; tài liệu không bị thay đổi nghiệp vụ.

### - [ ] TASK-004 — Cài package nền tảng và cấu hình DI/MVVM/logging

- **Mục tiêu:** Thêm CommunityToolkit.Mvvm, DI và Serilog sau khi kiểm tra license/version.
- **Dependency:** TASK-001.
- **Acceptance criteria:** Composition root đăng ký service; logging local hoạt động; không có service locator; build sạch.

### - [ ] TASK-005 — Tạo shell UI

- **Mục tiêu:** Tạo MainWindow gồm Toolbar, Source Workspace, Output Tray, Status Bar và splitter.
- **Dependency:** TASK-004.
- **Acceptance criteria:** Bố cục đúng tỷ lệ trong UI spec, resize được ở 1366×768 và chưa chứa business logic trong code-behind.

## Phase 1 — Import và đọc PDF

### - [ ] TASK-006 — Chọn wrapper PDF và thư viện composition bằng spike

- **Mục tiêu:** Kiểm chứng render/composition trên .NET 10, Windows x64 trước khi chốt package.
- **Dependency:** TASK-004.
- **Acceptance criteria:** Ghi kết quả compatibility, license, native deployment, password và fidelity; chỉ thêm package được chọn.

### - [ ] TASK-007 — Tạo model và interface PDF nguồn

- **Mục tiêu:** Tạo `SourcePdfFile`, `SourcePdfPage`, trạng thái lỗi và interface metadata.
- **Dependency:** TASK-006.
- **Acceptance criteria:** Model không phụ thuộc WPF/PDF library; page number và file identity được test.

### - [ ] TASK-008 — File dialog import nhiều PDF

- **Mục tiêu:** Cho chọn nhiều PDF qua dialog được cô lập bằng `IFileDialogService`.
- **Dependency:** TASK-007.
- **Acceptance criteria:** Chọn nhiều file; cancel không đổi state; chỉ chuyển đường dẫn hợp lệ cho use case import.

### - [ ] TASK-009 — Kéo thả file PDF

- **Mục tiêu:** Nhận nhiều file từ Windows Explorer.
- **Dependency:** TASK-007.
- **Acceptance criteria:** Drop PDF kích hoạt cùng pipeline với dialog; file không hỗ trợ được báo rõ; UI không khóa.

### - [ ] TASK-010 — Validate và đọc metadata PDF

- **Mục tiêu:** Đọc đường dẫn, tên, dung lượng, số trang, encryption và fingerprint.
- **Dependency:** TASK-006, TASK-007.
- **Acceptance criteria:** Import 10 file; nhận dạng bằng path/ID/fingerprint thay vì chỉ tên; lỗi từng file không dừng toàn batch.

### - [ ] TASK-011 — Hiển thị và xóa file khỏi phiên

- **Mục tiêu:** Bind danh sách file nguồn và hỗ trợ xóa an toàn.
- **Dependency:** TASK-010.
- **Acceptance criteria:** Hiển thị đúng metadata; xóa file chưa dùng hoạt động; file có item đầu ra phải cảnh báo và không âm thầm xóa item.

### - [ ] TASK-012 — Xử lý PDF lỗi và PDF có mật khẩu

- **Mục tiêu:** Hiển thị lỗi có kiểu và luồng nhập/bỏ qua mật khẩu.
- **Dependency:** TASK-010.
- **Acceptance criteria:** Phân biệt corrupt/permission/encrypted/missing; mật khẩu không log hoặc lưu plain text; sai mật khẩu cho thử lại.

## Phase 2 — Thumbnail workspace

### - [ ] TASK-013 — Xây dựng PDF render service

- **Mục tiêu:** Render thumbnail ngoài UI thread và dispose native resource đúng cách.
- **Dependency:** TASK-006, TASK-010.
- **Acceptance criteria:** Render đúng trang/rotation; nhận cancellation; exception được trả có ngữ cảnh; không rò document handle trong test.

### - [ ] TASK-014 — Tạo section file và Thumbnail Card

- **Mục tiêu:** Hiển thị mọi file/trang nối tiếp trong một workspace.
- **Dependency:** TASK-011, TASK-013.
- **Acceptance criteria:** Card có preview, page number, tick, badge và các state theo UI spec.

### - [ ] TASK-015 — Lazy loading theo viewport

- **Mục tiêu:** Chỉ ưu tiên render trang trong hoặc gần viewport.
- **Dependency:** TASK-014.
- **Acceptance criteria:** Thumbnail nhìn thấy bắt đầu render trước; import không render toàn bộ độ phân giải cao; UI vẫn tương tác được.

### - [ ] TASK-016 — Virtualization workspace

- **Mục tiêu:** Giới hạn số visual/container khi có hàng nghìn trang.
- **Dependency:** TASK-014.
- **Acceptance criteria:** Container được recycle; cuộn không tạo toàn bộ card; selection/model không mất khi virtualize.

### - [ ] TASK-017 — Render queue, cancellation và concurrency

- **Mục tiêu:** Điều phối render nền có ưu tiên và giới hạn tài nguyên.
- **Dependency:** TASK-015.
- **Acceptance criteria:** Có bounded concurrency; tác vụ xa viewport hủy được; không cập nhật nhầm thumbnail sau recycle.

### - [ ] TASK-018 — Thumbnail cache có giới hạn

- **Mục tiêu:** Cache thumbnail theo memory budget và eviction.
- **Dependency:** TASK-013, TASK-017.
- **Acceptance criteria:** Cache hit tránh render lại; vượt giới hạn sẽ evict/dispose; project đóng giải phóng cache.

### - [ ] TASK-019 — Collapse file và zoom thumbnail

- **Mục tiêu:** Thu gọn section và đổi kích thước card.
- **Dependency:** TASK-014, TASK-016.
- **Acceptance criteria:** Slider/preset/Ctrl+wheel hoạt động; không mất selection hoặc vị trí cuộn; state collapse được giữ trong phiên.

### - [ ] TASK-020 — Loading, error và retry state

- **Mục tiêu:** Hiển thị rõ tiến độ/lỗi render từng trang.
- **Dependency:** TASK-013, TASK-014.
- **Acceptance criteria:** Có skeleton, placeholder lỗi và retry; lỗi một trang không khóa file khác.

## Phase 3 — Chọn và preview trang

### - [ ] TASK-021 — Click chọn và bỏ chọn trang

- **Mục tiêu:** Quản lý selection độc lập Output Tray.
- **Dependency:** TASK-014.
- **Acceptance criteria:** Click toggle đúng; badge output không đổi; test xác nhận chọn không tự thêm đầu ra.

### - [ ] TASK-022 — Ctrl+Click và Shift+Click

- **Mục tiêu:** Chọn rời rạc và chọn dải trong file.
- **Dependency:** TASK-021.
- **Acceptance criteria:** Ctrl thêm/bớt item; Shift dùng anchor đúng và không chọn nhầm file; virtualized item vẫn giữ state.

### - [ ] TASK-023 — Chọn/bỏ chọn tất cả theo file

- **Mục tiêu:** Thao tác hàng loạt và cập nhật thống kê.
- **Dependency:** TASK-021.
- **Acceptance criteria:** Chọn/bỏ đủ trang trong file; UI không treo với file lớn; Undo/Redo có thể tích hợp sau.

### - [ ] TASK-024 — Render preview lớn

- **Mục tiêu:** Mở đúng file/trang trong modal hoặc panel nổi.
- **Dependency:** TASK-013, TASK-021.
- **Acceptance criteria:** Double click/kính lúp/Enter mở đúng trang; đóng không mất state; chỉ giữ preview cần thiết trong RAM.

### - [ ] TASK-025 — Zoom, fit và điều hướng preview

- **Mục tiêu:** Thêm zoom, Fit Width/Height, trước/sau và chọn trong preview.
- **Dependency:** TASK-024.
- **Acceptance criteria:** Điều hướng không vượt biên; render có cancellation; thao tác chọn đồng bộ Thumbnail Card.

## Phase 4 — Output Tray

### - [ ] TASK-026 — Tạo model OutputGroup và OutputPageItem

- **Mục tiêu:** Biểu diễn đầu ra bằng tham chiếu logic tới trang nguồn.
- **Dependency:** TASK-007.
- **Acceptance criteria:** ID item độc lập; một nguồn tạo nhiều item; thứ tự collection là nguồn sự thật; unit test đầy đủ.

### - [ ] TASK-027 — Thêm selection vào Output Tray

- **Mục tiêu:** Mỗi lần thêm tạo một group ở cuối tray.
- **Dependency:** TASK-022, TASK-026.
- **Acceptance criteria:** Thứ tự theo file rồi page tăng dần; không tự bỏ/xóa output khi selection đổi; không thêm khi selection rỗng.

### - [ ] TASK-028 — Hiển thị group, item và thứ tự đầu ra

- **Mục tiêu:** Bind Output Tray dạng list với index liên tục.
- **Dependency:** TASK-027.
- **Acceptance criteria:** Hiển thị file/page/group đúng; index và badge cập nhật sau thay đổi; tray có thể collapse.

### - [ ] TASK-029 — Xóa item, nhiều item và group

- **Mục tiêu:** Xóa có chủ đích khỏi đầu ra mà không sửa nguồn.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Xóa đúng selection; group rỗng được xử lý nhất quán; index/badge/tổng trang tính lại đúng.

### - [ ] TASK-030 — Thống kê đầu ra và status bar

- **Mục tiêu:** Hiển thị tổng file, trang nguồn, selection, đầu ra và render state.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Số liệu cập nhật theo state model, không quét bitmap; tổng trang đúng sau thêm/xóa.

## Phase 5 — Nhân bản và sắp xếp

### - [ ] TASK-031 — Nhân bản một hoặc nhiều item

- **Mục tiêu:** Tạo item ID mới tham chiếu cùng trang nguồn.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Chèn sau bản gốc hoặc cuối danh sách; số lượng 1–999 được validate; thứ tự đúng.

### - [ ] TASK-032 — Nhân bản group

- **Mục tiêu:** Sao chép đầy đủ group và thứ tự nội bộ.
- **Dependency:** TASK-031.
- **Acceptance criteria:** `B1,B2,B3` thành `B1,B2,B3,B1,B2,B3`; bản sao có group/item ID mới; badge đúng.

### - [ ] TASK-033 — Kéo thả item

- **Mục tiêu:** Di chuyển một/nhiều item và hiển thị insertion marker.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Drop cập nhật model đúng; không mất/nhân đôi item; index tính lại; hỗ trợ virtualized tray.

### - [ ] TASK-034 — Kéo thả và collapse group

- **Mục tiêu:** Di chuyển nguyên group mà giữ thứ tự bên trong.
- **Dependency:** TASK-032, TASK-033.
- **Acceptance criteria:** Group di chuyển nguyên khối; collapse không thay đổi output; thứ tự flatten đúng.

### - [ ] TASK-035 — Undo/Redo

- **Mục tiêu:** Hoàn tác/làm lại add, delete, duplicate, reorder và bulk selection.
- **Dependency:** TASK-029, TASK-034.
- **Acceptance criteria:** Command history có giới hạn; redo bị xóa khi có nhánh thao tác mới; state/index/badge nhất quán sau mỗi lần.

## Phase 6 — Export PDF

### - [ ] TASK-036 — Xây dựng export service

- **Mục tiêu:** Export bằng page object qua interface độc lập thư viện.
- **Dependency:** TASK-006, TASK-026.
- **Acceptance criteria:** Duyệt output flatten theo đúng thứ tự; không rasterize; resource được dispose khi thành công/lỗi.

### - [ ] TASK-037 — Validate đích và bảo vệ file nguồn

- **Mục tiêu:** Ngăn ghi đè nguồn và kiểm tra điều kiện trước export.
- **Dependency:** TASK-036.
- **Acceptance criteria:** Chặn tray rỗng, đích trùng nguồn, thiếu quyền/dung lượng và nguồn thiếu/đổi fingerprint.

### - [ ] TASK-038 — Ghi file tạm và xử lý lỗi an toàn

- **Mục tiêu:** Không để lại file đích hỏng khi export thất bại.
- **Dependency:** TASK-037.
- **Acceptance criteria:** Chỉ commit file sau hoàn tất; lỗi/cancel dọn file tạm; không thay đổi bất kỳ nguồn nào.

### - [ ] TASK-039 — Giữ page size, rotation và fidelity

- **Mục tiêu:** Xác nhận mixed page size/orientation và chất lượng nội dung.
- **Dependency:** TASK-036.
- **Acceptance criteria:** Fixture giữ MediaBox/rotation; text/vector/font/ảnh không bị rasterize hoặc scale ngoài ý muốn.

### - [ ] TASK-040 — Progress và cancel export

- **Mục tiêu:** Báo tiến độ theo trang và cho hủy an toàn.
- **Dependency:** TASK-038.
- **Acceptance criteria:** UI không khóa; progress tăng hợp lệ; cancel dừng ở ranh giới an toàn và dọn file tạm.

### - [ ] TASK-041 — Kiểm thử ví dụ đầu ra 10 trang

- **Mục tiêu:** Nghiệm thu luồng cốt lõi của PRD.
- **Dependency:** TASK-039.
- **Acceptance criteria:** File có đúng 10 trang theo `A3,A4,B1,B2,B3,B4,B5,B1,B2,B3`, mở được và nguồn không đổi.

## Phase 7 — Foxit và persistence

### - [ ] TASK-042 — Phát hiện và cấu hình Foxit

- **Mục tiêu:** Tìm Foxit phổ biến và cho chọn executable thủ công.
- **Dependency:** TASK-004.
- **Acceptance criteria:** Không hard-code một đường dẫn; validate executable; lưu cấu hình bằng settings JSON.

### - [ ] TASK-043 — Mở file xuất bằng Foxit hoặc viewer mặc định

- **Mục tiêu:** Mở file an toàn sau export, không tự in.
- **Dependency:** TASK-041, TASK-042.
- **Acceptance criteria:** Path có khoảng trắng hoạt động; Foxit lỗi thì fallback shell default; exception được log/thông báo.

### - [ ] TASK-044 — Lưu và mở project JSON

- **Mục tiêu:** Persist nguồn, fingerprint, group, item, thứ tự và UI state cần thiết.
- **Dependency:** TASK-026, TASK-035.
- **Acceptance criteria:** Round-trip giữ đúng output; có schema version; không lưu password hoặc nội dung PDF.

### - [ ] TASK-045 — Auto-save và phục hồi phiên

- **Mục tiêu:** Phục hồi sau khi ứng dụng đóng bất thường.
- **Dependency:** TASK-044.
- **Acceptance criteria:** Ghi file tạm an toàn; không ghi liên tục quá mức; startup phát hiện và đề nghị phục hồi.

### - [ ] TASK-046 — Relink file nguồn bị thiếu

- **Mục tiêu:** Giữ output references và cho chọn lại nguồn.
- **Dependency:** TASK-044.
- **Acceptance criteria:** File thiếu được đánh dấu; relink xác minh page count/fingerprint; không tự xóa item trước xác nhận.

## Phase 8 — Hoàn thiện

### - [ ] TASK-047 — Hoàn thiện test suite

- **Mục tiêu:** Bao phủ business rule, persistence và export quan trọng.
- **Dependency:** TASK-041, TASK-046.
- **Acceptance criteria:** Unit/integration test cho AC cốt lõi chạy ổn định; fixture có license phù hợp; `dotnet test` thành công.

### - [ ] TASK-048 — Kiểm thử lỗi và sửa warning/bug

- **Mục tiêu:** Xử lý corrupt/encrypted/missing/permission/disk-full và warning hợp lý.
- **Dependency:** TASK-047.
- **Acceptance criteria:** Không crash/nuốt exception; thông báo rõ; build không có warning có thể xử lý hợp lý.

### - [ ] TASK-049 — Đo và tối ưu RAM/UI

- **Mục tiêu:** Kiểm chứng 10 file/500 trang và thiết kế mở rộng hàng nghìn trang.
- **Dependency:** TASK-018, TASK-040, TASK-048.
- **Acceptance criteria:** Thumbnail đầu tiên/khả năng phản hồi đạt mục tiêu PRD trong môi trường test; RAM cache bị giới hạn; ghi kết quả đo.

### - [ ] TASK-050 — Publish Windows x64

- **Mục tiêu:** Tạo bản self-contained để chạy trên máy Windows sạch.
- **Dependency:** TASK-047, TASK-049.
- **Acceptance criteria:** `win-x64` publish thành công; native PDF DLL load đúng; smoke test import/render/export/Foxit; single-file chỉ bật nếu tương thích.

### - [ ] TASK-051 — Viết hướng dẫn sử dụng ngắn

- **Mục tiêu:** Hướng dẫn import, chọn, group, duplicate, reorder, export và mở Foxit.
- **Dependency:** TASK-050.
- **Acceptance criteria:** Hướng dẫn tiếng Việt ngắn gọn, khớp UI thực tế và không mô tả chức năng ngoài MVP.
