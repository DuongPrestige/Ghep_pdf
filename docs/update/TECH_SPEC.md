# Đặc tả kỹ thuật — Module Cập nhật (Check update)

Quy trình phát hành bản mới: `docs/update/RELEASE.md`. Lịch sử phiên bản: `docs/CHANGELOG.md`.

## Thành phần

| Thành phần | Vai trò |
|---|---|
| `Interfaces/IUpdateService.cs`, `Services/UpdateService.cs` | kiểm tra, tải, kiểm tra hash, bung gói, tạo script cài |
| `Models/AppUpdateManifest.cs` | `Version`, `DownloadUrl`, `Sha256` (tùy chọn), `Notes` |
| `Models/AppUpdateResult.cs` | `NotConfigured`, `UpToDate`, `UpdateStarted`, `Failed` + thông báo |
| `Models/AppSettings.UpdateManifestUrl` | mặc định `https://raw.githubusercontent.com/DuongPrestige/Ghep_pdf/main/latest.json` (từ v1.1.0) |
| `MainViewModel.CheckForUpdateCommand` | nút **Check update**; ghi kết quả lên status bar, đóng app khi cài đặt bắt đầu |
| `latest.json` ở gốc repo, nhánh `main` | manifest đang phát hành |

Phiên bản ứng dụng lấy từ `<Version>` trong `PDFPageComposer.App.csproj` (đọc qua `Assembly.GetName().Version`).

## Luồng `CheckAndInstallUpdateAsync`

1. Đọc `UpdateManifestUrl` từ settings; trống thì trả `NotConfigured`.
2. Tải và parse `latest.json`; thiếu `version` hoặc `downloadUrl` là lỗi. Version cho phép tiền tố `v`.
3. So sánh với version đang chạy; không lớn hơn thì trả `UpToDate`.
4. Tải zip vào `%TEMP%\PDFPageComposer\updates\<guid>\update.zip`.
5. Nếu manifest có `sha256`, so khớp SHA-256 (không phân biệt hoa thường); lệch thì dừng với lỗi.
6. Bung zip vào thư mục `staging` cạnh file zip.
7. Tạo script PowerShell `install-<guid>.ps1`: chờ process hiện tại thoát → `Copy-Item staging\* → thư mục cài đặt -Recurse -Force` → mở lại exe → xóa staging và chính script.
8. Chạy `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script>`, trả `UpdateStarted`; ViewModel đợi 500 ms rồi đóng app.

Lỗi mạng, IO, JSON, hash đều trả `Failed` kèm thông báo; không có thay đổi nào trên thư mục cài đặt.

## Ràng buộc với gói phát hành

- Zip chứa output của `dotnet publish -r win-x64 --self-contained true` **nằm trực tiếp ở gốc zip** (không bọc thư mục cha), gồm `PDFPageComposer.App.exe`, `pdfium.dll`, `PDFiumCore.dll`.
- Script chỉ chép đè, không xóa file cũ không còn dùng. Nếu một bản mới đổi tên hoặc bỏ file, file cũ vẫn nằm lại trong thư mục cài đặt (vô hại nhưng cần biết khi debug).
- Thư mục cài đặt phải có quyền ghi của người dùng; đặt trong `Program Files` sẽ cần quyền administrator.
- Dữ liệu ở `%LOCALAPPDATA%\PDFPageComposer` (settings, `tiktok.db`, recovery) không bị đụng tới. Migration SQLite chạy khi app mở lần đầu sau cập nhật.

## Test

`tests/PDFPageComposer.Tests/Services/UpdateServiceTests.cs`: chưa cấu hình, đã mới nhất, có bản mới (bung gói, tạo và chạy script cài). Chưa có test cho trường hợp hash sai.

## Hạn chế đã biết

- `raw.githubusercontent.com` cache khoảng 5 phút, nên máy người dùng có thể chưa thấy bản mới ngay sau khi phát hành.
- Thông báo trên status bar của luồng update hiện là tiếng Việt không dấu.
- Chưa có cơ chế rollback: muốn quay lại bản cũ thì tải zip của release cũ và giải nén đè thủ công.
