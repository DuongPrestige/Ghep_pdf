# Kiến trúc chung

Tài liệu mô tả phần dùng chung cho mọi module. Chi tiết từng module nằm trong `docs/composer/`, `docs/tiktok/`, `docs/update/`.

## Tech stack (đã chốt)

| Thành phần | Lựa chọn | Ghi chú |
|---|---|---|
| Nền tảng | C#, .NET 10, Windows 10/11 x64 | `net10.0-windows`, nullable bật |
| UI | WPF, MVVM với `CommunityToolkit.Mvvm` 8.4 | `[ObservableProperty]`, `[RelayCommand]` |
| DI | `Microsoft.Extensions.DependencyInjection` | composition root ở `ServiceRegistration.cs` |
| Render PDF | `PDFiumCore` (Apache-2.0) | xem `docs/spikes/pdf-library-spike.md` |
| Ghép PDF | `PDFsharp` 6.2 (MIT) | copy page object, không rasterize |
| Dữ liệu đơn TikTok | `Microsoft.Data.Sqlite` (MIT) | không dùng EF Core |
| Settings, project | `System.Text.Json` | |
| Log | `Serilog` + `Serilog.Sinks.File` | rolling theo ngày, giữ 14 file |
| Test | xUnit | 380 test |

Không dùng iText hoặc thư viện AGPL. Trước khi thêm package mới phải kiểm tra license, tình trạng duy trì và tương thích .NET 10.

## Cấu trúc solution

Chỉ có một project ứng dụng và một project test; không tách Domain/Application/Infrastructure khi chưa có nhu cầu thật.

```text
Ghep_pdf/
├── src/PDFPageComposer.App/
│   ├── App.xaml(.cs)            # khởi động: Serilog, DI, kiểm tra phục hồi, mở MainWindow
│   ├── ServiceRegistration.cs   # AddPdfPageComposer() + AddTikTokModule(databasePath)
│   ├── MainWindow.xaml(.cs)     # màn hình Ghép lẻ
│   ├── Assets/app.ico           # logo
│   ├── Models/                  # model Ghép lẻ, AppSettings, TikTokSettings, manifest update
│   ├── Interfaces/ Services/    # service dùng chung + service Ghép lẻ + UpdateService
│   ├── ViewModels/              # MainViewModel (Ghép lẻ)
│   ├── ViewModels/TikTok/       # ViewModel module TikTok
│   ├── Views/                   # cửa sổ module TikTok
│   ├── TikTok/                  # logic module TikTok (Import, Matching, Data, Export, Models)
│   ├── Behaviors/ Converters/   # hành vi và converter WPF dùng chung
├── tests/PDFPageComposer.Tests/ # Models, Services, ViewModels, Behaviors, TikTok/**
├── tools/PdfLibraryNormalizer/  # công cụ console chuẩn hóa tên kho PDF card (scan / plan / apply)
├── spikes/TikTokApiSpike/       # spike TikTok Open API (đang dừng)
├── docs/                        # tài liệu, xem docs/README.md
└── latest.json                  # manifest cho nút Check update
```

## Ranh giới module

| Module | Code | Phụ thuộc |
|---|---|---|
| Ghép lẻ (composer) | `MainWindow`, `MainViewModel`, `Models/`, các service PDF, project, auto-save, Foxit | dùng chung |
| Đơn TikTok | `TikTok/**`, `ViewModels/TikTok/**`, `Views/**`, `TikTokWindowService` | dùng `IPdfRenderService`, `IPdfMetadataService`, `IPdfExportService`, `IFoxitLauncherService`, `IFileDialogService`, `IClipboardService`, `IAppSettingsService` |
| Cập nhật | `IUpdateService`, `UpdateService`, `AppUpdateManifest`, nút **Check update** trong `MainViewModel` | `IAppSettingsService`, `IProcessLauncher` |

Quy tắc:

- Module TikTok **không** thêm state hoặc command vào `MainViewModel`; màn hình chính chỉ có nút mở cửa sổ qua `ITikTokWindowService`.
- Module Ghép lẻ không biết gì về TikTok. Hàm tiện ích dùng chung (ví dụ `TextNormalizer.MatchesSearch` cho tìm không dấu) được gọi một chiều.
- `TikTok/Import` và `TikTok/Matching` là code thuần, không phụ thuộc WPF hay SQLite.

## Service dùng chung

| Interface | Trách nhiệm |
|---|---|
| `IPdfMetadataService` | validate, số trang, kích thước, mã hóa, fingerprint |
| `IPdfRenderService` | render thumbnail / preview qua PDFium; một `SemaphoreSlim` tĩnh tuần tự hóa lời gọi PDFium |
| `IThumbnailCacheService`, `IThumbnailRenderQueue` | cache LRU theo dung lượng, hàng đợi render có giới hạn và hủy được |
| `IPdfExportService` | ghép trang vào file đích qua file tạm |
| `IFoxitLauncherService`, `IProcessLauncher` | mở file bằng Foxit hoặc viewer mặc định; không tự in |
| `IFileDialogService`, `IClipboardService` | cô lập dialog và clipboard khỏi ViewModel |
| `IAppSettingsService` | đọc / ghi `%LOCALAPPDATA%\PDFPageComposer\settings.json` |
| `IProjectPersistenceService`, `IAutoSaveService` | project và phục hồi phiên của Ghép lẻ |
| `IUpdateService` | Check update |

Mọi service đăng ký singleton. Cửa sổ và ViewModel TikTok là transient (mỗi lần mở là một phiên mới).

## MVVM

- ViewModel kế thừa `ObservableObject`, dùng source generator. Command dài dùng `AsyncRelayCommand`; command render thumbnail theo item đặt `AllowConcurrentExecutions = true`.
- ViewModel không gọi trực tiếp filesystem, `Process.Start` hoặc API PDF; đi qua interface.
- Code-behind chỉ xử lý focus, phím tắt, kéo thả và vòng đời cửa sổ.
- Thumbnail lazy: `LoadedCommandBehavior` (render khi phần tử được tạo) và `RenderWhenVisibleBehavior` (render một lần khi phần tử nằm trong khoảng một màn hình quanh vùng đang cuộn).

## Dữ liệu cục bộ

| Dữ liệu | Vị trí | Định dạng |
|---|---|---|
| Cài đặt (Foxit, cache, link update, cài đặt TikTok) | `%LOCALAPPDATA%\PDFPageComposer\settings.json` | JSON có `Version` |
| Dữ liệu đơn TikTok | `%LOCALAPPDATA%\PDFPageComposer\tiktok.db` | SQLite, migration có version (v6) |
| Project / phục hồi Ghép lẻ | tự lưu phục hồi ở `%LOCALAPPDATA%\PDFPageComposer\recovery.ppc.json` | JSON có `Version` |
| Log | `logs/pdf-page-composer-YYYYMMDD.log` trong thư mục làm việc | text |

Cập nhật phần mềm chỉ thay file trong thư mục cài đặt, không đụng tới dữ liệu ở `%LOCALAPPDATA%`.

## Log và riêng tư

- Ứng dụng chạy offline, trừ nút Check update (tải `latest.json` và file zip từ GitHub).
- Không log nội dung PDF, mật khẩu, hoặc dữ liệu người nhận của đơn TikTok; module TikTok không lưu tên người nhận, số điện thoại, địa chỉ.

## Build, test, phát hành

Xem `docs/DEVELOPMENT.md` (build, chạy, test) và `docs/update/RELEASE.md` (đóng gói, phát hành).
