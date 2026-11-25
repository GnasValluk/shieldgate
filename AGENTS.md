# AGENTS.md — ShieldGate 2.0

> Tệp hướng dẫn cho agent/dev làm việc trong repo này. Đọc `plan.md` trước khi
> sửa code. Mỗi phần xong phải cập nhật **Nhật ký tiến độ** trong `plan.md`.

## Tổng quan

ShieldGate là công cụ Windows bảo vệ **thiết bị ngoại vi** (USB, HID, thẻ nhớ,
điện thoại, Bluetooth) khỏi mã độc lây qua ổ rời, và sửa hậu quả trên ổ/máy.
Không phải antivirus toàn hệ thống. Không gửi dữ liệu ra mạng.

- Ngôn ngữ: C# 5 (compiler `csc.exe` v4.0.30319), .NET Framework 4.0.
- UI: WinForms, tự vẽ, không designer, hỗ trợ DPI.
- Đóng gói: một `ShieldGate.exe`, không installer.
- Song ngữ Việt/Anh; mọi câu chữ đi qua `src/ui/Strings.cs`.

## Lệnh bắt buộc trước khi báo xong

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
powershell -ExecutionPolicy Bypass -File test.ps1
```

- Build phải tạo `dist\ShieldGate.exe`.
- Self-test phải in `PASS <n>/<n>` (hoặc toàn bộ mục đạt) và exit 0.
- Nếu chỉ sửa tài liệu: vẫn chạy build/test nếu có thay đổi mã.
- Không dùng `dotnet build`; repo không có project/solution.

## Bố cục

```
src/core/     lõi, không phụ thuộc WinForms
src/ui/       giao diện, Strings.cs, self-test entry
tests/        TestMain.cs (gọi SelfTest.Run)
assets/       icon
legacy/       bản PowerShell cũ, không sửa
viruts/       mẫu thật, KHÔNG commit (đã gitignore)
```

Luồng chính:

- `Program.Main` → GUI hoặc lệnh `--scan --status --install --watch --tray
  --uninstall --selftest --devices --events --repair --defender`.
- `Guard.RunWatch` vòng lặp nền: áp chính sách, quét ổ mới, kiểm tra gốc ổ,
  kiểm kê thiết bị, giám sát hành vi.
- `UsbScan.Detect` quét ổ; `UsbScan.Apply` cách ly; `Quarantine` lưu/phục hồi.
- `Devices` kiểm kê + trust store; `Events` ghi timeline; `Repair` thao tác ổ.

## Quy tắc bắt buộc

### An toàn dữ liệu

1. **Không bao giờ** ghi lên ổ hệ thống. Dùng `Repair.RefuseReason` trước mọi
   lệnh chkdsk/format/backup ghi.
2. Mọi thao tác phá huỷ phải có dry-run hoặc xem trước, xác nhận rõ, ghi log.
3. Không xoá tệp người dùng khi chưa cách ly được (trừ trường hợp antivirus
   chặn đọc, đã có logic riêng trong `Quarantine.Add`).
4. Không thêm tính năng tự động sửa boot sector/MBR, format, hay “dọn ổ”.
5. Nội dung cách ly là mẫu sống: không đưa vào log, không gửi mạng, giữ ACL.

### Phát hiện

6. Detector mới phải là một lớp `IDetector` trong `Detectors.cs` (hoặc
   `IDirectoryDetector` nếu cần cả thư mục), không nhét vào `Scan.cs`.
7. Trả `Finding` với `Code`, `Score`; chỉ đặt `Strong=true` khi gần như chắc
   chắn. Ngưỡng lấy từ `Rules.ScoreThreshold`, không hard-code.
8. Tên tệp/đuôi/ký tự/trọng số phải nằm trong `rules.json` (hoặc default
   trong `Rules.cs`), không rải hằng số khắp nơi.
9. Không tự cách ly mẫu `FlagOnly` (macro Office, DLL nghi vấn). Chỉ gắn cờ.
10. Không thêm mạng. Mọi tính năng mạng phải opt-in, chỉ gửi hash, có tài liệu
    (xem `PRIVACY.md`); hiện chỉ có `--vt` khi người dùng đã bật VirusTotal.

### Thiết bị

11. `DeviceInfo` là mô hình chung; không thêm code chỉ cho “ổ USB” nếu áp dụng
    được cho HID/thẻ nhớ/MTP.
12. Trust store ghi theo khoá ổn định (VID/PID/serial/instance id), không theo
    ký tự ổ.
13. Không tự disable thiết bị. Chỉ đánh dấu/cảnh báo; hành động phải do người
    dùng bấm và có xác nhận.
14. BadUSB chỉ là heuristic. Không quảng cáo là “chặn BadUSB”.

### UI và chuỗi

15. Không viết chuỗi tiếng Việt/Anh trực tiếp trong UI; thêm vào `Strings.cs`
    với `T(vi, en)`.
16. UI không chạy việc nặng trên luồng giao diện; dùng thread + `BeginInvoke`.
17. Giữ phím tắt `Ctrl+1..7`, tab order, tên trợ năng (`AccessibleName`).

### Bảo mật công cụ

18. Đường dẫn khôi phục cách ly phải chuẩn hoá và nằm trong thư mục đích hợp
    lệ; không tin `manifest.csv` tuyệt đối.
19. Không thêm named pipe/service nếu chưa có thiết kế xác thực. Named pipe W23
    đã có nhưng tắt mặc định (`pipe.enabled=0`); chỉ dùng khi bật, các luồng
    khác vẫn dùng tệp + ACL.
20. Mọi tệp cấu hình trong thư mục dữ liệu phải chịu được dữ liệu rác/hỏng:
    parse lỗi → dùng mặc định, ghi log, không crash.

## Thêm một detector mới (checklist)

1. Thêm mã + trọng số vào `Rules.DefaultJson` và `Rules.Weight`.
2. Viết lớp `XxxDetector : IDetector` trong `Detectors.cs`; đăng ký trong
   `DetectorPipeline.PerFile`.
3. Thêm nhánh câu chữ trong `S.Reason.Text` (Việt + Anh).
4. Thêm mẫu giả vào `SelfTest.Run` và một mục kiểm tra.
5. Chạy build + test; cập nhật `plan.md`.
6. Nếu detector đọc nội dung tệp: giới hạn kích thước (≤ 8 MB) và thời gian,
   dùng `FileShare.ReadWrite`, bắt mọi exception.

## Thêm một trang UI mới (checklist)

1. `PageId` trong `Controls.cs`; thêm vào `RailPanel`, `MainForm._pages`,
   `CreatePage`, phím `Ctrl+N`.
2. Kế thừa `PageBase`, dùng `MakeHeading/MakeLabel/MakeButton/MakeLink`.
3. `OnResize` → `LayoutPage`; `OnShown` nạp dữ liệu trên thread nền.
4. Mọi chuỗi qua `Strings.cs`; cập nhật self-test nếu trang ảnh hưởng logic.

## Quy ước mã

- Comment tiếng Việt, ngắn, giải thích **vì sao**, không mô tả lại code.
- Không thêm thư viện ngoài. Chỉ tham chiếu assembly có trong .NET 4.0.
- Không dùng cú pháp C# 6+ (`?.`, `nameof`, string interpolation, expression
  bodied members, auto-property initializer).
- Bắt exception tại biên (WMI, file, registry); không để vòng lặp nền chết.
- Tên tệp/hàm theo phong cách hiện có: `PascalCase` cho public nội bộ,
  `_camel` cho field private.
- Log dùng `Log.Write(message, "INFO"|"WARN"|"ERROR")`; không log nội dung tệp.

## Kiểm thử

- Self-test chỉ ghi trong `%TEMP%\shieldgate-selftest-<pid>` và đặt
  `SHIELDGATE_HOME` trước lần chạm `Paths`.
- Không gọi mạng ngoài (self-test VirusTotal chỉ dùng loopback `127.0.0.1`),
  không quét ổ thật, không đổi cấu hình máy.
- Mẫu giả phải sinh bằng code; không commit mẫu virus.
- Khi thêm mục, tăng `total` trong `SelfTest.Run` và thêm chuỗi `S.Selftest`.

## Việc còn lại (bản sau)

- Named pipe W23: còn checklist máy thật (GUI thường + engine SYSTEM, client tự
  viết, rút USB giữa job, pipe từ máy khác, Windows 7 x86).
- Raw Input key attribution + ETW cho BadUSB.
- VirusTotal opt-in (chỉ hash) và YARA.
- Sửa boot sector/MBR có đối chiếu mẫu sạch.
- TestDisk/PhotoRec, file carving, format ổ.
- Ký số release, đổi tên repo `shieldgate`, issue template.
