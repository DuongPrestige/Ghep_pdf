# Quy tắc bắt buộc

## Bất biến nghiệp vụ

1. **Bảo toàn chất lượng:** export bằng cách copy/import trực tiếp page object; không rasterize trừ trường hợp bất khả kháng đã được chấp thuận.
2. **Nguồn và đầu ra độc lập:** chọn/bỏ chọn thumbnail không tự thêm, xóa hoặc thay đổi mục đã có trong khay đầu ra.
3. **Cho phép lặp:** một trang nguồn có thể xuất hiện nhiều lần trong đầu ra; mỗi lần là một `OutputPageItem` riêng.
4. **Khay đầu ra là nguồn sự thật:** PDF xuất phải khớp tuyệt đối thứ tự phẳng đang hiển thị trong khay.
5. **Thứ tự thêm mặc định:** theo thứ tự file trong vùng nguồn, rồi số trang tăng dần trong mỗi file.
6. **Nhân bản nhóm:** sao chép đủ mọi mục và giữ nguyên thứ tự nội bộ; bản sao được chèn ngay sau bản gốc hoặc cuối danh sách theo lựa chọn.
7. **Không tự động in:** ứng dụng chỉ xuất và mở file; Foxit/ứng dụng mặc định thực hiện in.
8. **Không sửa nguồn:** mọi thao tác với PDF nguồn là chỉ đọc.
9. **Nhận dạng file:** dùng ID nội bộ, đường dẫn đầy đủ và/hoặc fingerprint; tên file không đủ để định danh.
10. **PDF mã hóa:** phải phát hiện và yêu cầu mật khẩu trước khi render/export; không lưu mật khẩu plain text.

## Quy tắc dữ liệu và xuất file

- Giữ kích thước, hướng và rotation của từng trang; hỗ trợ portrait/landscape và nhiều kích thước trong cùng đầu ra.
- Không tự động scale trang.
- Kiểm tra fingerprint/file nguồn trước export; cảnh báo nếu bị đổi hoặc thiếu.
- Export lỗi không được để lại file đích hỏng; file tạm phải được dọn.
- Xóa file nguồn khỏi phiên không được âm thầm xóa mục đầu ra liên quan; cần cảnh báo/xác nhận hoặc đánh dấu unresolved.
- Project có version schema và đủ thông tin để phục hồi nguồn, group, thứ tự đầu ra và trạng thái UI.

## Quy tắc hiệu năng và luồng

- Không chạy đọc/render/export đồng bộ trên UI thread.
- Ưu tiên render viewport, lazy load và hủy công việc không còn cần thiết.
- Virtualize cả vùng nguồn và khay đầu ra cho dữ liệu lớn.
- Cache thumbnail phải có giới hạn dung lượng và chiến lược loại bỏ.
- Không giữ hàng loạt bitmap preview độ phân giải cao trong RAM.
- Tác vụ nền lỗi ở một file/trang không được khóa thao tác với phần đã sẵn sàng.

## Quy tắc riêng tư, bảo mật và log

- Xử lý hoàn toàn offline; không upload PDF hay gửi phân tích nội dung.
- Log cục bộ, rolling theo ngày; không log nội dung tài liệu hoặc mật khẩu.
- Không lưu mật khẩu sau khi đóng phiên, trừ khi có thiết kế lưu bí mật an toàn và người dùng chủ động bật.
- Kiểm tra quyền đọc nguồn, quyền ghi đích và dung lượng đĩa trước các tác vụ liên quan.

## Quy tắc phụ thuộc

- Đánh giá license mọi thư viện PDF trước khi phân phối.
- Không dùng iText/AGPL trong sản phẩm đóng nếu chưa mua license hoặc chưa có phê duyệt pháp lý.
- Cô lập thư viện PDF qua interface Infrastructure để có thể thay renderer/composer.
