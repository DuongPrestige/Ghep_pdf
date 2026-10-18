# Task — Module Cập nhật và phát hành

Phạm vi: nút Check update, manifest `latest.json`, đóng gói và phát hành các phiên bản. Đặc tả: `docs/update/`.

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
| Done | 7 |
| Chưa xong (Backlog/Blocked) | 0 |
| Đã hủy | 0 |

## Phase 9 — Update distribution

### - [x] TASK-052 - Check for update

- **Muc tieu:** Them nut check update de nguoi dung tai va cai ban moi tu manifest online.
- **Dependency:** TASK-050.
- **Acceptance criteria:** Nut toolbar goi service rieng; manifest co version/downloadUrl; neu co version moi thi tai zip, bung goi tam, tao script thay the file sau khi app thoat va mo lai app; co test cho luong chua cau hinh, moi nhat va co ban moi.

Completed: 2026-07-17
Files changed: `src/PDFPageComposer.App/Interfaces/IUpdateService.cs`, `src/PDFPageComposer.App/Services/UpdateService.cs`, `src/PDFPageComposer.App/Models/AppUpdateManifest.cs`, `src/PDFPageComposer.App/Models/AppUpdateResult.cs`, `src/PDFPageComposer.App/Models/AppSettings.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/App.xaml.cs`, `tests/PDFPageComposer.Tests/Services/UpdateServiceTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `docs/UPDATE_RELEASE.md`
Verification: `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test PDFPageComposer.slnx --no-build` succeeded with 94 passing tests.
Notes: Update packages must be zip files containing publish output at the zip root. `UpdateManifestUrl` is read from `%LOCALAPPDATA%\PDFPageComposer\settings.json`.

### - [x] TASK-053 - Output preview editing

- **Muc tieu:** Cho phep thao tac nhanh tren trang dang xem trong man `Xem truoc dau ra`.
- **Dependency:** TASK-035, TASK-052.
- **Acceptance criteria:** Khi xem mot trang dau ra cu the, co the them ban sao, di chuyen len/xuong va xoa trang hien tai; preview cap nhat dung thu tu/trang hien tai sau moi thao tac; khong them business logic vao code-behind; co test ViewModel cho luong thao tac trong preview.

Completed: 2026-07-17
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `src/PDFPageComposer.App/PDFPageComposer.App.csproj`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `latest.json`
Verification: `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test PDFPageComposer.slnx --no-build` succeeded with 95 passing tests.
Notes: `v1.1.0` keeps grid preview for overview and shows editing controls only after opening a specific output page.

### - [x] TASK-054 - Output group colors

- **Muc tieu:** Gan mau rieng cho tung group trong Output Tray de nguoi dung de phan biet cac lan them trang.
- **Dependency:** TASK-028, TASK-044, TASK-053.
- **Acceptance criteria:** Moi group moi co mau hien thi rieng trong tray; duplicate group co mau moi; mau duoc giu qua undo/redo, project save/load va auto-save; khong anh huong thu tu export.

Completed: 2026-07-27
Files changed: `src/PDFPageComposer.App/Models/OutputGroup.cs`, `src/PDFPageComposer.App/Models/OutputTrayItemView.cs`, `src/PDFPageComposer.App/Models/ProjectState.cs`, `src/PDFPageComposer.App/Services/ProjectStateMapper.cs`, `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/Models/OutputGroupTests.cs`, `tests/PDFPageComposer.Tests/Services/ProjectPersistenceServiceTests.cs`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `docs/TASKS.md`
Verification: `dotnet build PDFPageComposer.slnx --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test PDFPageComposer.slnx --no-build` succeeded with 99 passing tests.
Notes: Group color is lightweight UI metadata stored as hex color; older project files without color receive a default palette color when loaded.

### - [x] TASK-055 - Output add clears selection and group duplicate UI

- **Muc tieu:** Hoan thien thao tac Output Tray: them nut nhan ban group, bo selection sau khi them vao dau ra, va hien so trang dang chon tren nut them.
- **Dependency:** TASK-027, TASK-032, TASK-054.
- **Acceptance criteria:** Sau khi bam **Them vao dau ra**, source pages vua them duoc bo chon va thong ke selection ve 0; toolbar hien so trang dang chon ngay tren nut add; nguoi dung co the nhan ban ca group tu Output Tray; co test ViewModel cho behavior moi.

Completed: 2026-07-27
Files changed: `src/PDFPageComposer.App/ViewModels/MainViewModel.cs`, `src/PDFPageComposer.App/MainWindow.xaml`, `tests/PDFPageComposer.Tests/ViewModels/MainViewModelTests.cs`, `docs/TASKS.md`
Verification: `dotnet test PDFPageComposer.slnx --no-build /p:NuGetAudit=false` succeeded with 100 passing tests. `dotnet build PDFPageComposer.slnx --no-restore /p:NuGetAudit=false` succeeded with 0 errors; the environment still reports NU1900 vulnerability-feed warnings because it cannot reach `https://api.nuget.org/v3/index.json`.
Notes: Group duplication logic already existed in ViewModel; this task exposes it in the tray row UI and aligns add-output behavior with the requested workflow.

### - [x] TASK-056 - Publish update v1.2.0

- **Muc tieu:** Build goi update moi va cap nhat manifest de user co the update len ban chua cac cai tien Output Tray.
- **Dependency:** TASK-054, TASK-055.
- **Acceptance criteria:** Tang app version; publish `win-x64` self-contained; zip publish output khong boc folder cha; tinh SHA256; cap nhat `latest.json`; push thay doi len git.

Completed: 2026-07-28
Files changed: `src/PDFPageComposer.App/PDFPageComposer.App.csproj`, `latest.json`, `docs/TASKS.md`
Verification: `dotnet publish .\src\PDFPageComposer.App\PDFPageComposer.App.csproj -c Release -r win-x64 --self-contained true --no-restore -o .\artifacts\publish-v1.2.0` succeeded; `artifacts\PDFPageComposer-v1.2.0.zip` contains `PDFPageComposer.App.exe`, `pdfium.dll`, `PDFiumCore.dll` at the zip root; SHA256 is `0E7F8CB5427F6192D30B21CF5B410F999CD1A5AC3ECF480864F76EE6E94156A8`; `dotnet test PDFPageComposer.slnx --no-build` succeeded with 100 passing tests.
Notes: Release asset `PDFPageComposer-v1.2.0.zip` was uploaded to GitHub Releases tag `v1.2.0` after the commit and tag were pushed.

### - [x] TASK-075 — Logo và publish v1.3.1

- **Mục tiêu:** Thêm logo cho exe, taskbar và các cửa sổ; phát hành bản update.
- **Dependency:** TASK-069.
- **Acceptance criteria:** Icon nhiều kích thước (16–256 px); `latest.json` trỏ tới v1.3.1.

Completed: 2026-10-17
Files changed: `src/PDFPageComposer.App/Assets/app.ico` (mới), `PDFPageComposer.App.csproj` (`ApplicationIcon`, 1.3.1), các cửa sổ (`Icon`), `docs/assets/logo.png`, `README.md`, `latest.json`.
Verification: Publish `win-x64` self-contained chạy được (FileVersion 1.3.1.0); release `v1.3.1` trên GitHub, asset tải lại khớp SHA256 `067A4C34A65ED50B8332A1C0B35AFAF2DEDEA26EAF883C6AB04D500051D16CA3`; link raw `latest.json` trả về 1.3.1.
Notes: Chưa thử luồng Check update trên máy khác.

### - [x] TASK-077 — Publish v1.3.2

- **Mục tiêu:** Phát hành bản có ô tìm đơn (TASK-076) và tài liệu tách theo module.
- **Dependency:** TASK-076.
- **Acceptance criteria:** Release `v1.3.2` có zip; `latest.json` trỏ tới 1.3.2 chỉ sau khi asset đã tải lại khớp SHA256.

Completed: 2026-10-18
Files changed: `src/PDFPageComposer.App/PDFPageComposer.App.csproj` (1.3.2), `latest.json`, `docs/CHANGELOG.md`, dòng phiên bản trong `README.md`, `docs/README.md`, `docs/update/RELEASE.md`, các `USER_GUIDE.md`.
Verification: 380/380 test passed; publish `win-x64` self-contained chạy được (FileVersion 1.3.2.0); release `v1.3.2` (id 416220262), asset tải lại khớp SHA256 `BD912FBA395F5DA53244AB2ACBAB8A32A47AAE190734779512C0D9C58E99EE2A`.
