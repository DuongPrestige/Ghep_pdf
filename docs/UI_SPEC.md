# Đặc tả giao diện

## Nguyên tắc thiết kế

- Trực quan, ưu tiên thao tác bằng chuột và thumbnail.
- Hiển thị đồng thời mọi file và mọi trang trong một vùng cuộn.
- Không bắt nhập số trang thủ công cho thao tác chính.
- Phù hợp màn hình từ 1366×768 và hỗ trợ DPI cao.
- Các khu vực có thể resize; ưu tiên hiệu năng khi có nhiều thumbnail.
- Selection chỉ là trạng thái chuẩn bị; tick trang không tự thêm vào Output Tray.

## Bố cục màn hình chính

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Toolbar: Thêm PDF | Xóa | Chọn/Bỏ chọn | Zoom | Undo/Redo | Xuất PDF       │
├──────────────────────────────────────────────────────┬───────────────────────┤
│ SOURCE WORKSPACE (70–75%)                            │ OUTPUT TRAY (25–30%)  │
│                                                      │                       │
│ A.pdf — 6 trang — đang chọn 2                        │ Group 1               │
│ [1] [2] [✓3] [✓4] [5] [6]                           │ 01 A.pdf — Trang 3   │
│                                                      │ 02 A.pdf — Trang 4   │
│ B.pdf — 5 trang — đang chọn 3                        │                       │
│ [✓1] [✓2] [✓3] [4] [5]                              │ Tổng: 2 trang        │
├──────────────────────────────────────────────────────┴───────────────────────┤
│ File: 2 | Trang nguồn: 11 | Đang chọn: 5 | Đầu ra: 2 | Render: sẵn sàng   │
└──────────────────────────────────────────────────────────────────────────────┘
```

Source Workspace và Output Tray nằm trong splitter. Output Tray có thể thu gọn.

## Toolbar

Các thao tác MVP:

- **Thêm PDF**, **Xóa file**, **Chọn tất cả**, **Bỏ chọn**.
- Thu gọn/mở rộng section file.
- Slider/preset zoom thumbnail.
- **Undo**, **Redo**, **Xuất PDF**.
- Nút **Thêm vào đầu ra** phải dễ nhận biết và chỉ bật khi có trang được chọn.

## Source Workspace

- Các file hiển thị nối tiếp theo chiều dọc; mỗi file là một section.
- Header section có tên file, tổng trang, số trang đang chọn và tổng lượt các trang của file đã xuất hiện trong đầu ra.
- Cho phép collapse, chọn/bỏ chọn tất cả và xóa file khỏi phiên.
- Có thể kéo đổi thứ tự file nếu tính năng này được triển khai; thứ tự file quyết định thứ tự mặc định khi thêm nhiều trang.
- Xóa file có item trong đầu ra phải cảnh báo và không âm thầm xóa item.

## Thumbnail Card

Mỗi card gồm preview, số trang, dấu tick, badge số lần đã có trong đầu ra và nút kính lúp khi hover.

| Trạng thái | Biểu hiện |
|---|---|
| Default | Viền trung tính, không tick |
| Hover | Viền/nền nổi bật nhẹ, hiện nút preview |
| Selected | Viền nhấn và dấu tick |
| Added to output | Badge `×N`, không đồng nghĩa đang chọn |
| Selected + Added | Đồng thời có tick và badge |
| Loading | Skeleton/progress, không khóa phần khác |
| Error | Placeholder lỗi và nút thử lại |
| Locked PDF | Biểu tượng khóa, yêu cầu mật khẩu trước render |

## Chọn trang

- Click: chọn hoặc bỏ chọn một trang.
- Ctrl+Click: thêm/bớt trang rời rạc khỏi selection.
- Shift+Click: chọn dải từ anchor gần nhất trong cùng file.
- Chọn tất cả/bỏ chọn toàn bộ trong một file; toolbar áp dụng theo phạm vi hiện tại.
- Thứ tự thêm mặc định: thứ tự file trong workspace, sau đó số trang tăng dần.
- Selection không tự động thêm hoặc xóa item trong Output Tray.

## Zoom thumbnail

- Slider và ba preset: Nhỏ, Vừa, Lớn.
- Ctrl + con lăn chuột.
- Zoom không làm mất selection và giữ vị trí cuộn gần trang đang nhìn.

## Preview lớn

- Mở bằng double click, nút kính lúp hoặc Enter.
- Modal/panel nổi có Trang trước, Trang sau, Zoom, Fit Width, Fit Height và rotate tạm để xem.
- Có thể chọn/bỏ chọn trang ngay trong preview.
- Đóng preview không thay đổi selection ngoài thao tác người dùng đã thực hiện.

## Output Tray

Output Tray là nguồn sự thật duy nhất của nội dung và thứ tự PDF xuất.

Mỗi item hiển thị số thứ tự đầu ra, tên file nguồn, số trang nguồn, group chứa item và thumbnail nhỏ khi ở chế độ thumbnail.

Hỗ trợ:

- Kéo thả item/group và hiển thị vị trí thả.
- Chọn một hoặc nhiều item; xóa item hoặc cả group.
- Nhân bản một trang, nhiều trang hoặc group; số lượng 1–999.
- Chèn bản sao sau bản gốc hoặc cuối danh sách.
- Collapse và di chuyển group.
- Hiển thị tổng số trang, cập nhật số thứ tự sau mọi thay đổi.

## Group

- Mỗi lần bấm **Thêm vào đầu ra** tạo một group mới ở cuối tray.
- Group giữ thứ tự trang của lần thêm.
- Nhân bản group tạo bản sao đầy đủ và đúng thứ tự nội bộ.
- Một trang nguồn có thể xuất hiện trong nhiều group và nhiều lần trong cùng đầu ra.

## Ví dụ UI bắt buộc

Để tạo:

```text
A3, A4, B1, B2, B3, B4, B5, B1, B2, B3
```

Người dùng:

1. Chọn A3–A4, bấm **Thêm vào đầu ra** → Group 1.
2. Chọn B1–B3, thêm → Group 2.
3. Chọn B4–B5, thêm → Group 3.
4. Chọn lại B1–B3 và thêm, hoặc nhân bản Group 2 → Group 4.
5. Tray hiển thị 10 item đúng thứ tự trên; badge B1–B3 là `×2`.

## Dialog xuất PDF

- Chọn tên file và thư mục; hiển thị tổng số trang.
- Nếu tray rỗng, chặn export và giải thích cách thêm trang.
- Không cho chọn chính file nguồn làm đích.
- Trong lúc xuất hiển thị progress và tùy chọn hủy nếu an toàn.
- Thành công hiển thị **Mở thư mục**, **Mở bằng Foxit**, **Đóng**.

## Status Bar

Hiển thị tổng file, tổng trang nguồn, số trang đang chọn, tổng trang đầu ra, trạng thái render và trạng thái/progress export.

## Empty State và Error State

- Chưa import: hướng dẫn kéo thả hoặc bấm **Thêm PDF**.
- PDF lỗi: tên file, lý do và tùy chọn bỏ qua/thử lại.
- PDF có mật khẩu: hộp nhập mật khẩu hoặc bỏ qua; không lưu plain text.
- File bị thiếu: đánh dấu, cho chọn lại đường dẫn, giữ item đầu ra cho tới khi xác nhận.
- Không tìm thấy Foxit: chọn executable hoặc mở bằng viewer mặc định.
- Export thất bại: thông báo dễ hiểu, chi tiết trong log và không để lại file đích hỏng.

## Phím tắt MVP

| Phím | Chức năng |
|---|---|
| Ctrl+O | Thêm PDF |
| Ctrl+A | Chọn tất cả trong phạm vi hiện tại |
| Ctrl+Shift+A | Bỏ chọn tất cả |
| Ctrl+Enter | Thêm selection vào Output Tray |
| Ctrl+D | Nhân bản item/group |
| Delete | Xóa khỏi Output Tray |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+S | Lưu project |
| Ctrl+E | Xuất PDF |
| Enter / Esc | Mở / đóng preview |
| Ctrl+con lăn | Zoom thumbnail |
