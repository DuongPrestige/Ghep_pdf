# Đặc tả kỹ thuật — Module TikTok Order Print

Tài liệu đi kèm `docs/tiktok/PRD.md`; giao diện ở `docs/tiktok/UI_SPEC.md`. Các quy tắc trong `AGENTS.md`, `docs/ARCHITECTURE.md` và `docs/composer/TECH_SPEC.md` (render, export PDF) vẫn áp dụng.

> **v0.2 (2026-10-16):** Nguồn đơn là file CSV xuất từ Seller Center. Phần TikTok Open API được tạm dừng; spike nằm trong `spikes/TikTokApiSpike`, đánh giá ở `docs/spikes/tiktok-api-spike.md`.

## 1. Vị trí trong codebase

Không tách project mới. Code đặt trong `PDFPageComposer.App`:

```text
src/PDFPageComposer.App/
├── TikTok/
│   ├── Import/       # IOrderSource, SellerCenterCsvOrderSource, CsvParser, ImportedOrderGrouper, OrderImportMerger, OrderImportService
│   ├── Data/         # SqliteTikTokRepository (partial: Catalog, Orders, Overrides, Printing), TikTokSchemaMigrator
│   ├── Matching/     # SkuConfigSuggester, PdfAssetNameParser, PdfLibraryScanner, PdfAssetMatcher, OrderClassifier, PrintPlanBuilder
│   ├── Export/       # PrintBatchExporter, SeparatorPageRenderer, OrderListExport*, font resolver
│   ├── Models/       # TikTokOrder, PdfAsset, SkuModels, PrintModels, FileOverride, enums
│   ├── TikTokInterfaces.cs, TextNormalizer.cs, OrderNotes.cs, TikTokSettingsLoader.cs
├── ViewModels/TikTok/  # TikTokOrdersViewModel + một ViewModel cho mỗi tab, OrderFilesViewModel, PrintPreviewViewModel,
│                       # TikTokWorkspace (trạng thái dùng chung), PrintFileRunner, PageImageCache — KHÔNG thêm vào MainViewModel
├── Views/              # TikTokOrdersWindow, OrderFilesWindow, PrintPreviewWindow
└── Services/TikTokWindowService.cs
```

- Phần `Import/` và `Matching/` là code thuần (không phụ thuộc WPF hay DB) để dễ unit test.
- `IOrderSource` trả về `IReadOnlyList<ImportedOrderLine>`. Nếu sau này có nguồn đơn khác (API TikTok nếu được cấp quyền) chỉ cần thêm một implementation khác của interface này; `OrderImportService` (lọc trùng) dùng chung.

## 2. Package mới

| Package | Mục đích | License |
|---|---|---|
| `Microsoft.Data.Sqlite` | Database cục bộ | MIT |

- Không dùng EF Core hay thư viện CSV ngoài. Parser CSV tự viết theo RFC 4180, khoảng 60 dòng, có unit test.
- Chưa cần DPAPI, vì không còn secret nào phải lưu.

## 3. Import file xuất đơn Seller Center

### 3.1. Định dạng đã quan sát (file mẫu `Chờ vận chuyển đơn hàng-2026-10-16-08_38.csv`)

- Phân cách bằng dấu phẩy. Ô chứa dấu phẩy được bọc ngoặc kép. Header tiếng Anh, giá trị tiếng Việt.
- **Mỗi dòng là một dòng hàng.** Đơn nhiều món có nhiều dòng cùng `Order ID`. Ví dụ file mẫu: 36 dòng, 33 đơn.
- Các giá trị số và ngày có **tab ở cuối** (`586610736328377928\t`). Ô trống được ghi là `"\t"`. Cần `Trim()` mọi ô.
- Ngày có dạng `dd/MM/yyyy HH:mm:ss`, giờ Việt Nam (UTC+7).
- Cột `Seller SKU` trống ở mọi dòng của file mẫu.
- Cột người nhận, số điện thoại, địa chỉ đã bị TikTok che một phần, nhưng app vẫn **không đọc** các cột này.

### 3.2. Cột sử dụng (ánh xạ theo **tên header**, không theo vị trí)

| Header | Trường | Bắt buộc |
|---|---|---|
| `Order ID` | OrderId | ✓ |
| `Order Status` | StatusText | ✓ |
| `Order Substatus` | SubstatusText | |
| `SKU ID` | SkuId | ✓ |
| `Product Name` | ProductName | ✓ |
| `Variation` | VariationName | ✓ |
| `Quantity` | Quantity | ✓ |
| `Created Time` | CreatedAt | ✓ |
| `Buyer Username` | BuyerUsername (đã bị che) | |
| `Buyer Message` | BuyerMessage | |
| `Seller Note` | SellerNote | |
| `Package ID` | PackageId | |

- Thiếu cột bắt buộc → báo lỗi định dạng file, không import gì.
- Encoding: đọc UTF-8 (có hoặc không BOM). Nếu gặp ký tự lỗi thì báo cho người dùng.
- Trạng thái được nhận: `Cần vận chuyển`, `Chờ vận chuyển`, `AWAITING_SHIPMENT`. Danh sách này đặt trong settings để thêm được.
- Sau này hỗ trợ thêm `.xlsx` nếu cần (chưa làm).

### 3.3. `OrderImportService` — chống trùng (TT-09)

```text
Input : shopId, mode (Full | Partial), rows từ IOrderSource
1. Chuẩn hóa: Trim mọi ô; bỏ dòng có trạng thái không được nhận (đếm Skipped)
2. Gom theo OrderId; trong một đơn, gộp trùng theo SkuId (giữ dòng đầu, ghi cảnh báo DuplicateLineInFile)
3. Trong một transaction SQLite:
   với mỗi đơn:
     chưa có  → INSERT Order + OrderLine (Inserted)
     đã có    → UPDATE Status, SellerNote, LastSeenImportId; đồng bộ OrderLine (Updated)
                nếu đã in và (SkuId, Quantity) khác snapshot lúc in → ChangedAfterPrint = 1
   mọi SKU chưa có trong bảng Sku → INSERT với ConfigStatus = Unconfigured + gợi ý (SkuConfigSuggester)
   nếu mode = Full: đơn cùng shop, đang pending, không có trong file → IsActive = 0
4. Ghi ImportBatch + trả về ImportReport { Inserted, Updated, AlreadyPrinted, Skipped, Deactivated, NewSkus, Warnings }
```

- Import là thao tác **idempotent**: chạy lại cùng file thì kết quả không đổi.
- Không ghi dữ liệu người mua vào log; log chỉ ghi số lượng.

## 4. Database (SQLite)

Đường dẫn: `%LOCALAPPDATA%\PDFPageComposer\tiktok.db`. Schema có version, migration chạy khi khởi động.

```sql
Shop(ShopId INTEGER PK, Name TEXT UNIQUE)

Sku(SkuId TEXT PK, ProductName, VariationName,
    ProcessType TEXT /* Auto | Manual | Ignore */, ConfigStatus TEXT /* Unconfigured | Confirmed */,
    Idol NULL, CardCount NULL, FirstSeenAt, UpdatedAt)
IdolAlias(Alias TEXT PK /* đã chuẩn hóa */, Idol)

PdfAsset(FilePath TEXT PK, Idol, PageCount, FileDate, ActualPageCount, LastWriteTime, IsValid, Error)

ImportBatch(ImportId INTEGER PK, ShopId, FileName, Mode, ImportedAt,
            Inserted, Updated, AlreadyPrinted, Skipped, Deactivated)

"Order"(OrderId TEXT PK, ShopId, StatusText, CreatedAt, BuyerUsername, BuyerMessage, SellerNote,
        IsActive INTEGER, FirstImportId, LastSeenImportId,
        PrintedBatchId NULL, PrintedAt NULL, PrintedSource NULL /* Batch | SellerNote */,
        ChangedAfterPrint INTEGER DEFAULT 0,
        NoteReviewedHash NULL /* SHA-256 của BuyerMessage + '\n' + SellerNote lúc bấm "Đã xem – cho phép ghép" */,
        SourceRow INTEGER DEFAULT 0 /* số dòng đầu tiên của đơn trong file CSV, dùng để hiển thị đúng thứ tự file */)
OrderLine(OrderId, SkuId, LineNo, ProductName, VariationName, Quantity, PRIMARY KEY(OrderId, SkuId))

/* File người dùng tự chọn cho một bộ card (dialog "Xem và tự chọn file") */
OrderFileOverride(OrderId, SkuId, CopyIndex, Seq, PdfAssetPath, PagesUsed, FirstPage DEFAULT 1,
                  FilePageCount NULL /* chỉ có với PDF ngoài kho */,
                  PRIMARY KEY(OrderId, SkuId, CopyIndex, Seq))

PrintBatch(BatchId TEXT PK, CreatedAt, OutputPath, SeparatorMode, OrderCount, PageCount, Status)
PrintBatchItem(BatchId, Seq, OrderId, SkuId, CopyIndex, Quantity, PdfAssetPath, Warning NULL,
               PagesUsed NULL, FirstPage NULL,
               PRIMARY KEY(BatchId, Seq))
```

Migration hiện tại: **v6** (v3 `Order.SourceRow`; v4 bảng `OrderFileOverride`; v5 `FirstPage`; v6 `OrderFileOverride.FilePageCount`).

- **Không có** cột người nhận, số điện thoại hay địa chỉ (AC-T07).
- `PrintBatchItem.Quantity` là snapshot số lượng tại thời điểm in, dùng để phát hiện `ChangedAfterPrint`.
- Bỏ đánh dấu một đơn: đặt `PrintedBatchId = NULL`; các dòng lịch sử vẫn giữ.

## 5. Logic khớp đơn (code thuần, có unit test)

### 5.1. `PdfLibraryScanner`

- Regex tên file: `^(?<idol>.+?)\s*-\s*(?<pages>\d+)\s*PAGES?\s*-\s*(?<d>\d{1,2})\.(?<m>\d{1,2})(?:\s*\(\d+\))?\.pdf$`, không phân biệt hoa thường (cho phép hậu tố ` (2)` khi trùng tên).
- Suy ra năm: nếu ngày lớn hơn hôm nay thì lấy năm trước.
- Số trang thật đọc qua `IPdfMetadataService` có sẵn. Cache theo `LastWriteTime`.

### 5.2. `SkuConfigSuggester`

Mọi phép so khớp chạy trên chuỗi đã qua `TextNormalizer.Normalize`: viết hoa, bỏ dấu (Đ → D), gộp khoảng trắng.

```text
P = Normalize(ProductName), V = Normalize(VariationName)
CardCount : regex \b(\d+)\s*CARD\b trên V              ("20 CARD, 1 Mặt" → 20)
Sides     : regex \b(\d+)\s*MAT\b trên V               ("2 MẶT (MẶT ẢNH + CHỮ KÝ)" → 2)
ProcessType (xét theo thứ tự):
  P không chứa "BO GOC"                   → Ignore   (móc khóa, nhãn vở, PTB, polaroid…)
  CardCount = null                        → Manual   ("1 mặt, A4 (decal bóc dán)")
  Sides = 2 hoặc V chứa "THEO YEU CAU"    → Manual
  Sides = 1                               → Auto
  còn lại                                 → Manual
Idol (tính cho mọi loại khác Ignore, để người dùng sửa nhanh):
  1. Tách V theo dấu phẩy, lấy đoạn cuối. Nếu đoạn đó không rỗng và không chứa nguyên từ CARD, MAT, IN hay cụm THEO YEU CAU → ứng viên.
  2. Nếu không có: tách ProductName theo '[', ']' và ' - ', lấy đoạn đầu tiên có "BO GOC",
     bỏ các từ chung: CARD, BO, GOC, COMBO, THE, ANH, MOI, TANG, CHU, KY → ứng viên.
  3. Có ứng viên: tra alias theo TextNormalizer.IdolKey(ứng viên); có alias thì dùng Idol của alias,
     không thì dùng Normalize(ứng viên). Không có ứng viên → null.
```

Idol được so khớp giữa SKU và file kho bằng `TextNormalizer.IdolKey` (Normalize, rồi bỏ mọi ký tự không phải chữ hoặc số). Ví dụ `SƠN.K` → `SONK`, `Công B` → `CONGB`.

Test bắt buộc (kỳ vọng):

| Product (rút gọn) | Variation | Kết quả |
|---|---|---|
| `[CARD BO GÓC CODY NAM VÕ] - …` | `30 CARD, 1 Mặt` | Auto, 30, idol `CODY NAM VO` |
| `[CARD BO GÓC Vỹ Vỹ] - …` | `100 CARD, 1 Mặt` | Auto, 100, idol `VY VY` |
| `[CARD CAPTAIN BOY BO GÓC] - …` | `20 CARD, 1 Mặt` | Auto, 20, idol `CAPTAIN BOY` |
| `[CARD BO GÓC IVAN] - …` | `20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)` | Manual |
| `[CARD BO GÓC TINH HÀ SAY HI] - …` | `20 CARD, 1 MẶT, IN THEO YÊU CẦU (GỬI ẢNH CHO SHOP)` | Manual |
| `CARD BO GÓC A4 A5 KHỔNG LỒ - …` | `1 mặt, A4 (decal bóc dán)` | Manual |
| `[MÓC KHÓA ALBUM MINI] - …` | `Tất Cả Các Khung, IVan` | Ignore |
| `[Ảnh Polar IG] - …` | `18 CARD, QUANG HÙNG MASTER-D, khung ngẫu nhiên (tổng hợp)` | Ignore |
| `[PTB Tinh Hà Say Hi 2026] - …` | `10 PHOTOBOOTH, Quang Hùng MasterD, Dọc 1x4 ảnh` | Ignore |
| `[CARD BO GÓC TINH HÀ SAY HI] - …` | `50 CARD, 1 MẶT, COMBO 24 ANH TRAI` + alias `COMBO 24 ANH TRAI`→`24AT` | Auto, 50, idol `24AT` |
| `[CARD BO GÓC TINH HÀ SAY HI] - …` | `20 CARD, 1 MẶT, Jaysonlei` | Auto, 20, idol `JAYSONLEI` |

### 5.3. `OrderClassifier` (TT-08) và `PrintPlanBuilder` (TT-04 → TT-07)

Từ v1.3.0, mặc định bật `AlwaysUseNewestFiles` (cài đặt "Luôn lấy ảnh mới nhất…"):

```text
orders = active, chưa in (hoặc In lại), sort theo TikTokOrderOrdering.InFileOrder
         (LastSeenImportId, SourceRow, CreatedAt, OrderId) = đúng thứ tự file CSV
for Auto line: pages = ceil(CardCount/9)
    files = PdfAssetMatcher.FindIdolFiles(idol) // mọi file hợp lệ của idol, mới nhất trước
    files rỗng → MissingFile
    mỗi copy: bắt đầu lại từ file mới nhất của đơn; copy sau trong cùng đơn lùi tiếp sang file cũ hơn
        remaining = pages
        while remaining > 0: used = min(remaining, file.PageCount); lấy trang 1..used; remaining -= used
    // Ví dụ: Đình Bắc 20 card (3 tờ), file mới nhất 5 trang → 3 trang đầu của file 5 trang
FileOverrides (OrderFileOverride) của một copy thay hoàn toàn kết quả tự động:
    mỗi đoạn trang FirstPage..FirstPage+PagesUsed-1, IsManualChoice = true;
    đường dẫn ngoài kho + FilePageCount → PrintPlanBuilder.ExternalAsset; thiếu FilePageCount → chặn đơn
```

Khi tắt cài đặt trên, dùng thuật toán cũ (file đúng số trang, xoay vòng):

```text
orders = active, chưa in (hoặc người dùng chọn In lại), sort CreatedAt ASC, OrderId ASC
cursor = Dictionary<(idol,pages), int>       // reset mỗi lô
for order:
    lines = order.Lines where Sku.ProcessType != Ignore
    Printed (PrintedBatchId hoặc SellerNote chứa "DA IN" sau chuẩn hóa) → Printed
    lines rỗng                               → NoCardNeeded
    note = Trim(BuyerMessage) + Trim(SellerNote) không rỗng
        và NoteReviewedHash != hash(note hiện tại)  → HasNote   (TT-10)
    có Sku Unconfigured                       → NeedsConfig
    có Manual                                 → Manual
    for Auto line: pages = ceil(CardCount/9); assets(idol,pages) DESC by FileDate
        assets rỗng                           → MissingFile
    nếu Ready: tính trên bản sao cursor; commit cursor khi cả đơn hợp lệ
        mỗi copy 1..Quantity: asset = assets[c++ % assets.Count]; warning nếu Quantity > assets.Count
```

## 6. Xuất PDF

- Tái sử dụng `IPdfExportService`: mỗi file kho là một `SourcePdfFile`, mỗi đơn là một `OutputGroup`.
- Trang phân cách: `SeparatorPageRenderer` tạo một trang A4 trắng (từ 2026-10-17 không in chữ để tiết kiệm mực).
- `PrintBatchExporter` chỉ lấy các trang `FirstPage..LastPage` của mỗi copy và ghi `PagesUsed`, `FirstPage` vào `PrintBatchItem`.
- Tên file gợi ý: `TIKTOK_<yyyyMMdd_HHmm>_<số đơn>don.pdf`.
- Chỉ sau khi `ExportAsync` thành công mới ghi `PrintBatch` / `PrintBatchItem` và cập nhật `Order` trong một transaction, rồi mở Foxit.

## 7. UI

Xem `docs/tiktok/UI_SPEC.md`. Đăng ký DI: `ServiceRegistration.AddTikTokModule` (repository, scanner, classifier, plan builder, exporter là singleton; `TikTokServices`, `TikTokOrdersViewModel`, `TikTokOrdersWindow` là transient). `TikTokServices.PageRenderer` dùng chung `IPdfRenderService` của màn hình Ghép lẻ.

## 8. Kiểm thử

- Đưa file mẫu đã **xóa hẳn các cột người nhận, số điện thoại, địa chỉ, username** vào `tests/PDFPageComposer.Tests/TestData/tiktok/orders-sample.csv`.
- Unit test: CSV parser (dấu phẩy và ngoặc kép, tab thừa, BOM), mapping header, `OrderImportService` (import 2 lần idempotent, chế độ Full/Partial, `ChangedAfterPrint`, Seller Note "ĐÃ IN"), `OrderClassifier` (đơn có Buyer Message hoặc Seller Note → HasNote; "Đã xem" hợp lệ; ghi chú đổi thì hủy "Đã xem"), `SkuConfigSuggester` (các mẫu ở 5.2), `PdfLibraryScanner`, `PrintPlanBuilder` (ví dụ mục 7 của PRD).
- Integration test: repository SQLite trên DB tạm; không đánh dấu đơn khi export lỗi.
