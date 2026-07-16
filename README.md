# PDF Page Composer

## Tổng quan

PDF Page Composer là ứng dụng desktop Windows dùng để:

- Import nhiều file PDF và hiển thị mọi trang dưới dạng thumbnail.
- Click chọn trang rồi thêm vào Output Tray.
- Nhân bản trang hoặc group trang.
- Sắp xếp chính xác thứ tự đầu ra.
- Xuất một file PDF tổng và mở bằng Foxit PDF Reader để in.

Ứng dụng không sửa PDF nguồn và không in trực tiếp.

## Trạng thái dự án

Dự án đang ở giai đoạn phát triển ban đầu. PRD và đặc tả đã được chuẩn bị; source code ứng dụng chưa được khởi tạo.

## Tech stack

- C# / .NET 10.
- WPF, MVVM, CommunityToolkit.Mvvm.
- PDFium hoặc wrapper phù hợp để render.
- PDFsharp hoặc thư viện có giấy phép phù hợp để ghép PDF.
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

## Cấu trúc thư mục dự kiến

```text
PDFPageComposer/
├── src/
│   └── PDFPageComposer.App/
├── tests/
│   └── PDFPageComposer.Tests/
├── docs/
├── PRD.md
├── AGENTS.md
└── README.md
```

Không áp dụng Clean Architecture nhiều project ngay từ đầu. Chỉ tách project khi phạm vi và dependency thực tế yêu cầu.

## Các lệnh dự kiến

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project .\src\PDFPageComposer.App
```

Lệnh đóng gói dự kiến, **chỉ chạy khi đã đến giai đoạn publish** và sau khi kiểm tra native PDF library tương thích:

```powershell
dotnet publish .\src\PDFPageComposer.App -c Release -r win-x64 --self-contained true
```

## Tài liệu dự án

- `PRD.md`: nguồn sự thật cao nhất về nghiệp vụ và phạm vi sản phẩm.
- `AGENTS.md`: quy tắc bắt buộc khi Codex code và thực hiện task.
- `docs/UI_SPEC.md`: bố cục, hành vi và trạng thái giao diện.
- `docs/TECH_SPEC.md`: stack, kiến trúc, model, service và ràng buộc kỹ thuật.
- `docs/TASKS.md`: thứ tự triển khai và trạng thái công việc.
