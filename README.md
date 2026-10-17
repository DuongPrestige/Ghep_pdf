# PDF Page Composer

<img src="docs/assets/logo.png" alt="Logo PDF Page Composer" width="128" />

## Tổng quan

PDF Page Composer là ứng dụng desktop Windows dùng để:

- **Ghép lẻ:** import nhiều file PDF, chọn trang, thêm vào khay đầu ra, nhân bản, kéo thả sắp xếp, xuất một file PDF tổng và mở bằng Foxit PDF Reader để in.
- **Đơn TikTok:** import file CSV đơn hàng từ Seller Center, tự chọn file ảnh mới nhất trong kho PDF theo idol và số card, xem trước và chỉnh file in, tạo file in, xuất danh sách đơn chưa làm.

Ứng dụng không sửa PDF nguồn và không in trực tiếp.

## Trạng thái dự án

Phiên bản hiện tại: **1.3.1** (2026-10-17). Ứng dụng đang được sử dụng thực tế; máy người dùng cập nhật bằng nút **Check update** (xem `docs/UPDATE_RELEASE.md`).

## Tech stack

- C# / .NET 10.
- WPF, MVVM, CommunityToolkit.Mvvm.
- PDFiumCore để render thumbnail.
- PDFsharp để ghép PDF.
- Microsoft.Data.Sqlite cho dữ liệu đơn TikTok (`%LOCALAPPDATA%\PDFPageComposer\tiktok.db`).
- Microsoft.Extensions.DependencyInjection.
- Serilog và System.Text.Json.
- xUnit cho kiểm thử.

## Yêu cầu môi trường

- Windows 10 hoặc Windows 11 x64.
- Visual Studio Code.
- .NET 10 SDK.
- C# Dev Kit.
- Git.
- Foxit PDF Reader.

Kiểm tra môi trường:

```powershell
dotnet --version
git --version
code --version
```

## Cấu trúc thư mục

```text
Ghep_pdf/
├── src/PDFPageComposer.App/       # ứng dụng WPF (TikTok/ chứa module đơn TikTok)
├── tests/PDFPageComposer.Tests/   # xUnit
├── docs/                          # đặc tả, hướng dẫn, quy trình phát hành
│   └── tiktok/                    # PRD và TECH_SPEC của module TikTok
├── latest.json                    # manifest cho Check update
├── PRD.md
├── AGENTS.md
└── README.md
```

## Các lệnh

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project .\src\PDFPageComposer.App
```

Đóng gói và phát hành bản update: xem `docs/UPDATE_RELEASE.md`.

## Tài liệu dự án

- `PRD.md`: nguồn sự thật cao nhất về nghiệp vụ và phạm vi sản phẩm.
- `AGENTS.md`: quy tắc bắt buộc khi code và thực hiện task.
- `docs/USER_GUIDE.md`: hướng dẫn sử dụng (ghép lẻ, đơn TikTok, cập nhật).
- `docs/UI_SPEC.md`: bố cục, hành vi và trạng thái giao diện.
- `docs/TECH_SPEC.md`: stack, kiến trúc, model, service và ràng buộc kỹ thuật.
- `docs/tiktok/PRD_TIKTOK.md`, `docs/tiktok/TECH_SPEC_TIKTOK.md`: nghiệp vụ và kỹ thuật module đơn TikTok.
- `docs/UPDATE_RELEASE.md`: cơ chế Check update, quy trình phát hành, lịch sử phiên bản.
- `docs/TASKS.md`: thứ tự triển khai và trạng thái công việc.
