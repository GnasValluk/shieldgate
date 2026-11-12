# ShieldGate 4.8 — bản chạy đơn

Đây là bản PowerShell cũ, giữ lại để đối chiếu. Tệp `ShieldGate_4.8.bat` tự chứa toàn bộ mã, không cần thư viện hay runtime nào khác.

## Cách dùng

1. Chép `ShieldGate_4.8.bat` vào USB.
2. Chạy tệp. Script tự xin quyền quản trị.
3. Script chỉ chạy khi thư mục hiện tại không nằm trên ổ hệ thống `C:`.

Script sẽ:

- bỏ thuộc tính ẩn/system/read-only cho tệp và thư mục trong thư mục chứa nó;
- xóa các tệp `.lnk`, `.vbs`, `.vbe`;
- giữ nguyên `autorun.inf` để tránh làm hỏng USB có giải pháp autorun hợp lệ.

## Lưu ý

- Đây là bản xóa trực tiếp, **không có vùng cách ly**. Tệp đã xóa không khôi phục được.
- Không dùng cho ổ hệ thống.
- Bản hiện tại của ShieldGate (`dist\ShieldGate.exe`) có cách ly, khôi phục, bảo vệ nền và hai ngôn ngữ; nên dùng bản đó cho công việc hằng ngày.
