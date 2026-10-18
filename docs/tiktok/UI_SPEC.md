# Đặc tả giao diện — Module Đơn TikTok

Tài liệu đi kèm `docs/tiktok/PRD.md` và `docs/tiktok/TECH_SPEC.md`. Phản ánh giao diện bản 1.3.1.

## Nguyên tắc

- Giao diện tiếng Việt có dấu, chữ 14px, các vùng là thẻ bo góc (cùng phong cách với màn hình Ghép lẻ).
- Thông tin tối thiểu trên lưới đơn; chi tiết xem trong dialog.
- Mọi thao tác có mã đơn đều cho copy mã đơn (nút hoặc nháy đúp).
- Tác vụ dài (import, quét kho, tạo file in) chạy nền, có tiến trình và không khóa giao diện.
- Thumbnail trong dialog chỉ render khi cuộn tới (`RenderWhenVisibleBehavior`) và dùng chung `PageImageCache`.
- Không đặt logic nghiệp vụ trong code-behind hoặc `MainViewModel`; mỗi tab có ViewModel riêng dưới `ViewModels/TikTok/`.

## Cửa sổ `TikTokOrdersWindow`

Mở từ nút **Đơn TikTok** trên thanh công cụ màn hình chính; `TikTokWindowService` mở cửa sổ ở chế độ phóng to.

```text
┌────────────────────────────────────────────────────────────────────────────────┐
│ Import đơn | Quét kho                              Thư mục kho: D:\...\KHO   │
├────────────────────────────────────────────────────────────────────────────────┤
│ [Đơn hàng] [Cấu hình SKU] [Kho PDF] [Lịch sử] [Cài đặt]                       │
├────────────────────────────────────────────────────────────────────────────────┤
│ Sẵn sàng 12 | Thiếu file 1 | Cần cấu hình 0 | ...   Shop ▾                    │
│                       Phạm vi ▾ [Xuất CSV]  Đang chọn 12 đơn · 40 trang       │
│                                        [Xem trước file in] [Tạo file in]      │
│ ☑ Nhóm      Mã đơn ⧉   Sản phẩm   Phân loại   Số tờ  File in        Ghi chú  │
│ ☑ Sẵn sàng  5801…  ⧉   CARD BO…   20 CARD…    3      DINH BAC…  👁           │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Tab Đơn hàng

- **Chip nhóm** theo thứ tự: Sẵn sàng, Thiếu file, Cần cấu hình, Thủ công, Có ghi chú, Đã tạo file in, Không cần in card. Mỗi chip là một màu riêng; lọc thêm theo shop.
- **Ô Tìm mã đơn / sản phẩm:** gõ một phần mã đơn hoặc tên sản phẩm / phân loại, không cần dấu (`cody nam vo`, `43768581`). Khi ô có chữ, đơn ở **mọi nhóm** đều được tìm (kể cả nhóm đang tắt); lọc shop vẫn áp dụng. Nút **Xóa** trả về bộ lọc theo chip.
- **Lưới đơn** theo đúng thứ tự dòng trong file CSV (`TikTokOrderOrdering.InFileOrder`). Cột: chọn, Nhóm, Mã đơn (nút copy), Sản phẩm, Phân loại, Số tờ, File in (nút xem / chọn file), Ghi chú. Dòng chọn tô xanh nhạt.
- Nháy đúp ô mã đơn để copy. **Copy mã đơn** copy các đơn đang tick, không tick thì copy mọi đơn đang hiển thị.
- Đơn nhóm *Có ghi chú* có nút **Cho phép ghép** (đã xem ghi chú).
- Cột File in mô tả từng bộ: `· bộ i/q` khi mua nhiều bộ, `· x/y trang` hoặc `tr a-b` khi chỉ lấy một phần file, `· tự chọn` khi người dùng đã chọn tay.
- **Góc trên bên phải:** phạm vi xuất (Đơn chưa làm file in / Đơn đã làm file in / Tất cả đơn đang chờ / Lần import gần nhất) và **Xuất CSV**; tóm tắt số đơn và số trang đang chọn; **Xem trước file in**; **Tạo file in** (nút xanh chính).
- **Trang trắng giữa đơn:** Không chèn / Chỉ đơn phức tạp / Mọi đơn.

### Tab Cấu hình SKU

- Lọc **Chỉ hiện SKU chưa cấu hình**. Cột: Chọn, SKU ID, Sản phẩm, Phân loại, Loại xử lý (Tự động / Thủ công / Bỏ qua), Idol, Số card, Trạng thái, Thao tác.
- **Xác nhận gợi ý đã chọn**, **Gợi ý lại**, **Đặt cả sản phẩm là Bỏ qua**.
- Khung **Alias idol**: cách viết (vd `Công B`) → mã idol (vd `CONGB`); **Thêm alias**, **Xóa**.

### Tab Kho PDF

- Cột: Tên file, Idol, Số trang (tên), Số trang thực, Ngày, Trạng thái, Đường dẫn. Lọc **Chỉ hiện file lỗi**.
- **Quét kho** đọc lại thư mục kho; file bị xóa khỏi ổ đĩa sẽ không còn trong danh sách.

### Tab Lịch sử

- **Lô import:** #, Thời gian, Shop, Chế độ, File, Kết quả. **Xóa lần import** xóa các đơn chỉ thuộc lần import đó; **Xóa toàn bộ đơn** xóa mọi đơn, lựa chọn file tay và lô import (giữ cấu hình SKU, alias, kho và lịch sử lô in).
- **Lô in:** Mã lô, Thời gian, Kết quả, Phân cách, File; **Mở file**, **Tạo lại file in**, **Xuất DS đơn của lô**.
- **Đơn trong lô:** Mã đơn, Số bộ; **Copy mã đơn**, **Bỏ đánh dấu** (đơn quay lại danh sách chưa in).
- **Chi tiết bộ in:** #, Mã đơn, Bộ, File kho, Cảnh báo.

### Tab Cài đặt

- Shop (thêm, đổi tên), thư mục kho PDF, thư mục lưu file in (trống thì hỏi mỗi lần), trang phân cách mặc định.
- Quy tắc: Seller Note chứa "ĐÃ IN" thì coi là đã in; đơn có ghi chú thì không ghép tự động; **Luôn lấy ảnh mới nhất** (mặc định bật).

### Dialog Import đơn

- Chọn shop (hoặc **Thêm shop mới**), chế độ **File đầy đủ (ẩn đơn của shop không còn trong file)** hoặc **Bổ sung (không ẩn đơn nào)**, chọn file `.csv` hoặc kéo thả vào cửa sổ, **Bắt đầu import**. Sau khi import hiện báo cáo: thêm mới, cập nhật, đã in, bỏ qua, bị ẩn.

## Dialog "Xem và tự chọn file" (`OrderFilesWindow`)

Mở từ nút xem ở cột File in. Mỗi bộ card cần in là một ô (slot).

```text
┌─────────────────────────────────────────────┬──────────────────────────────┐
│ Mã đơn ⧉ · Sản phẩm · Phân loại            │ Bộ 1 (3 trang cần)   ⚠ 4/3  │
│ Tìm: [dinh bac]   [Thêm file PDF ngoài…]    │ [tr1] [tr2] [tr3] [tr4]     │
│ Kho file · bấm vào trang để chọn / bỏ chọn  │  ◀ Lên  Xuống ▶  ✕          │
│ DINH BAC - 5 PAGE - 24.09   [Lấy cả file]   │ Bộ 2 …                       │
│ [1✓][2✓][3✓][4][5]                          │                              │
│ DINH BAC - 3 PAGE - 31.07                   │                              │
│ [1][2][3]                                   │                              │
├─────────────────────────────────────────────┴──────────────────────────────┤
│ Dùng lại chọn tự động                              Hủy   [Lưu lựa chọn]    │
└────────────────────────────────────────────────────────────────────────────┘
```

- Bên trái: các file của idol, mới nhất trước, lọc theo tên (mặc định là idol của bộ đang chọn, tìm không dấu). PDF ngoài kho thêm bằng **Thêm file PDF ngoài…** được ghim ở đầu danh sách.
- Bấm trang để thêm / bỏ khỏi bộ đang chọn; **Lấy cả file** thêm mọi trang chưa chọn của file.
- Đơn có nhiều bộ card: dòng **Đơn có nhiều bộ card · bấm vào bộ cần sửa** hiện mỗi bộ thành một nút (`Bộ n: IDOL · số card = số trang · đã chọn x/y`, dòng dưới là tên sản phẩm); bộ đang sửa tô xanh. Thư viện bên trái tự lọc theo idol của bộ đang chọn.
- Bên phải: trang của từng bộ theo thứ tự in; đổi thứ tự bằng **◀ Lên / Xuống ▶**, bỏ trang bằng **✕**.
- Được chọn nhiều hoặc ít trang hơn số card; dialog hiện cảnh báo nhưng vẫn cho lưu.
- **Lưu lựa chọn** chỉ bật khi mọi bộ có ít nhất một trang và không trang nào thuộc file đã mất; file kho đã bị xóa khỏi ổ đĩa bị ẩn, trang lỗi render hiển thị "Không mở được file".
- **Dùng lại chọn tự động** xóa lựa chọn tay của đơn.

## Dialog "Xem trước file in" (`PrintPreviewWindow`)

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Xem trước file in · 12 đơn · 41 trang     [Copy mã tất cả đơn] [Tạo file in] │
│ Thứ tự giống file in. Bấm "Đổi file" để chọn lại trang cho đơn, …            │
├──────────────────────────────────────────────────────────────────────────────┤
│ 1. 5801… ⧉  Sản phẩm · Phân loại          [Đổi file] [Bỏ khỏi lô]           │
│    [Trang trắng] [p1] [p2] [p3]                                              │
│ 2. …                                                                         │
└──────────────────────────────────────────────────────────────────────────────┘
```

- Hiển thị mọi trang theo đúng thứ tự file in, gồm trang trắng phân cách.
- Đơn có nhiều bộ card (nhiều món, hoặc một món mua nhiều bộ) được tách thành từng khối `Bộ n: IDOL · phân loại · số trang` kèm tên sản phẩm; trang trắng phân cách (nếu có) đứng trước các khối.
- **Đổi file bộ này** mở dialog "Xem và tự chọn file" đúng bộ đó; **Đổi file** ở đầu đơn mở bộ 1. Đóng dialog thì xem trước được dựng lại.
- **Bỏ khỏi lô** bỏ tick đơn, không in trong lô này.
- **Tạo file in** tạo file với các đơn còn lại.
