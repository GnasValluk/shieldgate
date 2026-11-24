# Thiết kế BadUSB (W20)

> Trạng thái: **đề xuất, chưa viết mã**. Cần chủ dự án duyệt các mục ở §11 trước
> khi bắt đầu W21. Tài liệu này chỉ nói về phát hiện và **cảnh báo**; không có
> tính năng chặn thiết bị.

## 1. Mục tiêu

1. Phát hiện một bàn phím/HID **mới, chưa tin cậy** vừa được cắm vào và đang gõ
   với tốc độ phi nhân loại, ngay trước khi một shell tương tác xuất hiện.
2. Cảnh báo người dùng bằng balloon + sự kiện + log, kèm thiết bị và bằng chứng
   tối thiểu để họ tự quyết định.
3. Không đọc, không lưu, không gửi mã phím hay nội dung gõ.
4. Không tự tắt thiết bị, không chặn cổng, không sửa registry thiết bị.

## 2. Không nằm trong phạm vi

- Chặn BadUSB ở mức kernel/driver, kiểm tra firmware, attestation USB.
- Chống cắm thiết bị đã có sẵn trước khi ShieldGate chạy.
- Phân biệt tuyệt đối bàn phím người với thiết bị giả lập bàn phím.
- Ghi lại từng phím, từng cửa sổ hay nội dung clipboard.
- Tắt/disable thiết bị, kể cả khi người dùng bấm nhầm; chỉ có hướng dẫn.
- Bluetooth pairing attack, wireless keyboard sniffing.

## 3. Hiện trạng (2.1)

Đã có trong `Devices.cs` và `Guard.cs`:

- `Devices.Inventory` liệt kê HID/bàn phím, phân loại bàn phím ngoài bằng bus
  (`ACPI\`, `ROOT\`, `SWD\`, HID không VID = trong máy; `USB\`, `BTH\` = ngoài).
- `NewKeyboard` = có bàn phím ngoài chưa có trong trust store.
- `CompositeSuspect` = cùng VID/PID vừa có ổ nhớ vừa có bàn phím.
- `RecentShellProcess(20 giây)` tìm `cmd/powershell/pwsh/wscript/cscript/mshta`
  mới tạo; nếu có thì log WARN + sự kiện.

Hạn chế:

- Chỉ dựa vào thời điểm, không có tốc độ gõ; một bàn phím mới cắm rồi người dùng
  mở Command Prompt bình thường cũng có thể báo.
- Không kiểm tra tiến trình cha; shell do installer/service tạo cũng tính.
- Không có cooldown theo thiết bị, không có cách đánh dấu "đã tin cậy" ngay từ
  cảnh báo.
- Log hiện tại có thể chứa cả `CommandLine` của shell; với BadUSB, dòng lệnh có
  thể chứa nội dung người dùng gõ. W22 sẽ bỏ phần này.

## 4. Kiến trúc đề xuất

```
TrayApp (quyền người dùng, session tương tác)
  ├─ cửa sổ ẩn HWND_MESSAGE
  │    ├─ RegisterRawInputDevices(usage 0x01/0x06,
  │    │     RIDEV_INPUTSINK | RIDEV_DEVNOTIFY)
  │    ├─ WM_INPUT            → chỉ đọc RAWINPUTHEADER (hDevice, thời điểm)
  │    └─ WM_INPUT_DEVICE_CHANGE → GIDC_ARRIVAL / GIDC_REMOVAL
  │
  ├─ InputRate (W21)
  │    └─ đếm sự kiện theo bucket 100 ms, cửa sổ trượt 5 s
  │         → InputBurst { device, first, last, events, peakPerSecond }
  │
  ├─ ProcessWatch (W22)
  │    └─ 1 s/lần: Win32_Process (tên, pid, ppid, CreationDate)
  │         + Toolhelp32 để tra tên tiến trình cha
  │         → ShellStart { pid, name, parentName, created }
  │
  └─ BadUsb.Evaluate (W22)
       ├─ thiết bị mới + chưa trust?
       ├─ burst vượt ngưỡng?
       ├─ shell mới trong 10 s sau burst?
       ├─ tổ tiên gần nhất là explorer.exe?
       └─ cooldown theo thiết bị?
             └─ WARN: balloon + Events + Log (không tắt thiết bị)
```

### 4.1. Raw Input (W21)

- Đăng ký **một lần** cho `usUsagePage = 0x01`, `usUsage = 0x06` (Keyboard),
  `dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY`, `hwndTarget` = cửa sổ ẩn.
  `RIDEV_INPUTSINK` cần cửa sổ nhận input cả khi không foreground.
- Trong `WM_INPUT`: cấp buffer đủ lớn cho `GetRawInputData(RID_INPUT)` nhưng
  **chỉ đọc `RAWINPUTHEADER`** (`dwType`, `hDevice`, `wParam`); không đọc, không
  lưu và không chuyển tiếp phần `RAWKEYBOARD`. Không gọi `MapVirtualKey`,
  `GetKeyboardState`, `GetAsyncKeyState`, không `SetWindowsHookEx`. Thời điểm
  lấy từ `DateTime.UtcNow` ngay khi xử lý `WM_INPUT`.
- `hDevice == IntPtr.Zero` → bỏ qua. Đây là input tổng hợp (RDP, automation,
  on-screen keyboard) và không gắn được với thiết bị vật lý.
- `WM_INPUT_DEVICE_CHANGE`: `GIDC_ARRIVAL`/`GIDC_REMOVAL`; lấy tên thiết bị bằng
  `GetRawInputDeviceInfo(RIDI_DEVICENAME)` để ghép với instance ID đã có trong
  `devices.json`. Tên raw chỉ giữ trong bộ nhớ, không ghi log.
- Chỉ sống trong tiến trình tray; đóng cửa sổ là dừng. Không chạy trong GUI
  chính để tránh hai nguồn đếm trùng.
- Không cần quyền quản trị cho Raw Input trong session của chính người dùng.

### 4.2. Theo dõi tiến trình (W22)

Roadmap ghi `Win32_ProcessStartTrace`; thực tế tray hiện vẫn nằm trong danh sách
cần quyền quản trị (`NeedsAdmin`) nên có thể chạy elevated. Thiết kế vẫn chọn
**polling 1 giây** làm đường chính, vì:

- `Win32_ProcessStartTrace` nằm trong `root\cimv2` và cần quyền quản trị.
- Polling `SELECT ProcessId, ParentProcessId, Name, CreationDate FROM
  Win32_Process WHERE Name=...` đọc được tiến trình của chính người dùng, không
  cần admin, và đủ để bắt shell sống lâu hơn 1 giây (payload BadUSB luôn vậy).
- Có thể thêm `ManagementEventWatcher` (`__InstanceCreationEvent WITHIN 1`) sau
  này nếu đo thấy polling bỏ sót; API `ProcessWatch` giữ kín cách lấy dữ liệu.
- Polling cũng giữ W22 chạy được nếu W23 tách tray sang quyền thường.

Bù cho việc tiến trình có thể thoát trước khi tra cha:

- Mỗi vòng poll lấy một Toolhelp snapshot (rẻ) cho `pid → (ppid, name)`; WMI
  chỉ dùng để lấy `CreationDate` của shell. Không cần cache riêng.
- Đi ngược tối đa 3 cấp để bắt `cmd.exe → powershell.exe → explorer.exe`; chỉ
  cần **một** tổ tiên là `explorer.exe`.
- Nếu không xác định được cha (đã thoát, WMI lỗi) → **không cảnh báo**. Thà bỏ
  sót còn hơn báo nhầm khi không có bằng chứng.

Không dùng `CommandLine` cho cảnh báo BadUSB: nội dung dòng lệnh có thể chứa
chuỗi người dùng vừa gõ. Chỉ log tên image và tên cha.

### 4.3. Luồng dữ liệu và trạng thái

- `InputRate` giữ ring 50 bucket × 100 ms (5 giây) cho mỗi `hDevice`.
- Mỗi lần có sự kiện: tăng bucket hiện tại; tính `peakPerSecond` = số sự kiện
  lớn nhất trong 20 bucket liên tiếp / 2.
- Khi phát hiện burst (vượt ngưỡng), ghi lại `InputBurst` gần nhất và **xóa
  ring** để không báo lặp; cooldown xử lý phần còn lại.
- `BadUsb` không giữ lịch sử phím; chỉ giữ burst gần nhất và mốc cảnh báo cuối
  theo khóa thiết bị (VID/PID/serial/instance ID như trust store).

## 5. Mô hình dữ liệu (dự kiến)

```csharp
// src/core/InputRate.cs (W21) — bộ đếm thuần, không P/Invoke.
// src/core/BadUsb.cs (W22) — ngưỡng, trust, cooldown.
internal sealed class InputBurst
{
    public string Device;         // nhãn VID/PID để log, không có serial
    public string Raw;            // đường dẫn raw, chỉ trong bộ nhớ
    public DateTime First;
    public DateTime Last;
    public int Events;
    public double PeakPerSecond;
}

internal sealed class ShellStart
{
    public int Pid;
    public string Name;           // cmd.exe, powershell.exe, ...
    public string ParentName;     // explorer.exe nếu tìm được
    public DateTime Created;      // UTC
    public bool ExplorerAncestor; // có explorer.exe trong 3 cấp
}

internal sealed class BadUsbVerdict
{
    public bool Alert;
    public string Reason;         // "rate+new+shell+explorer"
    public int Score;             // 0..10, để log; không dùng ngưỡng phát hiện tệp
}

internal static class BadUsb
{
    public static bool Enabled { get; }  // Settings.BadUsbEnabled, mặc định từ rules.json
    public static BadUsbVerdict Evaluate(InputBurst burst, DeviceInfo device,
                                         ShellStart shell, DateTime now);
    public static void MarkTrusted(string deviceKey);
}
```

`src/ui/InputCollector.cs` là nơi duy nhất P/Invoke Raw Input; `src/core/InputRate.cs`
giữ phép đếm thuần để self-test chạy được mà không cần bàn phím thật. `BadUsb`
nhận dữ liệu đã tổng hợp.

## 6. Ngưỡng đề xuất

| Tham số | Mặc định | Lý do |
| --- | --- | --- |
| `newDeviceSeconds` | 120 | BadUSB hành động trong vài giây sau khi cắm; 2 phút đủ rộng nhưng không bám theo thiết bị cả ngày |
| `windowSeconds` | 2 | Đủ để phân biệt burst với gõ bình thường, không quá dài để bỏ lỡ |
| `minEvents` | 30 | 15 sự kiện/giây × 2 giây; một phím kẹt không đủ |
| `eventsPerSecond` | 15 | Người gõ liên tục 5–8 ký tự/giây, đỉnh hiếm 12–15; BadUSB thường 100–1000 |
| `shellSeconds` | 10 | Payload mở Run/cmd trong vài giây sau khi gõ |
| `alertSeconds` | 600 | Một cảnh báo mỗi thiết bị mỗi 10 phút |
| `requireExplorerParent` | true | Chỉ cảnh báo khi shell do người dùng tương tác mở, không phải installer/service |
| `enabled` | true | Chỉ cảnh báo; có công tắc trong trang Bảo vệ |

Công thức:

```
alert = enabled
     && device != null && device.Trust != DeviceTrust.Trusted
     && IsNew(device.Key, now, newDeviceSeconds)
     && burst.Events >= minEvents
     && burst.PeakPerSecond >= eventsPerSecond
     && shell != null
     && shell.Created >= burst.First - 1s
     && shell.Created <= burst.Last + shellSeconds
     && (!requireExplorerParent || HasAncestor(shell, "explorer.exe", 3))
     && now >= lastAlert[device.Key] + alertSeconds
```

`IsNew` dùng `DeviceInfo.FirstSeen` do `DeviceStore.Sync` ghi khi thiết bị xuất
hiện lần đầu (đọc từ `devices.json`), đổi về UTC trước khi so với burst; không
thêm trường mới. Thiết bị đã có trong trust store (`DeviceTrust.Trusted`) bỏ qua
trước khi tính burst.

### 6.1. Vì sao vẫn có thể báo nhầm

- **Phím kẹt/auto-repeat**: giữ một phím tạo ~30 sự kiện/giây, vượt ngưỡng.
  Nhưng không có shell mới + cha explorer trong 10 giây thì không cảnh báo. Đây
  là lý do **không bao giờ** cảnh báo chỉ vì tốc độ.
- **Người gõ nhanh trên bàn phím mới** rồi mở Run → `cmd`: hiếm nhưng có thể.
  Cooldown 10 phút và nút "Tin cậy thiết bị" xử lý.
- **Macro/gaming keyboard** gõ hàng loạt: chỉ báo nếu đồng thời mở shell từ
  explorer trong cửa sổ 10 giây.
- **Barcode scanner** gõ rất nhanh: thường không mở shell; nếu quét vào Run thì
  có thể báo. Chấp nhận, vì đó đúng là hành vi giống BadUSB.
- **KVM/USB switch** làm bàn phím re-enumerate thành thiết bị mới: có thể báo
  nếu người dùng vừa gõ nhanh vừa mở shell; trust store là cách xử lý.
- **RDP/automation/on-screen keyboard**: `hDevice == 0` → bỏ qua.
- **Installer mở cmd từ explorer** ngay sau khi cắm bàn phím: có thể báo; log
  ghi rõ cha explorer và người dùng tự đánh giá.

## 7. Cảnh báo và hành động

- Balloon tray, không hộp thoại modal (không cắt ngang việc đang gõ).
- Nội dung: tên thiết bị, VID/PID, "N sự kiện trong X giây", tên shell và cha;
  câu kết luận luôn là "dấu hiệu, không phải kết luận".
- Sự kiện `KindDevice`, mức `WARN`, subject = tên thiết bị, detail = lý do.
- Log WARN cùng nội dung; **không** command line, không mã phím, không đường dẫn
  raw.
- Nút trong balloon/Devices: **Tin cậy thiết bị** (ghi trust store, không hỏi
  lại), **Xem thiết bị**, **Bỏ qua**. Balloon hiện chỉ mở trang Thiết bị; nút
  tin cậy sẵn có ở đó. Không có nút tắt thiết bị.
- Trang Bảo vệ thêm công tắc "Cảnh báo BadUSB" (mặc định bật), cạnh các công tắc
  hiện có; tắt thì không đăng ký Raw Input, không poll tiến trình.

## 8. Quyền riêng tư

- Không đọc mã phím/scan code; chỉ đếm sự kiện và thời điểm.
- W22 chỉ đọc PID, PID cha, tên image và `CreationDate` của shell; **không**
  `CommandLine`, không tiêu đề cửa sổ, không clipboard.
- Không lưu chuỗi sự kiện; ring bucket nằm trong bộ nhớ và bị xóa sau mỗi burst.
- Không ghi `RAWINPUTDEVICENAME` (có thể chứa serial) vào log; chỉ dùng để ghép
  với instance ID đã có trong `devices.json`.
- Không gửi gì ra mạng.
- W21 cập nhật `PRIVACY.md`: thêm mục "Input timing (BadUSB)" nói rõ dữ liệu nào
  được đếm, thời gian giữ (trong bộ nhớ), và việc không có keylogging.
- Kiểm tra tĩnh trong `test.ps1` (W21): fail nếu mã nguồn chứa `RAWKEYBOARD`,
  `SetWindowsHookEx`, `GetAsyncKeyState`, `GetKeyboardState` hoặc
  `MapVirtualKey`. Đây là hàng rào rẻ tiền để không vô tình thành keylogger.

## 9. Kiểm thử

### 9.1. Self-test (W21/W22)

- 6 sự kiện/giây trong 2 giây + thiết bị mới + shell explorer → **không** báo.
- 50 sự kiện/giây + thiết bị mới + shell explorer → báo.
- Burst mạnh nhưng thiết bị đã trust → không báo.
- Burst mạnh + shell có cha `services.exe` → không báo.
- Burst mạnh + shell tạo trước burst 30 giây → không báo.
- Burst mạnh + shell tạo sau burst 15 giây → không báo (ngoài `shellSeconds`).
- Hai burst liên tiếp trong 10 phút → một cảnh báo.
- `enabled = false` → `Evaluate` trả `Alert = false`.
- Không có API nào nhận mã phím (kiểm tra bằng chữ ký hàm, không chỉ hành vi).

Đã triển khai thành self-test 64–65 (`total` 70 → 72); các ca trên máy thật ở
§9.2 vẫn chờ thiết bị thử.

### 9.2. Máy thật

| Kịch bản | Kỳ vọng |
| --- | --- |
| Gõ bình thường 8 giờ trên bàn phím có sẵn | 0 cảnh báo |
| Gõ nhanh trên bàn phím mới, không mở shell | 0 cảnh báo |
| Giữ phím (auto-repeat) 30 giây | 0 cảnh báo |
| BadUSB thử nghiệm (Rubber Ducky/vật tương đương), 5 lần | ≥ 4 lần cảnh báo trong 10 giây |
| BadUSB nhưng thiết bị đã trust | 0 cảnh báo |
| KVM switch + mở cmd | có thể 1 cảnh báo; trust xong thì im |
| RDP/automation gõ lệnh | 0 cảnh báo (`hDevice = 0`) |
| Barcode scanner quét vào Notepad | 0 cảnh báo |

Đo báo nhầm: ghi số cảnh báo/8 giờ trên 3 máy (laptop có bàn phím trong, desktop
dùng bàn phím USB, máy có KVM). Mục tiêu: **0 cảnh báo** ở 8 giờ gõ bình thường,
và không có trường hợp nào tự tắt thiết bị.

## 10. Triển khai và phụ thuộc

| Bước | Tệp | Nội dung |
| --- | --- | --- |
| W21 ✅ | `src/ui/InputCollector.cs` | cửa sổ ẩn, Raw Input, đếm bucket, `InputBurst`; `src/core/InputRate.cs` giữ phép đếm |
| W21 ✅ | `src/ui/TrayApp.cs` | tạo/hủy cửa sổ ẩn theo vòng đời tray |
| W21 ✅ | `PRIVACY.md`, `test.ps1` | mục input timing; kiểm tra tĩnh API bị cấm |
| W22 ✅ | `src/core/BadUsb.cs` | ngưỡng, trust, cooldown, `Evaluate` |
| W22 ✅ | `src/core/ProcessWatch.cs` | poll WMI + Toolhelp, cache cha |
| W22 ✅ | `src/core/Rules.cs` | `badusb` trong `rules.json`, version 6, clamp giá trị |
| W22 ✅ | `src/ui/Strings.cs` | câu cảnh báo, công tắc; tin cậy dùng nút sẵn có ở trang Thiết bị |
| W22 ✅ | `src/ui/ProtectPage.cs` | công tắc "Cảnh báo BadUSB" |
| W22 ✅ | `src/ui/SelfTest.cs` | các ca ở §9.1, tăng `total` |

Phụ thuộc: W9 (phân loại bàn phím ngoài) đã xong. Không phụ thuộc W17–W19.

## 11. Cần duyệt trước khi viết mã

1. **Ngưỡng mặc định** ở §6 (15 sự kiện/giây, 30 sự kiện, cửa sổ 2 giây, thiết
   bị mới 120 giây, shell 10 giây) có chấp nhận không?
2. **Mặc định bật** cảnh báo BadUSB (chỉ cảnh báo), hay tắt để người dùng tự
   bật?
3. **Polling 1 giây** thay cho `Win32_ProcessStartTrace` (không cần admin) có
   chấp nhận không? Tray hiện vẫn có thể chạy elevated, nhưng polling giữ đường
   chạy đúng nếu sau này tách tray sang quyền thường. Nếu cần bắt shell sống
   < 1 giây, phải thêm event watcher và chấp nhận yêu cầu quyền.
4. **Tin cậy thiết bị** dùng chung trust store hiện có (toàn máy) hay tách riêng
   theo người dùng?
5. **Không log command line** của shell trong cảnh báo BadUSB — đồng ý chứ?
6. Có **thiết bị thử BadUSB** (Rubber Ducky hoặc tương đương) để đo §9.2 không?
   Nếu không, W22 chỉ có self-test tổng hợp và phải ghi rõ chưa đo trên phần cứng
   thật.

**Trạng thái:** chủ dự án đã duyệt "mặc định" ngày 23/11/2026 (ngưỡng §6, bật
sẵn, polling 1 giây, dùng trust store chung, không log command line). W21 và
W22 đã triển khai theo đúng các mục trên; chưa có thiết bị BadUSB thật nên §9.2
vẫn là việc đo trên phần cứng ở bản phát hành.

## 12. Rủi ro

| Rủi ro | Giảm thiểu |
| --- | --- |
| Vô tình thành keylogger | Chỉ đọc header; kiểm tra tĩnh API bị cấm; review bắt buộc |
| Báo nhầm vì auto-repeat | Không cảnh báo chỉ vì tốc độ; bắt buộc shell + cha explorer |
| Bỏ sót payload chậm | Ngưỡng cấu hình được; ghi rõ là heuristic |
| Tray chạy quyền thường không thấy shell | Polling cùng user; fail open (không báo) khi thiếu dữ liệu |
| Hai nguồn đếm (GUI + tray) | Chỉ tray đăng ký Raw Input |
| Log rò nội dung gõ | Không log command line/raw device name; chỉ tên image và cha |
| Người dùng hoảng vì cảnh báo | Câu chữ "dấu hiệu, không phải kết luận"; có nút tin cậy; không tự tắt thiết bị |
