# ShieldGate — Lộ trình hoàn thiện 2.1 → 3.0

> Nguồn: `ShieldGate 2.0 — Phân tích mã nguồn và lộ trình hoàn thiện.md` (17/11/2026).
> Tệp này biến bản phân tích thành việc làm cụ thể: mỗi mục có điểm sửa trong mã,
> cách làm, kiểm thử và tiêu chí xong. `plan.md` vẫn là nơi ghi nhật ký tiến độ.

## 0. Đã kiểm chứng lại bản phân tích (18/11/2026)

Baseline lúc lập kế hoạch (18/11, cây làm việc đang có thay đổi chưa commit):
`build.ps1` → `dist\ShieldGate.exe` 480 KB, SHA256 `7e09f1ce…d499cb`;
`test.ps1` → `Đạt 37/37 phép kiểm tra.`, exit 0.

Số dòng dưới đây theo cây ngày 18/11. Nếu mã đã dịch, đối chiếu theo **tên hàm**
(`Native.Run`, `Repair.HasBackup`, `Guard.Configure`, `UsbScan.Unhide`…) thay vì số dòng.

| Mã | Kết luận | Bằng chứng trong mã |
| --- | --- | --- |
| A1 | Đúng | `Native.Run` (`Core.cs:293`) không đặt đường dẫn/`WorkingDirectory`; ~40 điểm gọi tên trần (`Guard.cs` 18 chỗ `schtasks`, `Repair.cs` 152/244/341/409, `Scan.cs` 1344/1387, `Core.cs` 131/137, `Devices.cs` 671, `Process.Start("explorer.exe")` ở 4 tệp UI, `powershell.exe`/`cmd.exe` ở `Uninstaller.cs:171/185`) |
| A2 | Đúng | `Uninstaller.DataEntries` (`Uninstaller.cs:40-63`) xoá đệ quy `log`, `quarantine`, `backup`, `bin`, `repair` dưới `Paths.Root`; `Root` do người dùng chọn (`Core.cs:56-78`, `Bootstrap.cs:50-72`) |
| A3 | Đúng | `Uninstaller.Run` không có tham số giữ dữ liệu; `AboutPage.cs:151` chỉ có một hộp xác nhận |
| A4 | Đúng | Marker `backup-<chữ ổ>.ok` (`Repair.cs:360-370`), không serial, không hạn, không kiểm tra bản sao còn tồn tại |
| A5 | Đúng | `Quarantine.RestoreBatch` (`Core.cs:624-663`) dùng thẳng `OriginalPath`/`QuarantinePath` từ manifest, chỉ kiểm SHA256 |
| A6 | Đúng | `Scan.BackupReg` (`Scan.cs:1335-1345`) bỏ qua mã thoát `reg export`; `Fix` vẫn xoá/ghi tiếp (`Scan.cs:1366`) |
| A7 | Đúng | `Detect.IsSigned` (`Detect.cs:249-257`) chỉ gọi `CreateFromSignedFile` |
| A8 | Đúng | `Events.Csv` chỉ bọc nháy kép, không chặn `= + - @` ở đầu ô |
| A9 | Đúng | `Rules.CandidatePath` (`Rules.cs:127-141`) ưu tiên `rules.json` cạnh exe; `FillSet` thay cả tập (`Rules.cs:184-211`) |
| B1 | Đúng (cần đo trên x64) | `Policy.StopRemovableProcesses` dùng `p.MainModule.FileName` trong `try/catch { continue; }` (`Policy.cs:178-182`); build `/platform:x86` (`build.ps1:32`) |
| B2 | Đúng (cần đo thiết bị thật) | `Devices.IsInternalKeyboard` coi mọi `HID\` là trong (`Devices.cs:498-503`) |
| B4 | Đúng | `UsbScan.Unhide` bỏ Hidden/System cho **mọi** mục `Tree.Walk` trả về; gọi sau mỗi lần tự quét (`Guard.cs:1088`, `1117`) |
| B5 | Đúng | `Tree.Walk` dùng `Directory.GetFileSystemEntries` + `catch { continue; }` (`Core.cs:400-401`); `WinPath` chưa có hàm liệt kê |
| B6 | Đã sửa trong cây ngày 18/11 (chưa commit) | `Configure` ghi `guard.cfg` trước khi đổi registry (`Guard.cs:1183`), trước đây ghi sau khi đăng ký tác vụ; vòng nền vẫn đọc lại cấu hình mỗi 60 giây (`Guard.cs:881`). Tắt bảo vệ không ghi đè `guard.cfg`, nên lần bật sau dùng lại đúng lựa chọn cũ (`Guard.EnableConfig`). Đã thêm 2 kiểm thử hồi quy (`SelfTest` 12b, 12c). |
| B7 | Đúng | `Policy.ApplyWriteProtect` khoá ghi toàn cục; không có luồng tạm mở khoá khi quét/cách ly |
| B8 | Đúng | `BehaviorMonitor` bùng nổ → quét cả ổ, chặn báo lặp 120 giây (`Behavior.cs:143-154`) |
| B9 | Đúng | `RunChkdsk` không điều khiển stdin, timeout 6 giờ (`Repair.cs:405-410`) |
| B10 | Đúng (cần đo mã thoát) | `robocopy` không có `/XD` cho `System Volume Information`/`$RECYCLE.BIN` (`Repair.cs:242-244`, `339-341`) |
| B11 | Đúng | `RefuseReason` chỉ chặn ổ hệ thống/mạng/CD (`Repair.cs:79-118`) |
| B12 | Đúng theo mã | `PcScan` đọc HKCU và hồ sơ người dùng đang chạy |
| C | Đúng | Hằng số nằm trong `Detect.cs:26-102`, `Detect.cs:217`; `ScoreThreshold` chỉ **nâng** điểm chứ không quyết định `High` (`Scan.cs:249-250`); `Devices.SetEnabled` (`Devices.cs:665`) không nơi nào gọi |
| D | Đúng | `Program.cs:56-76` không có `--help`, `--version`, `--json`; `SelfTest.cs` nằm trong bản phát hành vì `build.ps1:25-26` biên dịch cả `src\ui\*.cs` |

### Đính chính nhỏ so với bản phân tích

1. `AdsDetector` không gọi `FileInfo`; nó dùng `TreeItem.Size` rồi mới gọi
   `FindFirstStreamW`. Kết luận (tốn một syscall/tệp, vô ích trên FAT32) giữ nguyên.
2. `OfficeMacroDetector` bỏ qua tệp > 32 MB **và** chỉ đọc 8 MB đầu — nặng hơn mô tả
   trong bản phân tích (tệp 8–32 MB vẫn có thể bị bỏ sót nếu `vbaProject.bin` nằm sau
   8 MB).
3. `robocopy` được coi là thành công với mã thoát 0–7, nên giả thuyết "luôn exit ≥ 8"
   chỉ đúng khi thư mục hệ thống của ổ thực sự gây lỗi. Vẫn phải sửa `/XD`, nhưng
   đừng chặn `chkdsk /f` chỉ vì suy đoán — cần đo.
4. `.github/workflows/build.yml` đã tồn tại; không cần thêm CI, chỉ cần thêm bước
   kiểm tra tên công cụ trần vào `build.ps1`.

### Phải kiểm chứng trên máy thật (không tự tin từ đọc mã)

B1 đường dẫn tiến trình 64-bit; B2 phân loại bàn phím; B9 `chkdsk /f` có hỏi dismount;
B10 mã thoát robocopy trên NTFS; A6 view registry của `reg.exe` x86; A7
`WinVerifyTrust` trên Windows 7; B3 tỉ lệ báo nhầm của heuristic shell.

## 1. Cách làm chung

- Mỗi mục dưới đây là **một nhánh + một commit** (hoặc một PR nhỏ), không gộp mục
  an toàn với mục tính năng.
- Mỗi mục phải thêm **ít nhất một phép kiểm tra** vào `SelfTest.Run`, tăng `total`
  (`SelfTest.cs:29`) và thêm chuỗi `S.Selftest`.
- Thứ tự bắt buộc: sửa mã → `build.ps1` → `test.ps1` → cập nhật `plan.md`.
- Không đổi hành vi mặc định của người dùng đang chạy 2.0 nếu không có ghi chú phát hành.

### Tiêu chí "xong" cho mọi mục

1. `build.ps1` exit 0, tạo `dist\ShieldGate.exe`.
2. `test.ps1` in `Đạt n/n`, exit 0; `n` lớn hơn con số 37 hiện tại.
3. Không còn `Native.Run("<tên trần>"` / `Process.Start("<tên trần>"` (cổng kiểm tra ở W1).
4. `plan.md` có mục nhật ký kèm số liệu build/test.
5. Không thêm lời gọi mạng ngoài `VirusTotal.cs` (khi tới 2.2).
6. Chuỗi mới đi qua `Strings.cs`; UI không chạy việc nặng trên luồng giao diện.

## 2. Phát hành 2.1.0 — Nền tảng an toàn (ước 9–11 ngày)

Mục tiêu: đóng A1–A9 trước khi chạm vào bất kỳ tính năng nào. Đây là bản duy nhất
được phép thay đổi định dạng dữ liệu, nên phải làm sớm.

| # | Việc | Tệp chính | Kiểm thử | Xong khi |
| --- | --- | --- | --- | --- |
| W1 ✅ | Đường dẫn tuyệt đối cho công cụ hệ thống (A1) | `Core.cs`, toàn bộ điểm gọi | `SysExe` trả đường dẫn tuyệt đối, tồn tại; `Run("cmd.exe")` tự phân giải | Grep sạch; `build.ps1` chặn tên trần |
| W2 ✅ | Marker sao lưu theo ổ thật + `robocopy /XD` + giới hạn `chkdsk /f` (A4, B10, B11) | `Repair.cs`, `Native` | Cùng chữ ổ khác serial → từ chối; marker hết hạn → từ chối; ổ cố định → từ chối `/f` | `CheckBackup` trả lý do cụ thể; UI hiện lý do |
| W3 ✅ | Gỡ cài đặt an toàn + tuỳ chọn giữ dữ liệu (A2, A3) | `Uninstaller.cs`, `Paths`, `AboutPage.cs`, `Strings.cs` | `Owned` nhận marker hoặc chữ ký 2.0; `RemoveData(true)` giữ cách ly + `repair`; thư mục lạ còn nguyên | Không xoá gì ngoài mục có marker/adopt |
| W4 ✅ | Chuẩn hoá đường dẫn khôi phục (A5) | `Core.cs` `RestoreBatch` | Manifest có `..\..`, src ngoài lô, dest `C:\Windows\…` → bị từ chối | Có bộ đếm `refused` + sự kiện WARN |
| W5 ✅ | Ba sửa nhỏ: `reg export` (A6), CSV injection (A8), phạm vi `Unhide` (B4) | `Scan.cs`, `Events.cs`, `Scan.cs` `Unhide` | 3 phép kiểm tra riêng | `desktop.ini` và thư mục ẩn của người dùng không bị đổi thuộc tính |
| W6 ✅ | `rules.json` chỉ đọc từ thư mục dữ liệu + merge theo phiên bản (A9) | `Rules.cs`, `Program.cs` | File v2 thiếu khoá mới vẫn có khoá mới; không xoá được đuôi mặc định; file cạnh exe bị bỏ qua | `version` 3 + `--import-rules` |
| W7 | Xác minh chữ ký bằng `WinVerifyTrust` (A7) | `Signature.cs` mới, `Detect.cs` | Tệp chưa ký → `Unsigned`; tệp hệ thống có ký → `Signed` (bỏ qua nếu máy không có) | Không gọi mạng (`WTD_CACHE_ONLY_URL_RETRIEVAL`) |

Trạng thái 19/11: W1–W6 đã xong, có self-test và cổng build; chi tiết và bằng
chứng ở mục nhật ký cuối `plan.md`. Trước W5 đã sửa xong F1–F5 (mục 2b) vì log thực
địa cho thấy bản 2.0 đang thu gom tài liệu người dùng và ghi hàng trăm lỗi giả.
Mục W7 còn lại chưa chạm tới.

### W1 — chi tiết

```csharp
// Core.cs, trong Native
public static string SysExe(string name)   // Sysnative → SystemDirectory → System32 → WindowsPowerShell\v1.0
public static string ShellExe(string name) // %WINDIR%\explorer.exe
```

- Cho `Native.Run` tự phân giải tên trần để mọi điểm gọi bị bỏ sót vẫn an toàn.
- Khi chạy x86 trên x64, `System32` bị chuyển hướng sang `SysWOW64`; `schtasks`,
  `reg`, `chkdsk`, `robocopy`, `fsutil`, `icacls`, `cmd` đều có ở đó, nhưng
  `pnputil.exe` và `powershell.exe` **không** — nên `SysExe` thử `Sysnative` trước và
  tra `WindowsPowerShell\v1.0` cho `powershell`. Không cần tắt WOW64 redirection.
- `powershell.exe` (`Uninstaller.cs:171`) và `cmd.exe` (`Uninstaller.cs:185`) dùng
  đường dẫn tuyệt đối qua `SysExe`; `explorer.exe` (4 tệp UI) dùng `%WINDIR%\explorer.exe`.
- B6 (thứ tự `SaveConfig`) đã được sửa trong cây ngày 18/11; việc còn lại chỉ là thêm
  phép kiểm tra hồi quy, không làm lại.
- Cổng kiểm tra: `tools/check-bare-tools.ps1` (grep hai mẫu trên), gọi từ `build.ps1`
  trước `csc`; thất bại thì dừng build.

### W2 — định dạng marker

Đường dẫn: `repair\backup-<chữ ổ>-<serial>.ok`, nội dung `key=value`:

```
state=complete
volumeSerial=1A2B3C4D
volumeBytes=16000000000
source=E:\
dest=C:\ProgramData\ShieldGate\repair\backup-E-20261118_101500
when=2026-11-18T10:15:00
files=1834
bytes=912345678
```

- `CheckBackup(drive, out reason)` kiểm: serial khớp, `dest` nằm trong `RepairDir`,
  `state=complete`, tuổi < 24 giờ (`Repair.MarkerMaxAgeHours`), `dest` còn tệp.
- Marker 2.0 (`backup-E.ok`) bị coi là không hợp lệ; log INFO "cần sao lưu lại" và
  ghi vào ghi chú phát hành. Đây là thay đổi có chủ ý: thà bắt sao lưu lại.
- `Backup`: thêm `/XD "System Volume Information" "$RECYCLE.BIN"`; kiểm dung lượng
  trống của ổ chứa `Paths.Root` (≥ `plan.Bytes × 1.1 + 100 MB`); chỉ ghi marker khi
  mã thoát < 8 **và** hoặc nguồn rỗng, hoặc đích có ít nhất một mục (ổ rỗng hợp lệ
  thì đích rỗng là đúng).
- `RefuseReason(drive, bool write, out reason)`: `write=true` chỉ cho
  `DriveType.Removable`; chẩn đoán chỉ đọc vẫn cho ổ cố định phụ (trừ ổ hệ thống).

### W3 — marker sở hữu thư mục (đã làm 18/11)

- `Paths.EnsureDirs` ghi `.shieldgate` (nội dung: `app`, `version`, `created`) vào từng
  thư mục do ứng dụng tạo và vào `Paths.Root`.
- `Uninstaller.Owned(dir)` = có marker, **hoặc** khớp chữ ký bản 2.0
  (`log\shieldgate.log`, `quarantine\*_*\manifest.csv`, `bin\ShieldGate.exe`,
  `repair\*.ok` / `repair\backup-*`), **hoặc** thư mục rỗng. Khi chỉ khớp chữ ký thì
  `Adopt` ghi marker rồi mới xoá; không khớp thì bỏ qua và ghi WARN
  (`S.Log.UninstallSkip`).
- `DataEntries(keepData)` bỏ `quarantine` và `repair` khi giữ dữ liệu; `RemoveData`
  chỉ dọn thư mục gốc khi đã xoá hết và không giữ dữ liệu.
- Hộp thoại gỡ dùng `SwitchRow` **"Giữ cách ly và bản sao lưu ổ"**, mặc định **bật**,
  kèm dung lượng ước tính (quét giới hạn 600 ms để không treo UI). CLI:
  `--uninstall --keep-data` (mặc định) / `--purge-data`.
- Sau khi gỡ mà giữ dữ liệu, thư mục `quarantine` và `repair` vẫn nằm trong thư mục
  dữ liệu; thông báo trên UI và CLI nói rõ đường dẫn.

### W4 — kiểm tra manifest (đã làm 19/11)

- `RestoreBatch` thêm bộ đếm `refused`; mỗi dòng phải qua hai cửa:
  - **nguồn** (`QuarantinePath`) phải nằm trong đúng thư mục lô (`Inside(src, batchRoot)`);
  - **đích** (`OriginalPath`) phải tuyệt đối, có tên tệp, không nằm trong vùng cách ly,
    `bin` của ShieldGate, `%WINDIR%`, `Program Files` hoặc `Program Files (x86)`.
- `Core.Canonical` tự gộp `.`/`..` bằng chuỗi thay vì `Path.GetFullPath`: trên .NET
  Framework 4.0 `GetFullPath` ném `PathTooLongException` với đường dẫn > 260 ký tự,
  đúng loại đường dẫn mà vùng cách ly phải khôi phục được (self-test 25).
- Dòng bị từ chối ghi WARN + một sự kiện `scan` với lý do; UI hiện số bị từ chối và
  **không xoá lô** khi còn dòng bị từ chối, để người dùng mở manifest xem lại.
- Không chặn cả thư mục chứa exe: bản chạy từ USB quét chính USB đó, còn tệp đã tồn
  tại thì luôn bị bỏ qua (không ghi đè).

### W5 — ba sửa nhỏ (đã làm 19/11)

- **A6 — `reg export` có kiểm chứng.** `PcScan.BackupReg` trả `bool` + `error`; chỉ
  coi là có bản sao khi `reg.exe` thoát 0 **và** tệp `.reg` tồn tại với kích thước > 0.
  `PcScan.Fix` dừng từng mục (`RunValue`, `Winlogon*`, `AppInit`, `MountPoint`,
  `LnkAssoc`, `RegHealth`) nếu không sao lưu được; `RegistryGuard.Ensure` cũng không
  ghi đè giá trị và bỏ khoá khỏi danh sách "đã sao lưu" để lần sau thử lại. Bỏ điều
  kiện `IsAdmin` ở bước sao lưu: `HKCU` export được cả khi không nâng quyền, còn
  `HKLM` không nâng quyền thì từ chối sửa thay vì xoá mù.
- **A8 — CSV injection.** `Events.Csv` thêm `'` trước ô mở đầu bằng `= + - @` (và
  tab/CR) trước khi bọc nháy kép, nên mở `shieldgate-events.csv` bằng Excel không
  chạy công thức từ tên tệp do virus đặt. Tệp TSV nội bộ và `manifest.csv` không đổi:
  ô manifest luôn là đường dẫn tuyệt đối, và thêm `'` sẽ làm sai đường dẫn khôi phục.
- **B4 — phạm vi `Unhide`.** Thêm `UnhideScope`: `VirusOnly` (mặc định, dùng sau mỗi
  lần tự quét và sau khi cách ly) chỉ gỡ `Hidden` khi mục có **cả** `Hidden` và
  `System` — dấu hiệu `attrib +h +s` hàng loạt của worm; `AllHidden` (nút "Hiện tệp"
  do người dùng bấm) gỡ cả mục chỉ có `Hidden`. Cả hai bỏ qua `desktop.ini`,
  `autorun.inf`, `System Volume Information` và tên bắt đầu bằng `$`
  (`$RECYCLE.BIN`), nên không làm hỏng hiển thị thư mục.

Kiểm thử: 39 CSV có ô `=cmd|' /C calc'!A0` → xuất ra `'=cmd…`; 40 `reg export` khoá
không tồn tại → `false` + lý do, khoá thật `HKCU\Control Panel\Desktop` → `true` +
tệp > 0 byte; 41 `desktop.ini` và `mine.docx` (Hidden) giữ nguyên, `worm.doc`
(Hidden+System) được gỡ, `$RECYCLE.BIN` giữ nguyên.

### W6 — merge luật (đã làm 19/11)

- `CandidatePath()` luôn trả `Paths.RulesFile`. Nếu thấy `rules.json` cạnh tệp chạy,
  ghi WARN **một lần mỗi tiến trình** rồi bỏ qua: bản chạy từ USB nằm trên ổ do người
  khác quản lý, tệp cạnh exe có thể bị thay mà người dùng không biết.
- `FillSet` (xoá rồi thay) → `MergeSet` (chỉ thêm). Tệp v2 thiếu `payloadExts` vẫn có
  đủ đuôi mới; người dùng không xoá được `.exe`, `.dll`… khỏi bộ đuôi mặc định bằng
  cách liệt kê đuôi khác.
- `scoreThreshold` kẹp 1–10, trọng số kẹp 0–10 (trước đây nhận cả 99 hay số âm);
  `version` nâng lên 3; tệp cũ hơn được ghi INFO "đã bổ sung mặc định", **không ghi
  đè tệp của người dùng**.
- `Rules.Import(path, out error)`: chỉ nhận tệp ≤ 1 MB parse được, giữ bản cũ thành
  `rules.json.bak`, rồi ghi đè và `Reload()`. CLI `--import-rules <tệp> --yes`
  (thiếu `--yes` chỉ in hướng dẫn), tự nâng quyền vì thư mục dữ liệu bị khoá ACL khi
  bảo vệ đang bật.
- Tệp mẫu `DefaultJson` vẫn viết tay: chưa có phép kiểm tra so từng khoá với
  `Defaults()` (đã ghi ở "việc còn lại" cuối mục này). Test 21 chỉ bảo đảm tệp thiếu
  khoá thì mặc định trong mã vẫn được dùng (`payloadExts`, `version`).

Kiểm thử: 21 (tệp chỉ có `scriptExts: ["abc"]` vẫn giữ `.vbs`, trọng số 99 → 10,
`.abc` được thêm, `version` = 3); 42 (tệp cạnh exe có `besideonly` bị bỏ qua, nhập
tệp v2 thiếu `payloadExts` vẫn có `.dll`, `rules.json.bak` tồn tại, nhập tệp không
tồn tại bị từ chối).

## 2b. Sửa lỗi từ nhật ký thực địa 03/11/2026 (F1–F5, xong 19/11)

Nhật ký `log shieldgate tren may benh 2.log` (1.746 dòng, 14:27–14:34) cho thấy ba
lỗi không nằm trong danh sách A/B ban đầu. Đã sửa trước W5 vì chúng làm hỏng dữ liệu
người dùng và làm nhật ký không đọc được.

| # | Việc | Tệp chính | Kiểm thử | Xong khi |
| --- | --- | --- | --- | --- |
| F1 ✅ | Chỉ một tiến trình được dọn một ổ (khoá `clean.lock`) | `CleanLock.cs` mới, `Scan.cs`, `Guard.cs` | self-test 37 | Luồng khác bị chặn; cùng luồng vào lại được |
| F2 ✅ | Thư mục giả: chỉ cách ly payload, giữ tài liệu người dùng | `Scan.QuarantineTree`, `Rules.PayloadExts`, `UsbRestore.IsPayloadFile` | self-test 35 | 400 tệp `.doc/.pdf` Base64 vẫn còn tại chỗ |
| F3 ✅ | "Tệp đã biến mất" không phải lỗi, không trừ bộ đếm | `Quarantine.Add`, `Scan.Apply/Fix` | self-test 36 | `error` rỗng; không ghi ERROR |
| F4 ✅ | Bộ đếm không còn âm; bỏ vỏ batch rỗng | `Guard.CleanRoot/ScanAndClean`, `Scan.ApplyLocked` | self-test 38 | `quarantined` = số tệp `Apply` trả về; không còn thư mục trống |
| F5 ✅ | Ngôn ngữ nhật ký của tác vụ SYSTEM | `Program.cs`, `Settings` | (kiểm tay) | Tác vụ nền ghi tiếng Việt như GUI |

Chi tiết F1: `CleanLock` là tệp `clean.lock` trong `Paths.Root` mở với
`FileShare.None`. Dùng tệp thay vì named pipe: mọi tiến trình đã dùng chung thư mục
dữ liệu, hệ điều hành tự nhả khoá khi tiến trình chết, không cần xác thực client.
`Apply` và `QuarantineTree` giữ khoá (vào lại được trong cùng luồng); `CleanRoot`
và `ScanAndClean` bỏ qua vòng quét khi khoá đang bận thay vì quét lại cả ổ.

Chi tiết F2: log cho thấy `D:\Docusment\3\22AB820634F924A0\` chứa tài liệu thật bị
virus đổi tên Base64 (`...docx` giải mã ra "cuong_do cuong.docx"). Bản cũ cách ly
cả thư mục nên 600+ tài liệu bị thu gom, khôi phục lại mang tên Base64. Nay chỉ
`.exe/.dll/.dat/.lnk/...` (`Rules.PayloadExts`) bị cách ly; tài liệu giữ nguyên và
thư mục giả không bị xoá (còn dữ liệu), có ghi INFO "giữ lại N tệp".

Còn lại (chưa làm, đưa vào 2.2): khôi phục **tên gốc** cho tài liệu Base64 khi
người dùng yêu cầu, và báo rõ thư mục giả còn dữ liệu trên trang Ổ USB.

## 3. Phát hành 2.1 — Sửa tính năng đang sai (ước 12–15 ngày)

| # | Việc | Tệp chính | Kiểm thử | Xong khi |
| --- | --- | --- | --- | --- |
| W8 | Chặn thực thi đọc được tiến trình 64-bit (B1) | `Native`, `Policy.cs` | Đường dẫn tiến trình hiện tại khớp `Assembly.Location`; PID hệ thống không ném | Không còn `MainModule` trên đường nóng |
| W9 | Phân loại bàn phím ngoài (B2) | `Devices.cs` | 6 chuỗi PNP mẫu (ACPI, USB HID, Bluetooth HID, ROOT, SWD, composite) | `--devices` trên máy thật phân loại đúng |
| W10 | Tuân thủ AGENTS điều 7/8/13 (C) | `Detect.cs`, `Rules.cs`, `Scan.cs`, `Devices.cs` | "Strong nhưng dưới ngưỡng → không High"; luật đọc từ JSON | Không còn hằng số nhận diện trong `Detect.cs` |
| W11 | Đường dẫn > 260 ký tự (B5) | `Core.cs` `Tree.Walk`, `WinPath` | Cây sâu > 300 ký tự trong TEMP vẫn được quét | Không còn `Directory.GetFileSystemEntries` trong `Walk` |
| W12 | Hiệu năng và nhiễu quét (C) | `Detectors.cs`, `Detect.cs`, `Scan.cs`, UI | Macro đuôi ZIP; ADS chỉ trên NTFS; `IsSigned` không ném | Quét 5.000 tệp không tăng thời gian > 20 % (đo tay) |
| W13 | Luồng WriteProtect ↔ làm sạch (B7) | `Policy.cs`, `ProtectPage.cs` | Máy trạng thái `wp-pending` (hết hạn, khôi phục khi khởi động) | Tự bật lại khoá sau 10 phút hoặc khi xong |
| W14 | Quét lại thông minh (B8) | `Behavior.cs`, `Guard.cs` | 300 `.txt` không quét cả ổ; 30 `.lnk` thì quét; báo 1 lần/10 phút | Toast không lặp mỗi 2 phút |
| W15 | `chkdsk /f` không treo (B9) | `Native.Run`, `Repair.cs` | Không tự động trong self-test | Chạy tay trên VHD: trả lời dismount, không treo 6 giờ |
| W16 | Hàng đợi sự kiện có ID cho tray (thay pipe, giá rẻ) | `Events.cs`, `Settings.cs`, `TrayApp.cs` | Ghi 3 sự kiện, đọc từ id 1 → 2; xoay tệp không mất con trỏ; dòng 4 cột cũ vẫn đọc được | Tray hiện nhiều sự kiện, không mất thông báo |

### W10 — quyết định kỹ thuật

`High = strong && !flagOnly && score >= threshold`. Để 37 phép kiểm tra hiện có vẫn đạt,
phải chỉnh `scoreThreshold` hoặc trọng số trong cùng commit, và thêm một phép kiểm
tra chứng minh ngưỡng có hiệu lực. Nếu chọn hướng ngược lại (giữ "Strong là đủ"), thì
sửa `AGENTS.md` điều 7 cho khớp thay vì để tài liệu sai.

`Devices.SetEnabled`: khuyến nghị **xoá** (mã chết, `pnputil` chỉ có trên Windows 10
1809+). Nếu muốn giữ, phải có nút trong trang Thiết bị, xác nhận hai lớp và ghi log —
đúng điều 13.

### W12 — chi tiết

- Macro: đọc 1 MB cuối (central directory của ZIP) thay vì 8 MB đầu; bỏ trần 32 MB
  xuống còn "đọc đuôi" để không tốn bộ nhớ.
- ADS: `ScanContext.IsNtfs` cache theo ổ; ổ FAT32/exFAT bỏ qua detector này.
- `IsSigned`: thêm `PeHasSignature` (đọc DOS/NT header, không ném) rồi mới gọi API;
  cache theo `(path, size, lastWrite)` tối đa 4.096 mục.
- `FlagOnly`: UI mặc định ẩn mục chỉ gắn cờ, có chip "N mục chỉ gắn cờ" để mở.

## 4. Phát hành 2.2 — VirusTotal opt-in và chất lượng CLI (ước 5–6 ngày)

| # | Việc | Xong khi |
| --- | --- | --- |
| W17 | `VirusTotal.cs` là nơi **duy nhất** có mã mạng; `HttpWebRequest`, TLS 1.2 bật rõ, timeout 15 giây, 4 yêu cầu/phút, thử lại tối đa 1 lần | Máy chủ giả loopback nhận đúng URL chứa SHA-256 và `x-apikey`; không có tệp/tên/đường dẫn trong payload |
| W18 | Khoá API bằng DPAPI (`ProtectedData`), không ghi log; công tắc + ô nhập + hộp thoại đồng ý một lần trong `ProtectPage` | Tắt công tắc → không có lời gọi mạng; gỡ cài đặt xoá khoá |
| W19 | `PRIVACY.md`; README đổi "không mạng" → "không mạng trừ khi bạn bật VirusTotal"; `AGENTS.md` điều 10; `--vt <path>`, `--help`, `--version`, `--scan --json` | Tài liệu khớp hành vi; `--help` liệt kê đủ lệnh |

Nguyên tắc giữ nguyên: chỉ tra khi người dùng bấm, chỉ gửi hash, kết quả chỉ tham
khảo, **không** tự cách ly dựa trên VirusTotal.

## 5. Phát hành 2.3 — BadUSB tốt hơn (ước 12–15 ngày + thiết bị thử)

Chỉ bắt đầu sau W9 và sau khi `docs/design-badusb.md` được duyệt.

| # | Việc | Ghi chú |
| --- | --- | --- |
| W20 | `docs/design-badusb.md`: nguồn dữ liệu, ngưỡng, quyền riêng tư, cách đo báo nhầm | Chưa viết mã khi chưa duyệt |
| W21 | `InputRate` trong tiến trình tray: Raw Input `RIDEV_INPUTSINK`, cửa sổ ẩn, **chỉ** timestamp + handle thiết bị | Không ghi mã phím — tránh thành keylogger; ghi vào `PRIVACY.md` |
| W22 | Ghép với tiến trình cha qua WMI `Win32_ProcessStartTrace`; cảnh báo khi HID mới + tốc độ gõ phi nhân loại + shell có cha `explorer.exe` | Chỉ cảnh báo, không tắt thiết bị; README ghi rõ là heuristic |

## 6. Phát hành 3.0 — Named pipe (chỉ sau khi được duyệt)

Điều kiện tiên quyết: W4 xong, GUI không còn chạy admin toàn thời gian (hoặc có
`--tray` chạy quyền thường), và W16 đã giải quyết mất thông báo.

| # | Việc |
| --- | --- |
| W23 | Thiết kế + thi hành pipe: `PipeSecurity` chỉ SID người dùng tương tác + Administrators, `PIPE_REJECT_REMOTE_CLIENTS`, một instance; xác thực client bằng `GetNamedPipeClientProcessId` + đường dẫn exe đã ghim + băm; allow-list lệnh JSON có phiên bản, giới hạn kích thước, không nhận đường dẫn tự do |

Nếu chưa duyệt: giữ mô hình tệp + ACL, chỉ thêm W16.

## 7. Thứ tự, phụ thuộc, ước lượng

| Giai đoạn | Mục | Phụ thuộc | Ước lượng |
| --- | --- | --- | --- |
| 2.1.0 | W1–W7 | W1 trước (mọi lệnh khác dùng nó) | 9–11 ngày |
| 2.1 | W8–W16 | W10 sau W6; W13 sau W2; W16 độc lập | 12–15 ngày |
| 2.2 | W17–W19 | W6 (định dạng luật) không bắt buộc | 5–6 ngày |
| 2.3 | W20–W22 | W9 + thiết bị thử | 12–15 ngày |
| 3.0 | W23 | W4, W16, quyết định kiến trúc | 15–20 ngày |

Mốc phát hành: `v2.1.0` (nền tảng) → `v2.1.1` (tính năng) → `v2.2` (VT) → `v2.3`
(BadUSB) → `v3.0` (pipe). Mỗi mốc tăng `AppInfo.Version` và tag tương ứng.

## 8. Checklist kiểm chứng trên máy thật (trước mỗi mốc)

- [ ] Windows 10/11 x64, tài khoản **không** admin → luồng UAC, `--status` đúng.
- [ ] USB FAT32, exFAT, NTFS; USB có công tắc khoá ghi vật lý (B7).
- [ ] Bàn phím USB ngoài, bàn phím Bluetooth, thiết bị composite nếu có (B2, B3).
- [ ] Windows tiếng Việt: đọc được tổng kết `robocopy` (đã có `CountTree` dự phòng).
- [ ] USB nhiều tệp (≈ 300.000) và cây thư mục sâu > 260 ký tự (B5, W11).
- [ ] Windows 7 x86 nếu còn cam kết hỗ trợ: `WinVerifyTrust`, và `pnputil` không có
  `/disable-device` (chỉ dùng để chẩn đoán, không phải đường chạy chính).
- [ ] `chkdsk /f` trên USB nháp có dữ liệu không quan trọng (B9, W15).
- [ ] Gỡ cài đặt trên thư mục dữ liệu tuỳ chỉnh có tệp người dùng (W3).
- [ ] Chạy 37+ phép kiểm tra trên máy sạch, không có mạng (trừ khi bật VT).

## 9. Cần chủ dự án chốt trước khi bắt đầu

1. **Gỡ cài đặt**: mặc định **giữ** cách ly + bản sao lưu ổ, người dùng phải bỏ chọn
   mới xoá? (khuyến nghị: có)
2. **`chkdsk /f`**: chỉ cho ổ `Removable`, ổ cố định phụ chỉ được chẩn đoán đọc? (khuyến nghị: có)
3. **`rules.json`**: chỉ đọc từ thư mục dữ liệu, bỏ qua tệp cạnh exe? (khuyến nghị: có, kèm `--import-rules`)
4. **Marker sao lưu cũ mất hiệu lực** → người dùng phải sao lưu lại trước `chkdsk /f`? (khuyến nghị: có)
5. **VirusTotal**: DPAPI, chỉ chạy thủ công, đổi tuyên bố "không mạng" trong README? (khuyến nghị: có)
6. **BadUSB**: không ghi mã phím, chỉ cảnh báo? (khuyến nghị: có) — và có thiết bị thử không?
7. **Named pipe**: hoãn tới 3.0, làm W16 trước? (khuyến nghị: có)

## 10. Rủi ro khi thực hiện

| Rủi ro | Giảm thiểu |
| --- | --- |
| Sửa W1 làm hỏng lệnh trên Windows 7 (PowerShell ở `System32\WindowsPowerShell`) | `SysExe` kiểm `File.Exists`, fallback `%WINDIR%`, self-test cho từng công cụ |
| W2 làm người dùng không sửa được ổ vì thiếu sao lưu | Thông báo rõ lý do + nút sao lưu lại; không nới điều kiện |
| W3 xoá nhầm dữ liệu người dùng | Marker + adopt + test trên thư mục giả có `bin` của người dùng |
| W10 đổi ngưỡng làm tăng báo nhầm hoặc bỏ sót | Bộ mẫu lành (W10/W12) + đo trên `viruts/` cục bộ (không commit) |
| W13 để ổ ở trạng thái mở khoá nếu ứng dụng bị kill | Tệp `wp-pending` + khôi phục lúc khởi động + hết hạn 10 phút |
| W17 vô tình gửi dữ liệu | Chỉ một tệp có mã mạng; test loopback khẳng định payload chỉ có hash |
| BadUSB gây báo nhầm | Chỉ cảnh báo, ngưỡng cấu hình được, ghi rõ là heuristic |

## 11. Việc cố ý không làm (giữ nguyên quan điểm 2.0)

Sửa boot sector/MBR, format ổ, TestDisk/PhotoRec, file carving, YARA dùng DLL native,
tự động gửi mẫu ra mạng, tự động disable thiết bị. Nếu cần, chỉ làm hướng "báo cáo và
hướng dẫn" như bản phân tích đề xuất.
