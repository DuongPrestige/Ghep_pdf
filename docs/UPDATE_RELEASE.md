# Check for update

Co che update dung mot file manifest JSON online va mot goi `.zip` chua publish output.

## 1. Cau hinh may nguoi dung

App doc link manifest tu settings:

```text
%LOCALAPPDATA%\PDFPageComposer\settings.json
```

Them truong `UpdateManifestUrl`:

```json
{
  "Version": 1,
  "UpdateManifestUrl": "https://example.com/pdf-page-composer/latest.json"
}
```

Neu chua cau hinh truong nay, nut **Check update** se bao chua cau hinh.

## 2. Tao ban publish moi

Tang version trong:

```text
src/PDFPageComposer.App/PDFPageComposer.App.csproj
```

Vi du:

```xml
<Version>1.0.1</Version>
```

Publish:

```powershell
dotnet publish .\src\PDFPageComposer.App\PDFPageComposer.App.csproj -c Release -r win-x64 --self-contained true
```

Nen zip toan bo file nam truc tiep trong thu muc publish, khong boc them mot folder cha ben trong zip.

## 3. Tao latest.json

Upload file zip len host cua ban, sau do sua `latest.json`:

```json
{
  "version": "1.0.1",
  "downloadUrl": "https://example.com/pdf-page-composer/PDFPageComposer-1.0.1.zip",
  "sha256": "OPTIONAL_SHA256_HEX",
  "notes": "Sua loi va cai tien nho"
}
```

`sha256` khong bat buoc, nhung nen dung khi phat hanh that. Co the tinh hash bang PowerShell:

```powershell
Get-FileHash .\PDFPageComposer-1.0.1.zip -Algorithm SHA256
```

## 4. Luong nguoi dung bam nut update

1. App tai `latest.json`.
2. Neu version online moi hon version dang chay, app tai file zip.
3. App bung zip vao thu muc tam.
4. App tao script PowerShell, thoat app, copy file moi de len thu muc cai dat, roi mo lai app.

Luu y: neu app duoc dat trong `Program Files`, viec copy file moi co the can quyen administrator. De don gian cho ban ca nhan, nen dat app trong mot thu muc nguoi dung co quyen ghi, vi du `D:\Apps\PDFPageComposer`.
