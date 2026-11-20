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
- [ ] VirusTotal (opt-in) — chưa làm; cần khoá API và cam kết mạng rõ ràng.
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
| 2.1.0 | Nền tảng an toàn: A1–A9 (đường dẫn công cụ, marker sao lưu, gỡ cài đặt, khôi phục, luật) | W1–W7 | 9–11 ngày |
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
3. `rules.json` chỉ đọc từ thư mục dữ liệu; tệp cạnh exe bị bỏ qua — còn ở W6.

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

- Đọc `ShieldGate 2.0 — Phân tích mã nguồn và lộ trình hoàn thiện.md` và kiểm chứng
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
