# ShieldGate 2.0 — Phân tích mã nguồn và lộ trình

Ngày: 17/11/2026 · Phạm vi: đọc toàn bộ `src/core`, `Program`, `TrayApp`, `AboutPage`, xử lý nút của `UsbPage`/`ProtectPage`, self-test, README, plan, AGENTS, CI.

**Lưu ý trung thực:** phân tích dựa trên đọc mã, chưa build hay chạy trên Windows. Mục nào cần chạy thử để chắc chắn được ghi "cần kiểm chứng".

# Phần 2 — App còn thiếu, thừa, lỗi gì

## A. Lỗi bảo mật và an toàn dữ liệu (sửa trước mọi tính năng mới)

| # | Vấn đề | Vị trí | Mức |
| --- | --- | --- | --- |
| A1 | **Chạy công cụ hệ thống bằng tên trần.** `schtasks`, `icacls`, `reg`, `robocopy`, `chkdsk`, `fsutil`, `powershell`, `cmd`, `pnputil` đều gọi không kèm đường dẫn. CreateProcess tìm trong thư mục chứa exe trước. GUI chạy admin và là exe portable, nên chạy từ USB nhiễm hoặc Downloads có `reg.exe` giả sẽ chạy với quyền admin. | `Native.Run`, `Uninstaller`, \~40 chỗ | **Cao**, sửa rẻ |
| A2 | **Gỡ cài đặt xoá thư mục chung chung.** `DataEntries` xoá đệ quy `log`, `bin`, `backup`, `quarantine`, `repair` trong thư mục dữ liệu do người dùng chọn. Nếu họ chọn `D:\Tools` đang có `bin` hay `backup` riêng, chúng bị xoá. | `Uninstaller.cs` | **Cao** |
| A3 | Gỡ cài đặt xoá luôn bản sao lưu ổ và vùng cách ly, không cho chọn giữ. | `Uninstaller`, `Strings.UninstallBody` | Trung bình |
| A4 | **Điều kiện "đã sao lưu" quá lỏng.** Marker là `backup-E.ok`, gắn theo ký tự ổ. USB khác cũng là E: thì `chkdsk /f` vẫn được phép. Marker không hết hạn, không kiểm tra bản sao còn tồn tại. | `Repair.MarkerFile/HasBackup` | **Cao** |
| A5 | **Khôi phục cách ly không kiểm tra đường dẫn đích.** AGENTS điều 18 yêu cầu chuẩn hoá, nhưng `RestoreBatch` dùng thẳng `OriginalPath`/`QuarantinePath` trong `manifest.csv`. Thư mục có ACL nên rủi ro thấp, nhưng chưa tuân thủ quy tắc của repo. | `Core.cs:624` | Trung bình |
| A6 | `BackupReg` không kiểm tra mã thoát `reg export`, `Fix` vẫn xoá/ghi tiếp. Exe x86 nên `reg.exe` có thể export nhầm view Wow6432Node (cần kiểm chứng). | `Scan.cs:1344` | Trung bình |
| A7 | `IsSigned` chỉ kiểm tra có chữ ký (`CreateFromSignedFile`), không xác minh hash hay chuỗi tin cậy. | `Detect.IsSigned` | Trung bình |
| A8 | Xuất CSV dễ bị formula injection: tên tệp trên USB (\`=cmd | ...\`) vào cột Subject/Detail. | `Events.ExportCsv` |
| A9 | `rules.json` cạnh exe được tin trước. Chạy exe từ USB có `rules.json` kèm theo có thể nâng ngưỡng hoặc làm rỗng danh sách. | `Rules.CandidatePath` | Trung bình |

## B. Tính năng chạy sai hoặc không hiệu lực

| # | Vấn đề |
| --- | --- |
| B1 | **BlockExec gần như mất tác dụng trên Windows 64-bit.** Build `/platform:x86`; tiến trình x86 không đọc được `Process.MainModule` của tiến trình 64-bit (bị `catch → continue`). Quét `GetProcesses()` + `MainModule` mỗi 4 giây dưới SYSTEM cũng nặng. Sửa: `QueryFullProcessImageName` hoặc WMI `ExecutablePath`. Cần kiểm chứng. |
| B2 | **BadUSB/Composite gần như không bao giờ kích hoạt.** `IsInternalKeyboard` coi mọi ID bắt đầu `HID\` là bàn phím trong, nhưng bàn phím USB ngoài có dạng `HID\VID_xxxx&PID_xxxx...`. Cần kiểm chứng bằng `--devices` khi cắm bàn phím. |
| B3 | Heuristic BadUSB còn lại yếu: poll 15 giây, hỏi "có cmd/powershell/wscript tạo trong 20 giây qua"; nhiều tiến trình nền tự sinh các shell này nên báo nhầm; payload chạy xong rồi thoát thì bỏ sót; không xét tiến trình cha. |
| B4 | **Unhide xoá Hidden/System trên toàn ổ** sau mỗi lần cách ly (kể cả nền tự chạy), gồm `desktop.ini`, thư mục tuỳ biến, tệp người dùng cố tình ẩn. |
| B5 | **Đường dẫn > 260 ký tự không được quét.** `Tree.Walk` dùng System.IO, lỗi `PathTooLong` bị bỏ qua, dù `Quarantine.Add` có xử lý tên Base64 siêu dài. |
| B6 | Race khi bật bảo vệ: `Configure` chạy task nền trước rồi mới `SaveConfig`, tiến trình nền áp cấu hình cũ tới 60 giây. |
| B7 | WriteProtect xung đột với làm sạch USB: ổ mount chỉ đọc nên `Apply` không cách ly được, không có luồng "tạm mở khoá". |
| B8 | Chép 400 tệp hợp lệ vào USB trong 10 giây kích hoạt quét toàn ổ và toast, lặp mỗi 2 phút. |
| B9 | `chkdsk /f` không xử lý câu hỏi dismount (không điều khiển stdin, timeout 6 giờ). Cần thử. |
| B10 | `robocopy` không có `/XD "System Volume Information" "$RECYCLE.BIN"`, có thể luôn exit ≥ 8 trên NTFS → không ghi marker → không bao giờ cho `chkdsk /f` (cần kiểm chứng). Không kiểm tra dung lượng trống ổ C: trước khi sao lưu. |
| B11 | Sửa ổ cho phép cả ổ cố định phụ (D:, E: nội bộ); `RefuseReason` chỉ chặn ổ hệ thống, mạng, CD. |
| B12 | `PcScan` chỉ quét hồ sơ người dùng hiện tại; nâng quyền bằng admin khác thì HKCU/Startup/Desktop là của admin. |

## C. Nợ kỹ thuật và thừa thải

- **Quy tắc AGENTS bị phá trong mã:** điều 7 (ngưỡng điểm không quyết định `High` ở đường 1.0), điều 8 (`DangerExes`, `PayloadNames`, `PayloadHashes`, `SystemNames` hardcode trong `Detect.cs`), điều 13 (`Devices.SetEnabled` dùng `pnputil` tồn tại nhưng không ai gọi, là mã chết).
- **`rules.json` bị cắt nửa vời:** `scriptExts` trong JSON chỉ ảnh hưởng `BehaviorMonitor`, quét dùng `Detect.ScriptExts` hardcode. `FillSet` ghi đè cả tập nên nâng cấp thêm đuôi/trọng số mới sẽ không có hiệu lực với file cũ. Trường `version` không dùng để migrate. `DefaultJson` và `Defaults()` đồng bộ tay.
- **Trùng mã:** `ScriptProcesses()` ở `UsbScan` và `PcScan`; khối ghi registry lặp ở `ApplyMachinePolicy`, `ApplyConfig`, `Configure`; `Vaccinate` tạo thư mục `autorun.inf` không đặt thuộc tính ẩn/hệ thống/chỉ đọc.
- **Hiệu năng quét:** `OfficeMacroDetector` đọc tới 8 MB **đầu** mỗi docx/xlsx/pptx (comment nói đọc cuối ZIP; tệp > 8 MB bị bỏ sót). `AdsDetector` gọi `FileInfo` + `FindFirstStreamW` cho từng tệp, vô ích trên FAT32/exFAT. `IsSigned` ném exception cho mỗi tệp chưa ký. Cờ `FlagOnly` cho `.xlsm` hợp lệ tạo nhiễu.
- **Kênh GUI↔nền chỉ là `settings.txt` một ô:** tray chỉ thấy sự kiện gần nhất; thống kê theo ký tự ổ (`usb.E.time`); tray chạy `HighestAvailable` (admin) chỉ để hiện icon.
- **Nhật ký lưu chuỗi đã dịch:** tiến trình SYSTEM không biết ngôn ngữ người dùng chọn nên log lẫn Việt/Anh. Nên lưu mã + tham số. Còn 11 chuỗi tiếng Việt hardcode trong `Guard.cs`.
- **Self-test (35 mục) thiếu vùng rủi ro nhất:** không test `Policy`, `Behavior`, BadUSB, uninstall, tray; không có bộ mẫu lành đo báo nhầm; `SelfTest.cs` còn được biên dịch vào bản phát hành.
- Phát hành hiện bám sát một họ virus (`Docusment`, Opera `1.exe`, 5 hash); độ phủ họ khác chưa đo.
- Mẫu virus thật nằm trong zip (`viruts/`, đã gitignore): cần chắc không bao giờ commit/đóng gói; nên đưa hash vào test thay vì giữ tệp.

## D. Còn thiếu

- Đối chiếu mẫu sạch cho boot sector/MBR: hiện chỉ đọc 512 byte của **volume** (`\\.\E:`), không đọc `PhysicalDrive` nên không thấy bảng phân vùng/MBR.
- Defender chỉ gọi thủ công; không quét sau khi cách ly.
- CLI không có `--json`, `--help`, `--version`, không có chế độ làm sạch tự động.
- Kiểm tra cập nhật luật, ký số exe, đổi tên repo, issue template, tài liệu quyền riêng tư.
- "Trusted" chỉ là nhãn hiển thị, không ảnh hưởng hành vi; khoá thiết bị (VID/PID/serial) có thể bị giả mạo, cần ghi rõ giới hạn.
- GUI chạy admin toàn thời gian là bề mặt tấn công lớn (lý do named pipe được nhắc nhưng chưa có thiết kế).

# Phần 1 — Plan thứ tự và chi tiết từng mục

## Nguyên tắc sắp xếp

1. **Sửa nền tảng trước, tính năng mới sau.** BadUSB, VirusTotal, chặn thực thi dựa trên các hàm đang lỗi (B1, B2, A1).
2. Đi từ **rủi ro thấp, giá trị cao** sang **cần thiết kế và xác nhận**.
3. Mỗi giai đoạn kết thúc bằng `build.ps1` + `test.ps1` + cập nhật `plan.md`, đúng AGENTS.

## Giai đoạn 0 — Nền tảng an toàn (1–2 tuần, làm trước tất cả)

| Bước | Việc | Cách làm | Tiêu chí xong |
| --- | --- | --- | --- |
| 0.1 | Đường dẫn đầy đủ cho công cụ hệ thống (A1) | Thêm `Native.SysExe("schtasks.exe")` trả `Environment.SystemDirectory` (hoặc `Sysnative` khi x86 trên x64). Thay mọi `Native.Run("x.exe")` và `Process.Start("explorer.exe")`. | grep không còn lời gọi tên trần |
| 0.2 | Marker sao lưu gắn theo ổ thật (A4, B10, B11) | Marker lưu volume serial + device key + thời điểm + đường dẫn bản sao; `HasBackup` kiểm tra đủ và hết hạn (ví dụ 24 giờ). Thêm `/XD` cho `System Volume Information`, `$RECYCLE.BIN`. Kiểm tra dung lượng trống. `RefuseReason` mặc định chỉ cho ổ Removable. | Test: cùng chữ E: khác serial → từ chối `/f` |
| 0.3 | Gỡ cài đặt an toàn (A2, A3) | Luôn dùng thư mục con `ShieldGate` + tệp marker `.shieldgate`; chỉ xoá khi có marker. Hộp thoại gỡ có checkbox "giữ quarantine và sao lưu ổ". | Test trên thư mục giả có `bin` của người dùng |
| 0.4 | Chuẩn hoá đường dẫn khôi phục (A5) | `RestoreBatch`: `GetFullPath`, từ chối `..`, `QuarantinePath` phải nằm trong `QuarDir`, `OriginalPath` có ổ hợp lệ, không phải thư mục hệ thống. | Test manifest bị sửa |
| 0.5 | Sửa nhỏ | Kiểm tra mã thoát `reg export` (A6); chống CSV injection (A8); đổi thứ tự `SaveConfig` trước khi chạy task (B6); giới hạn Unhide (B4). | Test từng mục |
| 0.6 | Ngưng tin `rules.json` cạnh exe (A9) | Chỉ đọc từ thư mục dữ liệu có ACL, hoặc bắt buộc băm ghi trong `bin`. Dùng `version` + merge mặc định (khoá mới thêm, không ghi đè). | Test nâng cấp từ rules cũ |
| 0.7 | Xác minh chữ ký thật (A7) | P/Invoke `WinVerifyTrust` (API Windows, không vi phạm "không thư viện ngoài"). | Test file ký hợp lệ / sửa 1 byte |

**Cần xác nhận của bạn:** 0.3 đổi cấu trúc thư mục dữ liệu (cần migrate); 0.6 đổi nguồn tin cậy của luật.

## Giai đoạn 1 — Sửa các tính năng đang sai (1–2 tuần)

| Bước | Việc | Chi tiết |
| --- | --- | --- |
| 1.1 | Sửa BlockExec x64 (B1) | `QueryFullProcessImageName` + `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`, bỏ `MainModule`. Cân nhắc WMI `Win32_ProcessStartTrace` thay poll 4 giây. Cần chạy thử trên Win x64 thật. |
| 1.2 | Sửa `IsInternalKeyboard`/Composite (B2) | Xác định "ngoài" theo bus: ID có `VID_` và cha là `USB\` hoặc Bluetooth; loại trừ `ACPI\`, `ROOT\`, I2C HID. Test bằng chuỗi PNP mẫu. |
| 1.3 | Tuân thủ AGENTS về luật (C) | Chuyển `DangerExes`, `PayloadNames`, `PayloadHashes`, `SystemNames`, `ScriptExts` vào `rules.json`; để `ScoreThreshold` thực sự quyết định `High` hoặc sửa AGENTS cho đúng hiện trạng. Xoá hoặc đưa `SetEnabled` vào UI có xác nhận (điều 13). |
| 1.4 | Đường dẫn dài (B5) | `Tree.WalkExt` dùng `WinPath.List` (đã có). |
| 1.5 | Tối ưu quét | Macro: đọc đuôi ZIP; ADS: bỏ qua nếu không phải NTFS; `IsSigned` tránh exception; ẩn mặc định `FlagOnly` macro, thêm bộ lọc. |
| 1.6 | Luồng WriteProtect ↔ làm sạch (B7) | "Chế độ làm sạch": tắt tạm WriteProtect, yêu cầu rút/cắm, quét, bật lại, có xác nhận và log. |
| 1.7 | Quét lại thông minh (B8) | Debounce: chỉ quét thư mục gốc + thư mục mới khi ghi hàng loạt không có `.lnk`/script/exe lạ. |
| 1.8 | Bộ test lành + hồi quy | Sinh bằng mã 30–50 mẫu lành (portable app, `.xlsm`, `.url`, `desktop.ini` hợp lệ), đo báo nhầm. Thêm test cho `Policy`, `Behavior`, uninstall. |

## Giai đoạn 2 — VirusTotal opt-in (làm được ngay theo điều 10, \~1 tuần)

1. **Tắt mặc định**, công tắc riêng + ô nhập API key, hộp thoại giải thích một lần: chỉ gửi SHA-256, không gửi tệp, tên tệp hay đường dẫn.
2. Mã mạng nằm trong **một lớp** `VirusTotal.cs` dùng `HttpWebRequest`, TLS 1.2 bật rõ ràng trên .NET 4.0, timeout ngắn, giới hạn 4 yêu cầu/phút (gói miễn phí), không thử lại vô hạn.
3. **Lưu key bằng DPAPI** (`ProtectedData`), không lưu rõ trong `settings.txt`, không ghi vào log, xoá khi gỡ cài đặt.
4. Chỉ tra khi người dùng bấm (bản đầu), không tự gửi từ luồng nền; kết quả chỉ tham khảo, không tự cách ly dựa trên VT.
5. Tài liệu `PRIVACY.md`, cập nhật README ("không mạng" → "không mạng trừ khi bạn bật VirusTotal"), cập nhật AGENTS điều 10.
6. Test: máy chủ giả loopback kiểm tra payload chỉ chứa hash; test mã hoá key; khi tắt thì không có lời gọi mạng.

## Giai đoạn 3 — BadUSB tốt hơn (Raw Input + ETW, 2–3 tuần, có thiết kế trước)

Chỉ làm sau bước 1.2, cần `docs/design-badusb.md` trước khi viết mã.

- **Tốc độ gõ:** Raw Input đăng ký `RIDEV_INPUTSINK` trên cửa sổ ẩn **trong tiến trình tray (phiên người dùng)**, vì SYSTEM không có cửa sổ tương tác. Ghi chỉ thời gian và handle thiết bị, **không ghi mã phím** (tránh thành keylogger; ghi vào tài liệu quyền riêng tư).
- **Tín hiệu:** thiết bị HID mới + tốc độ gõ phi nhân loại (ví dụ > 30 phím/giây liên tục) + sau đó `cmd/powershell/wscript/mshta` mới sinh ra với cha là `explorer.exe`. Chỉ cảnh báo, không disable (điều 13/14).
- **ETW:** `Microsoft-Windows-Kernel-Process` để biết tiến trình cha chính xác; cần admin và session riêng. Phương án nhẹ hơn: WMI `Win32_ProcessStartTrace`.
- README ghi rõ đây là heuristic, không phải "chặn BadUSB".
- Thử với bàn phím thật và thiết bị giả lập (Arduino/Digispark) trước khi tuyên bố gì.

## Giai đoạn 4 — Named pipe GUI↔SYSTEM (3–4 tuần, cần thiết kế và chấp thuận trước khi code)

- **Mục tiêu:** GUI chạy quyền thường, chỉ engine SYSTEM đụng hệ thống.
- **Pipe:** `PipeSecurity` chỉ cho SID tương tác của người dùng hiện tại + Administrators; `PIPE_REJECT_REMOTE_CLIENTS`; một instance server; xác thực client bằng `GetNamedPipeClientProcessId` + đường dẫn exe khớp `bin\ShieldGate.exe` đã ghim và băm đúng.
- **Giao thức:** allow-list lệnh cố định, JSON có phiên bản, giới hạn kích thước, không có lệnh nhận đường dẫn tự do (quarantine/restore chỉ nhận **ID lô**). Hành động nguy hiểm vẫn xác nhận ở GUI và log ở engine.
- **Cần làm trước:** hoàn tất 0.4.
- **Phương án rẻ hơn:** giữ mô hình tệp + ACL nhưng thêm **hàng đợi sự kiện có ID** (giải quyết mất thông báo tray) và để GUI vẫn chạy admin. Nên làm ở Giai đoạn 1 rồi quyết định pipe sau.

## Giai đoạn 5 — Những mục chưa nên làm

| Mục | Quan điểm |
| --- | --- |
| Sửa boot sector/MBR, format, TestDisk/PhotoRec, carving | AGENTS điều 4 cấm tự động. Nếu muốn: hướng **báo cáo và hướng dẫn** (đọc MBR/bảng phân vùng từ `PhysicalDrive`, so mẫu sạch, đề xuất lệnh). Format chỉ qua `format`/`diskpart` sau 3 lớp xác nhận, ổ Removable, có sao lưu đã kiểm chứng. TestDisk/PhotoRec là exe ngoài, vi phạm single-exe và "không mạng" nên chỉ chỉ đường, không gói kèm. |
| YARA | Cần DLL native, vi phạm quy ước. Thay bằng bộ khớp chuỗi/hex nhỏ trong `rules.json`. |
| Ký số release, đổi tên repo | Ngoài phạm vi mã, làm khi có chứng chỉ. |

## Giai đoạn 6 — Hoàn thiện và phát hành

- README: mô tả đúng hạn chế chặn thực thi/BadUSB; thêm `PRIVACY.md`; cập nhật `AGENTS.md` cho khớp mã.
- CLI: `--help`, `--version`, `--scan --json`.
- i18n: log/events lưu mã + tham số.
- `LeastPrivilege` cho tray.
- Phát hành 2.1 (Gđ 0–1) → 2.2 (VT, BadUSB) → 3.0 (pipe nếu duyệt).

## Tóm tắt thứ tự đề xuất

1. Giai đoạn 0 (A1–A9) — bắt buộc, rủi ro thấp
2. Giai đoạn 1 (B1–B11, nợ kỹ thuật, test lành)
3. Giai đoạn 2 VirusTotal opt-in
4. Giai đoạn 3 BadUSB (sau 1.2)
5. Giai đoạn 4 Named pipe (sau thiết kế được duyệt)
6. Giai đoạn 5 boot/MBR/format/YARA: chỉ khi xác nhận hướng "báo cáo + hướng dẫn"

## Cần bạn quyết định

1. Đổi cấu trúc thư mục dữ liệu (luôn có thư mục con `ShieldGate` + marker) ở bước 0.3?
2. VirusTotal: DPAPI cho khoá API, chỉ chạy thủ công ở bản đầu, đổi tuyên bố "không mạng" trong README?
3. Bắt đầu ngay từ Giai đoạn 0.1–0.2 (sửa A1 và A4)?
