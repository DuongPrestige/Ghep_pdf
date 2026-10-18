# Tài liệu PDF Page Composer

Phiên bản ứng dụng hiện tại: **1.3.2**. Mã task tiếp theo: **TASK-078**.

## Cấu trúc

```text
docs/
├── README.md            # trang này: mục lục và quy tắc bảo trì tài liệu
├── ARCHITECTURE.md      # stack, cấu trúc solution, ranh giới module, service dùng chung, dữ liệu cục bộ
├── DEVELOPMENT.md       # build, chạy, test, công cụ đi kèm
├── CHANGELOG.md         # lịch sử phiên bản
├── USER_GUIDE.md        # hướng dẫn người dùng: trang đầu, cập nhật phần mềm, khi gặp lỗi
├── composer/            # Module Ghép lẻ PDF (màn hình chính)
│   ├── PRD.md  UI_SPEC.md  TECH_SPEC.md  USER_GUIDE.md  TASKS.md  PERFORMANCE.md
├── tiktok/              # Module Đơn TikTok
│   ├── PRD.md  UI_SPEC.md  TECH_SPEC.md  USER_GUIDE.md  TASKS.md
├── update/              # Module Cập nhật và phát hành
│   ├── TECH_SPEC.md  RELEASE.md  TASKS.md
├── spikes/              # kết quả thử nghiệm kỹ thuật (thư viện PDF, TikTok API)
├── assets/              # logo
└── archive/             # tài liệu lập kế hoạch ban đầu, chỉ để tham khảo
```

Mỗi module dùng cùng bộ file (module `update` chỉ có `TECH_SPEC.md`, `RELEASE.md`, `TASKS.md`):

| File | Nội dung | Ai đọc |
|---|---|---|
| `PRD.md` | nghiệp vụ, quy tắc, tiêu chí nghiệm thu — nguồn sự thật của module | chủ sản phẩm, dev |
| `UI_SPEC.md` | bố cục, nút, trạng thái giao diện | dev, tester |
| `TECH_SPEC.md` | vị trí code, thuật toán, dữ liệu, test | dev |
| `USER_GUIDE.md` | cách dùng, tên nút đúng như trên giao diện | người dùng |
| `TASKS.md` | task đã làm / còn lại của module | dev |

## Đọc gì trước

- **Người dùng:** `USER_GUIDE.md` → hướng dẫn của module cần dùng.
- **Dev mới:** `../README.md` → `ARCHITECTURE.md` → `DEVELOPMENT.md` → `../AGENTS.md` → bộ tài liệu của module sắp sửa.
- **Phát hành bản mới:** `update/RELEASE.md`.

## Quy tắc bảo trì

Khi sửa code, cập nhật tài liệu trong **cùng commit**:

| Thay đổi | Cập nhật |
|---|---|
| Quy tắc nghiệp vụ của một module | `PRD.md` của module (ghi ngày và lý do thay đổi) |
| Nút, cột, dialog, phím tắt | `UI_SPEC.md` và `USER_GUIDE.md` của module |
| Thuật toán, schema SQLite, migration, class mới | `TECH_SPEC.md` của module |
| Package, service dùng chung, DI, đường dẫn dữ liệu | `ARCHITECTURE.md` |
| Lệnh build/test, công cụ | `DEVELOPMENT.md` |
| Xong một task | `TASKS.md` của module (Completed, Files changed, Verification, Notes); tăng "Mã task tiếp theo" ở trên |
| Phát hành bản mới | `CHANGELOG.md`, dòng phiên bản ở đầu trang này, `update/RELEASE.md`, `update/TASKS.md` |

Quy ước chung:

- Viết tiếng Việt có dấu, câu ngắn; tên class, file, lệnh để trong backtick.
- Tên nút trong tài liệu phải giống hệt trên giao diện.
- Mã task đánh số chung toàn dự án (`TASK-001` …), không đánh lại khi chuyển module.
- Mỗi module chỉ mô tả phần của mình; phần dùng chung đưa lên `ARCHITECTURE.md` và tham chiếu tới, không chép lại.
- Không xóa task hoặc quyết định cũ; đánh dấu đã hủy `[~]` hoặc ghi "đã chốt / thay đổi ngày …".

## Đổi vị trí tài liệu (2026-10-18)

Các mục "Files changed" trong task cũ còn dùng đường dẫn trước khi tách. Bảng đối chiếu:

| Trước | Sau |
|---|---|
| `PRD.md` (gốc repo) | `docs/composer/PRD.md` |
| `docs/UI_SPEC.md`, `docs/TECH_SPEC.md`, `docs/performance.md` | `docs/composer/UI_SPEC.md`, `docs/composer/TECH_SPEC.md`, `docs/composer/PERFORMANCE.md` |
| `docs/USER_GUIDE.md` (một file) | `docs/USER_GUIDE.md` (trang đầu) + `docs/composer/USER_GUIDE.md` + `docs/tiktok/USER_GUIDE.md` |
| `docs/TASKS.md` | `docs/composer/TASKS.md` (Phase 0–8, 11), `docs/update/TASKS.md` (Phase 9, TASK-075), `docs/tiktok/TASKS.md` (Phase 10) |
| `docs/tiktok/PRD_TIKTOK.md`, `docs/tiktok/TECH_SPEC_TIKTOK.md` | `docs/tiktok/PRD.md`, `docs/tiktok/TECH_SPEC.md` |
| `docs/UPDATE_RELEASE.md` | `docs/update/RELEASE.md` (+ `docs/update/TECH_SPEC.md`, `docs/CHANGELOG.md` mới) |
| `docs/RUN.md` | `docs/DEVELOPMENT.md` |
| `ARCHITECTURE.md`, `CONTEXT.md`, `SPEC.md`, `RULES.md` (gốc repo) | `docs/archive/` |
