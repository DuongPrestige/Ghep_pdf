# Phát triển: build, chạy, test

## Môi trường

- Windows 10/11 x64, .NET 10 SDK, Git.
- VS Code + C# Dev Kit (hoặc Visual Studio).
- Foxit PDF Reader để kiểm tra file xuất (không bắt buộc).

Nếu terminal không thấy `dotnet` sau khi cài SDK, nạp lại PATH trong PowerShell:

```powershell
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
```

## Build và chạy

Chạy từ thư mục gốc repo:

```powershell
dotnet build .\PDFPageComposer.slnx
dotnet run --project .\src\PDFPageComposer.App\PDFPageComposer.App.csproj
```

Hoặc chạy file đã build: `src\PDFPageComposer.App\bin\Debug\net10.0-windows\PDFPageComposer.App.exe`.

Trước khi build lại, đóng app đang chạy; nếu không, file exe bị khóa và build báo lỗi copy.

## Test

```powershell
dotnet test .\tests\PDFPageComposer.Tests\PDFPageComposer.Tests.csproj
```

- 381 test. Có test STA mở thật `TikTokOrdersWindow`, `PrintPreviewWindow` và `OrderFilesWindow` để bắt lỗi binding; `MainWindow` chưa có test này, nên kiểm tra bằng cách mở app.
- Khi app đang chạy làm khóa thư mục `bin`, dùng thư mục build riêng: thêm `--artifacts-path "$env:TEMP\gpdf-verify"`.
- Dữ liệu mẫu TikTok (đã xóa thông tin người nhận): `tests/PDFPageComposer.Tests/TestData/tiktok/orders-sample.csv`.

## Công cụ đi kèm

- `tools/PdfLibraryNormalizer`: chuẩn hóa tên file kho PDF card về dạng `<IDOL> - <N> PAGE - <dd.MM>.pdf`. Ba bước `scan` (chỉ đọc), `plan` (tạo `plan.csv` để duyệt), `apply` (copy theo kế hoạch, không sửa file gốc). Chạy `dotnet run --project .\tools\PdfLibraryNormalizer` để xem hướng dẫn.
- `spikes/TikTokApiSpike`: thử TikTok Open API; đang dừng vì shop không được cấp quyền.

## Dữ liệu khi debug

- Cài đặt: `%LOCALAPPDATA%\PDFPageComposer\settings.json`.
- Đơn TikTok: `%LOCALAPPDATA%\PDFPageComposer\tiktok.db` (mở bằng DB Browser for SQLite). Muốn thử trên dữ liệu thật mà không ảnh hưởng, copy file này ra chỗ khác rồi trỏ test harness vào bản copy.
- Log: thư mục `logs` trong thư mục làm việc khi chạy app.

## Quy ước

Quy tắc code và quy trình làm task: `AGENTS.md`. Đóng gói và phát hành: `docs/update/RELEASE.md`.
