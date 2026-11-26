# ShieldGate 2.1 — Kế hoạch triển khai

> Bản 2.1 kế thừa phạm vi 2.0. Mỗi mục xong được đánh dấu `[x]` và ghi vào
> **Nhật ký tiến độ** ở cuối tệp, kèm ngày và bằng chứng kiểm tra.

## 1. Phân tích kế hoạch đề xuất

Kế hoạch gốc đúng hướng nhưng có ba điểm cần chỉnh trước khi viết code:

1. **Không thể làm hết 6 giai đoạn trong một bản phát hành.** Kế hoạch gốc ước
   lượng 20–25 tuần. Bản 2.0 này chốt một lõi hoàn chỉnh, có kiểm thử, rồi mới
   mở rộng. Các phần nguy hiểm (sửa boot sector, cứu dữ liệu, phân tích gõ phím
   BadUSB theo thời gian thực) được đưa ra khỏi phạm vi 2.0 và ghi rõ trong
   `README.md`/`Limits`.
2. **Giữ .NET Framework 4.0 + C# 5.** Đổi sang .NET 8 sẽ phá cam kết chạy trên
   Windows 7, phải viết lại toàn bộ WinForms và quy trình build `csc.exe` hiện
   tại. Không có giá trị bảo mật tương xứng trong một bản 2.0. Điều kiện để đổi:
   bỏ hỗ trợ Windows 7 và có nguồn lực viết lại UI.
3. **Không thêm named pipe ở bản này.** Kiến trúc hiện tại đã tách tiến trình:
   tác vụ SYSTEM (`--watch`) và GUI người dùng, trao đổi qua thư mục dữ liệu đã
   khoá ACL. Thêm named pipe lúc này tăng bề mặt tấn công (xác thực pipe, giả
   mạo client, tranh chấp quyền) mà chưa giải quyết vấn đề người dùng thấy được.
   Ghi nhận là việc của bản sau, khi GUI không còn chạy admin.

Các điểm giữ nguyên từ kế hoạch gốc:

- Ranh giới **“không phải antivirus toàn hệ thống”**. Chỉ thiết bị ngoại vi và
  hậu quả của mã độc lây qua thiết bị ngoại vi.
- Chấm điểm thay vì khớp cứng; mọi hành động phá huỷ phải xác nhận, có dry-run,
  có sao lưu và ghi log.
- Luật/chữ ký tách khỏi mã, cập nhật qua tệp JSON.
- Song ngữ Việt/Anh trong `src/ui/Strings.cs`.

## 2. Kiến trúc 2.0

```
src/core/
  Core.cs          đường dẫn, log, Win32, cây tệp, cách ly
  Json.cs          MỚI: đọc/ghi JSON tối thiểu (không thêm dependency)
  Rules.cs         MỚI: luật JSON, trọng số điểm, ngưỡng
  Detect.cs        nhận diện 1.0 (shortcut, script, payload)
  Detectors.cs     MỚI: IDetector + các detector mới
  Devices.cs       MỚI: DeviceInfo, kiểm kê thiết bị, trust store, BadUSB heuristic
  Events.cs        MỚI: timeline sự kiện, xuất CSV/HTML
  Policy.cs        MỚI: chặn ghi ổ USB, chặn thực thi từ ổ rời
  Behavior.cs      MỚI: FileSystemWatcher phát hiện ghi hàng loạt
  Defender.cs      MỚI: gọi MpCmdRun như lớp quét thứ hai (thủ công)
  Repair.cs        MỚI: chẩn đoán, chkdsk có bảo vệ, sao lưu, đọc thử, VBR report
  Scan.cs          quét ổ: tích hợp detector + điểm
  Guard.cs         bảo vệ nền: thiết bị mới, hành vi, thực thi từ USB
  Settings.cs      trạng thái nhỏ, sự kiện thiết bị cho tray
src/ui/
  DevicesPage.cs   MỚI: thiết bị, tin cậy/chặn, timeline, xuất CSV/HTML
  RepairPage.cs    MỚI: chẩn đoán, đọc thử, sao lưu, chkdsk, boot sector, Defender
  ProtectPage.cs   thêm công tắc chặn ghi / chặn thực thi
  Program.cs       thêm --devices, --events, --repair, --defender
  Strings.cs       toàn bộ câu chữ mới, hai ngôn ngữ
  SelfTest.cs      thêm kiểm thử cho JSON, luật, detector, thiết bị, sửa chữa
tests/
  TestMain.cs      gọi SelfTest.Run
```

### Mô hình dữ liệu

`DeviceInfo`: `InstanceId`, `Name`, `DeviceClass`, `Vid`, `Pid`, `Serial`,
`Drives`, `FirstSeen`, `LastSeen`, `Trust` (unknown/trusted/blocked),
`Removable`, `Keyboard`, `CompositeSuspect`, `Status`.

`Finding`: `Code`, `Detail`, `Score`, `Strong`, `FlagOnly`.
`IDetector.Inspect(ScanContext, TreeItem, List<Finding>)`: thêm detector mới
không sửa `Scan.cs`. Detector theo thư mục dùng `IDirectoryDetector`.

### Tệp dữ liệu mới

```
rules.json        luật + trọng số; đọc cạnh exe hoặc trong thư mục dữ liệu
devices.json      trạng thái tin cậy của thiết bị
events.tsv        timeline sự kiện thiết bị/hành vi (xuất CSV/HTML)
repair\           báo cáo chẩn đoán, bản sao trước khi sửa
```

`rules.json` được tạo từ mặc định khi thiếu. Người dùng có thể sửa để cập nhật
chữ ký/tên tệp/ngưỡng mà không build lại.

## 3. Phạm vi bản 2.0

### Giai đoạn 0 — Lõi

- [x] JSON tối thiểu, có kiểm thử round-trip.
- [x] `Rules` + `rules.json`, trọng số theo mã, ngưỡng điểm.
- [x] `IDetector` và pipeline detector.
- [x] Giữ .NET 4.0/C# 5; ghi quyết định vào README.
- [x] Không thêm named pipe; ghi lý do và việc còn lại.

### Giai đoạn 1 — Kiểm kê cổng

- [x] Liệt kê thiết bị qua WMI: storage, HID, Bluetooth, WPD/MTP, composite.
- [x] Tách VID/PID/serial từ `PNPDeviceID`.
- [x] Trust store `devices.json`: unknown/trusted/blocked.
- [x] Cảnh báo thiết bị mới trong bảo vệ nền; timeline + xuất CSV/HTML.
- [x] Heuristic composite: cùng VID/PID vừa storage vừa bàn phím.
- [x] Trang **Thiết bị** trong GUI + `--devices`.

### Giai đoạn 2 — Protect

- [x] Chặn ghi toàn cục cho ổ USB (opt-in, có xác nhận, khôi phục khi tắt).
- [x] Chặn thực thi từ ổ rời (opt-in): phát hiện tiến trình, dừng tiến trình.
- [x] Chính sách thiết bị: đánh dấu blocked, cảnh báo khi cắm lại.
- [x] Heuristic BadUSB mức 1: bàn phím mới + shell process trong 20 giây → cảnh báo.
- [x] Giám sát hành vi: bùng nổ `.lnk`/script hoặc ghi hàng loạt trên ổ.
- [ ] Raw Input gán phím theo thiết bị (bản sau; cần driver/ETW, dễ sai).
- [ ] Tự động disable thiết bị lạ (bản sau; chỉ cảnh báo ở 2.0).

### Giai đoạn 3 — Detect

- [x] Double extension (`report.pdf.exe`).
- [x] Ký tự RLO `U+202E` trong tên tệp.
- [x] Script ở gốc ổ (`.bat .cmd .ps1 .scr .cpl .hta ...`).
- [x] `.url/.scf` rò rỉ NTLM qua `IconFile=\\...`.
- [x] Alternate Data Streams (bỏ qua `Zone.Identifier`).
- [x] DLL sideloading theo danh sách tên DLL bị lạm dụng + không có chữ ký.
- [x] Office macro: chỉ gắn cờ, không cách ly.
- [x] Gọi Windows Defender thủ công (`MpCmdRun -Scan -ScanType 3 -File`).
- [x] VirusTotal (opt-in, chỉ gửi hash) — có từ 2.2 qua `--vt` và trang Bảo vệ.
- [ ] YARA — chưa làm; cần thư viện native, trái mục tiêu single-exe.

### Giai đoạn 4 — Repair

- [x] Chẩn đoán ổ: hệ tệp, dung lượng, read-only, ổ hệ thống.
- [x] Sao lưu tệp trước khi sửa bằng `robocopy` (có xem trước `/L`).
- [x] `chkdsk` chế độ chỉ đọc và `/f` có xác nhận hai lần.
- [x] Đọc thử tuần tự phát hiện lỗi I/O (có giới hạn thời gian).
- [x] Báo cáo VBR/boot sector (chỉ đọc, không ghi).
- [x] Từ chối ổ hệ thống ở mọi thao tác ghi.
- [ ] Sửa boot sector/MBR — cố ý không làm trong 2.0.
- [ ] TestDisk/PhotoRec, file carving — cố ý không làm trong 2.0.
- [ ] Format/làm sạch ổ — cố ý không làm trong 2.0.

### Giai đoạn 5–6 — Chất lượng, phát hành

- [x] Mẫu thử giả sinh trong thư mục tạm cho mọi detector mới.
- [x] Self-test mở rộng (mục tiêu ≥ 30 mục).
- [x] Chuẩn hoá đường dẫn khi khôi phục cách ly (đã có từ 1.0, thêm kiểm thử).
- [x] README 2.0 + Limits trung thực.
- [x] `AGENTS.md` cho agent/dev tiếp theo.
- [ ] Ký số release — cần chứng chỉ, ngoài phạm vi mã nguồn.
- [ ] Đổi tên repo GitHub — thao tác trên GitHub, không phải mã nguồn.

## 4. Tiêu chí “xong” cho 2.0

1. `build.ps1` tạo `dist\ShieldGate.exe` không lỗi.
2. `test.ps1` chạy self-test, tất cả mục đạt; chạy lại trên máy Windows sạch
   trước khi phát hành (repo chưa có tệp CI).
3. Không có detector mới nào tự cách ly khi chưa đủ điểm/ngưỡng.
4. Mọi thao tác ghi lên ổ đều từ chối ổ hệ thống và có dry-run/xác nhận.
5. README mô tả đúng những gì làm được, không quảng cáo quá mức.
6. Dữ liệu mới (`devices.json`, `events.tsv`) không chứa nội dung tệp, không
   gửi ra mạng.

## 5. Rủi ro và cách giảm

| Rủi ro | Giảm thiểu |
|---|---|
| False positive do detector mới | Chấm điểm, ngưỡng, flag-only cho macro, test mẫu lành |
| Chặn nhầm USB hợp lệ | Chặn ghi/thực thi mặc định tắt, có xác nhận, khôi phục được |
| `chkdsk /f` làm hỏng dữ liệu | Từ chối ổ hệ thống, bắt buộc sao lưu, xác nhận hai lần, log |
| WMI chậm/không có trên máy lạ | Mọi truy vấn có timeout, lỗi không làm chết vòng lặp nền |
| Scope creep | Mỗi mục trong tệp này có tiêu chí xong; mục chưa làm ghi rõ “bản sau” |
| Tự tin quá mức vào BadUSB | Chỉ gọi là heuristic; ghi trong Limits |

## 6. Lộ trình hoàn thiện 2.1 → 3.0

Kế hoạch chi tiết nằm trong `docs/roadmap-2.1.md`. Tệp đó chia việc thành 23 mục, mỗi
mục có điểm sửa trong mã, cách làm, phép kiểm tra mới và tiêu chí xong; mục nào cần
máy thật hoặc cần quyết định kiến trúc đều được ghi rõ.

| Mốc | Nội dung | Mục | Ước lượng |
| --- | --- | --- | --- |
| 2.1.0 ✅ | Nền tảng an toàn: A1–A9 (đường dẫn công cụ, marker sao lưu, gỡ cài đặt, khôi phục, luật, chữ ký) | W1–W7 | 9–11 ngày |
| 2.1.1 | Sửa tính năng sai: B1–B9, nợ kỹ thuật, hàng đợi sự kiện | W8–W16 | 12–15 ngày |
| 2.2 | VirusTotal opt-in, `--help/--version/--json`, `PRIVACY.md` | W17–W19 | 5–6 ngày |
| 2.3 | BadUSB: Raw Input + WMI process start, chỉ cảnh báo | W20–W22 | 12–15 ngày |
| 3.0 | Named pipe có xác thực (sau khi được duyệt) | W23 | 15–20 ngày |

Ngoài 23 mục trên, bản 2.1.0 còn có **F1–F5** (mục 2b của lộ trình): năm lỗi phát
hiện từ nhật ký máy bệnh 03/11/2026 — hai tiến trình cùng cách ly một ổ, cách ly cả
tài liệu người dùng trong thư mục giả, "tệp đã biến mất" bị tính là lỗi, bộ đếm âm,
và nhật ký tác vụ SYSTEM lẫn tiếng Anh. Đã xong 19/11.

Nguyên tắc giữ nguyên: sửa nền tảng trước tính năng; mỗi mục kết thúc bằng
`build.ps1` + `test.ps1` + mục nhật ký bên dưới; mỗi mục thêm ít nhất một phép kiểm
tra và tăng `total` trong `SelfTest.cs`.

Ba quyết định đã chốt và đang được thi hành (chi tiết ở mục 9 của lộ trình):

1. Gỡ cài đặt mặc định **giữ** cách ly và bản sao lưu ổ — đã làm ở W3 (`--purge-data`
   để xoá hết).
2. `chkdsk /f` chỉ cho ổ `Removable`; ổ cố định phụ chỉ chẩn đoán đọc — đã làm ở W2.
3. `rules.json` chỉ đọc từ thư mục dữ liệu; tệp cạnh exe bị bỏ qua — đã làm ở W6.

## 7. Nhật ký tiến độ

> Quy ước: mỗi mục ghi ngày, việc đã làm, bằng chứng (lệnh + kết quả), và
> phần còn lại. Không ghi “đã xong” nếu chưa chạy build/test.

### 2026-11-16 — Khảo sát và chốt phạm vi

- Đọc toàn bộ `README.md`, `build.ps1`, `test.ps1`, `src/core`, `src/ui`.
- Xác nhận: 22/22 self-test, C# 5, .NET 4.0, single exe, không mạng.
- Chốt: giữ .NET 4.0; không named pipe; 2.0 tập trung thiết bị + detector +
  protect + repair an toàn; các phần nguy hiểm ghi rõ là bản sau.

### 2026-11-17 — Hoàn thiện lõi 2.0, giao diện và kiểm thử

Đã làm:

- `Json.cs`, `Rules.cs` + `rules.json`, `Detectors.cs` (double extension, RLO,
  root script, NTLM leak, ADS, root desktop.ini, macro flag, sideload), tích hợp
  vào `Scan.cs` với ngưỡng `ScoreThreshold`; chỉ `Strong` mới đủ để tự cách ly.
- `Devices.cs`: kiểm kê WMI, khoá ổn định VID/PID/serial, trust store
  `devices.json`, heuristic composite và BadUSB; `DeviceStore.Sync` trả khoá mới.
- `Events.cs`: timeline TSV, xoay 2 MB, xuất CSV/HTML.
- `Policy.cs`: `WriteProtect` có sao lưu/khôi phục; dừng tiến trình chạy từ ổ rời.
- `Behavior.cs`: FileSystemWatcher phát hiện bùng nổ ghi; `Defender.cs`: gọi
  `MpCmdRun -Scan -ScanType 3 -File` thủ công.
- `Repair.cs`: `RefuseReason`, `Inspect`, `ReadTest`, `PreviewBackup`, `Backup`
  với marker `backup-<letter>.ok`, `RunChkdsk`, đọc boot sector chỉ đọc.
- `Guard.cs`: áp chính sách, quét ổ mới, kiểm kê thiết bị 15 giây/lần, giám sát
  hành vi, chặn thực thi 4 giây/lần; cảnh báo qua tray.
- UI 7 trang: `Overview, Usb, Protect, Devices, Repair, Log, About` với
  `Ctrl+1..7`; thêm `DevicesPage.cs`, `RepairPage.cs`; `ProtectPage` có công tắc
  chặn ghi / chặn thực thi. Mọi chuỗi mới nằm trong `Strings.cs`.
- Dòng lệnh: `--devices`, `--events [n]`, `--repair <drive>` (chỉ chẩn đoán),
  `--defender <path>`.
- `SelfTest` mở rộng từ 22 lên 34 mục: JSON round-trip/từ chối JSON hỏng, luật
  mặc định/tùy chỉnh/hỏng, chấm điểm, pipeline detector, timeline, trust store,
  chặn sửa ổ hệ thống, đọc thử, marker sao lưu.
- `README.md` viết lại cho 2.0, ghi rõ giới hạn (không sửa boot sector/MBR,
  không format/carving, không VT/YARA, BadUSB chỉ heuristic, không named pipe).

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  462 KB.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 34/34 phép kiểm tra.`

Rà soát cuối (cùng ngày), đã sửa:

- `Program.Status()` in thêm trạng thái chặn ghi USB và chặn thực thi.
- `Guard.Install` đọc `guard.cfg` cũ và giữ `WriteProtect`/`BlockExec`; cài lại
  không còn âm thầm tắt hai tuỳ chọn đã bật trong GUI.
- `Uninstaller.DataEntries` xoá thêm `rules.json`, `devices.json` (+`.tmp`),
  `events.tsv` (+`.1`) và thư mục `repair`.
- `Guard.Uninstall` gọi `Policy.ApplyWriteProtect(false)` trước khi xoá
  `guard.cfg`/`repair`, tránh kẹt khoá ghi; `Paths.HardenRoot` khoá ACL
  `repair` cùng `quarantine`/`backup`/`bin`.

### 2026-11-17 — Giao diện Thiết bị / Sửa ổ và sửa lỗi nút phải bấm 2–3 lần

Nguyên nhân mất click: control tự vẽ đổi vị trí giữa lúc nhấn và lúc nhả chuột
(cuộn tới control khi focus, hoặc layout đổi), nên điểm nhả rơi ra ngoài và
Windows bỏ click. Sửa theo hướng không đổi hành vi:

- `Ui.SameSpot` + `PageBase.ScrollToControl` trả `AutoScrollPosition` khi
  `Control.MouseButtons != None`: không cuộn trang lúc đang giữ chuột.
- `FlatButton`, `LinkText`, `NavButton`, `SwitchRow`, `SelectBox`, `Segment`,
  `UsbListControl`, `DriveList`: nhớ control/dòng lúc nhấn, chấp nhận click khi
  nhả chuột nếu con trỏ chưa rời chỗ (`SameSpot`).
- `Controls.cs`: thêm `DeviceListControl` (giữ lựa chọn theo `DeviceInfo.Key`,
  chip tin cậy, chừa thanh cuộn), `EventListControl` (dòng thời gian, phím,
  bánh xe), `InfoField`/`InfoCard` (lưới nhãn–giá trị, đường kẻ mảnh).
- `DevicesPage`/`RepairPage` vẽ lại theo ngôn ngữ chung: tiêu đề mục + đường kẻ,
  số lượng cạnh tiêu đề, dải thông báo, nút chính/phụ, bảng thông tin có viền,
  khung kết quả một khối. Tin cậy/quên cập nhật mô hình tại chỗ rồi đọc lại
  `events.tsv` trên luồng nền, không quét WMI lại.
- Dải thông báo của trang Thiết bị luôn có chữ: khi không có cảnh báo hiện
  “Chưa thấy dấu hiệu bất thường…”, tránh khoảng trống 38 px giữa tiêu đề mục
  và danh sách. Không đổi ngưỡng, không đổi hành động chặn.
- `Icons.cs`: thêm biểu tượng “chip” (Thiết bị) và “cờ lê” (Sửa ổ); `Strings.cs`
  thêm toàn bộ câu mới dạng `T(vi, en)`. Giữ `Ctrl+1..7`, thứ tự tab,
  `AccessibleName`.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  476 KB, SHA256 `d7a29f08…e40858`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 34/34 phép kiểm tra.`
- Ảnh chụp trang Thiết bị / Sửa ổ ở 1016×720 (harness reflection, 32-bit
  PowerShell): bố cục khớp trang Bảo vệ, danh sách và khung kết quả không bị cắt.

### 2026-11-17 — Sửa ổ: thanh icon có tooltip, đọc thông tin tự động

Đã làm:

- Thanh thao tác trang Sửa ổ đổi từ 3 hàng nút chữ sang **một hàng icon** chia
  3 nhóm (Kiểm tra / Sao lưu / Sửa chữa). Mỗi nút 44×40, rê chuột hiện tooltip
  hai dòng: tên chức năng + giải thích; `AccessibleName`/`AccessibleDescription`
  giữ nguyên tên cho trình đọc màn hình. Nút "Sửa lỗi ổ (chkdsk /f)" vẫn là nút
  đỏ để cảnh báo thao tác phá huỷ. Góc phải có gợi ý "Rê chuột để xem tên".
- `FlatButton.IconName` vẽ icon (kèm chữ nếu có); `Icons.cs` thêm `eye`,
  `disk`, `save`, `folder`, `ban`. Ba nút ở trang Thiết bị (Tin cậy / Bỏ đánh
  dấu / Chặn) dùng icon + chữ để cùng ngôn ngữ thị giác.
- Chọn ổ (kể cả ổ đầu tiên khi mở trang) tự gọi `Repair.Inspect` trên luồng nền:
  thẻ thông tin hết trống, không cần bấm "Chẩn đoán" trước. Chỉ đọc, giữ nguyên
  toàn bộ giới hạn an toàn.
- Sau khi sao lưu thành công, dòng "Sao lưu" trong thẻ được vẽ lại từ dữ liệu đã
  có (`_lastInfo`), không chạy lại `fsutil`.
- `Repair.PreviewBackup`: bản tổng kết robocopy bị bản địa hoá nên khoá
  `files`/`bytes` không đọc được trên Windows tiếng Việt. Thêm `CountTree` dự
  phòng bằng `Tree.Walk` (300.000 mục / 60 giây) khi bản tổng kết không có số.
- Trang Sửa ổ không còn cuộn dọc ở cửa sổ mặc định (nội dung 628 px, khung nhìn
  636 px): bỏ 3 hàng nút, hạ chiều cao tối thiểu khung kết quả xuống 140 px.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → 479 KB, SHA256
  `6306ffb3…9c474`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 35/35 phép kiểm tra.`
  (thêm mục 30: đếm dự phòng cho xem trước sao lưu, exit 0).
- Ảnh chụp 1016×720: thanh icon + thẻ thông tin đầy đủ (NTFS, nhãn Storage,
  205,3/300,3 GB, trạng thái, sao lưu), không có thanh cuộn trang.

Ghi chú: `.github/workflows/build.yml` đã có từ 1.0 và chạy `build.ps1` +
`test.ps1` trên `windows-latest`, nên mục "CI chưa có workflow" trong danh sách
còn lại không còn đúng.

Còn lại (bản sau, không chặn 2.0): Raw Input + ETW cho BadUSB, named pipe có
xác thực, VirusTotal opt-in/YARA, sửa boot sector/MBR có đối chiếu mẫu sạch,
TestDisk/PhotoRec, ký số release, đổi tên repo.

### 2026-11-17 — README có ảnh chụp, phát hành 2.0

Đã làm:

- `README.md` viết lại theo hướng trưng bày: logo, tên, một câu mô tả, huy hiệu
  (build, release, MIT, Windows 7+, .NET 4.0, dung lượng), ảnh Tổng quan khổ
  rộng, bảng 2 ảnh (Thiết bị / Sửa ổ), rồi các mục ngắn. Giữ nguyên nội dung kỹ
  thuật và phần Giới hạn; cập nhật self-test **35/35**.
- Ảnh chụp trong `docs/screenshots/` (1000×680, giao diện tiếng Anh, thư mục dữ
  liệu cách ly `SHIELDGATE_HOME` tạm, 3 sự kiện mẫu): `overview.png`,
  `devices.png`, `drive-repair.png`. `docs/logo.png` lấy từ khung 256 px của
  `assets/app.ico`.
- Sửa lỗi trang Tổng quan: lần làm mới đầu chạy trước khi cửa sổ có handle nên bị
  bỏ và `_refreshing` kẹt `true`, khiến dòng Bảo vệ/Cách ly trống. Thêm
  `OnHandleCreated` → `RefreshAsync`, kiểm tra `IsHandleCreated` và đặt lại cờ
  khi luồng nền thoát sớm.

Bằng chứng:

- `build.ps1` → `dist\ShieldGate.exe` 479 KB, SHA256 `7cd4b81e…75110`.
- `test.ps1` → `Đạt 35/35 phép kiểm tra.`, exit 0.
- `overview.png`: dòng Bảo vệ hiện "Protection is off" + liên kết Customize,
  Cách ly hiện "No files in quarantine" (trước đó trống).

Phát hành: commit + push `main`, tag `v2.0`, GitHub release kèm
`dist\ShieldGate.exe` và ghi chú tiếng Anh ngắn.

### 2026-11-18 — Đối chiếu bản phân tích với mã, lập lộ trình 2.1–3.0

Đã làm:

- Đọc `docs/analysis-2.0.md` (bản phân tích 2.0) và kiểm chứng
  lại từng kết luận bằng mã hiện tại. Xác nhận đúng A1–A9, B1–B12, phần nợ kỹ thuật
  và phần còn thiếu; không phát hiện kết luận nào sai hướng.
- Đính chính ba chi tiết nhỏ: `AdsDetector` dùng `TreeItem.Size` (không phải
  `FileInfo`); `OfficeMacroDetector` bỏ qua tệp > 32 MB **và** chỉ đọc 8 MB đầu;
  `robocopy` được coi là thành công với mã thoát 0–7 nên giả thuyết "luôn exit ≥ 8"
  chỉ đúng khi thư mục hệ thống của ổ gây lỗi thật.
- Viết `docs/roadmap-2.1.md`: 23 mục chia 5 mốc, mỗi mục có tệp/điểm sửa, cách làm,
  phép kiểm tra mới, tiêu chí xong, phụ thuộc và ước lượng; kèm checklist kiểm chứng
  trên máy thật và danh sách quyết định cần chốt.
- Thêm mục 6 (lộ trình) vào `plan.md`; đổi mục nhật ký thành 7.

Bằng chứng (baseline trước khi sửa mã, cây làm việc ngày 18/11):

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 480 KB
  (SHA256 thay đổi sau mỗi lần sửa đang dở; lần đo của luồng việc kế tiếp ghi ở mục
  dưới).
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 37/37 phép kiểm tra.`,
  exit 0.

Ghi chú: cây làm việc đang có thay đổi chưa commit từ một luồng việc khác (trạng thái
`ProtectionOn`, giữ lựa chọn khi bật bảo vệ, nút Huỷ, thứ tự `SaveConfig` trong
`Guard.Configure`, +2 phép kiểm tra). Vì vậy B6 trong lộ trình được ghi là "đã sửa,
chỉ cần kiểm thử hồi quy"; các mục còn lại chưa bị đụng tới.

Chưa sửa mã nguồn. Việc tiếp theo: chốt ba quyết định ở mục 9 của lộ trình, rồi làm
W1 (đường dẫn tuyệt đối cho công cụ hệ thống) và W2 (marker sao lưu theo ổ thật).

### 2026-11-18 — Sửa nút Kiểm tra máy nuốt cú bấm; bật bảo vệ không tự bật chặn AutoRun

Đã làm:

- Nút chính trang Tổng quan giữ bề rộng ổn định giữa các trạng thái ("Kiểm tra
  máy" / "Huỷ" / "Bật bảo vệ"), nên cú bấm thứ hai không còn rơi ra ngoài nút khi
  nút co lại. Cú bấm vào "Huỷ" trong 1,5 giây đầu bị bỏ qua: trước đây bấm đúp
  hoặc bấm lại vì tưởng máy chưa phản hồi sẽ huỷ luôn lần kiểm tra vừa bắt đầu,
  trạng thái trở về như cũ nên phải bấm nhiều lần. Thêm `S.Overview.StateStopping`.
- `StatusInfo.ProtectionOn`: còn ít nhất một lớp đang chạy là đang bảo vệ, không
  đòi đủ bốn lớp như `FullyProtected`. Trang Bảo vệ, dòng Bảo vệ ở Tổng quan và
  `--status` dùng nó, nên tắt chặn AutoRun không còn bị hiện "chưa bật bảo vệ".
- `ProtectPage.ToggleAll` bật lại đúng lựa chọn đã lưu (`Guard.EnableConfig`):
  mục người dùng chủ động tắt thì ở yên; "Bật bảo vệ" luôn bật bảo vệ nền; chưa
  có `guard.cfg` thì dùng bộ mặc định (chặn AutoRun, chặn autorun.inf, bảo vệ
  Registry). Hộp xác nhận đếm đúng số thiết lập sẽ đổi.
- `Guard.Configure` ghi `guard.cfg` trước khi sửa registry (B6) và không ghi đè
  khi tắt toàn bộ bảo vệ, nên vòng nền 60 giây không áp lại phần vừa tắt và lần
  bật sau không bật lại mục đã tắt. Cập nhật dòng B6 trong
  `docs/roadmap-2.1.md`.
- Self-test thêm 12b (`EnableConfig` giữ lựa chọn đã lưu, thiếu `guard.cfg` thì
  dùng mặc định) và 12c (`ProtectionOn` đúng khi thiếu một lớp); `total` 35 → 37.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  480 KB, SHA256 `5a0a2c2fbf571042…0707160be5`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 37/37 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: cần bấm thử nút Kiểm tra máy và tắt/bật công tắc
AutoRun trên bản có bảo vệ nền chạy quyền SYSTEM (không chạy được trong self-test
vì phải đổi cấu hình máy).

### 2026-11-18 — W1 + W2: đường dẫn công cụ tuyệt đối, marker sao lưu theo ổ

Đã làm:

- **W1 (A1).** Thêm `Native.SysExe` (thứ tự: `%WINDIR%\Sysnative` → `Environment.SystemDirectory`
  → `%WINDIR%\System32` → `%WINDIR%\System32\WindowsPowerShell\v1.0`), `Native.ShellExe`
  (`%WINDIR%`), và `Native.ResolveTool` để `Native.Run` tự phân giải
  tên trần trước khi `CreateProcess` tìm trong thư mục chứa ShieldGate. `Sysnative` là
  đường duy nhất chạy được `pnputil.exe` từ tiến trình x86 trên Windows 64-bit
  (`System32` bị chuyển hướng sang `SysWOW64`, nơi không có `pnputil`); `powershell.exe`
  nằm ở `WindowsPowerShell\v1.0` nên được tra riêng — nếu để rơi về tên trần thì đúng
  là lỗ hổng W1 muốn đóng. Thay toàn bộ
  điểm gọi: 19 chỗ `schtasks` trong `Guard.cs`, `reg`/`schtasks` trong `Scan.cs`,
  `fsutil`/`robocopy`/`chkdsk` trong `Repair.cs`, `icacls` trong `Core.cs`, `pnputil`
  trong `Devices.cs`, `powershell`/`cmd` trong `Uninstaller.cs`, `explorer.exe` ở
  `AboutPage`/`Dialogs`/`LogPage`/`RepairPage`. Kết quả: không còn lời gọi tên trần.
- Thêm `tools/check-bare-tools.ps1` (quét `Native.Run("`, `Process.Start("`,
  `ProcessStartInfo("` với `.exe"`) và gắn vào `build.ps1` trước `csc`; vi phạm thì
  build dừng. Đã thử cố tình vi phạm trong thư mục tạm → cổng trả exit 1.
- **W2 (A2, A4, B10, B11).** Marker sao lưu đổi từ `backup-<chữ ổ>.ok` (chỉ theo
  chữ ổ) sang `backup-<chữ ổ>-<serial>.ok` với nội dung `key=value` gồm
  `state=complete`, `volumeSerial`, `volumeBytes`, `source`, `dest`, `when`, `files`,
  `bytes`. `Repair.FindMarker` chỉ chấp nhận marker: đúng serial, đúng dung lượng
  volume, `state=complete`, còn hạn 24 giờ, `dest` nằm trong thư mục `repair\` và còn
  tệp. Marker 2.0 (`backup-E.ok`) bị bỏ qua và bị xoá sau lần sao lưu mới.
- `Repair.CheckBackup(drive, out reason)` trả lý do cụ thể (`BackupOtherVolume`,
  `BackupExpired`, `BackupMissing`, `NotReady`, …); `RepairPage` hiện đúng lý do thay
  vì câu chung, và dùng nó ở cả nút sao lưu lẫn `chkdsk /f`.
- `RefuseReason(drive, write, out reason)`: ghi (sao lưu, `chkdsk /f`) chỉ cho
  `DriveType.Removable`; chẩn đoán chỉ đọc vẫn cho ổ cố định phụ. `Backup`,
  `PreviewBackup` và `RunChkdsk(fix)` dùng biến thể này.
- `robocopy`: thêm `/XD "System Volume Information" "$RECYCLE.BIN"` cho cả xem trước
  và sao lưu (trước đây thư mục hệ thống của ổ làm exit ≥ 8 và marker không bao giờ
  được ghi), kiểm dung lượng trống trước khi chép, và chỉ ghi marker khi mã thoát
  0–7 **và** (nguồn rỗng hoặc đích có tệp).
- Self-test: `total` 37 → 39. Thêm "công cụ hệ thống dùng đường dẫn tuyệt đối"
  (`SysExe` phải ra đường dẫn tuyệt đối, tồn tại với `cmd`, `schtasks`, `reg`,
  `icacls`, `powershell`; `Run("cmd.exe")` vẫn chạy) và "marker quá 24
  giờ bị từ chối"; viết lại mục marker cho đúng serial, sai serial, marker cũ.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  488 KB, SHA256 `d1220b181e971aaa…608a46d4` (PE mang dấu thời gian nên hash đổi
  mỗi lần dựng, chỉ dung lượng là ổn định).
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 39/39 phép kiểm tra.`,
  exit 0.
- `tools\check-bare-tools.ps1 .` → exit 0; với tệp cố tình vi phạm → exit 1.

Thay đổi hành vi cần ghi trong ghi chú phát hành: marker 2.0 hết hiệu lực nên người
dùng phải **sao lưu lại** trước khi bấm `chkdsk /f`; sao lưu và sửa chỉ dành cho ổ
rời. Chưa kiểm chứng trên máy thật: sao lưu + `chkdsk /f` trên USB thật (self-test
không quét ổ thật).

### 2026-11-18 — W3: gỡ cài đặt không xoá nhầm, mặc định giữ cách ly

Đã làm:

- `Paths.EnsureDirs` ghi tệp `.shieldgate` (`app`, `version`, `created`) vào mỗi thư
  mục do ShieldGate tạo và vào thư mục gốc dữ liệu. `Paths.MarkerIn(dir)` là nơi duy
  nhất dựng đường dẫn marker.
- `Uninstaller` không còn xoá theo tên: `Owned(dir)` chỉ đúng khi có marker, hoặc
  khớp chữ ký bản 2.0 (`log\shieldgate.log`, `quarantine\*_*\manifest.csv`,
  `bin\ShieldGate.exe`, `repair\*.ok`/`repair\backup-*`), hoặc thư mục rỗng. Khớp
  chữ ký thì `Adopt` ghi marker rồi mới xoá (để bản 2.0 nâng cấp lên vẫn gỡ được);
  không khớp thì bỏ qua và ghi WARN (`S.Log.UninstallSkip`).
- `DataEntries(keepData)` và `RemoveData(keepData, includeSetup)`: mặc định **giữ**
  `quarantine` và `repair`, chỉ xoá nhật ký, luật, kho thiết bị, timeline, cấu hình và
  `bin`; chỉ dọn thư mục gốc khi đã xoá hết. `ScheduleCleanup(keepData)` theo cùng quy
  tắc nên tiến trình PowerShell dọn sau không xoá phần vừa giữ.
- `ConfirmDialog.Ask(...)` có thêm tham số công tắc (`SwitchRow`) và trả trạng thái
  qua `out bool`. Hộp thoại gỡ dùng nó với "Giữ cách ly và bản sao lưu ổ" mặc định
  bật, chi tiết hiện dung lượng ước tính; `Uninstaller.KeptSizes` quét tối đa 600 ms
  nên không treo giao diện.
- CLI: `ShieldGate.exe --uninstall` giữ dữ liệu (mặc định), `--purge-data` xoá hết;
  thông báo riêng cho từng trường hợp (`S.Cli.UninstallKept`/`UninstallDone`), và
  `clean=false` không còn bị báo nhầm là "còn tệp chưa dọn" khi người dùng chủ động
  giữ dữ liệu.
- Self-test thêm mục 33 (lồng trong sandbox riêng, không đụng dữ liệu thật): nhận diện
  chữ ký 2.0, thư mục lạ không bị coi là của ShieldGate, `DataEntries` bỏ/giữ đúng
  `quarantine`+`repair`, `RemoveData(true)` giữ cách ly và bản sao lưu, `RemoveData(false)`
  xoá chúng mà vẫn để nguyên thư mục của người dùng. `total` 39 → 40.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 494 KB.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 40/40 phép kiểm tra.`,
  exit 0.
- `tools\check-bare-tools.ps1 .` → exit 0.

Chưa kiểm chứng trên máy thật: bấm Gỡ cài đặt với hộp thoại mới (cần quyền quản trị
và sẽ thật sự gỡ ShieldGate khỏi máy), và `--uninstall --purge-data` trên bản cài
thật. Cần thử trên máy ảo trước khi phát hành.

### 2026-11-19 — W4: manifest cách ly không còn được tin tuyệt đối

Đã làm:

- `Quarantine.RestoreBatch` có thêm bộ đếm `refused` (giữ overload ba tham số cho chỗ
  gọi cũ). Mỗi dòng manifest phải qua hai cửa: nguồn (`QuarantinePath`) nằm trong đúng
  thư mục lô, đích (`OriginalPath`) tuyệt đối, có tên tệp, và không nằm trong vùng
  cách ly, `bin` của ShieldGate, `%WINDIR%`, `Program Files`, `Program Files (x86)`.
- `Core.Canonical` tự gộp `.`/`..` bằng chuỗi, không dùng `Path.GetFullPath`: trên
  .NET Framework 4.0 hàm đó ném `PathTooLongException` với đường dẫn > 260 ký tự —
  đúng loại đường dẫn self-test 25 khôi phục. `Inside` so sánh sau khi chuẩn hoá và
  thêm dấu `\` nên `C:\Temp2` không bị coi là nằm trong `C:\Temp`.
- Dòng bị từ chối: `Log.Write(..., "WARN")` + `Events.Add("scan", "WARN", path, reason)`
  với lý do cụ thể (`S.Log.RestoreOutside/RestoreSystemFolder/RestoreAppFolder/RestoreBadPath`).
- UI: `Dialogs` (trang Cách ly) và `UsbPage` hiện thêm số tệp bị từ chối và **không
  xoá lô cách ly** khi còn dòng bị từ chối; log ghi cả ba con số.
- Không chặn cả thư mục chứa exe: bản chạy từ USB quét chính USB đó, khôi phục về gốc
  USB là hợp lệ; tệp đã tồn tại luôn bị bỏ qua nên không có chuyện ghi đè.
- Self-test thêm mục 34: manifest có dòng nguồn `..\..` ra ngoài lô và dòng đích trong
  `%WINDIR%` → 1 tệp hợp lệ được khôi phục, 2 dòng bị từ chối, tệp thoát ra ngoài
  không hề được tạo. `total` 40 → 41.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 498 KB.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 41/41 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: khôi phục một lô thật từ USB (self-test dùng USB giả
trong `%TEMP%`).

### 2026-11-19 — F1–F5: sửa lỗi từ nhật ký máy bệnh 03/11

Nguồn: `log shieldgate tren may benh 2.log` (1.746 dòng, 14:27–14:34), đọc lại sau
khi W1–W4 xong. Kết luận: không phải lỗi "chạy chậm" mà là ba lỗi làm mất dữ liệu và
làm nhật ký không dùng được.

Đã làm:

- **F1 — khoá dọn ổ dùng chung.** Nguyên nhân gốc: GUI (`PcFix`) và tác vụ nền cùng
  quét rồi cùng cách ly một thư mục giả; bên này di chuyển tệp, bên kia ghi
  "Could not quarantine … The system cannot find the file specified" (286 dòng ERROR
  trong log, xen kẽ tiếng Việt/tiếng Anh của hai tiến trình). `src/core/CleanLock.cs`
  mở `clean.lock` trong `Paths.Root` với `FileShare.None` (thay cho named pipe, đã
  cân nhắc ở mục 6 lộ trình); `Scan.Apply` giữ khoá suốt lượt cách ly,
  `QuarantineTree` vào lại được cùng luồng, `Guard.CleanRoot/ScanAndClean` bỏ qua
  vòng khi khoá bận.
- **F2 — không cách ly tài liệu người dùng.** Log cho thấy 600+ tệp `.doc/.docx/.pdf`
  tên Base64 trong `D:\Docusment\3\22AB820634F924A0` là tài liệu thật bị virus đổi
  tên, nhưng bản cũ cách ly cả thư mục. Nay `QuarantineTree` chỉ lấy tệp payload theo
  `Rules.PayloadExts` (mới, có trong `rules.json`), `UsbRestore.IsPayloadFile` dùng
  chung với `HasRiskyContent`; tài liệu ở lại, thư mục giả còn dữ liệu thì không xoá,
  log INFO "Giữ lại N tệp…".
- **F3 — tệp đã biến mất không phải lỗi.** `Quarantine.Add` trả `error` rỗng khi
  nguồn đã mất (hoặc mất giữa lúc sao chép), nên `Apply`/`QuarantineTree`/`Fix`
  không ghi ERROR và không trừ vào `failed`; `Fix` coi tệp đã được xử lý là thành công.
- **F4 — bộ đếm không âm, không vỏ batch rỗng.** `Guard` lấy số tệp từ giá trị
  `Apply` trả về thay vì `auto.Count - failed` (log có `quarantined -67`); `Apply`
  xoá lô cách ly vừa tạo nếu không có tệp nào vào (vòng nền 10 giây/lần sẽ sinh vô
  số thư mục trống khi thư mục giả chỉ còn tài liệu).
- **F5 — ngôn ngữ nhật ký.** Tác vụ `SYSTEM` không có `%LOCALAPPDATA%\ShieldGate\setup.ini`
  nên rơi về tiếng Anh. Ghi ngôn ngữ vào `settings.txt` trong thư mục dữ liệu chung và
  đọc lại khi `Bootstrap.Language` trống.

Self-test: thêm mục 35 (thư mục giả giữ `.docx` Base64, chỉ mất `1.exe`/`adback.dat`),
36 (tệp biến mất → `error` rỗng), 37 (khoá loại trừ luồng khác, cùng luồng vào lại
được), 38 (không còn vỏ batch rỗng). `total` 41 → 45.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 504 KB,
  cổng `tools/check-bare-tools.ps1` đạt.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 45/45 phép kiểm tra.`,
  exit 0.

Chưa làm (ghi ở mục 2b lộ trình): khôi phục tên gốc cho tài liệu Base64 đã bị bản cũ
thu gom, và hiện rõ trên trang Ổ USB khi thư mục giả còn dữ liệu. Chưa kiểm chứng
trên máy bệnh thật.

### 2026-11-19 — W5: reg export, CSV injection, phạm vi Unhide

Ba lỗi nhỏ nhưng cùng loại "sửa mà không có đường lùi", đã sửa cùng một lượt.

Đã làm:

- **A6 — `reg export` phải chứng minh được.** `PcScan.BackupReg` đổi thành
  `bool BackupReg(hive, path, out error[, out file])`: chỉ trả `true` khi `reg.exe`
  thoát 0 **và** tệp `.reg` có thật, kích thước > 0. `PcScan.Fix` dừng từng mục
  registry (`RunValue`, `WinlogonShell/Userinit`, `AppInit`, `MountPoint`, `LnkAssoc`,
  `RegHealth`) khi không có bản sao, thay vì xoá/ghi tiếp như trước.
  `RegistryGuard.Ensure` cũng vậy và bỏ khoá khỏi tập "đã sao lưu" để lần kiểm tra
  sau thử lại. Bỏ nhánh `if (!Native.IsAdmin()) return;`: `HKCU` export được khi
  không nâng quyền, còn `HKLM` không nâng quyền thì dừng an toàn (trước đây vẫn thử
  xoá rồi thất bại).
- **A8 — CSV injection.** `Events.Csv` thêm `'` trước ô mở đầu bằng `=` `+` `-` `@`
  (và tab/CR) rồi mới bọc nháy kép. Chỉ ảnh hưởng tệp CSV xuất cho người dùng; TSV
  nội bộ và `manifest.csv` giữ nguyên vì ô manifest là đường dẫn tuyệt đối, thêm `'`
  sẽ làm hỏng khôi phục.
- **B4 — phạm vi `Unhide`.** Thêm `UnhideScope.VirusOnly` (mặc định, dùng sau tự quét
  và sau cách ly) chỉ gỡ `Hidden` khi mục có cả `Hidden` + `System` — dấu hiệu
  `attrib +h +s` của worm; `UnhideScope.AllHidden` cho nút "Hiện tệp" do người dùng
  bấm. Cả hai bỏ qua `desktop.ini`, `autorun.inf`, `System Volume Information` và
  tên bắt đầu bằng `$`. Nhờ vậy `desktop.ini` thật và thư mục người dùng tự ẩn
  không còn bị đổi thuộc tính sau mỗi vòng quét nền.

Kiểm thử: thêm mục 39 (CSV có ô `=cmd|' /C calc'!A0` → xuất thành `'=cmd…`), 40
(`reg export` khoá không tồn tại → `false` + lý do; `HKCU\Control Panel\Desktop` →
`true` + tệp > 0 byte), 41 (`desktop.ini` và `mine.docx` Hidden giữ nguyên,
`worm.doc` Hidden+System được gỡ, `$RECYCLE.BIN` giữ nguyên). Mục 9 đổi sang gọi
`UnhideScope.AllHidden` cho đúng nghĩa nút người dùng bấm. `total` 45 → 48.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 508 KB,
  cổng `tools/check-bare-tools.ps1` đạt.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 48/48 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: mã thoát `reg export` khi khoá bị AV khoá quyền, và
`Unhide` trên ổ có `desktop.ini` do Windows tạo (self-test dùng tệp giả trong `%TEMP%`).

### 2026-11-19 — W6: `rules.json` chỉ đọc từ thư mục dữ liệu, merge theo phiên bản

Đã làm:

- **A9 — chỉ một nguồn luật.** `Rules.CandidatePath()` luôn trả `Paths.RulesFile`.
  Nếu có `rules.json` cạnh tệp chạy thì ghi WARN một lần mỗi tiến trình
  (`S.Log.RulesBesideIgnored`) rồi bỏ qua. Lý do: bản chạy từ USB nằm trên ổ của
  người khác, tệp cạnh exe có thể bị thay mà người dùng không biết.
- **Merge thay vì thay thế.** `FillSet` (clear rồi nạp) đổi thành `MergeSet` (chỉ
  thêm). Tệp v2 thiếu `payloadExts` vẫn có đủ đuôi mới; liệt kê đuôi khác không xoá
  được `.exe`, `.dll`, `.lnk` khỏi bộ mặc định. `scoreThreshold` kẹp 1–10, trọng số
  kẹp 0–10; `CurrentVersion` và `DefaultJson` lên 3. Tệp cũ hơn chỉ ghi INFO
  "đã bổ sung mặc định", không ghi đè tệp người dùng.
- **`--import-rules <tệp> --yes`.** `Rules.Import` kiểm tra tệp ≤ 1 MB và parse
  được, giữ bản cũ thành `rules.json.bak`, ghi đè rồi `Reload`. Thiếu `--yes` chỉ in
  hướng dẫn; lệnh nằm trong `NeedsAdmin` nên tự nâng quyền (thư mục dữ liệu bị khoá
  ACL khi bảo vệ bật). `S.Cli.Usage` và README đã thêm lệnh.
- **Dọn khi gỡ cài đặt.** `Uninstaller.DataEntries` thêm `rules.json.bak` và
  `clean.lock` (khoá dọn ổ của F1) để `PruneEmpty` xoá được thư mục dữ liệu.

Kiểm thử: mục 21 đổi sang kiểm merge (`scriptExts: ["abc"]` vẫn còn `.vbs`, trọng số
99 → 10, `version` = 3, `payloadExts` mặc định còn). Thêm mục 42: tệp `rules.json`
cạnh exe có `besideonly` bị bỏ qua; nhập tệp v2 thiếu `payloadExts` vẫn có `.dll`;
`rules.json.bak` tồn tại; nhập tệp không tồn tại bị từ chối. `total` 48 → 49.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 512 KB,
  cổng `tools/check-bare-tools.ps1` đạt.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 49/49 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: `--import-rules` khi bảo vệ đang bật (ACL chỉ cho
Administrators/SYSTEM ghi) và khi tệp nguồn nằm trên USB chỉ đọc; self-test chỉ gọi
`Rules.Import` trong thư mục tạm.

### 2026-11-20 — Máy bệnh 05/11: virus hồi sinh sau khi dọn, tác vụ hẹn giờ

Bằng chứng từ `D:\download\LOG SHIELDGATE MAY BENH 2\shieldgate.log` (828 dòng,
05/11 02:31–02:58):

- Ổ F: được dọn rồi bị nhiễm lại **50 lần trong 27 phút** (02:31:55–02:58:41),
  khoảng cách trung vị 35 giây. Nguồn lây là tiến trình thường trú trên máy, không
  phải con USB: rút USB ra cũng không hết.
- `C:\ProgramData\LMIGuardianUrX\adback.dat` được thả lại đúng 3 lần
  (02:32:36, 02:39:16, 02:58:10), cùng nhịp với `Task DRPNPS` được "sửa" 3 lần.
  Tác vụ chỉ bị `/Change /DISABLE`; tác vụ đã tắt vẫn được liệt kê lại ở lần quét
  sau, nên người dùng thấy như virus tạo lại tác vụ.
- `mshta.exe` bị dừng một lần (02:32:35) nhưng không có bước nào nối tiến trình với
  thư mục payload, nên lần sau nó chạy lại từ tệp chưa bị cách ly.
- Ổ D: không ghi được: WARN lặp mỗi 10 giây, làm nhật ký bị nhiễu.

Đã làm:

- **Sửa theo lượt.** `PcScan.Fix` sắp xếp theo `FixPass`: 0 tiến trình, 1 tác vụ/WMI,
  2 startup/registry, 3 tệp. Trước đây sửa theo thứ tự quét nên tệp bị xoá trước khi
  tiến trình giữ nó bị dừng.
- **Tác vụ: sao lưu rồi xoá.** `TaskSuspicious` bỏ qua tác vụ `<Enabled>false</Enabled>`;
  `RemoveTask` xuất XML vào `Paths.BakDir` rồi `schtasks /Delete /F`. Nếu sao lưu
  thất bại thì chỉ tắt và ghi WARN, không xoá mất đường lùi.
- **Nối tiến trình với payload.** `LinkProcessesToPayloads` tìm tiến trình đang chạy
  từ thư mục chứa tệp payload đã biết hoặc thư mục giả, thêm tiến trình vào danh
  sách sửa và đưa cả tệp chạy vào diện cách ly sau khi tiến trình dừng.
- **Cảnh báo nhiễm lại.** `ReinfectWatch` đếm số lần cùng một ổ được dọn; lần thứ 3
  trong 10 phút thì ghi sự kiện, hiện balloon ở khay và ghi log, thay vì dọn im lặng.
- **Nhật ký quét máy.** `LogFindings` ghi từng mục (loại, tên, chi tiết) để lần sau
  đọc log biết virus nằm ở đâu; trước đây chỉ ghi "N mục đáng ngờ".
- **Giảm nhiễu ổ chỉ đọc.** `Guard.SkipReadOnly` nhớ ổ không ghi được trong 5 phút
  (thử lại sau đó) thay vì WARN mỗi 10 giây.

Kiểm thử: thêm mục 43–45 — tác vụ bật + lệnh nguy hiểm mới báo, tác vụ tắt và lệnh
lành không báo; `ReinfectWatch` báo đúng lần thứ 3, không báo lặp, đếm riêng từng ổ,
bỏ qua lượt không cách ly tệp; `FixPass` đúng thứ tự tiến trình → tác vụ → registry
→ tệp. `total` 49 → 52.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 522 KB,
  cổng `tools/check-bare-tools.ps1` đạt.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 52/52 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: không có mẫu sống nên chưa biết tiến trình thường trú
thật sự là gì; `LinkProcessesToPayloads` chỉ nối được khi payload còn nằm trong thư
mục mà tiến trình đang chạy, và việc dừng tiến trình vẫn phụ thuộc quyền Administrator.
Máy bệnh vẫn nên quét offline bằng Defender sau khi chạy "Kiểm tra máy" và khởi động lại.

### 2026-11-20 — Bổ sung: tác vụ ẩn, dịch vụ/driver, cảnh báo "sửa rồi vẫn nhiễm"

Ba câu hỏi còn lại từ hiện trường: phần mềm có tự xử lý được không, Task Scheduler
không thấy tác vụ thì sao, và xoá tác vụ rồi vẫn hồi sinh thì sao.

Đã làm:

- **Quét cả `\Microsoft\...`.** `WalkTasks` không còn bỏ qua cả cây
  `C:\Windows\System32\Tasks\Microsoft\...` — nơi Task Scheduler ẩn tác vụ và
  người dùng thường không thấy. Với nhánh này chỉ báo khi lệnh còn trỏ vào thư mục
  người dùng ghi được (`AppData`, `Temp`, `ProgramData`, `Users\Public`,
  `Downloads`, `Desktop`, `Documents`), script host (`mshta`/`wscript`/`cscript`,
  `.vbs/.js/.hta`) hoặc tệp tên số; PowerShell mã hoá của tác vụ Windows không bị
  báo nhầm. Chi tiết có thêm `[hidden]` khi XML đặt `<Hidden>true</Hidden>`.
- **Quét dịch vụ/driver.** `ScanServices` đọc
  `HKLM\SYSTEM\CurrentControlSet\Services`, bỏ qua `ImagePath` trong Windows,
  `Program Files`, và đặc biệt `ProgramData\Microsoft\Windows Defender` (engine
  Defender nằm ở đó, không phải virus); báo dịch vụ chạy từ
  `ProgramData`/`AppData`/`Temp`/`Public`, đường dẫn trần, hoặc file ngoài hai
  thư mục hệ thống không có chữ ký.
  `ServiceImagePath` hiểu `\??\`, `\SystemRoot\`, `%SystemRoot%`, ngoặc kép và
  tham số `svchost.exe -k`.
- **Sửa dịch vụ có đường lùi.** `RemoveService` xuất khoá Registry vào
  `Paths.BakDir` rồi `sc stop` + `sc delete`; không sao lưu được thì không đụng.
  Tệp chạy của dịch vụ được thêm thành `DroppedFile` để cách ly ở lượt 3, sau khi
  dịch vụ đã dừng ở lượt 0.
- **Leo thang khi sửa rồi vẫn nhiễm.** `PcScan.Fix` ghi mốc `pcfix.time`; khi
  `ReinfectWatch` báo tiếp trong 60 phút, log ghi ERROR và balloon đổi thành
  "đã sửa máy nhưng ổ vẫn nhiễm lại — nguồn có thể là driver/rootkit, hãy quét
  Microsoft Defender Offline", thay vì lặp lại lời khuyên sửa máy.
- **Tự quét nguồn lây khi phát hiện vòng lặp.** `Guard.HuntSource` chạy
  `PcScan.Detect` trên luồng nền (chỉ đọc, tối đa một lần mỗi 10 phút) ngay khi
  `ReinfectWatch` báo, rồi ghi từng mục bằng `LogFindings`. Nhờ vậy nhật ký có
  tên tiến trình/tác vụ/dịch vụ cụ thể, kể cả tác vụ ẩn dưới `\Microsoft\...`;
  không tự sửa vì quy tắc phải có xác nhận của người dùng.

Kiểm thử: thêm mục 45–46 (tác vụ `\Microsoft\`: PowerShell mã hoá bị bỏ qua,
lệnh trỏ `ProgramData\...\1.exe` bị báo; dịch vụ: `svchost`/`Program Files` bỏ qua,
`ProgramData`/`Public`/tên trần bị báo, `ProgramData\Microsoft\Windows Defender`
bị bỏ qua, đọc đúng `\SystemRoot\`); mục 47 kiểm `FixPass(Service) == 0`.
`total` 52 → 54.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`, 532 KB.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 54/54 phép kiểm tra.`,
  exit 0.

Giới hạn: đây vẫn là công cụ user-mode. Nếu nguồn lây là driver kernel/rootkit
hoặc tiến trình được bảo vệ, ShieldGate chỉ phát hiện dấu vết và báo; việc loại bỏ
phải làm bằng Microsoft Defender Offline hoặc môi trường WinPE. Không có mẫu sống
trên máy phát triển nên chưa kiểm chứng việc `sc delete` trên dịch vụ thật.

### 2026-11-21 — W7: xác minh chữ ký bằng WinVerifyTrust, hoàn tất 2.1.0

Đã làm:

- **`src/core/Signature.cs` mới.** `Signature.Verify` trả `Unsigned / Signed /
  Invalid / Error`; gọi `WinVerifyTrust` với `WTD_UI_NONE`, `WTD_REVOKE_NONE`,
  `WTD_CACHE_ONLY_URL_RETRIEVAL` và luôn đóng state bằng `WTD_STATEACTION_CLOSE`.
  Cấu trúc `WINTRUST_DATA` dừng ở `dwProvFlags` (layout Windows 7); Windows 8+
  chấp nhận `cbStruct` nhỏ hơn nên không cần `pSignatureSettings`.
- **Catalog cho tệp hệ thống.** Nhiều tệp Windows không nhúng chữ ký mà nằm trong
  catalog (`notepad.exe`, `cmd.exe`, driver). Sau khi thử chữ ký nhúng, chỉ với
  `%WINDIR%` và `Program Files` mới chạy
  `CryptCATAdminAcquireContext` → `CryptCATAdminCalcHashFromFileHandle` →
  `CryptCATAdminEnumCatalogFromHash` → `CryptCATCatalogInfoFromContext` rồi
  `WinVerifyTrust` với `WTD_CHOICE_CATALOG`. Exe trên USB không bị băm thêm.
- **`Detect.IsSigned` dùng kết quả xác minh**, không còn
  `X509Certificate.CreateFromSignedFile`: chữ ký giả, hết hạn, bị thu hồi hoặc
  chuỗi tin cậy đứt đều tính là không ký. Không có lời gọi mạng.
- `AssemblyInfo` lên `2.1.0.0` cho mốc 2.1.0.
- Self-test 48: tệp văn bản → `Unsigned` và `Detect.IsSigned` false; chọn
  `notepad.exe` / `cmd.exe` / `explorer.exe` đầu tiên có trên máy → `Signed`
  (bỏ qua phần này nếu máy không có tệp nào). `total` 54 → 55.
- README: thêm mục "Signatures are verified", cập nhật self-test 55/55 và phạm vi
  kiểm tra.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  536 KB, SHA256 `c0794f243273d086…e09cc5205b39`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 55/55 phép kiểm tra.`,
  exit 0.
- Máy thử: `notepad.exe` và `cmd.exe` trả `TRUST_E_NOSIGNATURE` khi chỉ kiểm tra
  chữ ký nhúng nhưng catalog trả `0`; `explorer.exe` có chữ ký nhúng trả `0`.

Chưa kiểm chứng trên máy thật: Windows 7 (layout `WINTRUST_DATA` không có
`dwUIContext`), máy offline thiếu catalog của Windows, và chữ ký bị thu hồi thật.
Self-test chỉ bỏ qua phần tệp hệ thống khi máy không có `notepad.exe`/`cmd.exe`/
`explorer.exe`. Mốc 2.1.0 đã đủ W1–W7; việc tiếp theo là W8 (đọc đường dẫn tiến
trình 64-bit) mở mốc 2.1.1.

### 2026-11-21 — W8: đọc đường dẫn tiến trình 64-bit

Đã làm:

- **`Native.ProcessImagePath(pid)`** dùng `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`
  + `QueryFullProcessImageNameW` + `CloseHandle`; trả chuỗi rỗng khi tiến trình đã
  thoát, thiếu quyền hoặc PID hệ thống, không ném lỗi. Không còn đọc
  `Process.MainModule` trên đường nóng — bản build x86 không đọc được trường này
  của tiến trình 64-bit nên trước đây bỏ qua âm thầm.
- Thay ba điểm gọi: `Policy.StopRemovableProcesses` (dừng tiến trình chạy từ ổ
  rời), `Guard.WaitGuardExit` (chờ bản nền nhả tệp) và `Scan` (kiểm tra đường dẫn
  trước khi dừng tiến trình theo yêu cầu sửa). Ở `Scan`, nếu không đọc được đường
  dẫn thì giữ hành vi cũ: chỉ kiểm tra tên tiến trình rồi mới dừng.
- Self-test 49: đường dẫn tiến trình hiện tại khớp `Assembly.GetEntryAssembly().Location`;
  gọi PID 4 (System) không ném. `total` 55 → 56.
- README: thêm mục "64-bit processes are visible to the 32-bit build", cập nhật
  56/56.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  538 KB, SHA256 `3523b9f4ec3f72a7…f446df938c`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 56/56 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: dừng một tiến trình 64-bit thật đang chạy từ USB
(cần quyền quản trị và USB thử); PID 4 có thể trả đường dẫn hoặc rỗng tùy quyền,
self-test chỉ yêu cầu không ném. Việc tiếp theo: W9 (phân loại bàn phím ngoài).

### 2026-11-21 — W9: phân loại bàn phím trong/ngoài theo bus

Đã làm:

- `Devices.IsInternalKeyboard(string)` tách khỏi mô hình `DeviceInfo` để kiểm thử
  bằng chuỗi PNP mẫu. Quy tắc: `ACPI\`, `ROOT\`, `SWD\` là trong; `USB\`,
  `BTHENUM\`, `BTHLEDEVICE\`, `BTH\` là ngoài; `HID\` có `VID_` là ngoài (USB HID
  hoặc Bluetooth HID), `HID\` không có `VID_` là trong (I2C HID như
  `MSFT0001`, `ELAN0001`). Chuỗi lạ mặc định là trong để tránh báo động giả.
- `MarkComposite` và `DeviceScanResult.NewKeyboard` dùng chung quy tắc mới nên
  bàn phím USB/Bluetooth ngoài mới kích hoạt được cảnh báo BadUSB/composite.
- Self-test 50: 8 chuỗi PNP mẫu — ACPI, ROOT, SWD, I2C HID (trong); USB, USB HID,
  Bluetooth HID, composite (ngoài). `total` 56 → 57.
- README: thêm mục "External keyboards are recognised", cập nhật 57/57.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  539 KB, SHA256 `7802f83af3469cd9…ec3eaa4`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 57/57 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: cắm bàn phím USB/Bluetooth ngoài và xem `--devices`;
laptop có bàn phím nội bộ nối qua USB hoặc I2C HID có `VID_` có thể bị xếp là ngoài.
Đây vẫn là heuristic, không phải chặn BadUSB. Việc tiếp theo: W10 (ngưỡng điểm và
luật trong `rules.json`).

### 2026-11-21 — W10: ngưỡng điểm và mẫu nhận diện vào rules.json

Đã làm:

- **Ngưỡng điểm có hiệu lực (AGENTS điều 7).** `UsbScan.Detect` không còn nâng điểm
  của mục `High` lên ngưỡng; thay vào đó `High` bị hạ khi `Score < ScoreThreshold`.
  Cùng một tệp `.pdf.exe` vẫn là `High` ở ngưỡng mặc định 3 nhưng chỉ còn cảnh báo
  khi người dùng đặt `scoreThreshold` 10. Không cần chỉnh trọng số vì mọi mã
  `Strong` hiện có đều có trọng số ≥ 3.
- **Mẫu nhận diện đọc từ `rules.json` (AGENTS điều 8).** `RulesData` thêm
  `dangerExes`, `systemNames`, `payloadNames`, `payloadHashes`, `payloadSizes`,
  `scriptMarkers`, `highPatterns`, `medPatterns`. `Detect.cs` chỉ còn thuộc tính
  chuyển tiếp tới `Rules.Current`; ba regex cấu trúc (tham số shortcut, autorun)
  vẫn nằm trong mã vì là logic đọc tệp, không phải tên/đuôi/ký tự.
  `RulesData.CurrentVersion` 3 → 4; tệp cũ được merge, không ghi đè.
- **Giữ nguyên phạm vi bộ đọc script 1.0.** `scriptExts` sẵn có rộng hơn (có
  `.bat`, `.ps1`, `.scr`…), nên thêm khoá riêng `scriptScanExts` cho đúng bộ cũ
  (`.vbs .vbe .js .jse .wsf .hta`) để việc mở rộng luật không đổi cách phân loại
  tệp trên ổ.
- **Regex hỏng bị bỏ qua.** `MergePatterns` thử biên dịch từng mẫu, mẫu sai hoặc quá
  500 ký tự bị bỏ để một dòng lỗi không làm hỏng cả tệp luật.
- **Xoá `Devices.SetEnabled` (AGENTS điều 13).** Mã chết, không nơi gọi, dùng
  `pnputil` chỉ có trên Windows 10 1809+.
- Self-test 29 mở rộng: JSON nạp được `dangerExes`, `payloadSizes`, `scriptMarkers`,
  `highPatterns` và vẫn giữ mặc định. Self-test 51: `.pdf.exe` là High ở ngưỡng 3,
  không High ở ngưỡng 10. `total` 57 → 58.
- README: thêm mục "Detection names and the score gate come from `rules.json`",
  cập nhật 58/58.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  549 KB, SHA256 `33d89da34a29de86…902b17a1`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 58/58 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên máy thật: bộ mẫu lành và mẫu sống trong `viruts/` để đo báo
nhầm/bỏ sót sau khi đổi ngưỡng. Việc tiếp theo: W11 (đường dẫn dài hơn 260 ký tự).

### 2026-11-21 — W11: quét cây sâu hơn 260 ký tự

Đã làm:

- **`Tree.Walk` dùng `WinPath.List` thay cho `Directory.GetFileSystemEntries`.**
  Mỗi thư mục được liệt kê qua `FindFirstFileW` với tiền tố `\\?\`, nên cây sâu
  hơn `MAX_PATH` không còn bị `PathTooLongException` bỏ qua cả nhánh. Thuộc tính
  lấy luôn từ `WIN32_FIND_DATA`, không gọi thêm `File.GetAttributes`.
- Đường dẫn trả về vẫn ở dạng thường (`C:\...`), không phải `\\?\...`, để phần
  lớn mã cũ (đọc tên, đuôi, hiển thị) giữ nguyên; các thao tác tệp đã dùng
  `WinPath.Ext` từ trước.
- Self-test 52: tạo cây 320 ký tự trong thư mục tạm, đặt `baocao.pdf.exe` ở cuối,
  chạy `UsbScan.Detect` với độ sâu 100 và yêu cầu thấy mã `double-extension`.
  `total` 58 → 59.
- README: thêm mục "Long paths are scanned", cập nhật 59/59.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  550 KB, SHA256 `aa6114de0082d809…37e9eedd`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 59/59 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên USB thật có tên tệp dài; self-test chỉ dùng thư mục tạm trên
NTFS. `WinPath.Ext` vẫn gọi `Path.GetFullPath` cho đường dẫn tương đối, nên đầu vào
phải là đường dẫn tuyệt đối như hiện tại. Việc tiếp theo: W12 (hiệu năng và nhiễu
quét).

### 2026-11-21 — W12: hiệu năng và nhiễu quét

Đã làm:

- **Macro đọc đuôi ZIP.** `OfficeMacroDetector.HasVbaProject` đọc 1 MB cuối
  (central directory) thay vì 8 MB đầu, bỏ trần 32 MB; tệp Office lớn không còn bị
  bỏ qua và không cấp phát 8 MB cho mỗi tệp.
- **ADS chỉ trên NTFS.** `ScanContext.IsNtfs` được đặt một lần trong
  `UsbScan.Detect` bằng `Native.IsNtfsVolume`; `AdsDetector` trả về ngay trên
  FAT32/exFAT thay vì gọi `FindFirstStreamW` cho từng tệp.
- **Chữ ký rẻ hơn.** `Signature.PeHasSignature` đọc DOS/NT header để biết có
  Certificate Table trước khi gọi `WinVerifyTrust`; kết quả `(path, size,
  lastWrite)` được cache tối đa 4.096 mục. Lỗi đọc header trả `true` để không bỏ
  sót chữ ký; trạng thái `Error` không vào cache. `WinPath.Stat` dùng
  `GetFileAttributesExW` nên cache dùng được cả với đường dẫn dài.
- **Mục chỉ gắn cờ ẩn mặc định.** `UsbListControl` tách `_rows` (toàn bộ) và
  `_view` (đang hiện); mục `FlagOnly` không hiện cho tới khi bấm chip
  "N mục chỉ gắn cờ" ở dòng ghi chú. Ẩn/hiện không làm mất lựa chọn; mỗi lần quét
  mới quay về ẩn.
- Self-test 53–55: tệp không phải PE không gọi `WinVerifyTrust` và vào cache;
  marker `vbaProject.bin` chỉ nằm trong 1 MB cuối của tệp 9 MB vẫn được nhận;
  stream ẩn thấy trên NTFS và bị bỏ qua khi `IsNtfs=false`. `total` 59 → 62.
- README: thêm mục "Scans skip work that cannot help", cập nhật 62/62.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  556 KB, SHA256 `de202ff31e017827…0261d661`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 62/62 phép kiểm tra.`,
  exit 0.

Chưa đo được mốc "5.000 tệp không tăng > 20 %" trên USB thật; self-test chỉ kiểm
tra đường đi và kết quả. Chip ẩn/hiện mục gắn cờ chưa có self-test giao diện (cần
message loop). Việc tiếp theo: W13 (luồng WriteProtect ↔ làm sạch).

### 2026-11-22 — W13: WriteProtect ↔ làm sạch

Đã làm:

- **Máy trạng thái `wp-pending`.** `Policy.BeginCleanPause` chỉ tạm mở khoá do
  ShieldGate bật (còn `repair\write-protect.txt`), ghi `repair\wp-pending.txt`
  gồm `pid` và hạn UTC 10 phút rồi mới tắt `StorageDevicePolicies\WriteProtect`.
  `EndCleanPause` bật lại và xoá ghi chú; khoá do Windows/người dùng bật không
  bao giờ bị tự mở.
- **Một luồng duy nhất.** `UsbScan.Apply` và `UsbScan.Unhide` tự gọi Begin/End
  sau khi giữ `CleanLock`, nên GUI, bảo vệ nền và dòng lệnh dùng chung. Bỏ các
  lần mở khoá lặp ở `Guard.CleanRoot`, `Guard.ScanAndClean` và `UsbPage.DoApply`;
  khôi phục cách ly và tiêm `autorun.inf` vẫn bọc ngoài vì không đi qua `UsbScan`.
- **Khôi phục khi bị kill.** `Policy.RecoverPending` chạy lúc GUI/tray khởi động
  và mỗi vòng bảo vệ: ghi chú hết hạn, hỏng, hoặc tiến trình giữ nó đã chết thì
  bật khoá lại và ghi log. Vòng lặp 60 giây không bật lại khi ghi chú còn hạn,
  nhưng lệnh tắt do người dùng chủ động vẫn đi qua và xoá ghi chú.
- **Công tắc không nhảy trạng thái.** `Guard.GetStatus` báo WriteProtect = bật
  khi đang tạm mở (`Policy.PendingPause`), nên trang Bảo vệ không tự hiện "tắt"
  rồi bật lại sau 10 phút.
- **Self-test 56:** hạn còn/hết, dữ liệu rác, thiếu `until`, và hướng chặn
  (`BlockedByPause` chỉ chặn chiều bật lại, không chặn chiều tắt). `total` 62 → 63.
- README: thêm mục "Write protection steps aside for cleaning", cập nhật 63/63.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  560 KB, SHA256 `2c72acfdb85e91f7…84e2fee9da`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 63/63 phép kiểm tra.`,
  exit 0.

Chưa kiểm chứng trên USB thật có công tắc khoá ghi vật lý (B7); self-test không
đụng registry. Việc tiếp theo: W14 (quét lại thông minh).

### 2026-11-22 — W14: quét lại thông minh

Đã làm:

- **Chỉ tệp nguy hiểm mới kích hoạt quét.** `RulesData.IsBurstRiskExt` gom `.lnk`,
  script và payload. `BehaviorMonitor.ShouldAlert` chỉ báo khi `risky >=
  lnkBurst` hoặc (`total >= writeBurst` **và** `risky > 0`); 300 `.txt`/`.jpg`
  không còn khởi động quét cả ổ, còn 30 `.lnk` thì vẫn quét.
- **Chống lặp toast.** Thêm `behavior.alertSeconds` (mặc định 600) thay cho hằng
  số 120 giây trong `BehaviorMonitor`; cùng một ổ chỉ ghi sự kiện và hiện bong
  bóng sau mỗi 10 phút.
- `rules.json` có `behavior.alertSeconds`; `RulesData.CurrentVersion` 4 → 5 để tệp
  cũ được ghép mặc định; giá trị nhỏ hơn 30 bị kẹp về 30.
- **Self-test 57:** 300 `.txt` (0 nguy hiểm) không báo; 30 `.lnk` báo; 500 mục có
  1 payload báo; 399 mục với 14 nguy hiểm không báo; cooldown 2 phút chưa hết,
  11 phút thì hết; `.lnk/.exe/.vbs` là nguy hiểm, `.txt/.jpg` không. `total` 63 → 64.
- README: thêm mục "Burst alerts look at what is written", cập nhật 64/64.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  560 KB, SHA256 `667f592ba4836382…a82fd7acc`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 64/64 phép kiểm tra.`,
  exit 0.

Chưa đo trên USB thật với 300 tài liệu và 30 shortcut; self-test chỉ kiểm tra hàm
quyết định và cooldown. Việc tiếp theo: W15 (`chkdsk /f` không treo).

### 2026-11-22 — W15: `chkdsk /f` không treo

Đã làm:

- **`Native.Run` nhận stdin.** Thêm overload `Run(file, args, timeoutMs, stdin,
  out output)`: bật `RedirectStandardInput`, ghi sẵn câu trả lời rồi đóng. Bản
  không có `stdin` giữ nguyên hành vi cho mọi lệnh khác.
- **`Repair.RunChkdsk` trả lời dismount.** Chỉ khi `/f`, truyền `"Y\r\n"` để trả
  lời câu hỏi "force a dismount? (Y/N)" khi ổ còn handle mở; trước đây không ai
  trả lời nên tiến trình chờ tới hết timeout 6 giờ. Chế độ chỉ đọc không đổi.
- Đóng stdin sau câu trả lời: prompt thứ hai nhận EOF và chkdsk đi tiếp hoặc
  thoát thay vì treo. Timeout 6 giờ vẫn là chốt cuối cho ổ hỏng thật.
- **Self-test 58 không chạy chkdsk.** Dùng `findstr` với `stdin` để chứng minh dữ
  liệu vào tới tiến trình con và stdin được đóng đúng. `total` 64 → 65.
- README: thêm mục "`chkdsk /f` answers the dismount prompt", cập nhật 65/65.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  561 KB, SHA256 `7f00e91257b0b9a9…0659c382`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 65/65 phép kiểm tra.`,
  exit 0.

Chưa chạy `chkdsk /f` trên VHD/USB thật theo mục 8 của roadmap; self-test chỉ kiểm
tra đường ống stdin. Việc tiếp theo: W16 (hàng đợi sự kiện có ID cho tray).

### 2026-11-22 — W16: hàng đợi sự kiện có ID cho tray

Đã làm:

- **Mỗi dòng `events.tsv` có ID tăng dần.** Bộ đếm nằm trong `events.tsv.id`, ngoài
  tệp sự kiện, nên xoay tệp không làm mất con trỏ. Mất `.id` thì lấy ID lớn nhất
  còn trong `.1`/tệp hiện tại rồi +1.
- **Khoá liên tiến trình.** `Events.Add` giữ `events.tsv.lock` (`FileShare.None`,
  chờ tối đa 1 giây) để GUI và bảo vệ nền không cấp trùng ID hoặc ghi xen dòng.
- **`Events.ReadSince(id)` / `Events.MaxId`.** Đọc cả `.1` lẫn tệp hiện tại, lọc
  `Id > id`, sắp xếp tăng dần; mỗi tệp chỉ đọc 512 KB cuối vì tray hỏi 5 giây một
  lần. `MaxId` cho tray bỏ qua sự kiện cũ lúc khởi động.
- **Tray đọc theo ID.** Bỏ bốn mốc thời gian trong `settings.txt`; dùng hàng đợi
  `_pending` + `_lastEventId`, mỗi nhịp 5 giây hiện một bong bóng nên nhiều sự kiện
  trong cùng một nhịp không ghi đè nhau. Sự kiện quét ổ và nhiễm lại có kind riêng
  (`drive`, `reinfect`).
- **Bỏ dead code.** `Settings.Set*Event`/`*EventTime` không còn nơi đọc; ba chuỗi
  tray `DeviceBlocked`, `BadUsb`, `Composite` không còn dùng.
- **Tương thích.** Dòng cũ 4/5 cột (không ID, có hoặc không severity) vẫn đọc được.
- **Self-test 59.** 3 sự kiện có ID tăng dần; `ReadSince` chỉ trả mục mới; chuyển
  tệp sang `.1` rồi ghi tiếp vẫn thấy mục mới với ID lớn hơn; dòng 4 cột cũ parse
  đúng. `total` 65 → 66.
- README: mục timeline nói rõ ID và hàng đợi; `settings.txt` không còn "tray notice
  state"; cập nhật 66/66.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  562 KB, SHA256 `4010a773b527cb26…5c435b8d`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 66/66 phép kiểm tra.`,
  exit 0.

Chưa thử tray thật với hai tiến trình ghi đồng thời; self-test chỉ chạy trong một
tiến trình. Việc tiếp theo: W17 (VirusTotal opt-in).

### 2026-11-22 — W17: VirusTotal chỉ gửi hash (chưa nối UI/CLI)

Đã làm:

- **`src/core/VirusTotal.cs` là nơi duy nhất có mã mạng.** `Lookup(hash, key)` từ
  chối hash không phải 64 ký tự hex và khoá rỗng trước khi tạo yêu cầu.
- **URL chỉ có hash.** `BaseUrl + sha256`; không tên tệp, đường dẫn hay nội dung.
  Khoá ở header `x-apikey`, không xuất hiện trong URL hay log.
- **`HttpWebRequest` GET**, `Proxy = null`, `AllowAutoRedirect = false` để khoá
  không bị gửi sang host khác; timeout/read-write 15 giây; TLS 1.2 bật bằng
  `(SecurityProtocolType)3072` vì .NET 4.0 không có tên `Tls12`.
- **`VtRateLimiter`:** 4 mốc trong 60 giây, `Wait()` ngủ tới khi có chỗ. Thử lại
  một lần chỉ với lỗi mạng/timeout hoặc HTTP 5xx; 4xx là kết luận.
- **Đọc tối đa 512 KB** phản hồi; tách `last_analysis_stats` rồi lấy `malicious`,
  `suspicious`, `harmless`, `undetected`, `timeout`, `type-unsupported`.
- **Chưa nối vào UI/CLI** (W18/W19), nên không có lời gọi mạng nào khi người dùng
  chưa bật.
- **Self-test 60** dựng `TcpListener` trên `127.0.0.1` (không ra Internet): request
  line đúng hash, `x-apikey` đúng một lần, không có tên tệp; phản hồi giả cho
  `malicious=3`, `Total=70`; hash sai/khoá rỗng không gửi; giới hạn 4 yêu cầu/phút
  đúng ở mốc 59 và 61 giây. `total` 66 → 67.
- AGENTS: ghi rõ self-test chỉ dùng loopback, không gọi mạng ngoài.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  570 KB, SHA256 `edcde6e63b263f39…ffe3e05cb`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 67/67 phép kiểm tra.`,
  exit 0.

Chưa gọi VirusTotal thật (không có khoá, và W18 mới thêm công tắc). Việc tiếp
theo: W18 (khoá DPAPI + công tắc và hộp thoại đồng ý trong trang Bảo vệ).

### 2026-11-22 — W18: khoá DPAPI + công tắc trong trang Bảo vệ

Đã làm:

- **`src/core/VtKey.cs`:** khoá API mã hoá bằng `ProtectedData.Protect` với scope
  `CurrentUser`, entropy riêng, ghi ra `vt.key`. `Load()` trả rỗng khi tệp hỏng,
  bị copy từ máy khác hoặc không phải của tài khoản này; không crash.
- **Không rò rỉ:** khoá không vào `settings.txt` hay log; `settings.txt` chỉ có
  `vt.enabled` và `vt.consent`. Ô nhập che ký tự và xoá sau khi lưu.
- **`VirusTotal.Query(hash)`:** kiểm tra công tắc trước rồi mới nạp khoá; tắt
  công tắc trả `Disabled` với `Ran=false`, không có yêu cầu mạng.
- **Trang Bảo vệ:** mục VirusTotal với công tắc tắt mặc định, hộp thoại đồng ý
  một lần (chỉ gửi hash; không tên/đường dẫn/nội dung), ô nhập khoá, nút Lưu,
  liên kết Xoá khoá; trạng thái "đã lưu khoá"/"chưa có khoá".
- **Gỡ cài đặt:** `vt.key` luôn nằm trong danh sách xoá, kể cả khi giữ cách ly/
  bản sao lưu; thêm `.id`/`.lock` của timeline để dọn sạch thư mục.
- `build.ps1` tham chiếu `System.Security.dll` cho `ProtectedData`.
- **Self-test 61:** lưu/đọc lại khoá DPAPI, ciphertext không chứa khoá dạng chữ,
  `settings.txt` không chứa khoá, tệp hỏng trả rỗng, công tắc tắt thì `Query`
  không gửi, cờ đồng ý đọc lại được, `vt.key` có trong danh sách gỡ cài đặt,
  xoá khoá hoạt động. `total` 67 → 68.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  580 KB, SHA256 `968aa5cf6b6ad787…bbcfa57`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 68/68 phép kiểm tra.`,
  exit 0.

Chưa thử nhập khoá thật và tra một tệp thật; W19 mới có lệnh `--vt`. Việc tiếp
theo: W19 (`--vt`, `--help`, `--version`, `--scan --json`, `PRIVACY.md`).

### 2026-11-23 — W19: CLI VirusTotal, JSON, trợ giúp và chính sách riêng tư

Đã làm:

- **`--vt <tệp>`:** kiểm tra tệp, công tắc rồi mới băm; in SHA-256 trước khi tra.
  Mã thoát `0` (sạch), `2` (có hãng gắn cờ), `1` (tắt/thiếu khoá/không đọc
  được/lỗi mạng). Không tự cách ly theo kết quả.
- **`--scan <ổ> --json`:** `src/core/ScanJson.cs` dựng JSON gồm drive, files,
  items, seconds, truncated, readOnly, counts, warnings, findings[]; mỗi finding
  có path, relative, kind, score, high, flagOnly, size, codes, reason. Không có
  nội dung tệp. Thêm `Json.Quote` vào `Json.cs` sẵn có (không tạo parser mới).
  Mã thoát giữ `0/1/2`.
- **`--help` / `--version`:** `--help` liệt kê lệnh, mã thoát và câu "không có
  lời gọi mạng nào trừ khi bạn bật VirusTotal và chạy --vt"; `--version` in tên
  và phiên bản.
- **`Native.Sha256(path, maxBytes)`:** `maxBytes <= 0` là không giới hạn, dùng
  cho `--vt`; call site cũ vẫn 10 MB.
- **`PRIVACY.md`:** bảng dữ liệu trên máy, mô tả yêu cầu mạng duy nhất (URL chỉ
  có hash, `x-apikey`, không tên/đường dẫn/nội dung), DPAPI, xoá khoá khi gỡ,
  lựa chọn của người dùng; có tóm tắt tiếng Việt.
- **README:** bỏ câu "chưa nối", thêm lệnh mới, mô tả JSON/mã thoát, sửa giới
  hạn service/driver thành heuristic đúng thực tế, trỏ `PRIVACY.md`.
- **AGENTS.md điều 10:** trỏ `PRIVACY.md`, nêu hiện chỉ có `--vt`.
- **Self-test 62:** JSON có đường dẫn chứa `"` và `\` được `Json.Parse` đọc lại
  đúng từng trường; `Json.Quote` không lọt newline; `--help` có `--scan`,
  `--vt`, `--json`, `--version`, `--help`. `total` 68 → 69.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  588 KB, SHA256 `cf9e9517faa1922b…7df118db9`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 69/69 phép kiểm tra.`,
  exit 0.
- `--version` → `ShieldGate 2.1`, exit 0. `--help` exit 0.
- `--scan <thư mục tạm> --json` → JSON hợp lệ, `files: 1`, exit 0.
- `--vt <tệp>` khi VirusTotal tắt → in thông báo tắt, exit 1, không gọi mạng.

Chưa thử tra VirusTotal thật (không có khoá). Việc tiếp theo: W20
(`docs/design-badusb.md`).

### 2026-11-23 — W20: thiết kế BadUSB

Đã làm:

- Viết `docs/design-badusb.md` (12 mục): mục tiêu/không làm, hiện trạng 2.1,
  kiến trúc W21/W22, mô hình dữ liệu, ngưỡng, cảnh báo, quyền riêng tư, kiểm
  thử, triển khai, câu hỏi duyệt, rủi ro.
- Chốt kỹ thuật đề xuất:
  - Raw Input trong tray, cửa sổ ẩn `HWND_MESSAGE`, `RIDEV_INPUTSINK |
    RIDEV_DEVNOTIFY`; **chỉ đọc `RAWINPUTHEADER`**, bỏ `hDevice = 0`.
  - Poll `Win32_Process` 1 giây + Toolhelp thay `Win32_ProcessStartTrace` vì tray
    không chạy admin; cache cha 5 giây; đi ngược tối đa 3 cấp tìm
    `explorer.exe`.
  - Không log `CommandLine` của shell để tránh giữ nội dung người dùng gõ.
  - Ngưỡng: 15 sự kiện/giây, cửa sổ 2 giây, tối thiểu 30 sự kiện, thiết bị mới
    120 giây, shell trong 10 giây, cooldown 600 giây; mặc định bật, chỉ cảnh báo.
  - W21 thêm kiểm tra tĩnh `test.ps1` chặn `RAWKEYBOARD`,
    `SetWindowsHookEx`, `GetAsyncKeyState`, `GetKeyboardState`,
    `MapVirtualKey`.
- Cập nhật roadmap (W20 ✅, W22 đổi sang poll 1 giây), README trỏ tài liệu thiết
  kế. Không đổi mã nguồn.

Bằng chứng: chỉ tài liệu; lần build/test gần nhất vẫn là W19 (`69/69`, exit 0).

Chờ duyệt §11 (ngưỡng, mặc định bật, polling, trust store, không log command
line, có thiết bị thử BadUSB hay không) trước khi làm W21.

### 2026-11-23 — W21: đo tốc độ gõ trong tray

Chủ dự án duyệt mặc định §11. Đã làm:

- **`src/core/InputRate.cs`:** 50 bucket × 100 ms, cửa sổ 20 bucket (2 giây);
  `MinEvents = 30`, `MinPerSecond = 15`. Nghỉ > 5 giây hoặc đồng hồ lùi đều xoá
  cửa sổ. `InputBurst` chỉ có nhãn thiết bị, mốc đầu/cuối, số sự kiện, đỉnh/giây;
  không có trường mã phím.
- **`src/ui/InputCollector.cs`:** cửa sổ ẩn message-only, đăng ký Raw Input
  `0x01/0x06` với `RIDEV_INPUTSINK | RIDEV_DEVNOTIFY`. Chỉ đọc header của gói
  tin; bỏ `hDevice = 0` (RDP/automation/on-screen keyboard). Tên raw chỉ trong
  bộ nhớ; log dùng nhãn `VID_xxxx&PID_xxxx`. Rút thiết bị thì xoá trạng thái;
  cooldown 600 giây/thiết bị.
- **Tray:** collector chạy cùng icon; burst ghi INFO `BadUSB (chỉ đo): …`, tối đa
  1 dòng/10 phút/thiết bị; đăng ký lỗi thì WARN. Thoát tray dừng collector.
- **`test.ps1`:** kiểm tra tĩnh `src/**/*.cs`, fail nếu có `RAWKEYBOARD`,
  `SetWindowsHookEx`, `GetAsyncKeyState`, `GetKeyboardState`, `MapVirtualKey`.
- **`PRIVACY.md`:** mục "Input timing (BadUSB measurement)" + tóm tắt tiếng Việt.
- **Self-test 63:** 6 sự kiện/giây không vượt; 100 sự kiện/~2 giây vượt; nghỉ
  10 giây và đồng hồ lùi reset. `total` 69 → 70.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  594 KB, SHA256 `8d231966a494c7e7…87b85f4`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 70/70 phép kiểm tra.`,
  exit 0.
- Chạy `--tray` 4 giây rồi thoát: không có WARN "không đăng ký được Raw Input",
  tức cửa sổ ẩn + đăng ký thành công.

Chưa đo bằng bàn phím thật (môi trường không có thiết bị thử); chưa cảnh báo,
chưa ghép shell. Việc tiếp theo: W22 (WMI + Toolhelp, cảnh báo BadUSB).

### 2026-11-23 — W22: cảnh báo BadUSB

Đã làm:

- **`src/core/BadUsb.cs`:** logic thuần, không P/Invoke. Chỉ báo khi công tắc
  bật, thiết bị chưa `Trusted`, `FirstSeen` trong 120 giây, burst đạt 30 sự kiện
  và 15 sự kiện/giây, shell tạo trong `burst.First - 1s … burst.Last + 10s`, và
  có tổ tiên `explorer.exe`. Cooldown 600 giây/khóa thiết bị; `Reset()` cho
  self-test.
- **`src/core/ProcessWatch.cs`:** thread nền poll 1 giây. WMI chỉ đọc PID, PID
  cha, tên và `CreationDate` của 6 shell; **không** đọc `CommandLine`. Toolhelp
  snapshot lấy tên cha, đi ngược tối đa 3 cấp. Poll đầu chỉ ghi nhớ shell đang
  có; giữ 120 giây, tối đa 20 shell. WMI lỗi thì bỏ qua vòng, không cảnh báo.
- **`src/core/Rules.cs`:** phiên bản 6, mục `badusb` với 7 khóa; giá trị ngoài
  khoảng bị kẹp (`minEvents` 5..10000, `eventsPerSecond` 2..10000,
  `shellSeconds` 1..120, `alertSeconds` 30..86400, `newDeviceSeconds`
  10..3600).
- **`src/core/Settings.cs`:** `badusb.enabled` ghi đè mặc định từ `rules.json`;
  công tắc trong trang Bảo vệ bật/tắt cả Raw Input lẫn poll tiến trình.
- **Tray:** `SyncBadUsb` kiểm tra công tắc mỗi 5 giây; burst được giữ tới hạn
  `burst.Last + shellSeconds` để chờ shell; kho thiết bị đọc trên thread nền;
  khớp đường dẫn raw với `devices.json` trước, VID/PID sau. Khi đủ điều kiện:
  log WARN + sự kiện `device` WARN (balloon qua hàng đợi sẵn có). Không tắt
  thiết bị.
- **`src/core/Devices.cs`:** `MatchesRaw` chuẩn hoá `\\?\HID#…` → `HID\…`;
  `RecentShellProcess` bỏ `CommandLine` để không giữ nội dung người dùng gõ.
- **Self-test 64–65:** ma trận BadUSB (báo, cooldown, thiết bị tin cậy, không
  explorer, shell cũ, shell muộn, gõ chậm, thiết bị cũ, công tắc tắt) và
  đọc-kẹp mục `badusb`. `total` 70 → 72.

Lỗi gặp và sửa: lần chạy tray đầu tiên ném `NullReferenceException` trong
`SyncBadUsb` vì `Refresh()` được gọi trước khi `_input` được tạo. Đã chuyển khối
khởi tạo `InputCollector`/`SyncBadUsb` lên trước `Refresh()`; chạy lại 7 giây
không còn lỗi trong log.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  608 KB, SHA256 `badcfe5bb374e852…4184118a`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 72/72 phép kiểm tra.`,
  exit 0.
- Chạy `--tray` 7 giây: không có ERROR/WARN mới; chỉ dòng merge `rules.json`
  phiên bản 3 → 6.

Chưa đo trên thiết bị BadUSB thật (môi trường không có). Kịch bản §9.2 của
`docs/design-badusb.md` vẫn là việc của bản phát hành. Việc tiếp theo: W23
(named pipe) nếu được duyệt; nếu không thì giữ mô hình tệp + ACL.

### 2026-11-24 — phát hành 2.3: quét thư mục, tên Base64, sửa công tắc

Đã làm:

- **Gộp W7–W22:** commit `30dd1e6` (VirusTotal opt-in, CLI JSON, BadUSB, chữ ký,
  đường dẫn dài). Không đổi hành vi mặc định ngoài các mục đã duyệt.
- **Sửa công tắc Protect không tắt được:** `Guard.Configure` bỏ qua `SaveConfig`
  khi mọi công tắc tắt, còn `guard.cfg` cũ vẫn còn `registry=1`, nên `GetStatus`
  trả lại trạng thái bật. Nay luôn ghi `guard.cfg`; khi tất cả tắt thì sao lưu
  lựa chọn trước đó sang `guard.last`, và `EnableConfig` đọc `guard.last` nếu
  `guard.cfg` trống/không có. Commit `f070e90`, self-test 12d, `total` 72 → 73.
- **Quét một thư mục:** `UsbScan.Detect` nhận cả gốc ổ và thư mục; thêm
  `UsbScanResult.IsDriveRoot`, `KeptFiles`, `Base64Names`; `UsbScan.IsDriveRoot`.
  UI có link `Quét thư mục…`, từ chối ổ hệ thống, ẩn tiêm autorun trong chế độ
  thư mục, và dùng `CurrentRoot()` cho quét/cách ly/unhide/quét lại. CLI `--scan`
  đã nhận đường dẫn thư mục; help ghi rõ.
- **Khôi phục tên Base64:** `UsbRestore.DecodeName` chỉ nhận Base64 giải ra tên
  tài liệu hợp lệ (không có `\`, `/`, ký tự điều khiển; đuôi trong
  `Rules.DoubleExtDecoys`; không phải đuôi payload). `RenameBase64` chỉ chạy
  trong thư mục giả/không tên, không ghi đè, ghi log từng lần đổi. UI có link
  `Khôi phục tên gốc (n)` kèm hộp xác nhận; dùng cờ bận `_unhiding` sẵn có.
- **Hiện dữ liệu còn trong thư mục giả:** `KeptFiles` đếm tài liệu được giữ lại;
  kết quả quét hiện cảnh báo "còn n tệp dữ liệu trong thư mục giả" thay vì để
  thư mục trông rỗng.
- **Self-test 66–67:** giải mã/đổi tên Base64 (đúng, tên thường, payload, thoát
  thư mục, trùng tên) và quét thư mục con (không phải gốc ổ, đếm tài liệu giữ).
  `total` 73 → 75.
- **Phiên bản:** `AppInfo.Version` 2.1 → 2.3; `AssemblyInfo` 2.3.0.0. README
  thêm mục 2.1–2.3, lệnh quét thư mục, `guard.last`; roadmap cập nhật trạng thái.

Bằng chứng:

- `powershell -ExecutionPolicy Bypass -File build.ps1` → `dist\ShieldGate.exe`,
  619 KB, SHA256 `e9c342a41f2587039fc07ce815883e6a7fb697e86d1a1e80d949e0e8f0f16463`.
- `powershell -ExecutionPolicy Bypass -File test.ps1` → `Đạt 75/75 phép kiểm tra.`,
  exit 0.

Chưa chạy được phần cần phần cứng của checklist (USB FAT32/exFAT/NTFS, công tắc
khoá ghi, bàn phím ngoài/Bluetooth/composite, Windows 7 x86, `chkdsk /f`, UAC
không admin). W23 (named pipe) vẫn chờ thiết kế được duyệt.

### 2026-11-24 — W23: thiết kế named pipe (chưa viết mã)

- **`docs/design-pipe.md`:** mục tiêu, kiến trúc GUI quyền thường ↔ engine
  SYSTEM, khung tin JSON 4 byte + 64 KiB, ACL `PipeSecurity` (SYSTEM +
  Administrators + Interactive, từ chối Network/Anonymous), cờ
  `PIPE_REJECT_REMOTE_CLIENTS`, xác thực client bằng PID → ảnh → SHA-256 đã ghim,
  allow-list lệnh, plan/apply hai bước, giới hạn job, đường lui tệp + ACL.
- **Không đổi mã.** Chờ chủ dự án duyệt §13 (host pipe, GUI non-admin, pin
  portable, phạm vi allow-list, mặc định bật/tắt, Windows 7, ký số).
- Roadmap W23 thêm liên kết tới thiết kế.

Việc tiếp theo: duyệt §13 rồi làm W23a–W23d (giao thức + pipe + xác thực) trước
khi nối GUI.

### 2026-11-25 — W23a–W23f, W23h: named pipe có xác thực (chưa nối GUI)

- **Giao thức (`PipeProtocol.cs`):** khung 4 byte little-endian + UTF-8 JSON,
  tối đa 64 KiB; bắt buộc đúng `v`/`id`/`cmd`/`args`, `id` 1–32 ký tự, khóa lạ
  hoặc `args` không phải object bị từ chối; mã lỗi `bad-json`, `bad-frame`,
  `too-large`, `bad-version`, `unknown-command`, `bad-args`, `denied`, `busy`,
  `not-found`, `expired`, `refused`, `failed`, `cancelled`.
- **Server (`PipeServer.cs`):** một instance, `PipeSecurity` cho SYSTEM +
  Administrators + Interactive, từ chối Network/Anonymous. .NET 4.0 không đặt
  được `PIPE_REJECT_REMOTE_CLIENTS`, nên dùng đường lui: `PipeAuth` từ chối
  client có `GetNamedPipeClientComputerName` khác rỗng.
- **Xác thực (`PipeAuth.cs`):** PID client → `OpenProcess` →
  `QueryFullProcessImageName` → session (khác 0) → SID người dùng (từ chối
  SYSTEM/LocalService/NetworkService/Anonymous) → so đường dẫn + SHA-256 với
  `bin\client.pin`. `--install` ghim `Paths.GuardExe` sau khi chép bản nền;
  `--uninstall` xoá pin.
- **Lệnh (`PipeCommands.cs`):** `status.get`, `devices.list`, `events.since`,
  `job.start`/`status`/`cancel` (quét ổ chạy nền), và `clean`/`unhide`/`rename`
  theo hai bước plan/apply. Chỉ nhận một ký tự ổ; engine tự dựng `E:\` và lặp
  lại `Repair.RefuseReason`, kiểm tra ổ rời, quyền ghi và serial. Plan hết hạn
  sau 120 giây, gắn SID + session + ổ + serial; `apply` bắt buộc
  `confirm:true`; `clean.apply` chỉ nhận chỉ số mục, không nhận đường dẫn.
- **Host (`Guard.cs`):** `--watch` mở pipe khi `pipe.enabled=1` trong
  `settings.txt` (mặc định tắt), dừng cùng vòng bảo vệ. Client một lần
  (`PipeClient.Send`) và phiên giữ kết nối (`PipeSession`) để plan → apply.
- **Self-test:** thêm 4 mục (khung tin, pin, roundtrip, plan/apply) và 1 mục ACL
  thật (`PipeSecurity` cho SYSTEM/Administrators/Interactive, từ chối
  Network/Anonymous, không có Everyone); roundtrip còn kiểm tra nhiều lệnh trên
  cùng phiên, sống tiếp sau lỗi, PID → ảnh/session/SID; plan/apply kiểm tra thêm
  `job.start` ổ hệ thống và `job.status` không tồn tại. `test.ps1` thêm kiểm tra
  tĩnh: server phải có `PipeSecurity`/SID, auth phải có API PID/ảnh/SID, không có
  `NamedPipeServerStream` ngoài `PipeServer.cs`.
- **Kết quả:** build 650 KB, SHA256
  `f4b23858be8472f63a5fcf13e3ed507d885773c96d001fa66cfc928d118fa36b`;
  self-test **80/80**. Tài liệu: `README.md`, `PRIVACY.md`, `docs/design-pipe.md`,
  `docs/roadmap-2.1.md`.
- **Còn lại:** W23g nối `UsbPage`/`ProtectPage` với pipe khi GUI không elevated;
  W23i phần tài liệu còn lại. Chưa chạy được checklist máy thật (GUI thường +
  engine SYSTEM, client tự viết, rút USB giữa job, pipe từ máy khác, Windows 7
  x86). Pipe vẫn tắt mặc định nên hành vi 2.3 không đổi.

### 2026-11-25 — W23g: GUI dùng pipe cho gốc ổ (không elevated)

- **`UsbPage`:** chỉ chọn đường pipe khi đủ ba điều kiện: `pipe.enabled=1`,
  GUI không elevated, và gốc thao tác là ổ (không phải thư mục). Thư mục và mọi
  trang khác vẫn chạy cục bộ như 2.3.
- **Luồng dọn:** mở một `PipeSession`, gọi `clean.plan`, khớp mục đã chọn theo
  `rel` (không gửi đường dẫn), rồi `clean.apply` với `confirm:true`; sau đó
  `unhide.plan`/`unhide.apply` trên cùng phiên. Nếu danh sách đã đổi, báo mục
  biến mất thay vì gửi apply rỗng; nếu chỉ khớp một phần thì dọn phần khớp và
  cảnh báo.
- **Lỗi pipe:** hiện thông báo "bật bảo vệ nền/pipe" và không tự nâng quyền.
  Hoàn tác một batch do engine ghi cần quyền quản trị vì GUI thường không đọc
  được manifest; nút Hoàn tác báo đúng như vậy.
- **`ProtectPage`:** giữ nguyên yêu cầu quyền quản trị. Allow-list đã duyệt không
  có lệnh đổi chính sách, nên không mở rộng giao thức chỉ để nối trang này.
- **Chuỗi:** thêm `S.Usb.UndoNeedsAdmin`, `S.Usb.PipeItemsGone`,
  `S.Usb.PipeItemsPartial`, `S.Err.PipeUnavailable`, `S.Log.PipeError`.
- **Tài liệu:** `README.md` (pipe đã dùng cho gốc ổ), `PRIVACY.md` (phạm vi pipe
  và hoàn tác cần quản trị), `docs/design-pipe.md`, `docs/roadmap-2.1.md`.
- **Kết quả:** build 654 KB, SHA256
  `4c9a1e0425554004c83e1a1f47efb921df9cbe724257a5697ca6629f8ddd88bc`;
  self-test **80/80**, `--version` in `ShieldGate 2.3`.
- **Còn lại:** checklist máy thật (GUI thường + engine SYSTEM, client tự viết,
  rút USB giữa job, pipe từ máy khác, Windows 7 x86). W23i đã xong cùng đợt này;
  pipe vẫn tắt mặc định.

### 2026-11-25 — W23j: công tắc `--pipe` (vẫn tắt mặc định)

- **Vì sao:** W23 xong phần mã nhưng chỉ bật được bằng cách sửa tay
  `settings.txt`. Thêm lệnh để người dùng bật/tắt có kiểm soát, vẫn không đổi
  hành vi mặc định và không thêm trang cấu hình mới.
- **`--pipe` (không tham số) và `--pipe status`:** chỉ đọc; in thiết lập, bảo vệ
  nền có đang chạy, và `bin\client.pin` có tồn tại. Không cần quyền quản trị nên
  không bật UAC.
- **`--pipe on|off`:** cần quyền quản trị, ghi `pipe.enabled` rồi đọc lại để xác
  nhận; báo cần khởi động lại bảo vệ nền vì pipe host trong `--watch`. Bật mà
  chưa có pin thì in cảnh báo chạy `--install` trước.
- **Nâng quyền:** `NeedsAdmin` chỉ yêu cầu quyền cho `on`/`off`. Giá trị gõ sai
  in cách dùng và trả mã 1 ngay, không mở hộp UAC (trước đó `--pipe maybe` vẫn
  đòi quyền rồi thoát im lặng).
- **Chuỗi:** `S.Cli.PipeUsage`, `PipeBadArg`, `PipeStatus`, `PipeOn`, `PipeOff`,
  `PipeNoPin`, `PipeSetFailed`; `--help` liệt kê `--pipe`.
- **Kiểm thử:** self-test thêm mục 35i (mặc định tắt, bật/tắt ghi được vào
  settings) → **81/81**; `test.ps1` thêm kiểm tra tĩnh: `pipe.enabled` mặc định
  `"0"`, `Program.cs` có `--pipe` và chỉ `on`/`off` mới đòi quyền.
- **Tài liệu:** `README.md` (lệnh + quyền), `docs/design-pipe.md`,
  `docs/roadmap-2.1.md`, `AGENTS.md`.
- **Kết quả:** build 658 KB, SHA256
  `3819aad5b6199822712c025de4e6bddc0493f4b446ac5671f944d1196102b61e`;
  `--pipe`, `--pipe status` trả 0, `--pipe maybe` trả 1 kèm cách dùng;
  self-test **81/81**, `--version` in `ShieldGate 2.3`.
- **Defender:** nhiều bản build trung gian bị gắn nhầm `Trojan:Win32/Wacatac.B!ml`
  rồi bị xoá ngay sau khi dựng; dựng lại tới khi qua thì self-test chạy bình
  thường. Không tắt Defender, không thêm loại trừ. Nếu lặp lại, gửi báo nhầm cho
  Microsoft và cân nhắc ký số release.
- **Chưa làm được:** `--pipe on|off` chưa chạy thử vì phiên hiện tại không có
  quyền quản trị; đường ghi đã được self-test phủ ở thư mục tạm.
- **Còn lại:** checklist máy thật; cân nhắc bump 3.0 khi người dùng thật bật
  pipe.

### 2026-11-26 — Phát hành 3.0 (named pipe, tắt mặc định)

- **Phiên bản:** `AppInfo.Version` 2.3 → 3.0; `AssemblyInfo` 3.0.0.0. Giữ hậu tố
  mutex `_2` để bản 2.3 và 3.0 dùng chung một instance khi nâng cấp.
- **README:** thêm mục "What is new in 3.0" (pipe cho GUI quyền thường, chỉ nhận
  client đã ghim, tắt mặc định, không đổi hành vi quét/sửa); đổi tiêu đề mục pipe
  thành "Named pipe (off by default)"; huy hiệu dung lượng 658 KB.
- **Tài liệu:** `docs/design-pipe.md` ghi mốc phát hành và điều kiện bật mặc định
  sau này; `docs/roadmap-2.1.md` đánh dấu 3.0 đã phát hành.
- **Kiểm tra:** build 658 KB, SHA256
  `c9c9f66c25d59f36eaeca10aba0d0241685e68eae9c4a2127d5e293f28a1909a`;
  `test.ps1` **81/81**, `--selftest` **81/81**; `--version` in `ShieldGate 3.0`;
  `--status` và `--pipe status` trả 0.
- **Phát hành:** tag `v3.0`, GitHub release kèm `ShieldGate.exe` và ghi chú về
  false positive của Defender, chưa ký số.
- **Còn lại:** checklist máy thật (GUI thường + engine SYSTEM, client tự viết,
  rút USB giữa job, pipe từ máy khác, Windows 7 x86), ký số Authenticode, và chỉ
  bật pipe mặc định sau khi hai việc đó xong.

### 2026-11-26 — Sửa hộp thoại chọn thư mục mở lại, thêm mẫu issue

- **Lỗi:** trong "Quét thư mục…", bấm X trên hộp thoại Browse For Folder phải
  bấm hai lần vì hộp thoại tự mở lại. Hộp thoại shell chạy vòng lặp modal ngay
  trong cú nhả chuột của liên kết, nên sự kiện đó bị xử lý lại khi hộp thoại đóng.
- **Sửa trong `UsbPage`:** mở hộp thoại qua `BeginInvoke` để cú nhả chuột kết
  thúc trước; thêm mốc `_folderClosedUtc` bỏ qua lần gọi trong 400 ms; kiểm tra
  `IsDisposed`/`Disposing` trước khi mở. Không đổi hành vi chọn thư mục hợp lệ.
- **Kiểm tra:** build 658 KB, SHA256
  `23c8d0fbd09bf7aba898c8734c4b750c2d67f86dac0c581c851a30e7c12a7f5c`;
  `test.ps1` **81/81**, `--selftest` **81/81**, `--version` in `ShieldGate 3.0.1`.
  Chưa bấm thử bằng tay trong phiên này; cần người dùng xác nhận một lần bấm X.
- **Phát hành 3.0.1:** tăng `AppInfo.Version`/`AssemblyInfo` lên 3.0.1, tag
  `v3.0.1`, GitHub release kèm exe và ghi chú "cần xác nhận trên máy thật".
- **Đổi tên repo:** `shieldgate-usb-guard` → `shieldgate` (người dùng đồng ý);
  GitHub chuyển hướng URL cũ, tag/release giữ nguyên; cập nhật badge README,
  link `PRIVACY.md` và mẫu issue.
- **Mẫu issue:** `.github/ISSUE_TEMPLATE/bug_report.yml`,
  `feature_request.yml`, `config.yml`; hỏi phiên bản, Windows, quyền, khu vực,
  bước tái hiện và nhắc không đính mẫu virus/dữ liệu cá nhân; nêu rõ việc ngoài
  phạm vi (MBR, format, tự tắt thiết bị, mạng).
- **Còn lại:** checklist máy thật, ký số release, Raw Input + ETW cho BadUSB,
  YARA. Các mục MBR/TestDisk/format vẫn cố ý không làm.

### 2026-11-26 — Dọn tài liệu, chuẩn bị ký số SignPath, cập nhật metadata

- **Dọn tệp:** chuyển bản phân tích 2.0 từ gốc repo vào
  `docs/analysis-2.0.md`, cập nhật lời dẫn trong `plan.md` và
  `docs/roadmap-2.1.md`; gốc repo chỉ còn `README.md`, `PRIVACY.md`, `LICENSE`,
  `AGENTS.md`, `plan.md` và `CODE_SIGNING.md`. Không xoá lịch sử hay tài liệu
  thiết kế đang được tham chiếu.
- **Ký số:** thêm `CODE_SIGNING.md` nêu phạm vi ký (chỉ `ShieldGate.exe` phát
  hành từ tag `v*`), quy trình build trên GitHub Actions, mẫu artifact
  configuration ZIP + PE, tên biến/secret, cách kiểm tra `Get-AuthenticodeSignature`
  và mục đích phòng thủ để nộp SignPath Foundation.
- **Workflow:** `.github/workflows/build.yml` thêm job `sign` chỉ chạy với tag
  `v*` khi biến `SIGNPATH_PROJECT_SLUG` đã đặt; tải artifact ID từ job build,
  gửi SignPath, tải bản đã ký, đăng artifact `ShieldGate-signed` và gắn vào
  release nếu release đã tồn tại. Chưa cấu hình biến/secret nên job bị bỏ qua,
  CI hiện tại không đổi.
- **README:** sửa số self-test 72/72 → 81/81; thêm đoạn build CI + ký số; mục
  Limits ghi rõ bản phát hành còn chưa ký cho tới khi SignPath duyệt.
- **GitHub metadata:** cập nhật mô tả, homepage và topic (USB security, removable
  media, shortcut virus, BadUSB, WinForms, offline, portable, privacy...).
- **Kiểm tra:** build 658 KB, SHA256
  `a2968b183e95a45fbdde06d79c5281af212c784e369b9452b04f1dd42fa90c42`;
  `test.ps1` **81/81**, `--selftest` **81/81**, `--version` in `ShieldGate 3.0.1`.
  Không đổi mã nên không phát hành bản mới; release 3.0.1 giữ nguyên.
- **CI:** run `45735330479` trên `main` thành công; job `windows` success, job
  `Sign release build` skipped đúng thiết kế vì chưa có biến SignPath.
- **README:** thêm dòng Download / Build from source / Code signing policy ở đầu.
- **Chưa làm được:** nộp đơn SignPath thay người dùng (cần tài khoản và email),
  tạo organization/project trên dashboard, thêm secret; các bước này ghi trong
  `CODE_SIGNING.md`.
