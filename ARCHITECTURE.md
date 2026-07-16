# Kiến trúc hệ thống

## Nền tảng và stack định hướng

- Windows 10/11 x64; .NET 8 hoặc bản .NET LTS được chốt khi triển khai; C#.
- WPF + MVVM, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`.
- Renderer: PDFium qua binding .NET được duy trì tốt.
- Composer: PDFsharp/PdfSharpCore nếu đáp ứng fidelity; chỉ cân nhắc iText sau đánh giá license.
- Drag/drop: `GongSolutions.WPF.DragDrop` hoặc behavior riêng.
- Persistence: JSON có version; logging bằng Serilog.
- Test: xUnit, FluentAssertions, Moq/NSubstitute; FlaUI cho UI test chọn lọc.
- Đóng gói: MSIX hoặc Inno Setup, self-contained x64.

Các lựa chọn thư viện là đề xuất, không phải cam kết cho tới khi có spike kiểm chứng fidelity, PDF mã hóa, license và khả năng đóng gói.

## Phân lớp

```text
PDFPageComposer.sln
src/
├── PDFPageComposer.App              # WPF Views, ViewModels, Controls, Behaviors, Themes
├── PDFPageComposer.Application      # Use cases, commands, DTOs, ports/interfaces
├── PDFPageComposer.Domain           # Entities, value objects, rules, domain events
└── PDFPageComposer.Infrastructure   # PDF, persistence, filesystem, Foxit, logging
tests/
├── PDFPageComposer.UnitTests
├── PDFPageComposer.IntegrationTests
└── PDFPageComposer.UITests
```

Phụ thuộc hướng vào trong: `App -> Application -> Domain`; `Infrastructure` hiện thực interface do Application định nghĩa và được nối bằng DI. Domain không biết UI, filesystem hay thư viện PDF.

## Thành phần chính

- `SourceImportService`: xác thực, metadata, encryption, fingerprint.
- `ThumbnailRenderQueue`: hàng đợi ưu tiên viewport, cancellation, bounded concurrency/cache.
- `PreviewService`: render trang theo zoom/fit mà không sửa nguồn.
- `SelectionService`: click/Ctrl/Shift/range/area và thứ tự chọn chuẩn hóa.
- `OutputComposer`: tạo group, duplicate, move, remove và flatten thứ tự.
- `UndoRedoService`: command/memento cho các thao tác được hỗ trợ.
- `ProjectStore`: save/load/version migration/auto-save/recovery/relink.
- `PdfExportService`: validate nguồn, copy page objects, ghi file tạm và commit an toàn.
- `PdfViewerLauncher`: phát hiện/cấu hình Foxit, fallback shell default.

## Mô hình dữ liệu

```text
SourcePdfFile
  Id, FilePath, DisplayName, PageCount, FileSize, Fingerprint
  IsEncrypted, IsMissing, IsCollapsed

SourcePdfPage
  Id, SourceFileId, PageNumber, Width, Height, Rotation
  ThumbnailState, IsSelected

OutputGroup
  Id, Name, CreatedAt, Items, IsCollapsed

OutputPageItem
  Id, GroupId, SourceFileId, SourcePageNumber, OutputIndex

ProjectState
  Version, SourceFiles, OutputGroups
  ThumbnailZoom, PanelWidth, LastExportPath, FoxitExecutablePath
```

`OutputPageItem` là tham chiếu logic, không chứa bản sao bitmap/page object trong bộ nhớ dài hạn. `OutputIndex` có thể là giá trị suy ra từ thứ tự collection; tránh hai nguồn sự thật.

Ví dụ JSON project tối thiểu:

```json
{
  "version": 1,
  "sourceFiles": [],
  "outputGroups": [],
  "uiState": {}
}
```

## Luồng chính

### Import và render

1. Xác thực file, metadata, encryption và fingerprint.
2. Thêm section nguồn ngay để UI phản hồi sớm.
3. Lập lịch thumbnail theo viewport với độ phân giải thấp.
4. Cache có giới hạn; retry/placeholder riêng cho trang lỗi.

### Thêm và chỉnh đầu ra

1. Chuẩn hóa selection theo thứ tự file rồi số trang.
2. Tạo `OutputGroup` và các item có ID độc lập.
3. Mọi duplicate/move/remove thực thi bằng command có Undo/Redo.
4. Tính lại thứ tự hiển thị và badge/tổng trang từ collection đầu ra.

### Export

1. Validate đầu ra không rỗng, nguồn còn tồn tại/không đổi, quyền ghi và dung lượng.
2. Duyệt danh sách đầu ra đã flatten đúng thứ tự.
3. Mở nguồn theo nhu cầu và import trực tiếp từng page object.
4. Ghi file tạm cùng volume, đóng/validate rồi đổi tên sang đích.
5. Dọn tài nguyên/file tạm; sau thành công cho mở Foxit hoặc viewer mặc định.

## UI và khả năng mở rộng

- Bố cục splitter: nguồn 70–75%, đầu ra 25–30%; khay có thể thu gọn.
- Vùng nguồn gồm section theo file và grid thumbnail được virtualize.
- Khay đầu ra có list/thumbnail mode nhưng dùng chung collection/view model.
- Preview là modal/panel nổi và giữ nguyên selection.
- Mục tiêu thiết kế 50–100 file, 2.000–5.000 trang nhờ lazy loading/virtualization.

## Spike kỹ thuật cần làm sớm

- Fidelity copy page với PDF font nhúng, vector, form, rotation và mixed page size.
- PDF mã hóa và vòng đời mật khẩu.
- Virtualization lồng section + wrap/grid trong WPF.
- Hành vi Foxit discovery/launch trên Windows 10/11.
- Đóng gói native PDFium và license của toàn bộ dependency.
