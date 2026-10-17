# Check update va phat hanh ban moi

Co che update dung file manifest `latest.json` o nhanh `main` cua repo va goi `.zip` dat tren GitHub Releases.

Ban hien tai: **v1.3.1** (https://github.com/DuongPrestige/Ghep_pdf/releases/tag/v1.3.1).

## 1. May nguoi dung

Tu v1.1.0, app mac dinh doc manifest tai:

```text
https://raw.githubusercontent.com/DuongPrestige/Ghep_pdf/main/latest.json
```

Khong can cau hinh gi them. Neu muon doi nguon, sua truong `UpdateManifestUrl` trong `%LOCALAPPDATA%\PDFPageComposer\settings.json`.

Luong khi bam **Check update**:

1. App tai `latest.json`.
2. Neu version online moi hon version dang chay, app tai file zip va kiem tra SHA256.
3. App bung zip vao thu muc tam.
4. App tao script PowerShell, thoat app, copy file moi de len thu muc cai dat, roi mo lai app.

Luu y:

- GitHub cache `raw.githubusercontent.com` khoang 5 phut, nen ngay sau khi phat hanh may nguoi dung co the van thay ban cu.
- Neu app dat trong `Program Files`, viec copy file moi can quyen administrator. Nen dat app o thu muc nguoi dung co quyen ghi, vi du `D:\Apps\PDFPageComposer`.

## 2. Quy trinh phat hanh

Lam dung thu tu de may nguoi dung khong bao gio doc manifest tro toi file chua ton tai.

1. Tang `<Version>` trong `src/PDFPageComposer.App/PDFPageComposer.App.csproj`, commit.
2. Chay test:

   ```powershell
   dotnet test .\tests\PDFPageComposer.Tests\PDFPageComposer.Tests.csproj
   ```

3. Publish va zip (file nam truc tiep o goc zip, khong boc folder cha):

   ```powershell
   dotnet publish .\src\PDFPageComposer.App\PDFPageComposer.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish-v1.3.1
   Compress-Archive -Path .\artifacts\publish-v1.3.1\* -DestinationPath .\artifacts\PDFPageComposer-v1.3.1.zip
   Get-FileHash .\artifacts\PDFPageComposer-v1.3.1.zip -Algorithm SHA256
   ```

4. Chay thu `artifacts\publish-vX\PDFPageComposer.App.exe`.
5. Push `main` va tag `vX` (`git tag v1.3.1; git push origin main v1.3.1`).
6. Tao GitHub Release cho tag, upload file zip. May nay chua cai `gh`, nen dung GitHub REST API voi token lay tu `git credential fill`.
7. Tai lai asset tu GitHub va so SHA256 voi ban build.
8. Chi sau buoc 7 moi sua `latest.json`, commit "Publish update vX" va push `main`:

   ```json
   {
     "version": "1.3.1",
     "downloadUrl": "https://github.com/DuongPrestige/Ghep_pdf/releases/download/v1.3.1/PDFPageComposer-v1.3.1.zip",
     "sha256": "067A4C34A65ED50B8332A1C0B35AFAF2DEDEA26EAF883C6AB04D500051D16CA3",
     "notes": "Them logo cho phan mem; ..."
   }
   ```

9. Mo link raw cua `latest.json` de kiem tra version da doi.

## 3. Lich su phat hanh

| Version | Ngay | Noi dung |
|---|---|---|
| 1.3.1 | 2026-10-17 | Logo moi cho exe, thanh taskbar va cac cua so. |
| 1.3.0 | 2026-10-17 | Module don TikTok (import CSV, cau hinh SKU, kho PDF, tao file in, xem truoc file in, tu chon file, xuat danh sach don); giao dien ghep le moi (khay dau ra dang luoi, keo tha, phim Enter, tim khong dau, chan file trung, chi render thumbnail khi cuon toi). |
| 1.2.0 | 2026-07-28 | Mau rieng cho tung nhom, nhan ban nhom, bo chon sau khi them vao dau ra. |
| 1.1.0 | 2026-07-17 | Sua nhanh trang trong man xem truoc dau ra. |
