# Đặc tả kỹ thuật — Module Ghép lẻ PDF

Tài liệu đi kèm `docs/composer/PRD.md` và `docs/composer/UI_SPEC.md`. Stack, cấu trúc solution, DI, MVVM, dữ liệu cục bộ và log dùng chung ở `docs/ARCHITECTURE.md`.

## Vị trí trong codebase

```text
src/PDFPageComposer.App/
├── MainWindow.xaml(.cs)        # màn hình chính; code-behind: kéo thả, phím Enter/Esc, Ctrl + con lăn
├── ViewModels/MainViewModel.cs # toàn bộ state và command của màn hình Ghép lẻ
├── Models/                     # SourcePdfFile, SourcePdfPage, OutputGroup, OutputPageItem, OutputTrayItemView,
│                               # ProjectState, OutputItemMoveRequest, OutputGroupMoveRequest, ...
├── Services/                   # PdfMetadataService, PdfRenderService, PdfiumLibrary, ThumbnailCacheService,
│                               # ThumbnailRenderQueue, PdfExportService, ProjectPersistenceService,
│                               # ProjectStateMapper, AutoSaveService, FoxitLauncherService
├── Behaviors/                  # LoadedCommandBehavior, RenderWhenVisibleBehavior
└── Converters/                 # chuyển ảnh BGRA sang ImageSource, NonZeroToVisibility
```

`MainViewModel` lớn nhưng chỉ chứa logic giao diện; mọi thao tác PDF, file, process đi qua interface.

## PDF rendering

- `PDFiumCore` qua `PdfiumLibrary` (khởi tạo một lần) và `PdfRenderService`.
- PDFium không an toàn đa luồng: mọi lời gọi đi qua một `SemaphoreSlim` tĩnh.
- Render ngoài UI thread, nhận `CancellationToken`, trả `RenderedPageImage` (BGRA) để ViewModel không biết wrapper.
- Thumbnail: `ThumbnailRenderQueue` giới hạn số render đồng thời; `ThumbnailCacheService` là LRU theo dung lượng (64 MB; trường `ThumbnailCacheLimit` trong settings hiện chưa được dùng).
- Thumbnail chỉ được yêu cầu khi thẻ trang nằm gần vùng đang cuộn (`RenderWhenVisibleBehavior`); với 115 trang trong khay chỉ khoảng 33 trang được render.
- Preview lớn render theo kích thước yêu cầu và bị xóa khi đóng preview.

## PDF composition

- `PdfExportService` dùng PDFsharp import trực tiếp page object theo thứ tự khay đầu ra.
- Giữ vector, font, ảnh, page size, rotation; hỗ trợ trang dọc/ngang và nhiều khổ trong cùng file. Không scale, không rasterize.
- Ghi file tạm cùng thư mục rồi đổi tên sang đích; lỗi thì xóa file tạm, trả `PdfExportException` với `PdfExportError`.
- Không cho file đích trùng một file nguồn.

## Data model chính

```text
SourcePdfFile   Id, FilePath, DisplayName, PageCount, FileSize, Fingerprint, IsEncrypted, IsMissing, IsCollapsed, Pages
SourcePdfPage   Id, SourceFileId, PageNumber, Width, Height, Rotation, IsSelected, ThumbnailState, OutputOccurrenceCount
OutputGroup     Id, Name, CreatedAt, ColorHex, IsCollapsed, Items
OutputPageItem  Id, GroupId, SourceFileId, SourcePageNumber
ProjectState    Version, SourceFiles, OutputGroups, UiState, LastExportPath
```

- Thứ tự collection trong group và thứ tự group là nguồn sự thật; số thứ tự đầu ra được suy ra.
- `OutputItemMoveRequest(SourceItem, TargetItem, PlaceAfter)`: kéo thả đặt trước hoặc sau trang đích.

## Quy tắc thêm file nguồn

- File trùng đường dẫn hoặc trùng `Fingerprint` với file đã có bị bỏ qua; status bar báo "bỏ qua N file đã có".
- Ô tìm kiếm lọc file theo `TextNormalizer.MatchesSearch` (bỏ dấu, không phân biệt hoa thường; `đ` → `d`).

## Persistence

- `AutoSaveService` ghi `recovery.ppc.json` khi phiên thay đổi; lúc khởi động `MainViewModel.CheckRecoveryOnStartupAsync` đề nghị **Phục hồi / Bỏ qua**.
- Project lưu đường dẫn, fingerprint, group (kèm màu), thứ tự đầu ra và UI state; không nhúng nội dung PDF, không lưu mật khẩu.
- File nguồn thiếu được đánh dấu; **Chọn lại** (relink) chỉ nhận file khớp số trang và fingerprint.

## Foxit integration

- Tìm Foxit theo cấu hình rồi các vị trí cài phổ biến; không hard-code một đường dẫn.
- Mở bằng `ProcessStartInfo` với argument an toàn; lỗi thì mở bằng viewer mặc định. Không gửi lệnh in.

## Testing

- `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`: tạo group từ selection, nhân bản item/group, sắp xếp, kéo thả trước/sau, xóa, ví dụ 10 trang `A3, A4, B1, B2, B3, B4, B5, B1, B2, B3`, chặn file trùng, tìm không dấu.
- `tests/PDFPageComposer.Tests/Services/*`: metadata, render, export (thứ tự, page size, rotation), cache LRU, render queue, project round-trip, auto-save, Foxit.
- `tests/PDFPageComposer.Tests/Behaviors/RenderWhenVisibleBehaviorTests.cs`.
- Số liệu hiệu năng: `docs/composer/PERFORMANCE.md`.

## Rủi ro kỹ thuật

| Rủi ro | Hướng xử lý |
|---|---|
| RAM thumbnail | Render khi hiển thị, cache LRU có giới hạn, virtualization |
| PDF lỗi / mã hóa | Lỗi có kiểu (`PdfMetadataError`), cô lập theo file |
| Nguồn đổi sau import | Fingerprint, relink có kiểm tra |
| Export bị gián đoạn | File tạm, dọn khi lỗi |
| Native PDFium | Publish `win-x64` self-contained, không bật single-file |
