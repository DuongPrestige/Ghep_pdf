# TikTok Shop API spike

Status: Blocked — 2026-10-16

## Kết quả

- Đã có công cụ spike `spikes/TikTokApiSpike` (console .NET 10): ký request HMAC-SHA256 (khớp vector tham chiếu tính bằng `openssl`), đổi auth code lấy token, lấy shop, đơn `AWAITING_SHIPMENT`, sản phẩm; lưu response đã xóa dữ liệu người mua vào `artifacts/tiktok-spike/`.
- Chưa chạy được với dữ liệu thật: TikTok không cấp quyền Open API cho shop.

## Quyết định

- Nguồn đơn chuyển sang file CSV xuất từ Seller Center (PRD_TIKTOK v0.2). File xuất có đủ `Order ID`, `SKU ID`, `Product Name`, `Variation`, `Quantity`, `Created Time`, `Seller Note`.
- Giữ mã spike để dùng lại nếu sau này được cấp API: chỉ cần thêm một implementation `IOrderSource` mới.
