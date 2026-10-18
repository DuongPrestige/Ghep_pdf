# Task — Module Đơn TikTok

Phạm vi: import CSV Seller Center, cấu hình SKU, kho PDF, phân loại đơn, tạo file in, lịch sử, xuất danh sách đơn. Đặc tả: `docs/tiktok/`.

Mã task đánh số chung cho cả dự án (xem `docs/README.md` để biết mã tiếp theo). Chỉ tối đa một task được ở trạng thái **In Progress** trong toàn dự án. `[ ]` là chưa hoàn thành, `[x]` là hoàn thành, `[~]` là đã hủy; trạng thái chi tiết ghi ngay dưới task.

Khi hoàn thành, bổ sung:

```text
Completed: YYYY-MM-DD
Files changed: ...
Verification: ...
Notes: ...
```

## Tổng quan trạng thái

| Trạng thái | Số lượng |
|---|---:|
| Done | 16 |
| Chưa xong (Backlog/Blocked) | 1 |
| Đã hủy | 1 |

## Phase 10 — Đơn TikTok

Tài liệu: `docs/tiktok/PRD.md`, `docs/tiktok/TECH_SPEC.md`, `docs/tiktok/UI_SPEC.md`. Nguồn đơn: file CSV xuất từ Seller Center.

### - [ ] TASK-057 — Spike TikTok Shop API

Status: Blocked (2026-10-16) — TikTok không cấp quyền Open API cho shop. Chuyển sang import file xuất đơn (TASK-059, TASK-060). Mã spike giữ trong `spikes/TikTokApiSpike`; xem `docs/spikes/tiktok-api-spike.md`.

- **Mục tiêu:** Kiểm chứng OAuth, ký request, API đơn hàng và sản phẩm.
- **Acceptance criteria:** Như bản v0.1; chỉ tiếp tục khi có quyền API.

### - [x] TASK-058 — SQLite: schema, migration, repository

- **Mục tiêu:** Thêm `Microsoft.Data.Sqlite`; tạo schema có version theo mục 4 của `docs/tiktok/TECH_SPEC.md` và repository cho Shop, Sku, IdolAlias, PdfAsset, ImportBatch, Order, OrderLine, PrintBatch.
- **Dependency:** —
- **Acceptance criteria:** Migration chạy khi khởi động; không có cột dữ liệu người nhận; có integration test trên DB tạm.

### - [x] TASK-059 — Đọc file CSV xuất đơn Seller Center

- **Mục tiêu:** CSV parser (RFC 4180) và `SellerCenterCsvOrderSource` implement `IOrderSource`; ánh xạ cột theo tên header.
- **Dependency:** —
- **Acceptance criteria:** Đọc đúng file mẫu đã ẩn danh (36 dòng, 33 đơn): bỏ tab thừa, ô có dấu phẩy và ngoặc kép, BOM, ngày `dd/MM/yyyy HH:mm:ss`; thiếu cột bắt buộc thì báo lỗi; không đọc các cột người nhận, số điện thoại, địa chỉ.

### - [x] TASK-060 — Import đơn và chống trùng

- **Mục tiêu:** `OrderImportService` theo TT-09, TT-10 và mục 3.3 của `docs/tiktok/TECH_SPEC.md`.
- **Dependency:** TASK-058, TASK-059, TASK-062.
- **Acceptance criteria:** Import cùng file 2 lần là idempotent; gộp dòng trùng trong file; chế độ Full ẩn đơn không còn trong file (chỉ trong cùng shop), chế độ Partial không ẩn; `ChangedAfterPrint`; Seller Note "ĐÃ IN" được đánh dấu đã in thủ công; có ImportReport; có test.

### - [x] TASK-061 — Quét kho PDF

- **Mục tiêu:** `PdfLibraryScanner` theo TT-01.
- **Dependency:** TASK-058.
- **Acceptance criteria:** Test tên hợp lệ và sai quy ước, trường hợp giáp năm, file lệch số trang; cache theo LastWriteTime.

### - [x] TASK-062 — Gợi ý cấu hình SKU và alias idol

- **Mục tiêu:** `SkuConfigSuggester` theo TT-03 và mục 5.2 của `docs/tiktok/TECH_SPEC.md`; chuẩn hóa chuỗi (bỏ dấu, viết hoa).
- **Dependency:** —
- **Acceptance criteria:** Đúng cho mọi mẫu variation liệt kê trong TECH_SPEC 5.2; import lại không ghi đè SKU đã Confirmed.

### - [x] TASK-063 — Phân loại đơn và PrintPlanBuilder

- **Mục tiêu:** `OrderClassifier` (TT-08) và `PrintPlanBuilder` (TT-04 → TT-07).
- **Dependency:** TASK-061, TASK-062.
- **Acceptance criteria:** Đúng ví dụ mục 7 của `docs/tiktok/PRD.md`; đơn có Buyer Message hoặc Seller Note vào nhóm Có ghi chú (TT-10), "Đã xem" tự hủy khi ghi chú đổi; dòng Ignore không chặn đơn; dòng Manual chặn cả đơn; xoay vòng reset theo lô; cảnh báo trùng hình; đơn bị chặn không làm lệch con trỏ.

### - [x] TASK-064 — Trang phân cách

- **Mục tiêu:** Tạo trang phân cách A4, hiển thị tiếng Việt có dấu, theo 3 chế độ của TT-07.
- **Dependency:** TASK-063.
- **Acceptance criteria:** Font có dấu hiển thị đúng; file tạm được dọn; có test.

### - [x] TASK-065 — Xuất lô in và đánh dấu đơn

- **Mục tiêu:** Chuyển plan thành dữ liệu cho `IPdfExportService`, xuất file, ghi lịch sử trong transaction, mở Foxit.
- **Dependency:** TASK-063, TASK-064, TASK-058.
- **Acceptance criteria:** Xuất lỗi hoặc hủy thì không đánh dấu đơn nào; thành công thì ghi đủ `PrintBatchItem` kèm snapshot Quantity; không rasterize.

### - [x] TASK-066 — UI: cửa sổ TikTok, dialog import, kho PDF, cài đặt

- **Mục tiêu:** `TikTokOrdersWindow` và `TikTokOrdersViewModel` riêng, mở từ toolbar; dialog Import (shop, chế độ, file, kéo thả) và báo cáo import; tab Kho PDF; cài đặt shop và thư mục.
- **Dependency:** TASK-060, TASK-061.
- **Acceptance criteria:** Không thêm logic vào `MainViewModel` hoặc code-behind; tác vụ dài không khóa UI; có test ViewModel.

### - [x] TASK-067 — UI: cấu hình SKU

- **Mục tiêu:** Tab Cấu hình SKU: lọc chưa cấu hình, sửa Loại xử lý/Idol/Card, xác nhận gợi ý hàng loạt, đặt cả sản phẩm là Bỏ qua, quản lý alias.
- **Dependency:** TASK-062, TASK-066.
- **Acceptance criteria:** Cấu hình khoảng 80 listing thao tác nhanh được; có test ViewModel.

### - [x] TASK-068 — UI: đơn hàng, tạo lô in, lịch sử

- **Mục tiêu:** Tab Đơn hàng (lọc theo nhóm và shop, hiển thị Buyer Message và Seller Note, nút "Đã xem – cho phép ghép", chọn đơn, Tạo file in có tiến trình và hủy, In lại) và tab Lịch sử (lô import, lô in, bỏ đánh dấu, tạo lại, copy danh sách mã đơn).
- **Dependency:** TASK-065, TASK-067.
- **Acceptance criteria:** Đạt AC-T02, AC-T03, AC-T06; có test ViewModel.

Completed (TASK-058 → TASK-068): 2026-10-16
Files changed: `src/PDFPageComposer.App/TikTok/**` (Models, Data, Import, Matching, Export, TextNormalizer, OrderNotes, TikTokSettingsLoader), `src/PDFPageComposer.App/ViewModels/TikTok/**`, `src/PDFPageComposer.App/Views/TikTokOrdersWindow.xaml`, `ServiceRegistration.cs`, `App.xaml.cs`, `MainWindow.xaml(.cs)`, `Interfaces/IFileDialogService.cs`, `IClipboardService.cs`, `ITikTokWindowService.cs`, `Services/FileDialogService.cs`, `ClipboardService.cs`, `TikTokWindowService.cs`, `Models/AppSettings.cs`, `Models/TikTokSettings.cs`, `PDFPageComposer.App.csproj` (Microsoft.Data.Sqlite 10.0.13), `tests/PDFPageComposer.Tests/TikTok/**`, `tests/PDFPageComposer.Tests/ViewModels/TikTok/**`, `tests/PDFPageComposer.Tests/TestData/tiktok/orders-sample.csv` (đã ẩn danh).
Verification: `dotnet build PDFPageComposer.slnx --no-incremental`: 0 warnings, 0 errors; `dotnet test PDFPageComposer.slnx`: 300/300 passed (gồm test end-to-end với file CSV mẫu, test STA mở TikTokOrdersWindow, test DI). Đã mở app thật và mở cửa sổ "Đơn TikTok" bằng UI Automation: đủ 5 tab, tạo được `%LOCALAPPDATA%\PDFPageComposer\tiktok.db`.
Notes: Làm bằng workflow nhiều agent, sau đó review đa góc nhìn kèm phản biện: 27 lỗi được xác nhận và đã sửa. Contract mở rộng: `OrderSourceResult.RejectedOrderIds` và overload `IOrderImportService.ImportAsync(..., protectedOrderIds, ...)`; đơn có dòng CSV lỗi bị loại nguyên đơn và không bị ẩn ở chế độ Full. Tên file in tự động thêm hậu tố _2, _3 nếu trùng. "In lại" bỏ qua cả chốt ghi chú đối với đơn đã in.

### - [x] TASK-069 — Kiểm thử thực tế và publish v1.3.0

- **Mục tiêu:** Chạy với file xuất thật của 2 shop, sửa lỗi, cập nhật USER_GUIDE, publish bản update.
- **Dependency:** TASK-068.
- **Acceptance criteria:** Đạt AC-T01 đến AC-T07; `latest.json` được cập nhật.

Completed: 2026-10-17
Files changed: `src/PDFPageComposer.App/PDFPageComposer.App.csproj` (1.3.0), `latest.json`, `docs/USER_GUIDE.md`, `docs/UPDATE_RELEASE.md`.
Verification: Chạy với các file CSV xuất thật của người dùng (gồm file sau khi đã in, đơn chuyển "Đang chờ lấy hàng", file có đơn gắn cờ đỏ), so sánh file in dự kiến với file người dùng ghép tay; 378/378 test passed; publish `win-x64` self-contained, release `v1.3.0` trên GitHub, tải lại asset khớp SHA256 rồi mới cập nhật `latest.json`.
Notes: Sửa lỗi phát hiện khi chạy thật được ghi ở TASK-072 → TASK-074. Cờ đỏ trên TikTok không có trong file CSV nên app không đọc được; đơn đã làm được nhận biết qua lịch sử lô in.

### - [~] TASK-070 — (Hủy) Chrome extension lấy đơn từ Seller Center

Status: Cancelled (2026-10-17) — người dùng chọn quy trình chỉ dùng file CSV: xuất CSV từ TikTok → import → tạo file in và xuất mã đơn chưa làm → lần sau import CSV mới, đơn đã làm tự được lọc. Không dùng extension, không tự gắn cờ trên TikTok.

- **Mục tiêu (cũ):** Extension (Manifest V3) đọc danh sách đơn chờ vận chuyển khi người dùng bấm nút, gửi vào app qua localhost.

### - [x] TASK-071 — Xuất danh sách đơn ra CSV (đánh dấu đơn đã làm file in)

- **Mục tiêu:** TT-12 của `docs/tiktok/PRD.md`: xuất danh sách đơn ra CSV theo phạm vi (đơn đã làm file in, tất cả đơn đang chờ, lần import gần nhất, đơn của một lô in), có cột "Đã làm file in" và file kho đã dùng.
- **Dependency:** TASK-068.
- **Acceptance criteria:** Mỗi dòng hàng một dòng CSV; Order ID/SKU ID có tab ở cuối; UTF-8 BOM, CRLF, quote theo RFC 4180; trạng thái giống tab Đơn hàng; không có đơn thì không tạo file; ghi lỗi không để lại file dở; có test.

Completed: 2026-10-17
Files changed: `src/PDFPageComposer.App/TikTok/Export/OrderListExportModels.cs`, `OrderListExportBuilder.cs`, `OrderListCsvWriter.cs`, `OrderListExportService.cs` (mới); `src/PDFPageComposer.App/ViewModels/TikTok/OrderListExportRunner.cs`, `OrdersTabViewModel.OrderList.cs` (mới), `HistoryTabViewModel.cs`, `TikTokServices.cs`; `Views/TikTokOrdersWindow.xaml`; `Interfaces/IFileDialogService.cs` (`PickSaveCsvFile`), `Services/FileDialogService.cs`; `ServiceRegistration.cs`; `tests/PDFPageComposer.Tests/TikTok/Export/OrderListExportBuilderTests.cs`, `OrderListCsvWriterTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/TikTok/OrderListExportViewModelTests.cs` (mới), `TikTokUiHarness.cs`, `ViewModels/MainViewModelTests.cs` (fake dialog); `docs/tiktok/PRD_TIKTOK.md` (TT-12).
Verification: `dotnet test tests/PDFPageComposer.Tests/PDFPageComposer.Tests.csproj`: 355/355 passed (sau khi sửa theo review), build 0 warnings, 0 errors (gồm test STA mở TikTokOrdersWindow không lỗi binding và test DI).
Notes: Không thêm method repository (dùng `GetOrdersAsync`, `GetOrdersByIdsAsync`, `GetImportBatchesAsync`, `GetPrintBatchItemsAsync`, `GetPrintBatchesAsync`, `GetPrintedItemsForOrdersAsync`). Trạng thái phân loại dùng `ClassificationOptions` hiện tại (không áp dụng "In lại"). "File kho đã dùng" lấy từ lô in hiện tại của đơn (`PrintedBatchId`); với phạm vi lô in, các cột Đã làm file in/Nguồn/Lô in/Thời gian in/File kho đã dùng lấy theo chính lô được chọn (kể cả đơn đã bỏ đánh dấu hoặc in lại ở lô khác), còn Trạng thái xử lý/Lý do theo phân loại hiện tại. File đã rời kho vẫn ghi "lấy N/M trang đầu" nhờ đọc M từ tên file. Ô chữ tự do bắt đầu bằng = + - @ được thêm dấu nháy đơn ở đầu để Excel không hiểu là công thức. "Đơn đã làm file in" gồm cả đơn không còn chờ vận chuyển (cột "Còn chờ vận chuyển" = Không). Tên file gợi ý `DS_DON_<DA_IN|TAT_CA|IMPORT_MOI|LO_<mã lô>>_<yyyyMMdd_HHmm>.csv`, thư mục mặc định là thư mục lưu file in.

### - [x] TASK-072 — Luôn lấy ảnh mới nhất, tự chọn file, thứ tự CSV, xóa import

- **Mục tiêu:** Sửa logic chọn file theo phản hồi khi chạy thật; cho người dùng tự chọn trang in; hiển thị đơn đúng thứ tự file CSV; xóa dữ liệu đã import.
- **Dependency:** TASK-063, TASK-068.
- **Acceptance criteria:** Mỗi bộ card ghép từ các file mới nhất của idol (file cuối chỉ lấy số trang đầu còn thiếu; mỗi đơn bắt đầu lại từ file mới nhất, bộ sau trong cùng đơn lùi sang file cũ hơn); dialog "Xem và tự chọn file" cho chọn trang, PDF ngoài kho, nhiều hoặc ít trang hơn số card; lựa chọn tay được lưu và dùng khi xuất; đơn hiện theo thứ tự dòng CSV; xóa một lần import hoặc toàn bộ đơn; trang phân cách là trang trắng; phạm vi xuất CSV "Đơn chưa làm file in".

Completed: 2026-10-17
Files changed: `TikTok/Matching/PdfAssetMatcher.cs`, `PrintPlanBuilder.cs`, `OrderClassifier.cs`, `TikTok/Models/PrintModels.cs`, `FileOverride.cs` (mới), `TikTokOrder.cs`, `TikTok/Data/TikTokSchemaMigrator.cs` (v3 → v6), `SqliteTikTokRepository.Orders.cs`, `.Overrides.cs` (mới), `.Printing.cs`, `TikTok/Import/OrderImportMerger.cs`, `TikTok/Export/PrintBatchExporter.cs`, `SeparatorPageRenderer.cs`, `OrderListExport*`, `Models/TikTokSettings.cs` (`AlwaysUseNewestFiles`), `ViewModels/TikTok/OrderFilesViewModel.cs` (mới), `OrdersTabViewModel.cs`, `OrderRowViewModel.cs`, `TikTokWorkspace.cs`, `Views/OrderFilesWindow.xaml` (mới), `Views/TikTokOrdersWindow.xaml`; test tương ứng.
Verification: Test plan builder với ví dụ thật (Jack 50 card 2 bộ; Đình Bắc 20 card lấy 3 trang đầu của file 5 trang mới nhất); test repository cho override và xóa import; chạy harness với dữ liệu thật và chụp màn hình dialog.
Notes: Cài đặt "Luôn lấy ảnh mới nhất" mặc định bật; tắt thì dùng lại thuật toán file đúng số trang, xoay vòng. File kho đã bị xóa khỏi ổ đĩa được ẩn khỏi dialog và chặn lưu nếu đang được chọn.

### - [x] TASK-073 — Xem trước file in TikTok và giao diện tab Đơn hàng

- **Mục tiêu:** Lưới đơn gọn, chữ to hơn, cột sản phẩm và phân loại riêng, copy mã đơn (nút, nháy đúp, copy tất cả); dialog "Xem trước file in" để sửa file, bỏ đơn khỏi lô trước khi tạo file in; nút xem trước và tạo file in ở góc trên bên phải; cửa sổ mở phóng to.
- **Dependency:** TASK-072.
- **Acceptance criteria:** Xem trước đúng thứ tự file in, gồm trang trắng phân cách; thumbnail chỉ render khi cuộn tới; có test ViewModel và test STA mở cửa sổ.

Completed: 2026-10-17
Files changed: `ViewModels/TikTok/PrintPreviewViewModel.cs`, `PageImageCache.cs` (mới), `Views/PrintPreviewWindow.xaml(.cs)` (mới), `Behaviors/RenderWhenVisibleBehavior.cs` (mới), `Views/TikTokOrdersWindow.xaml`, `Services/TikTokWindowService.cs`, `ViewModels/TikTok/TikTokServices.cs` (`PageRenderer`); test tương ứng.
Verification: 378/378 test passed; kiểm tra giao diện bằng harness chụp màn hình với dữ liệu thật.

### - [x] TASK-076 — Tìm đơn theo mã đơn và tên sản phẩm

- **Mục tiêu:** Ô tìm kiếm ở tab Đơn hàng: tìm theo một phần mã đơn hoặc tên sản phẩm / phân loại, không dấu.
- **Dependency:** TASK-073.
- **Acceptance criteria:** Khi đang tìm, đơn ở mọi nhóm (kể cả nhóm đang tắt) đều được tìm, lọc shop vẫn áp dụng; nút Xóa trả về lọc theo chip; có test ViewModel.

Completed: 2026-10-18
Files changed: `src/PDFPageComposer.App/ViewModels/TikTok/OrdersTabViewModel.cs` (`SearchText`, `ClearSearchCommand`, `MatchesSearch`), `src/PDFPageComposer.App/Views/TikTokOrdersWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/TikTok/OrdersTabViewModelTests.cs`, `docs/tiktok/UI_SPEC.md`, `docs/tiktok/USER_GUIDE.md`.
Verification: 380/380 test passed (gồm test STA mở `TikTokOrdersWindow`); build 0 warning, 0 error.
Notes: Dùng lại `TextNormalizer.MatchesSearch` (bỏ dấu, mọi từ phải khớp) trên chuỗi mã đơn + sản phẩm + phân loại. Ô tìm có độ trễ 200 ms để không lọc lại ở từng phím.
