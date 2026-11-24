# Privacy

ShieldGate is a local tool. It has no account, no telemetry, no analytics and no
update check. It does not contact the network at all unless you turn on the
optional VirusTotal lookup and run a lookup yourself.

## What stays on the machine

ShieldGate keeps its data in `C:\ProgramData\ShieldGate` (or
`%LOCALAPPDATA%\ShieldGate` when ProgramData is not writable):

| Data | Contents |
| --- | --- |
| `settings.txt` | language, switches, scan counters, VirusTotal on/off and consent flag |
| `guard.cfg` | which protection layers you chose |
| `rules.json` | detection rules and weights |
| `devices.json` | device trust store: instance ID, VID/PID, serial, label |
| `events.tsv` | local event timeline: time, event kind, device or path, action result |
| `last_scan.txt` | result of the last machine check |
| `log\shieldgate.log` | activity log |
| `quarantine\` | quarantined files and their manifests |
| `backup\`, `repair\` | Windows configuration backups and drive backup markers |
| `vt.key` | VirusTotal API key, encrypted with DPAPI |

The event timeline and the log can contain file paths and device names. They
never contain file contents. Quarantined files are stored as they were found, so
treat the quarantine folder as sensitive; it is restricted to Administrators and
SYSTEM while protection is on.

## Input timing (BadUSB measurement)

While the tray icon runs and the BadUSB switch is on, ShieldGate registers Raw
Input for keyboards and counts how many key events arrive per device in 100 ms
buckets. It reads only the device handle and the message time. It never reads or
stores key codes, scan codes, characters, clipboard data or window titles, and it
does not install a keyboard hook.

The per-event counters live in memory, are cleared after each burst or after
5 seconds without input, and are never written to disk. When one device reaches
30 events in 2 seconds (15 events/second), ShieldGate writes a single INFO line
to the local log with a VID/PID label and the counts, at most once per device per
10 minutes.

To decide whether to warn, the tray also polls running processes once per second.
It reads process IDs, parent process IDs, image names and creation times for a
small set of shells (`cmd`, `powershell`, `pwsh`, `wscript`, `cscript`, `mshta`)
and a Toolhelp process list for parent names. It never reads command lines, key
codes, window titles, clipboard data or file contents. Recent shell names and
parents stay in memory for 120 seconds.

If a new untrusted keyboard reaches the rate threshold and a shell starts from
Explorer within 10 seconds, ShieldGate writes one WARN line with the device name,
VID/PID, event count, shell image and parent image, and shows a tray balloon. It
writes at most one warning per device per 10 minutes. It never disables, blocks
or reports a device, and nothing is sent over the network. Closing the tray icon
or turning the switch off stops both the counting and the process polling.

## The only network feature: VirusTotal

VirusTotal is **off by default**. It becomes active only when all of these are
true:

1. You turn it on in **Protect → VirusTotal** and accept the one-time consent
   dialog.
2. An API key is saved.
3. You run `ShieldGate.exe --vt <file>` for a specific file.

What is sent:

- `GET https://www.virustotal.com/api/v3/files/<sha256>` — the SHA-256 hash of
  the file is part of the URL.
- The API key is sent in the `x-apikey` header.

What is **not** sent: file name, path, size, contents, device information,
machine name, user name, or any history. ShieldGate does not upload files and
does not use VirusTotal results to quarantine anything automatically.

The request uses TLS 1.2, a 15-second timeout, no proxy and no redirects, and is
limited to four requests per minute. A lookup is not retried more than once, and
only for a network error or a 5xx answer.

### API key

The key is encrypted with Windows DPAPI (`ProtectedData`, scope `CurrentUser`)
and written to `vt.key`. Only the Windows account that saved it can decrypt it;
a copy from another machine or account is treated as "no key". The key is never
written to `settings.txt`, the event timeline or the log. Remove it any time
with **Protect → VirusTotal → Remove key**; uninstalling ShieldGate deletes
`vt.key` even when you choose to keep quarantine and drive backups.

Turning the switch off stops all lookups immediately, even if a key is still
stored. ShieldGate does not contact VirusTotal in the background.

## Your choices

- Do not enable VirusTotal if you do not want any network request.
- Remove the API key when you no longer need lookups.
- `--uninstall` keeps quarantine and drive backups by default; `--purge-data`
  deletes those folders as well. `vt.key` is deleted in both cases.
- The event timeline can be exported to CSV or HTML from the app; those exports
  are ordinary local files that you control.

## Contact

Bugs and privacy questions: open an issue at
<https://github.com/GnasValluk/shieldgate-usb-guard/issues>.

---

## Tóm tắt tiếng Việt

ShieldGate không có tài khoản, không telemetry, không tự cập nhật và không gửi
dữ liệu ra mạng. Toàn bộ dữ liệu nằm trong thư mục dữ liệu trên máy: thiết lập,
luật phát hiện, tin cậy thiết bị, nhật ký sự kiện, cách ly và bản sao lưu. Nhật
ký và sự kiện có thể chứa đường dẫn tệp và tên thiết bị, nhưng không có nội
dung tệp.

Tính năng mạng duy nhất là tra cứu VirusTotal, mặc định **tắt**. Chỉ khi bạn bật
trong trang Bảo vệ, lưu khoá API và tự chạy `--vt <tệp>`, ShieldGate mới gửi
SHA-256 của đúng tệp đó tới virustotal.com; không gửi tên tệp, đường dẫn, nội
dung hay thông tin máy. Khoá API được mã hoá bằng DPAPI của tài khoản Windows
hiện tại, không nằm trong `settings.txt` hay nhật ký, và bị xoá khi gỡ cài đặt.
Tắt công tắc là dừng mọi lời gọi mạng ngay, kể cả khi khoá vẫn còn.

Khi biểu tượng khay chạy và công tắc BadUSB bật, ShieldGate đếm số sự kiện bàn
phím theo thiết bị: chỉ handle thiết bị và thời điểm, không đọc mã phím, không
lưu nội dung gõ. Bộ đếm nằm trong bộ nhớ và tự xoá sau mỗi burst hoặc sau 5 giây
không có input; log chỉ ghi tối đa một dòng INFO/10 phút/thiết bị với nhãn VID/PID
và số đếm.

Để quyết định có cảnh báo hay không, tray còn poll tiến trình mỗi giây: chỉ đọc
PID, PID cha, tên image và thời điểm tạo của 6 shell (`cmd`, `powershell`, `pwsh`,
`wscript`, `cscript`, `mshta`) cùng danh sách Toolhelp để lấy tên cha. Không đọc
`CommandLine`, mã phím, tiêu đề cửa sổ, clipboard hay nội dung tệp. Tên shell và
cha gần đây chỉ nằm trong bộ nhớ 120 giây.

Nếu bàn phím mới chưa tin cậy vượt ngưỡng tốc độ và có shell mở từ Explorer
trong 10 giây, ShieldGate ghi một dòng WARN (tên thiết bị, VID/PID, số sự kiện,
tên shell và tên cha) và hiện balloon. Tối đa một cảnh báo/thiết bị/10 phút.
Không tắt, không chặn, không báo cáo thiết bị và không gửi gì ra mạng. Đóng biểu
tượng khay hoặc tắt công tắc là dừng cả đếm lẫn poll tiến trình.
