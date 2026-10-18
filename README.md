# PDF Page Composer

<img src="docs/assets/logo.png" alt="Logo PDF Page Composer" width="128" />

## Tổng quan

PDF Page Composer là ứng dụng desktop Windows dùng để:

- **Ghép lẻ:** import nhiều file PDF, chọn trang, thêm vào khay đầu ra, nhân bản, kéo thả sắp xếp, xuất một file PDF tổng và mở bằng Foxit PDF Reader để in.
- **Đơn TikTok:** import file CSV đơn hàng từ Seller Center, tự chọn file ảnh mới nhất trong kho PDF theo idol và số card, xem trước và chỉnh file in, tạo file in, xuất danh sách đơn chưa làm.

Ứng dụng không sửa PDF nguồn và không in trực tiếp.

## Trạng thái dự án

Phiên bản hiện tại: **1.3.3** (2026-10-18). Ứng dụng đang được sử dụng thực tế; máy người dùng cập nhật bằng nút **Check update** (xem [docs/CHANGELOG.md](docs/CHANGELOG.md)).

## Cài đặt cho người dùng

1. Tải file `PDFPageComposer-vX.Y.Z.zip` ở [Releases](https://github.com/DuongPrestige/Ghep_pdf/releases/latest).
2. Giải nén vào một thư mục có quyền ghi, ví dụ `D:\Apps\PDFPageComposer` (không đặt trong `Program Files`).
3. Chạy `PDFPageComposer.App.exe`. Các lần sau bấm **Check update** để lên bản mới.

Hướng dẫn sử dụng: [docs/USER_GUIDE.md](docs/USER_GUIDE.md).

## Dành cho dev

- Stack: C# / .NET 10, WPF + MVVM (CommunityToolkit.Mvvm), PDFiumCore, PDFsharp, Microsoft.Data.Sqlite, Serilog, xUnit.
- Yêu cầu: Windows 10/11 x64, .NET 10 SDK, Git; VS Code + C# Dev Kit hoặc Visual Studio.

```powershell
dotnet build .\PDFPageComposer.slnx
dotnet test .\tests\PDFPageComposer.Tests\PDFPageComposer.Tests.csproj
dotnet run --project .\src\PDFPageComposer.App\PDFPageComposer.App.csproj
```

```text
Ghep_pdf/
├── src/PDFPageComposer.App/       # ứng dụng WPF (TikTok/ chứa module Đơn TikTok)
├── tests/PDFPageComposer.Tests/   # xUnit
├── tools/PdfLibraryNormalizer/    # công cụ chuẩn hóa tên kho PDF card
├── docs/                          # tài liệu theo module, xem docs/README.md
├── latest.json                    # manifest cho Check update
├── AGENTS.md                      # quy tắc bắt buộc khi code
└── README.md
```

## Tài liệu

Mục lục và quy tắc bảo trì: [docs/README.md](docs/README.md).

| Module | Tài liệu |
|---|---|
| Ghép lẻ PDF | [docs/composer/](docs/composer/) — PRD, UI_SPEC, TECH_SPEC, USER_GUIDE, TASKS |
| Đơn TikTok | [docs/tiktok/](docs/tiktok/) — PRD, UI_SPEC, TECH_SPEC, USER_GUIDE, TASKS |
| Cập nhật và phát hành | [docs/update/](docs/update/) — TECH_SPEC, RELEASE, TASKS |
| Dùng chung | [ARCHITECTURE](docs/ARCHITECTURE.md), [DEVELOPMENT](docs/DEVELOPMENT.md), [CHANGELOG](docs/CHANGELOG.md) |
