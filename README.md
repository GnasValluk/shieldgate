# ShieldGate

Cắm USB vào một máy lạ, mở lên chỉ thấy một loạt shortcut, còn tệp thật thì biến mất hoặc bị ẩn. Đó là virus shortcut: nó giấu thư mục, tạo shortcut cùng tên để bạn bấm nhầm, rồi lây sang ổ khác.

ShieldGate là công cụ nhỏ để chặn kiểu lây đó và dọn những gì đã bị tạo ra. Chương trình chạy trực tiếp từ USB, không cần cài, không gửi dữ liệu ra ngoài.

## ShieldGate làm gì

- Kiểm tra một ổ USB và liệt kê shortcut, script, autorun có dấu hiệu độc hại.
- Để bạn xem danh sách trước, bỏ chọn tệp muốn giữ, rồi mới cách ly. Tệp bị cách ly vẫn khôi phục được.
- Tắt tự chạy của Windows, chặn tệp `autorun.inf`, và tùy chọn chặn Windows Script Host.
- Chạy nền để kiểm tra USB ngay khi cắm vào, nếu bạn bật mục này. Khi bảo vệ nền đang chạy, ShieldGate có biểu tượng trong khay hệ thống (vùng biểu tượng ẩn cạnh đồng hồ): nhấn đúp để mở ứng dụng, chuột phải để xem trạng thái.

## Chạy lần đầu

Lần đầu mở ShieldGate, một cửa sổ thiết lập hiện ra để chọn:

- **Thư mục dữ liệu** — nơi lưu nhật ký, tệp cách ly và bản sao lưu. Mặc định `C:\ProgramData\ShieldGate`; có thể chọn thư mục khác.
- **Ngôn ngữ** — Tiếng Việt hoặc English.

Lựa chọn được lưu ở `%LOCALAPPDATA%\ShieldGate\setup.ini`. Muốn chạy lại thiết lập, tắt bảo vệ, đóng ứng dụng, xóa tệp đó rồi mở lại.

## Dùng trên USB

1. Chép `ShieldGate.exe` vào USB.
2. Chạy tệp đó. Windows sẽ hỏi quyền quản trị — đồng ý, vì phần bảo vệ cần sửa thiết lập hệ thống.
3. Mở mục **Xử lý USB**, chọn ổ, bấm **Kiểm tra USB**.
4. Xem danh sách. Bỏ chọn tệp bạn chắc là sạch, rồi bấm **Cách ly**.
5. Muốn lấy lại tệp đã cách ly, mở mục **Tổng quan** → **Khôi phục tệp**.

Lần đầu dùng, nên mở **Bảo vệ** và bật ít nhất mục đầu tiên. Mục chặn Windows Script Host chỉ nên bật nếu máy không dùng phần mềm cần VBScript; nếu sau này có phần mềm trục trặc, tắt lại là Windows trở về cấu hình cũ.

## Dòng lệnh

```
ShieldGate.exe                 mở giao diện
ShieldGate.exe --status        xem trạng thái bảo vệ
ShieldGate.exe --scan E:\      kiểm tra một ổ, chỉ đọc, không cách ly
ShieldGate.exe --install       bật bảo vệ không cần mở giao diện
ShieldGate.exe --uninstall     tắt bảo vệ, trả cấu hình cũ
ShieldGate.exe --watch         chạy bảo vệ nền (dùng cho tác vụ hệ thống)
ShieldGate.exe --selftest      tự kiểm tra trên USB giả trong thư mục tạm
```

`--selftest` không sửa máy thật. Nó tạo một ổ giả, chạy các phép kiểm tra phát hiện, cách ly, khôi phục, rồi xóa thư mục tạm. Dùng lệnh này để kiểm tra bản tải về có chạy đúng trên máy bạn.

## Dữ liệu lưu ở đâu

Mặc định: `C:\ProgramData\ShieldGate` (hoặc thư mục bạn chọn lần đầu).

```
settings.txt        thiết lập và số liệu lần kiểm tra
log\shieldgate.log  nhật ký hoạt động
quarantine\         tệp đã cách ly
backup\             bản sao cấu hình Windows trước khi bật bảo vệ
bin\ShieldGate.exe  bản chạy nền, kèm SHA256
```

Nếu không ghi được vào `ProgramData`, chương trình dùng `%LOCALAPPDATA%\ShieldGate`. Không có dữ liệu nào rời khỏi máy.

## Tự dựng bản chạy

Cần Windows có .NET Framework 4.0. Không cần Visual Studio.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Kết quả là `dist\ShieldGate.exe`. Mã nguồn chia hai phần: `src\core\` là lõi, `src\ui\` là giao diện. Mọi câu chữ hiện ra nằm trong `src\ui\Strings.cs`, có hai ngôn ngữ Việt và Anh.

Mã dùng C# 5 và WinForms, biên dịch bằng `csc.exe` của .NET Framework 4.0. Đây là lựa chọn có chủ ý để bản chạy được trên Windows 7 cũ mà không kéo theo runtime mới.

## Cấu trúc kho mã

```
src\core\      lõi: đường dẫn, quét, cách ly, bảo vệ, cấu hình khởi động
src\ui\        giao diện: năm màn hình, hộp thoại, thiết lập lần đầu, chuỗi hai ngôn ngữ
assets\        biểu tượng
tools\         công cụ vẽ biểu tượng
legacy\4.8\    bản PowerShell 4.8 chạy đơn, giữ để đối chiếu
build.ps1      dựng dist\ShieldGate.exe
```

## Bản 4.8 chạy đơn

Bản PowerShell `legacy\4.8\ShieldGate_4.8.bat` vẫn được giữ nguyên. Đây là bản chạy độc lập, không cần .NET, chỉ hiện lại tệp bị ẩn và xóa shortcut/script trong thư mục chứa nó. Xem `legacy\4.8\README.md`.

## Giới hạn đã biết

- Chưa có số liệu đo trên bộ mẫu virus shortcut thật. Ngưỡng phát hành là báo nhầm dưới 0,5% và bỏ sót dưới 5%; khi có số liệu, kết quả sẽ nằm trong kho tài liệu.
- Chương trình chỉ xử lý lây nhiễm qua USB. Nó không thay antivirus và không quét toàn bộ ổ hệ thống.
- Bản phát hành chưa ký số. Windows SmartScreen có thể cảnh báo; hãy đối chiếu SHA256 trước khi chạy.
- Không hỗ trợ ổ mạng, ổ quang, hay thiết bị MTP.
- Cần quyền quản trị để mở, kể cả khi bạn chỉ muốn kiểm tra.

## Giấy phép và đóng góp

Mã nguồn mở theo giấy phép MIT, xem [LICENSE](LICENSE). Báo lỗi và góp ý qua mục Issues của kho mã. Khi báo lỗi, ghi kèm phiên bản Windows, loại ổ USB và vài dòng cuối trong `shieldgate.log`.
