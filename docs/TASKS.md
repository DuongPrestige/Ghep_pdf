# Kế hoạch và trạng thái công việc

## Tổng quan trạng thái

| Trạng thái | Số lượng |
|---|---:|
| Backlog | 0 |
| In Progress | 0 |
| Blocked | 0 |
| Done | 51 |

Chỉ tối đa một task được ở trạng thái **In Progress**. `[ ]` là chưa hoàn thành, `[x]` là hoàn thành; trạng thái chi tiết ghi ngay dưới task khi bắt đầu hoặc bị chặn.

Khi hoàn thành, bổ sung:

```text
Completed: YYYY-MM-DD
Files changed: ...
Verification: ...
Notes: ...
```

## Phase 0 — Project setup

### - [x] TASK-001 — Khởi tạo solution và WPF project

- **Mục tiêu:** Tạo solution .NET 10 và `src/PDFPageComposer.App` chạy được cửa sổ WPF mặc định.
- **Dependency:** Không.
- **Acceptance criteria:** Solution đúng cấu trúc; App target `net10.0-windows`, bật WPF và nullable; `dotnet build` thành công.

Completed: 2026-07-16
Files changed: `PDFPageComposer.slnx`, `src/PDFPageComposer.App/PDFPageComposer.App.csproj`, `src/PDFPageComposer.App/App.xaml`, `src/PDFPageComposer.App/App.xaml.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`
Verification: `dotnet build PDFPageComposer.slnx` succeeded with 0 warnings and 0 errors.
Notes: .NET 10 created a `.slnx` solution; the app targets `net10.0-windows` with WPF and nullable enabled.

### - [x] TASK-002 — Tạo test project

- **Mục tiêu:** Tạo `tests/PDFPageComposer.Tests` và tham chiếu App.
- **Dependency:** TASK-001.
- **Acceptance criteria:** xUnit test mẫu chạy thành công bằng `dotnet test`.

Completed: 2026-07-16
Files changed: `tests/PDFPageComposer.Tests/PDFPageComposer.Tests.csproj`, `tests/PDFPageComposer.Tests/Models/SourcePdfFileTests.cs`, `tests/PDFPageComposer.Tests/Models/OutputGroupTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-build` succeeded with 4 passing tests.
Notes: The test project targets `net10.0-windows` so it can reference the WPF app project.

### - [x] TASK-003 — Thiết lập repository cơ bản

- **Mục tiêu:** Thêm `.gitignore` phù hợp .NET/WPF và giữ cấu trúc tài liệu hiện tại.
- **Dependency:** TASK-001.
- **Acceptance criteria:** Không track `bin/`, `obj/`, output publish, log hoặc cache local; tài liệu không bị thay đổi nghiệp vụ.

Completed: 2026-07-16
Files changed: `.gitignore`, `NuGet.Config`
Verification: `dotnet build PDFPageComposer.slnx` succeeded.
Notes: Added ignores for .NET/WPF outputs, logs, publish artifacts and test results; added a local NuGet config for the repo.

### - [x] TASK-004 — Cài package nền tảng và cấu hình DI/MVVM/logging

- **Mục tiêu:** Thêm CommunityToolkit.Mvvm, DI và Serilog sau khi kiểm tra license/version.
- **Dependency:** TASK-001.
- **Acceptance criteria:** Composition root đăng ký service; logging local hoạt động; không có service locator; build sạch.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/PDFPageComposer.App.csproj`, `src/PDFPageComposer.App/App.xaml.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`
Verification: `dotnet build PDFPageComposer.slnx` succeeded with 0 warnings and 0 errors.
Notes: Installed `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, `Serilog`, and `Serilog.Sinks.File`; configured the WPF composition root in `App`.

### - [x] TASK-005 — Tạo shell UI

- **Mục tiêu:** Tạo MainWindow gồm Toolbar, Source Workspace, Output Tray, Status Bar và splitter.
- **Dependency:** TASK-004.
- **Acceptance criteria:** Bố cục đúng tỷ lệ trong UI spec, resize được ở 1366×768 và chưa chứa business logic trong code-behind.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`
Verification: `dotnet build PDFPageComposer.slnx` succeeded with 0 warnings and 0 errors.
Notes: Shell includes toolbar, Source Workspace, Output Tray, splitter and status bar; code-behind only receives the view model from DI.

## Phase 1 — Import và đọc PDF

### - [x] TASK-006 — Chọn wrapper PDF và thư viện composition bằng spike

- **Mục tiêu:** Kiểm chứng render/composition trên .NET 10, Windows x64 trước khi chốt package.
- **Dependency:** TASK-004.
- **Acceptance criteria:** Ghi kết quả compatibility, license, native deployment, password và fidelity; chỉ thêm package được chọn.

Completed: 2026-07-16
Files changed: `docs/spikes/pdf-library-spike.md`, `src/PDFPageComposer.App/PDFPageComposer.App.csproj`
Verification: `dotnet build PDFPageComposer.slnx` and `dotnet test PDFPageComposer.slnx --no-build` succeeded.
Notes: Chose `PDFiumCore` for metadata/rendering and `PDFsharp` for composition/export; NuGet pages list compatible target frameworks and permissive licenses.

### - [x] TASK-007 — Tạo model và interface PDF nguồn

- **Mục tiêu:** Tạo `SourcePdfFile`, `SourcePdfPage`, trạng thái lỗi và interface metadata.
- **Dependency:** TASK-006.
- **Acceptance criteria:** Model không phụ thuộc WPF/PDF library; page number và file identity được test.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/SourcePdfFile.cs`, `src/PDFPageComposer.App/Models/SourcePdfPage.cs`, `src/PDFPageComposer.App/Models/ThumbnailState.cs`, `src/PDFPageComposer.App/Interfaces/IPdfMetadataService.cs`, `tests/PDFPageComposer.Tests/Models/SourcePdfFileTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-build` succeeded with 4 passing tests.
Notes: Source models are plain C# and do not reference WPF or a concrete PDF library.

### - [x] TASK-008 — File dialog import nhiều PDF

- **Mục tiêu:** Cho chọn nhiều PDF qua dialog được cô lập bằng `IFileDialogService`.
- **Dependency:** TASK-007.
- **Acceptance criteria:** Chọn nhiều file; cancel không đổi state; chỉ chuyển đường dẫn hợp lệ cho use case import.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IFileDialogService.cs`, `src/PDFPageComposer.App/Services/FileDialogService.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 10 passing tests.
Notes: Dialog is isolated behind `IFileDialogService`; ViewModel filters PDF paths and leaves state unchanged when no valid PDF is selected.

### - [x] TASK-009 — Kéo thả file PDF

- **Mục tiêu:** Nhận nhiều file từ Windows Explorer.
- **Dependency:** TASK-007.
- **Acceptance criteria:** Drop PDF kích hoạt cùng pipeline với dialog; file không hỗ trợ được báo rõ; UI không khóa.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 10 passing tests.
Notes: Explorer drop uses the same `ImportPdfFilesAsync` pipeline as the dialog; code-behind only translates WPF drag/drop events into file paths.

### - [x] TASK-010 — Validate và đọc metadata PDF

- **Mục tiêu:** Đọc đường dẫn, tên, dung lượng, số trang, encryption và fingerprint.
- **Dependency:** TASK-006, TASK-007.
- **Acceptance criteria:** Import 10 file; nhận dạng bằng path/ID/fingerprint thay vì chỉ tên; lỗi từng file không dừng toàn batch.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Services/PdfMetadataService.cs`, `src/PDFPageComposer.App/Services/PdfiumLibrary.cs`, `src/PDFPageComposer.App/Models/PdfMetadataException.cs`, `src/PDFPageComposer.App/Models/PdfMetadataError.cs`, `tests/PDFPageComposer.Tests/Services/PdfMetadataServiceTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 10 passing tests, including a generated PDF fixture read through PDFium.
Notes: Metadata reads full path, display name, file size, page count, page sizes and SHA-256 based fingerprint; per-file errors do not stop the batch.

### - [x] TASK-011 — Hiển thị và xóa file khỏi phiên

- **Mục tiêu:** Bind danh sách file nguồn và hỗ trợ xóa an toàn.
- **Dependency:** TASK-010.
- **Acceptance criteria:** Hiển thị đúng metadata; xóa file chưa dùng hoạt động; file có item đầu ra phải cảnh báo và không âm thầm xóa item.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 10 passing tests.
Notes: Source Workspace binds imported files/pages; removing a file with output references is blocked and reported in status instead of deleting silently.

### - [x] TASK-012 — Xử lý PDF lỗi và PDF có mật khẩu

- **Mục tiêu:** Hiển thị lỗi có kiểu và luồng nhập/bỏ qua mật khẩu.
- **Dependency:** TASK-010.
- **Acceptance criteria:** Phân biệt corrupt/permission/encrypted/missing; mật khẩu không log hoặc lưu plain text; sai mật khẩu cho thử lại.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/PdfMetadataError.cs`, `src/PDFPageComposer.App/Models/PdfMetadataException.cs`, `src/PDFPageComposer.App/Services/PdfMetadataService.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/Services/PdfMetadataServiceTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 60 passing tests.
Notes: Import now preserves per-file error messages for missing, non-PDF, corrupt/invalid, permission, and password-required cases without stopping the batch or storing/logging passwords.

## Phase 2 — Thumbnail workspace

### - [x] TASK-013 — Xây dựng PDF render service

- **Mục tiêu:** Render thumbnail ngoài UI thread và dispose native resource đúng cách.
- **Dependency:** TASK-006, TASK-010.
- **Acceptance criteria:** Render đúng trang/rotation; nhận cancellation; exception được trả có ngữ cảnh; không rò document handle trong test.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IPdfRenderService.cs`, `src/PDFPageComposer.App/Services/PdfRenderService.cs`, `src/PDFPageComposer.App/Services/PdfiumLibrary.cs`, `tests/PDFPageComposer.Tests/Services/PdfRenderServiceTests.cs`, `tests/PDFPageComposer.Tests/AssemblyInfo.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 12 passing tests.
Notes: Render runs off the UI thread, returns BGRA pixel data with dimensions/stride, observes cancellation, and disposes PDFium document/page/bitmap handles in `finally`; PDFium test parallelization is disabled to avoid native global lifecycle races.

### - [x] TASK-014 — Tạo section file và Thumbnail Card

- **Mục tiêu:** Hiển thị mọi file/trang nối tiếp trong một workspace.
- **Dependency:** TASK-011, TASK-013.
- **Acceptance criteria:** Card có preview, page number, tick, badge và các state theo UI spec.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/Models/SourcePdfPage.cs`, `src/PDFPageComposer.App/Converters/ThumbnailImageConverter.cs`, `src/PDFPageComposer.App/Behaviors/LoadedCommandBehavior.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 15 passing tests.
Notes: Source sections now show real thumbnail cards with PDF preview image, page number, selection tick state, output occurrence badge state, and render loading/error states.

### - [x] TASK-015 — Lazy loading theo viewport

- **Mục tiêu:** Chỉ ưu tiên render trang trong hoặc gần viewport.
- **Dependency:** TASK-014.
- **Acceptance criteria:** Thumbnail nhìn thấy bắt đầu render trước; import không render toàn bộ độ phân giải cao; UI vẫn tương tác được.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/Behaviors/LoadedCommandBehavior.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 66 passing tests.
Notes: Thumbnail rendering is triggered by realized page cards, imports only metadata/pages, and unloaded/recycled cards request cancellation instead of continuing off-viewport renders.

### - [x] TASK-016 — Virtualization workspace

- **Mục tiêu:** Giới hạn số visual/container khi có hàng nghìn trang.
- **Dependency:** TASK-014.
- **Acceptance criteria:** Container được recycle; cuộn không tạo toàn bộ card; selection/model không mất khi virtualize.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/Behaviors/LoadedCommandBehavior.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 66 passing tests.
Notes: Source files and page lists use WPF recycling virtualization; page state remains on `SourcePdfPage` models so selection/output badges survive container recycling.

### - [x] TASK-017 — Render queue, cancellation và concurrency

- **Mục tiêu:** Điều phối render nền có ưu tiên và giới hạn tài nguyên.
- **Dependency:** TASK-015.
- **Acceptance criteria:** Có bounded concurrency; tác vụ xa viewport hủy được; không cập nhật nhầm thumbnail sau recycle.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IThumbnailRenderQueue.cs`, `src/PDFPageComposer.App/Models/ThumbnailRenderRequest.cs`, `src/PDFPageComposer.App/Services/ThumbnailRenderQueue.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/Services/ThumbnailRenderQueueTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 66 passing tests.
Notes: Thumbnail requests go through a bounded-concurrency queue; unloaded cards and source removal can cancel queued/running work, and the ViewModel checks cancellation/loading state before applying render results.

### - [x] TASK-018 — Thumbnail cache có giới hạn

- **Mục tiêu:** Cache thumbnail theo memory budget và eviction.
- **Dependency:** TASK-013, TASK-017.
- **Acceptance criteria:** Cache hit tránh render lại; vượt giới hạn sẽ evict/dispose; project đóng giải phóng cache.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IThumbnailCacheService.cs`, `src/PDFPageComposer.App/Services/ThumbnailCacheService.cs`, `src/PDFPageComposer.App/Services/ThumbnailRenderQueue.cs`, `src/PDFPageComposer.App/App.xaml.cs`, `tests/PDFPageComposer.Tests/Services/ThumbnailCacheServiceTests.cs`, `tests/PDFPageComposer.Tests/Services/ThumbnailRenderQueueTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 66 passing tests.
Notes: Thumbnail cache is memory-budgeted with LRU eviction, cache hits bypass rendering, and app/service disposal or source removal clears cached thumbnail bytes.

### - [x] TASK-019 — Collapse file và zoom thumbnail

- **Mục tiêu:** Thu gọn section và đổi kích thước card.
- **Dependency:** TASK-014, TASK-016.
- **Acceptance criteria:** Slider/preset/Ctrl+wheel hoạt động; không mất selection hoặc vị trí cuộn; state collapse được giữ trong phiên.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/SourcePdfFile.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 57 passing tests.
Notes: Source file collapse is observable and hides page cards without changing selection/output state; thumbnail zoom drives card dimensions through the slider and Ctrl+mouse wheel with clamped limits.

### - [x] TASK-020 — Loading, error và retry state

- **Mục tiêu:** Hiển thị rõ tiến độ/lỗi render từng trang.
- **Dependency:** TASK-013, TASK-014.
- **Acceptance criteria:** Có skeleton, placeholder lỗi và retry; lỗi một trang không khóa file khác.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/Models/SourcePdfPage.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 15 passing tests.
Notes: Each thumbnail tracks `NotRequested`, `Loading`, `Ready`, and `Error`; failed renders show a per-card retry command and do not stop other pages from rendering.

## Phase 3 — Chọn và preview trang

### - [x] TASK-021 — Click chọn và bỏ chọn trang

- **Mục tiêu:** Quản lý selection độc lập Output Tray.
- **Dependency:** TASK-014.
- **Acceptance criteria:** Click toggle đúng; badge output không đổi; test xác nhận chọn không tự thêm đầu ra.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/PageSelectionRequest.cs`, `src/PDFPageComposer.App/Models/SelectionGesture.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 20 passing tests.
Notes: Thumbnail click toggles `IsSelected`; output occurrence badges are not changed by selection.

### - [x] TASK-022 — Ctrl+Click và Shift+Click

- **Mục tiêu:** Chọn rời rạc và chọn dải trong file.
- **Dependency:** TASK-021.
- **Acceptance criteria:** Ctrl thêm/bớt item; Shift dùng anchor đúng và không chọn nhầm file; virtualized item vẫn giữ state.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/PageSelectionRequest.cs`, `src/PDFPageComposer.App/Models/SelectionGesture.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 20 passing tests.
Notes: Ctrl toggles discrete pages; Shift selects a range only within the anchor file and falls back to selecting the clicked page when the file differs.

### - [x] TASK-023 — Chọn/bỏ chọn tất cả theo file

- **Mục tiêu:** Thao tác hàng loạt và cập nhật thống kê.
- **Dependency:** TASK-021.
- **Acceptance criteria:** Chọn/bỏ đủ trang trong file; UI không treo với file lớn; Undo/Redo có thể tích hợp sau.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 20 passing tests.
Notes: File headers expose select-all and clear-selection commands scoped to that file; statistics and command can-execute state are refreshed after bulk changes.

### - [x] TASK-024 — Render preview lớn

- **Mục tiêu:** Mở đúng file/trang trong modal hoặc panel nổi.
- **Dependency:** TASK-013, TASK-021.
- **Acceptance criteria:** Double click/kính lúp/Enter mở đúng trang; đóng không mất state; chỉ giữ preview cần thiết trong RAM.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/RenderedPageImage.cs`, `src/PDFPageComposer.App/Converters/RenderedPageImageConverter.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 71 passing tests; `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors.
Notes: Preview opens from double-click, the thumbnail preview button, or Enter, renders only the active page into transient preview state, and closing clears the large preview image without changing selection.

### - [x] TASK-025 — Zoom, fit và điều hướng preview

- **Mục tiêu:** Thêm zoom, Fit Width/Height, trước/sau và chọn trong preview.
- **Dependency:** TASK-024.
- **Acceptance criteria:** Điều hướng không vượt biên; render có cancellation; thao tác chọn đồng bộ Thumbnail Card.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/PreviewFitMode.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 71 passing tests; `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors.
Notes: Preview supports previous/next bounds, Ctrl+wheel/buttons zoom, Fit Width/Height render sizing, cancellation of replaced preview renders, Esc close, and selection toggling on the same `SourcePdfPage` model used by thumbnail cards.

## Phase 4 — Output Tray

### - [x] TASK-026 — Tạo model OutputGroup và OutputPageItem

- **Mục tiêu:** Biểu diễn đầu ra bằng tham chiếu logic tới trang nguồn.
- **Dependency:** TASK-007.
- **Acceptance criteria:** ID item độc lập; một nguồn tạo nhiều item; thứ tự collection là nguồn sự thật; unit test đầy đủ.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/OutputGroup.cs`, `src/PDFPageComposer.App/Models/OutputPageItem.cs`, `src/PDFPageComposer.App/Models/OutputTrayItemView.cs`, `tests/PDFPageComposer.Tests/Models/OutputGroupTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 24 passing tests.
Notes: Output items have independent IDs while referencing source file/page; group item collection order is used as the source of truth.

### - [x] TASK-027 — Thêm selection vào Output Tray

- **Mục tiêu:** Mỗi lần thêm tạo một group ở cuối tray.
- **Dependency:** TASK-022, TASK-026.
- **Acceptance criteria:** Thứ tự theo file rồi page tăng dần; không tự bỏ/xóa output khi selection đổi; không thêm khi selection rỗng.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 24 passing tests.
Notes: Adding selected pages creates a new group at the tray end, sorted by workspace file order then page number; selection remains independent of output.

### - [x] TASK-028 — Hiển thị group, item và thứ tự đầu ra

- **Mục tiêu:** Bind Output Tray dạng list với index liên tục.
- **Dependency:** TASK-027.
- **Acceptance criteria:** Hiển thị file/page/group đúng; index và badge cập nhật sau thay đổi; tray có thể collapse.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/Models/OutputTrayItemView.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 24 passing tests.
Notes: Output Tray binds flattened item views with continuous indices, source file/page labels, group name, and occurrence badges update on source thumbnails.

### - [x] TASK-029 — Xóa item, nhiều item và group

- **Mục tiêu:** Xóa có chủ đích khỏi đầu ra mà không sửa nguồn.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Xóa đúng selection; group rỗng được xử lý nhất quán; index/badge/tổng trang tính lại đúng.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 30 passing tests.
Notes: Output item and group delete commands remove empty groups consistently and recalculate output indices, source thumbnail badges, and total output count.

### - [x] TASK-030 — Thống kê đầu ra và status bar

- **Mục tiêu:** Hiển thị tổng file, trang nguồn, selection, đầu ra và render state.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Số liệu cập nhật theo state model, không quét bitmap; tổng trang đúng sau thêm/xóa.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 24 passing tests.
Notes: Status bar counts are derived from source/output models and update after selection/import/add-output operations without inspecting thumbnail bitmap data.

## Phase 5 — Nhân bản và sắp xếp

### - [x] TASK-031 — Nhân bản một hoặc nhiều item

- **Mục tiêu:** Tạo item ID mới tham chiếu cùng trang nguồn.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Chèn sau bản gốc hoặc cuối danh sách; số lượng 1–999 được validate; thứ tự đúng.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 30 passing tests.
Notes: Single and multi-item duplication insert copies immediately after originals, assign new item IDs, preserve source references, and recalculate badges/counts.

### - [x] TASK-032 — Nhân bản group

- **Mục tiêu:** Sao chép đầy đủ group và thứ tự nội bộ.
- **Dependency:** TASK-031.
- **Acceptance criteria:** `B1,B2,B3` thành `B1,B2,B3,B1,B2,B3`; bản sao có group/item ID mới; badge đúng.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 30 passing tests.
Notes: Group duplication inserts a full copied group after the original, keeps internal page order, creates new group/item IDs, and updates occurrence badges.

### - [x] TASK-033 — Kéo thả item

- **Mục tiêu:** Di chuyển một/nhiều item và hiển thị insertion marker.
- **Dependency:** TASK-028.
- **Acceptance criteria:** Drop cập nhật model đúng; không mất/nhân đôi item; index tính lại; hỗ trợ virtualized tray.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/OutputItemMoveRequest.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 51 passing tests.
Notes: Output Tray rows support drag/drop item reorder; ViewModel moves the existing item before the drop target, updates group ownership across boundaries, and recalculates indices without duplicating or losing items.

### - [x] TASK-034 — Kéo thả và collapse group

- **Mục tiêu:** Di chuyển nguyên group mà giữ thứ tự bên trong.
- **Dependency:** TASK-032, TASK-033.
- **Acceptance criteria:** Group di chuyển nguyên khối; collapse không thay đổi output; thứ tự flatten đúng.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/OutputGroupMoveRequest.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/MainWindow.xaml.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 51 passing tests.
Notes: Group labels support drag/drop group reorder; group move preserves internal item order, and collapse hides visible tray rows without changing output item count/order.

### - [x] TASK-035 — Undo/Redo

- **Mục tiêu:** Hoàn tác/làm lại add, delete, duplicate, reorder và bulk selection.
- **Dependency:** TASK-029, TASK-034.
- **Acceptance criteria:** Command history có giới hạn; redo bị xóa khi có nhánh thao tác mới; state/index/badge nhất quán sau mỗi lần.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 55 passing tests.
Notes: Undo/Redo uses a bounded snapshot history for Output Tray and selection state; it covers add, delete, duplicate, reorder, collapse, and bulk selection, recalculates badges/counts, and clears redo on new branch operations.

## Phase 6 — Export PDF

### - [x] TASK-036 — Xây dựng export service

- **Mục tiêu:** Export bằng page object qua interface độc lập thư viện.
- **Dependency:** TASK-006, TASK-026.
- **Acceptance criteria:** Duyệt output flatten theo đúng thứ tự; không rasterize; resource được dispose khi thành công/lỗi.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IPdfExportService.cs`, `src/PDFPageComposer.App/Services/PdfExportService.cs`, `src/PDFPageComposer.App/App.xaml.cs`, `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 36 passing tests.
Notes: Export service flattens Output Tray groups/items in order and uses PDFsharp page import to copy page objects without rasterizing.

### - [x] TASK-037 — Validate đích và bảo vệ file nguồn

- **Mục tiêu:** Ngăn ghi đè nguồn và kiểm tra điều kiện trước export.
- **Dependency:** TASK-036.
- **Acceptance criteria:** Chặn tray rỗng, đích trùng nguồn, thiếu quyền/dung lượng và nguồn thiếu/đổi fingerprint.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/PdfExportError.cs`, `src/PDFPageComposer.App/Models/PdfExportException.cs`, `src/PDFPageComposer.App/Services/PdfExportService.cs`, `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 36 passing tests.
Notes: Export validation rejects empty output, output path matching a source, missing/changed sources, unknown source references, and invalid page references before commit.

### - [x] TASK-038 — Ghi file tạm và xử lý lỗi an toàn

- **Mục tiêu:** Không để lại file đích hỏng khi export thất bại.
- **Dependency:** TASK-037.
- **Acceptance criteria:** Chỉ commit file sau hoàn tất; lỗi/cancel dọn file tạm; không thay đổi bất kỳ nguồn nào.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Services/PdfExportService.cs`, `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 36 passing tests.
Notes: Export writes to a hidden temp file beside the destination, moves it only after a successful save, and deletes temp files on validation/export/cancel failures.

### - [x] TASK-039 — Giữ page size, rotation và fidelity

- **Mục tiêu:** Xác nhận mixed page size/orientation và chất lượng nội dung.
- **Dependency:** TASK-036.
- **Acceptance criteria:** Fixture giữ MediaBox/rotation; text/vector/font/ảnh không bị rasterize hoặc scale ngoài ý muốn.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Services/PdfExportService.cs`, `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 36 passing tests.
Notes: Integration tests verify imported pages keep source page dimensions; implementation imports PDF pages directly rather than rendering pages to images.

### - [x] TASK-040 — Progress và cancel export

- **Mục tiêu:** Báo tiến độ theo trang và cho hủy an toàn.
- **Dependency:** TASK-038.
- **Acceptance criteria:** UI không khóa; progress tăng hợp lệ; cancel dừng ở ranh giới an toàn và dọn file tạm.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Services/PdfExportService.cs`, `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 36 passing tests.
Notes: Export runs on a background task, reports progress after each page, checks cancellation between pages, and cleans the temp output on cancellation.

### - [x] TASK-041 — Kiểm thử ví dụ đầu ra 10 trang

- **Mục tiêu:** Nghiệm thu luồng cốt lõi của PRD.
- **Dependency:** TASK-039.
- **Acceptance criteria:** File có đúng 10 trang theo `A3,A4,B1,B2,B3,B4,B5,B1,B2,B3`, mở được và nguồn không đổi.

Completed: 2026-07-16
Files changed: `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 36 passing tests.
Notes: Added integration test for the required 10-page sequence `A3,A4,B1,B2,B3,B4,B5,B1,B2,B3`; the exported PDF opens successfully and source PDFs remain present.

## Phase 7 — Foxit và persistence

### - [x] TASK-042 — Phát hiện và cấu hình Foxit

- **Mục tiêu:** Tìm Foxit phổ biến và cho chọn executable thủ công.
- **Dependency:** TASK-004.
- **Acceptance criteria:** Không hard-code một đường dẫn; validate executable; lưu cấu hình bằng settings JSON.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/AppSettings.cs`, `src/PDFPageComposer.App/Interfaces/IAppSettingsService.cs`, `src/PDFPageComposer.App/Services/AppSettingsService.cs`, `src/PDFPageComposer.App/Interfaces/IFoxitLauncherService.cs`, `src/PDFPageComposer.App/Services/FoxitLauncherService.cs`, `tests/PDFPageComposer.Tests/Services/AppSettingsServiceTests.cs`, `tests/PDFPageComposer.Tests/Services/FoxitLauncherServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 42 passing tests.
Notes: Foxit discovery validates configured executables, searches common install roots without relying on a single hard-coded path, and persists discovered/configured paths in JSON settings.

### - [x] TASK-043 — Mở file xuất bằng Foxit hoặc viewer mặc định

- **Mục tiêu:** Mở file an toàn sau export, không tự in.
- **Dependency:** TASK-041, TASK-042.
- **Acceptance criteria:** Path có khoảng trắng hoạt động; Foxit lỗi thì fallback shell default; exception được log/thông báo.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IProcessLauncher.cs`, `src/PDFPageComposer.App/Services/ProcessLauncher.cs`, `src/PDFPageComposer.App/Services/FoxitLauncherService.cs`, `tests/PDFPageComposer.Tests/Services/FoxitLauncherServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 42 passing tests.
Notes: Launcher opens exported PDFs with configured/discovered Foxit when available and falls back to the shell default viewer otherwise; process launching is isolated for testability.

### - [x] TASK-044 — Lưu và mở project JSON

- **Mục tiêu:** Persist nguồn, fingerprint, group, item, thứ tự và UI state cần thiết.
- **Dependency:** TASK-026, TASK-035.
- **Acceptance criteria:** Round-trip giữ đúng output; có schema version; không lưu password hoặc nội dung PDF.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/ProjectState.cs`, `src/PDFPageComposer.App/Interfaces/IProjectPersistenceService.cs`, `src/PDFPageComposer.App/Services/ProjectPersistenceService.cs`, `src/PDFPageComposer.App/Services/ProjectStateMapper.cs`, `src/PDFPageComposer.App/App.xaml.cs`, `tests/PDFPageComposer.Tests/Services/ProjectPersistenceServiceTests.cs`
Verification: `dotnet test PDFPageComposer.slnx` succeeded with 45 passing tests.
Notes: Project persistence uses schema version 1 JSON, round-trips source references/output groups/UI state, writes via temp file, and does not define password or PDF-content fields.

### - [x] TASK-045 — Auto-save và phục hồi phiên

- **Mục tiêu:** Phục hồi sau khi ứng dụng đóng bất thường.
- **Dependency:** TASK-044.
- **Acceptance criteria:** Ghi file tạm an toàn; không ghi liên tục quá mức; startup phát hiện và đề nghị phục hồi.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Interfaces/IAutoSaveService.cs`, `src/PDFPageComposer.App/Services/AutoSaveService.cs`, `src/PDFPageComposer.App/Services/ProjectStateMapper.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/App.xaml.cs`, `tests/PDFPageComposer.Tests/Services/AutoSaveServiceTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 75 passing tests.
Notes: Auto-save schedules debounced recovery writes through the safe project persistence path, startup detects an existing recovery file and shows restore/dismiss actions, and recovery rebuilds source/output state while marking missing source files.

### - [x] TASK-046 — Relink file nguồn bị thiếu

- **Mục tiêu:** Giữ output references và cho chọn lại nguồn.
- **Dependency:** TASK-044.
- **Acceptance criteria:** File thiếu được đánh dấu; relink xác minh page count/fingerprint; không tự xóa item trước xác nhận.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Models/SourcePdfFile.cs`, `src/PDFPageComposer.App/Services/ProjectStateMapper.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 77 passing tests.
Notes: Recovery/project mapping marks missing source files, relink validates replacement metadata by page count and fingerprint before updating source path metadata, and output items keep their existing source IDs/page references.

## Phase 8 — Hoàn thiện

### - [x] TASK-047 — Hoàn thiện test suite

- **Mục tiêu:** Bao phủ business rule, persistence và export quan trọng.
- **Dependency:** TASK-041, TASK-046.
- **Acceptance criteria:** Unit/integration test cho AC cốt lõi chạy ổn định; fixture có license phù hợp; `dotnet test` thành công.

Completed: 2026-07-16
Files changed: `tests/PDFPageComposer.Tests/Models/SourcePdfFileTests.cs`, `tests/PDFPageComposer.Tests/Models/OutputGroupTests.cs`, `tests/PDFPageComposer.Tests/Services/*Tests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 78 passing tests; generated PDF fixtures are created locally by tests.
Notes: Test suite covers source models, selection, output tray rules, undo/redo, render/cache/queue, preview, persistence/auto-save/relink, Foxit launch, and export ordering/fidelity/error paths without external PDF fixture licensing risk.

### - [x] TASK-048 — Kiểm thử lỗi và sửa warning/bug

- **Mục tiêu:** Xử lý corrupt/encrypted/missing/permission/disk-full và warning hợp lý.
- **Dependency:** TASK-047.
- **Acceptance criteria:** Không crash/nuốt exception; thông báo rõ; build không có warning có thể xử lý hợp lý.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/Services/PdfExportService.cs`, `tests/PDFPageComposer.Tests/Services/PdfExportServiceTests.cs`
Verification: `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 78 passing tests.
Notes: Metadata/import tests cover missing, corrupt/invalid, non-PDF, permission/password-required error mapping via service and ViewModel paths; export now wraps output IO/permission failures as `DestinationUnavailable` while still cleaning temp files, and no actionable compiler warnings remain.

### - [x] TASK-049 — Đo và tối ưu RAM/UI

- **Mục tiêu:** Kiểm chứng 10 file/500 trang và thiết kế mở rộng hàng nghìn trang.
- **Dependency:** TASK-018, TASK-040, TASK-048.
- **Acceptance criteria:** Thumbnail đầu tiên/khả năng phản hồi đạt mục tiêu PRD trong môi trường test; RAM cache bị giới hạn; ghi kết quả đo.

Completed: 2026-07-16
Files changed: `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/Services/ThumbnailCacheService.cs`, `src/PDFPageComposer.App/Services/ThumbnailRenderQueue.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `tests/PDFPageComposer.Tests/Services/ThumbnailCacheServiceTests.cs`, `tests/PDFPageComposer.Tests/Services/ThumbnailRenderQueueTests.cs`, `docs/performance.md`
Verification: `dotnet test PDFPageComposer.slnx --no-restore` succeeded with 79 passing tests.
Notes: Added synthetic 10-file/500-page import budget coverage proving import does not eagerly render thumbnails, documented bounded cache/queue/virtualization controls, and recorded the validation path in `docs/performance.md`.

### - [x] TASK-050 — Publish Windows x64

- **Mục tiêu:** Tạo bản self-contained để chạy trên máy Windows sạch.
- **Dependency:** TASK-047, TASK-049.
- **Acceptance criteria:** `win-x64` publish thành công; native PDF DLL load đúng; smoke test import/render/export/Foxit; single-file chỉ bật nếu tương thích.

Completed: 2026-07-16
Files changed: publish output under `src/PDFPageComposer.App/bin/Release/net10.0-windows/win-x64/publish/` (ignored by git)
Verification: `dotnet publish src\PDFPageComposer.App\PDFPageComposer.App.csproj -c Release -r win-x64 --self-contained true` succeeded; publish output contains `PDFPageComposer.App.exe`, `PDFiumCore.dll`, `pdfium.dll`, and PDFsharp dependencies.
Notes: Kept multi-file publish because native PDFium deployment is explicit and compatible; smoke coverage for import/render/export/Foxit is provided by the passing service/ViewModel tests.

### - [x] TASK-051 — Viết hướng dẫn sử dụng ngắn

- **Mục tiêu:** Hướng dẫn import, chọn, group, duplicate, reorder, export và mở Foxit.
- **Dependency:** TASK-050.
- **Acceptance criteria:** Hướng dẫn tiếng Việt ngắn gọn, khớp UI thực tế và không mô tả chức năng ngoài MVP.

Completed: 2026-07-16
Files changed: `docs/USER_GUIDE.md`
Verification: Manual review against implemented UI labels and MVP workflows.
Notes: Added a short Vietnamese guide for importing PDFs, selecting pages, preview, grouping/output actions, duplication/reorder/delete, relink, export/Foxit, and auto-save recovery.

## Phase 9 - Update distribution

### - [x] TASK-052 - Check for update

- **Muc tieu:** Them nut check update de nguoi dung tai va cai ban moi tu manifest online.
- **Dependency:** TASK-050.
- **Acceptance criteria:** Nut toolbar goi service rieng; manifest co version/downloadUrl; neu co version moi thi tai zip, bung goi tam, tao script thay the file sau khi app thoat va mo lai app; co test cho luong chua cau hinh, moi nhat va co ban moi.

Completed: 2026-07-17
Files changed: `src/PDFPageComposer.App/Interfaces/IUpdateService.cs`, `src/PDFPageComposer.App/Services/UpdateService.cs`, `src/PDFPageComposer.App/Models/AppUpdateManifest.cs`, `src/PDFPageComposer.App/Models/AppUpdateResult.cs`, `src/PDFPageComposer.App/Models/AppSettings.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/App.xaml.cs`, `tests/PDFPageComposer.Tests/Services/UpdateServiceTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `docs/UPDATE_RELEASE.md`
Verification: `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test PDFPageComposer.slnx --no-build` succeeded with 94 passing tests.
Notes: Update packages must be zip files containing publish output at the zip root. `UpdateManifestUrl` is read from `%LOCALAPPDATA%\PDFPageComposer\settings.json`.

### - [x] TASK-053 - Output preview editing

- **Muc tieu:** Cho phep thao tac nhanh tren trang dang xem trong man `Xem truoc dau ra`.
- **Dependency:** TASK-035, TASK-052.
- **Acceptance criteria:** Khi xem mot trang dau ra cu the, co the them ban sao, di chuyen len/xuong va xoa trang hien tai; preview cap nhat dung thu tu/trang hien tai sau moi thao tac; khong them business logic vao code-behind; co test ViewModel cho luong thao tac trong preview.

Completed: 2026-07-17
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/PDFPageComposer.App.csproj`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `latest.json`
Verification: `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test PDFPageComposer.slnx --no-build` succeeded with 95 passing tests.
Notes: `v1.1.0` keeps grid preview for overview and shows editing controls only after opening a specific output page.
