# Đặc tả kỹ thuật

## Tech stack đã chốt

- C# và .NET 10.
- WPF, MVVM.
- Visual Studio Code và C# Dev Kit.
- CommunityToolkit.Mvvm.
- Microsoft.Extensions.DependencyInjection.
- Serilog, System.Text.Json và xUnit.
- PDFium/wrapper phù hợp để render; PDFsharp hoặc thư viện có giấy phép phù hợp để composition.

## Kiến trúc đơn giản

Ban đầu chỉ dùng hai project:

```text
src/PDFPageComposer.App/
tests/PDFPageComposer.Tests/
```

Trong App:

```text
Models/          # Model và entity độc lập WPF
ViewModels/      # UI state và command
Views/           # Window, Page, Dialog
Services/        # Use case/service triển khai
Interfaces/      # Port cho PDF, filesystem, persistence, launcher
Commands/        # Chỉ thêm command riêng khi Toolkit không đủ
Controls/        # WPF control tái sử dụng
Converters/      # Value converters nhỏ, không chứa business logic
Behaviors/       # UI behavior và drag/drop
Infrastructure/  # Adapter thư viện PDF, filesystem, process
Resources/       # Style, icon và chuỗi giao diện
```

Không tách 4–5 project khi chưa có nhu cầu. Nếu Domain/service trở nên lớn hoặc cần tái sử dụng độc lập, việc tách project phải có lý do cụ thể và giữ dependency hướng vào model/use case.

## Dependency Injection

Dùng `Microsoft.Extensions.DependencyInjection`. Đăng ký interface và implementation tại composition root trong startup của WPF. Tránh service locator và không gọi container từ ViewModel.

Lifetime mặc định:

- Singleton: settings, bounded thumbnail cache, render queue, logging.
- Scoped/transient theo thao tác: export session hoặc PDF document handle khi phù hợp.
- Transient: ViewModel/dialog nhỏ không giữ tài nguyên native dài hạn.

## MVVM

- ViewModel kế thừa `ObservableObject` hoặc dùng source generator tương ứng.
- `RelayCommand` cho thao tác nhanh; `AsyncRelayCommand` cho import, render, save và export.
- ViewModel không trực tiếp gọi filesystem, `Process.Start` hoặc API PDF.
- View chỉ bind state/command; code-behind dành cho focus, window lifecycle hoặc sự kiện WPF không thể biểu diễn hợp lý bằng binding/behavior.
- Model không tham chiếu `System.Windows`, bitmap WPF hoặc control.

## PDF rendering

Adapter PDFium phải:

- Đọc page count, page size và rotation.
- Render thumbnail và preview theo kích thước yêu cầu.
- Chạy ngoài UI thread, nhận `CancellationToken` và giới hạn concurrency.
- Dispose document/page/native bitmap đúng cách, kể cả khi hủy hoặc lỗi.
- Trả dữ liệu ảnh theo hợp đồng không buộc ViewModel biết wrapper cụ thể.
- Hỗ trợ PDF mã hóa hoặc trả về trạng thái cần mật khẩu rõ ràng.

Chưa chốt wrapper trước khi spike xác nhận .NET 10, Windows x64, high-DPI, password handling, license, tình trạng duy trì và đóng gói native DLL.

## PDF composition

Composer phải:

- Copy/import trực tiếp page object theo thứ tự Output Tray.
- Giữ vector, font, ảnh, page size và rotation tối đa có thể.
- Hỗ trợ nhiều kích thước, portrait và landscape trong cùng file.
- Không tự scale và không rasterize khi export.
- Ghi qua file tạm; chỉ thay thế/đổi tên sang đích sau khi hoàn tất.
- Không cho output trùng bất kỳ file nguồn nào.

Phải kiểm tra license và fidelity của PDFsharp, PdfSharpCore, iText hoặc lựa chọn thay thế trước khi dùng. Không đưa dependency AGPL vào sản phẩm đóng nếu chưa có giấy phép phù hợp.

## Data model chính

### SourcePdfFile

```text
Id, FilePath, DisplayName, PageCount, FileSize, Fingerprint
IsEncrypted, IsMissing, IsCollapsed, Pages
```

### SourcePdfPage

```text
Id, SourceFileId, PageNumber, Width, Height, Rotation
IsSelected, ThumbnailState, OutputOccurrenceCount
```

### OutputGroup

```text
Id, Name, CreatedAt, IsCollapsed, Items
```

### OutputPageItem

```text
Id, GroupId, SourceFileId, SourcePageNumber
```

Thứ tự collection trong group và thứ tự group là nguồn sự thật; output index nên là giá trị suy ra, tránh lưu hai thứ tự có thể lệch nhau.

### ProjectState

```text
Version, SourceFiles, OutputGroups, UiState, LastExportPath
```

### AppSettings

```text
Version, FoxitExecutablePath, ThumbnailCacheLimit, LastOpenDirectory
```

## Service interfaces

```text
IPdfMetadataService
IPdfRenderService
IPdfExportService
IProjectPersistenceService
IFoxitLauncherService
IFileDialogService
IThumbnailCacheService
```

Trách nhiệm:

- `IPdfMetadataService`: validate, metadata, encryption và fingerprint.
- `IPdfRenderService`: render thumbnail/preview có cancellation.
- `IPdfExportService`: validate và copy trang vào file đích an toàn.
- `IProjectPersistenceService`: save/load/version/auto-save và relink metadata.
- `IFoxitLauncherService`: discover, validate executable và fallback viewer mặc định.
- `IFileDialogService`: cô lập dialog WPF khỏi ViewModel.
- `IThumbnailCacheService`: cache có giới hạn và eviction.

Không tạo implementation giả hoặc abstraction bổ sung nếu chưa có use case.

## Async và hiệu năng

- Chỉ lập lịch thumbnail trong/gần viewport; preview lớn chỉ render khi được mở.
- Virtualize Source Workspace và Output Tray.
- Dùng background queue có độ ưu tiên, `CancellationToken` và bounded concurrency.
- Hủy render khi item ra xa viewport hoặc project đóng.
- Cache thumbnail theo memory budget, không chỉ số lượng item; giải phóng bitmap/tài nguyên native khi evict.
- Không đọc toàn bộ PDF vào RAM nếu thư viện hỗ trợ stream/random access.
- Marshal kết quả tối thiểu về Dispatcher để cập nhật UI.
- Export báo progress theo số trang và hỗ trợ cancel tại ranh giới trang an toàn.

## Persistence

- Lưu project và settings bằng JSON qua `System.Text.Json`.
- Schema luôn có `Version`; migration chỉ thêm khi format thay đổi thực tế.
- Auto-save định kỳ và khi có thay đổi quan trọng, dùng ghi file tạm an toàn.
- Project lưu đường dẫn, fingerprint, output references và UI state cần thiết; không nhúng nội dung PDF.
- Không lưu mật khẩu PDF dạng plain text.

## Logging

- Serilog ghi file local, rolling theo ngày và có giới hạn retention.
- Log operation, duration, file ID/tên khi cần chẩn đoán và exception đầy đủ.
- Không log nội dung tài liệu, page text, raw binary hoặc mật khẩu.

## Foxit integration

- Kiểm tra cấu hình người dùng và các vị trí cài Foxit phổ biến; không hard-code một đường dẫn duy nhất.
- Cho chọn executable thủ công và lưu vào settings.
- Dùng `ProcessStartInfo`/`Process.Start`, truyền đường dẫn file bằng argument an toàn, không tự nối command string.
- Nếu Foxit không tồn tại hoặc khởi chạy lỗi, mở bằng PDF viewer mặc định qua shell.
- Chỉ mở file; không gửi lệnh in tự động.

## Testing

Dùng xUnit. Unit test tối thiểu:

- Tạo group từ selection theo thứ tự file/trang.
- Nhân bản item và group giữ đúng thứ tự.
- Sắp xếp/xóa và tính tổng trang.
- Selection độc lập Output Tray.
- Ví dụ 10 trang: `A3, A4, B1, B2, B3, B4, B5, B1, B2, B3`.
- Project JSON round-trip và version validation.

Khi đã chọn thư viện PDF, thêm integration test với fixture PDF cho mixed page size, rotation, vector/font/image, encrypted/corrupt file và thứ tự export. UI test chỉ dành cho luồng quan trọng ổn định.

## Build và publish

```powershell
dotnet build
dotnet test
dotnet publish .\src\PDFPageComposer.App -c Release -r win-x64 --self-contained true
```

Chỉ chạy publish ở Phase 8. Chỉ bật single-file sau khi xác nhận native PDF library được bundle/extract và load đúng; nếu không, phân phối native DLL cạnh executable.

## Rủi ro kỹ thuật

| Rủi ro | Hướng xử lý |
|---|---|
| Wrapper PDFium không tương thích .NET 10 | Spike tối thiểu: open, metadata, thumbnail, preview, cancel và dispose trước khi chốt |
| Native DLL deployment | Test publish `win-x64` sớm trên máy sạch; kiểm tra kiến trúc x64 và probing path |
| License thư viện PDF | Lập danh sách dependency/license trước khi cài; loại lựa chọn không phù hợp phân phối cá nhân đóng |
| RAM thumbnail | Lazy load, virtualization, bounded cache và đo working set với 500/2.000 trang |
| PDF lỗi/mã hóa | Trả lỗi có kiểu, cô lập theo file, nhập mật khẩu trong memory và cho bỏ qua |
| Nguồn đổi sau import | Lưu fingerprint, kiểm tra lại trước export và yêu cầu người dùng quyết định |
| Export bị gián đoạn | File tạm, cancel ở điểm an toàn, dispose đầy đủ và dọn file tạm |
