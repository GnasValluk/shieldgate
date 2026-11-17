# ShieldGate 2.0 — Kế hoạch triển khai

> Trạng thái: đang thực hiện. Mỗi mục xong sẽ được đánh dấu `[x]` và ghi vào
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

## 6. Nhật ký tiến độ

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
