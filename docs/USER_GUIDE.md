# Hướng dẫn sử dụng PDF Page Composer

Áp dụng cho phiên bản 1.3.2.

Ứng dụng có hai phần:

| Phần | Dùng khi | Hướng dẫn |
|---|---|---|
| Ghép lẻ PDF | Chọn trang từ nhiều file PDF, sắp xếp, nhân bản rồi xuất một file để in | [composer/USER_GUIDE.md](composer/USER_GUIDE.md) |
| Đơn TikTok | Tạo file in card theo file CSV đơn hàng xuất từ TikTok Seller Center | [tiktok/USER_GUIDE.md](tiktok/USER_GUIDE.md) |

## Cập nhật phần mềm

- Bấm **Check update** trên thanh công cụ. Nếu có bản mới, ứng dụng tự tải, tự đóng, cài đè và mở lại.
- Nếu vừa phát hành mà máy báo đang dùng bản mới nhất, đợi khoảng 5 phút rồi bấm lại.
- Nên đặt ứng dụng trong thư mục có quyền ghi (ví dụ `D:\Apps\PDFPageComposer`), không đặt trong `Program Files`.

## Khi gặp lỗi

- Log nằm trong thư mục `logs` cạnh file `PDFPageComposer.App.exe`, mỗi ngày một file, giữ 14 ngày.
- Dữ liệu đơn TikTok nằm ở `%LOCALAPPDATA%\PDFPageComposer\tiktok.db`; cài đặt ở `%LOCALAPPDATA%\PDFPageComposer\settings.json`. Cập nhật phần mềm không xóa hai file này.
