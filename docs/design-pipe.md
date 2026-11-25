# Thiết kế Named Pipe (W23)

> Trạng thái: **đề xuất, chưa viết mã**. Cần chủ dự án duyệt các mục ở §13 trước
> khi bắt đầu W23. Tài liệu này chỉ nói về kênh điều khiển giữa GUI và tiến trình
> nền; không thay đổi mô hình cách ly, trust store hay quy tắc an toàn hiện có.

## 1. Mục tiêu

1. Cho GUI chạy **quyền người dùng** gửi lệnh cần quyền SYSTEM (dọn ổ, đổi chính
   sách, đọc registry, tác vụ) mà không phải nâng quyền cả cửa sổ.
2. Không mở thêm bề mặt tấn công: chỉ tiến trình ShieldGate đã ghim mới nói được
   với engine; không nhận đường dẫn tự do, không nhận lệnh ngoài allow-list.
3. Giữ nguyên hành vi an toàn: mọi thao tác phá huỷ vẫn có xem trước, xác nhận,
   log; không ghi lên ổ hệ thống; không xoá trước khi cách ly được.
4. Giữ đường lui: nếu pipe tắt hoặc engine không chạy, GUI vẫn quét đọc và xử lý
   thư mục cục bộ như 2.3; không tự động nâng quyền thay người dùng.

## 2. Không nằm trong phạm vi

- Thay thế toàn bộ mô hình tệp + ACL. `clean.lock`, `guard.cfg`, `events.tsv`,
  `devices.json`, `quarantine` vẫn là nguồn sự thật.
- Pipe qua mạng, RPC, HTTP, WebSocket, hoặc điều khiển từ máy khác.
- Cho phép client gửi đường dẫn tuyệt đối, tên tệp, mã lệnh shell hay tham số
  dòng lệnh tự do.
- Chạy lệnh với quyền cao hơn quyền của engine (SYSTEM) hoặc tạo tiến trình con
  theo yêu cầu của client.
- Thay đổi giao diện xác nhận của người dùng. Hộp thoại vẫn do GUI hiện; engine
  chỉ nhận kế hoạch đã được người dùng xác nhận.
- Ký số release. W23 chỉ ghim đường dẫn + SHA-256; chữ ký Authenticode là việc
  của bản phát hành sau.

## 3. Hiện trạng 2.3

- GUI và engine dùng chung thư mục dữ liệu; `CleanLock` (`clean.lock`,
  `FileShare.None`) bảo đảm một tiến trình dọn một ổ tại một thời điểm.
- Thông báo từ nền sang tray đi qua `events.tsv` + `events.tsv.id` (W16), không
  cần pipe.
- `Guard` chạy dưới tác vụ SYSTEM; GUI hiện xin quyền admin khi mở.
- Các thao tác ghi đã có kiểm tra nền tảng: `Repair.RefuseReason`,
  `UsbScan.IsDriveRoot`, `Policy.BeginCleanPause`, `Quarantine.Add`.
- Chưa có kênh lệnh trực tiếp. GUI gọi hàm trong cùng tiến trình; CLI gọi tiến
  trình mới. Vì vậy GUI phải elevated để dọn ổ.

## 4. Kiến trúc đề xuất

```
GUI (quyền người dùng, session tương tác)
  └─ PipeClient
       ├─ mở \\.\pipe\ShieldGate.Engine.v1
       ├─ gửi JSON có phiên bản, id, cmd, args
       └─ nhận kết quả / tiến độ / lỗi

Engine (SYSTEM, do --watch hoặc --engine host)
  ├─ PipeServer (một instance, PIPE_REJECT_REMOTE_CLIENTS)
  │    ├─ ACL: SYSTEM + Administrators toàn quyền, Interactive đọc/ghi
  │    ├─ xác thực client: PID → image path → SHA-256 đã ghim
  │    ├─ kiểm tra session: khác 0, không phải tài khoản dịch vụ
  │    └─ allow-list lệnh + giới hạn kích thước/thời gian
  ├─ JobRunner (một job nặng tại một thời điểm)
  │    ├─ scan / clean / unhide / rename-base64
  │    └─ tiến độ ghi trong bộ nhớ, đọc qua job.status
  └─ lõi hiện có: UsbScan, Quarantine, UsbRestore, Policy, Repair, Events
```

### 4.1. Ai host pipe

Chọn một trong hai, cần chủ dự án duyệt:

- **Phương án A (khuyến nghị):** `--watch` mở pipe khi chạy. Không thêm tác vụ,
  không thêm tiến trình; engine đã có vòng quét nền và quyền SYSTEM.
- **Phương án B:** thêm `--engine` + tác vụ riêng. Tách vòng quét khỏi kênh lệnh,
  nhưng phải quản lý thêm vòng đời, mutex và cập nhật gỡ cài đặt.

Thiết kế dưới đây không phụ thuộc lựa chọn; chỉ khác điểm khởi tạo pipe.

## 5. Giao thức

### 5.1. Khung tin

- Pipe: `\\.\pipe\ShieldGate.Engine.v1`.
- Chế độ: `PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_ACCESS_DUPLEX`.
- Mỗi tin: 4 byte độ dài little-endian + UTF-8 JSON. Tối đa **64 KiB** mỗi tin.
- Một client hoạt động tại một thời điểm. Client thứ hai nhận `busy` rồi đóng.
- Timeout: kết nối 5 giây, đọc tin 10 giây, job tối đa 30 phút, idle 60 giây.

### 5.2. Yêu cầu

```json
{"v":1,"id":"7f3c","cmd":"status.get","args":{}}
```

- `v`: phiên bản giao thức, hiện `1`. Engine từ chối `v` khác.
- `id`: chuỗi 1–32 ký tự, do client sinh; engine trả lại nguyên văn.
- `cmd`: một trong allow-list §7.
- `args`: object; mọi khóa lạ bị từ chối, không bỏ qua im lặng.

### 5.3. Trả lời

```json
{"v":1,"id":"7f3c","ok":true,"result":{"guard":true,"registry":true}}
{"v":1,"id":"7f3c","ok":false,"error":"denied","message":"client image is not pinned"}
```

- `error` là mã ổn định cho máy đọc: `bad-json`, `too-large`, `bad-version`,
  `unknown-command`, `bad-args`, `denied`, `busy`, `not-found`, `expired`,
  `refused`, `failed`, `cancelled`.
- `message` là câu ngắn, không chứa nội dung tệp; GUI dịch lại nếu cần.

### 5.4. Job dài

Các lệnh quét/dọn có thể lâu hơn timeout đọc:

1. `job.start` trả `job` id ngay.
2. GUI gọi `job.status` mỗi 500 ms để lấy `state`, `files`, `items`, `seconds`,
   `warnings`.
3. `job.cancel` đặt cờ huỷ; engine dừng ở điểm an toàn kế tiếp.
4. Khi xong, `job.status` trả `plan` id (với quét) hoặc kết quả cuối.

Chỉ một job nặng tại một thời điểm; job thứ hai nhận `busy`.

## 6. Xác thực client và ACL

### 6.1. ACL pipe

Tạo pipe với `PipeSecurity`:

- `LocalSystemSid`: `FullControl`.
- `BuiltinAdministratorsSid`: `FullControl`.
- `InteractiveSid` (`IU`, S-1-5-4): `ReadWrite`, để GUI của người dùng đang đăng
  nhập mở được mà không cần admin.
- Từ chối `NetworkSid` và `AnonymousSid`.
- Chủ sở hữu: SYSTEM (hoặc Administrators nếu engine chạy admin).

Đặt cờ native `PIPE_REJECT_REMOTE_CLIENTS` (0x00000008) khi tạo pipe. .NET 4.0
không expose cờ này qua `NamedPipeServerStream`; dùng một wrapper `CreateNamedPipe`
nhỏ trong `Native`/`PipeServer`, không thêm thư viện. Nếu không đặt được cờ, từ
chối mọi client có `GetNamedPipeClientComputerName` khác rỗng.

### 6.2. Kiểm tra client

Trước khi đọc tin đầu tiên:

1. `GetNamedPipeClientProcessId` → PID.
2. `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`.
3. `QueryFullProcessImageName` → đường dẫn đầy đủ, chuẩn hoá.
4. `GetNamedPipeClientSessionId` → phải khác 0 (session dịch vụ bị từ chối).
5. `OpenProcessToken(TOKEN_QUERY)` + `GetTokenInformation(TokenUser)`:
   - từ chối SYSTEM, LocalService, NetworkService, Anonymous;
   - từ chối SID không phải người dùng tương tác.
6. So khớp ảnh tiến trình với **pin**:
   - `bin\client.pin` chứa hai dòng: đường dẫn đầy đủ + SHA-256.
   - Chấp nhận đúng đường dẫn đã ghim, hoặc bản sao `Paths.GuardExe` nếu băm
     trùng.
   - Sai đường dẫn hoặc sai băm → `denied`, ghi WARN, đóng kết nối.
7. Ghi log INFO: PID, session, tên tệp (không ghi hash đầy đủ? hash không nhạy
   cảm, có thể ghi 12 ký tự đầu).

`--install` ghi pin sau khi sao chép exe vào `bin`; `--uninstall` xoá pin. Khi
nâng cấp, `--install` ghi pin mới theo kiểu ghi tạm rồi `File.Replace` để không
để lại pin nửa vời.

### 6.3. Vì sao vẫn cần allow-list

ACL + băm chặn tiến trình lạ, nhưng một lỗi trong GUI vẫn có thể gửi lệnh sai.
Allow-list và kiểm tra tham số là lớp thứ hai; engine không bao giờ tin tham số
chỉ vì client đã qua xác thực.

## 7. Allow-list lệnh

| Lệnh | Quyền | Tham số | Ghi chú |
| --- | --- | --- | --- |
| `status.get` | đọc | `{}` | `Guard.GetStatus` + cấu hình hiện tại |
| `devices.list` | đọc | `{}` | cùng dữ liệu `--devices`, không ghi |
| `events.since` | đọc | `{"after":123}` | tối đa 200 dòng, cùng định dạng tray |
| `job.start` | theo lệnh con | `{"kind":"scan","drive":"E"}` | chỉ ổ, không đường dẫn |
| `job.status` | đọc | `{"job":"..."}` | tiến độ + kết quả |
| `job.cancel` | ghi | `{"job":"..."}` | dừng ở điểm an toàn |
| `clean.plan` | đọc | `{"drive":"E"}` | quét lại, trả plan + mục |
| `clean.apply` | ghi | `{"plan":"...","ids":[1,2],"confirm":true}` | hai bước |
| `unhide.plan` | đọc | `{"drive":"E"}` | xem trước |
| `unhide.apply` | ghi | `{"plan":"...","confirm":true}` | hai bước |
| `rename.plan` | đọc | `{"drive":"E"}` | đếm tên Base64 |
| `rename.apply` | ghi | `{"plan":"...","confirm":true}` | hai bước |

- `drive` chỉ là một ký tự ổ (`E`), engine tự dựng `E:\`. Không nhận
  `E:\sub`, `\\server\share`, `C:\Windows`, hay đường dẫn tương đối.
- Thao tác trên **thư mục** vẫn chạy trong GUI như 2.3: quét đọc, cách ly,
  unhide, đổi tên Base64 trên thư mục người dùng chọn. Pipe chỉ dành cho việc
  cần SYSTEM trên ổ.
- `clean.plan`/`unhide.plan`/`rename.plan` trả `plan` id + số liệu; engine giữ
  danh sách mục trong bộ nhớ, không nhận lại đường dẫn từ client.
- `*.apply` yêu cầu `confirm:true`; thiếu thì `bad-args`. Plan hết hạn sau 120
  giây, bị huỷ khi client ngắt kết nối, khi ổ rút, hoặc khi có plan mới.
- `plan` và `apply` phải đi trên cùng một kết nối (`PipeSession`); engine xoá
  plan khi client ngắt nên lệnh một lần `PipeClient.Send` không dùng được cho
  cặp plan/apply.
- Plan gắn với SID + session + serial ổ. Áp plan từ client/ổ khác → `refused`.

## 8. An toàn dữ liệu

Engine lặp lại **mọi** kiểm tra của đường GUI, không tin kế hoạch:

1. `Repair.RefuseReason(drive)` từ chối ổ hệ thống, ổ mạng, CD.
2. `UsbScan.IsDriveRoot` + `Native.IsRemovable` xác nhận đúng ổ rời.
3. `Native.IsReadOnlyVolume` → chỉ đọc thì `refused` trước khi ghi.
4. `Policy.BeginCleanPause`/`EndCleanPause` bọc thao tác ghi.
5. `CleanLock` vẫn giữ; pipe không thay khoá tệp.
6. Cách ly trước, xoá sau; `Quarantine.Add` là đường duy nhất.
7. Không bao giờ nhận đường dẫn đích; `UsbRestore` tự kiểm tra đích hợp lệ.
8. Mọi `*.apply` ghi log INFO/WARN kèm plan id, số mục, kết quả; không ghi nội
   dung tệp.

Nếu bất kỳ kiểm tra nào thất bại, trả `refused` kèm lý do đã dịch; không ghi gì.

## 9. Lỗi, giới hạn, đường lui

- **Engine không chạy:** client nhận `pipe-not-found`; GUI hiện "Bảo vệ nền chưa
  chạy" + nút mở/khởi động tác vụ. Không tự nâng quyền.
- **Pipe bận:** `busy`, GUI thử lại sau 1 giây, tối đa 5 lần.
- **JSON hỏng/quá lớn:** trả lỗi nếu còn đọc được `id`, rồi đóng kết nối; server
  không chết.
- **Client không hợp lệ:** đóng ngay, WARN một dòng, không trả chi tiết nội bộ.
- **Job bị huỷ/rút ổ:** dừng ở điểm an toàn, trả `cancelled`/`not-found`; không
  để lại batch cách ly rỗng.
- **Pipe tắt trong cấu hình:** `pipe.enabled=false` (mặc định trong 3.0 beta);
  GUI dùng đường cũ. Khi bật, vẫn giữ fallback tệp + ACL cho sự kiện/tray.
- **Xung đột phiên bản:** engine và GUI khác `v` → `bad-version`; GUI hiện yêu
  cầu cập nhật, không thử đoán.

## 10. Nhật ký và quyền riêng tư

- Log mỗi kết nối: PID, session, kết quả xác thực; mỗi lệnh: tên lệnh, plan id,
  số mục, thời gian, kết quả.
- Không log payload JSON, không log nội dung tệp, không log đường dẫn tệp trong
  plan (chỉ số lượng và ổ).
- Không gửi gì ra mạng. Pipe là cục bộ.
- `PRIVACY.md` thêm mục "Named pipe (3.0)": dữ liệu trao đổi, thời gian giữ
  trong bộ nhớ, và việc không có nội dung tệp.

## 11. Kiểm thử

### 11.1. Self-test (W23)

- Server/client trên pipe tên duy nhất: `status.get` trả đúng, `id` khớp.
- JSON hỏng, JSON thiếu `id`, JSON quá 64 KiB: lỗi đúng mã, server vẫn sống.
- `v` sai, `cmd` lạ, khóa `args` lạ: bị từ chối.
- `drive` là `C`, `E:\sub`, `\\server\share`, chuỗi rỗng: `bad-args`/`refused`.
- Plan hết hạn, plan id sai, `confirm` thiếu: bị từ chối.
- Plan của ổ A áp cho ổ B: `refused`.
- ACL: `PipeSecurity` có SYSTEM, Administrators, Interactive; không có
  Everyone/Anonymous.
- `client.pin` thiếu/sai băm: helper xác thực trả false.
- Kiểm tra tĩnh: không có `PipeDirection.InOut` mà thiếu ACL; không có
  `Process.Start` từ tham số client.

### 11.2. Máy thật

| Kịch bản | Kỳ vọng |
| --- | --- |
| GUI quyền thường + engine SYSTEM | dọn ổ thành công, không UAC |
| Client tự viết mở pipe | bị đóng, WARN, không có thao tác |
| Chạy lại sau reboot | engine mở pipe, GUI kết nối lại |
| Nâng cấp exe | pin cũ bị từ chối; `--install` cập nhật pin |
| Rút USB giữa job | job dừng, không có batch rỗng |
| Pipe từ máy khác | bị từ chối (remote flag/ACL) |
| Windows 7 x86 | pipe + `GetNamedPipeClientProcessId` hoạt động |

## 12. Triển khai đề xuất

| Bước | Tệp | Nội dung |
| --- | --- | --- |
| W23a | `src/core/PipeProtocol.cs` | khung tin, JSON, mã lỗi, giới hạn |
| W23b | `src/core/PipeServer.cs` | tạo pipe, ACL, cờ remote, vòng nhận |
| W23c | `src/core/PipeClient.cs` | kết nối, timeout, job polling |
| W23d | `src/core/PipeAuth.cs` | PID → ảnh → băm, session/SID, pin |
| W23e | `src/core/PipeCommands.cs` | allow-list, plan/apply, job |
| W23f | `src/core/Guard.cs` | host pipe trong `--watch` (hoặc `--engine`) |
| W23g | `src/ui/UsbPage.cs` | dùng client khi không elevated, chỉ gốc ổ; `ProtectPage` vẫn cần admin |
| W23h | `src/ui/SelfTest.cs`, `test.ps1` | các ca §11.1, kiểm tra tĩnh |
| W23i | `README.md`, `PRIVACY.md`, `plan.md` | tài liệu, nhật ký |

Ước lượng 15–20 ngày. Không thêm thư viện; chỉ P/Invoke có sẵn trong Win32.

## 13. Cần duyệt trước khi viết mã

1. **Host pipe:** trong `--watch` (khuyến nghị) hay tác vụ `--engine` riêng?
2. **GUI quyền thường:** có chuyển GUI sang non-admin trong 3.0 không, hay chỉ
   dùng pipe khi người dùng tự chạy không elevated?
3. **Pin client:** chấp nhận đường dẫn + SHA-256 trong `bin\client.pin`? Có cần
   hỗ trợ bản portable không, hay bản portable dùng đường cũ?
4. **Allow-list:** giữ đúng bảng §7, hay bỏ các lệnh `unhide`/`rename` khỏi pipe
   vì GUI đã làm được cục bộ?
5. **Hai bước plan/apply:** có cần cho mọi thao tác ghi, hay chỉ `clean.apply`?
6. **Mặc định:** `pipe.enabled=false` trong 3.0 beta rồi bật ở 3.0.1, hay bật
   ngay khi self-test + máy thật đạt?
7. **Windows 7:** vẫn cam kết hỗ trợ? Nếu có, `PIPE_REJECT_REMOTE_CLIENTS` và
   `GetNamedPipeClientProcessId` đều có từ Vista, nhưng cần test trên x86.
8. **Ký số:** W23 chỉ ghim băm; có cần chờ ký Authenticode rồi mới bật pipe
   mặc định không?

**Trạng thái:** đã duyệt ngày 2026-11-24 với các mặc định ở §13: host pipe
trong `--watch`, GUI vẫn có thể chạy elevated (chỉ dùng pipe khi chưa elevated),
pin `bin\client.pin`, giữ allow-list `clean`/`unhide`/`rename`, plan/apply cho
mọi lệnh ghi, `pipe.enabled=false` trong beta, giữ Windows 7, chưa ký số.

Đã thi hành W23a–W23h: `PipeProtocol`, `PipeServer`, `PipeClient`, `PipeAuth`,
`PipeCommands` (plan/apply/job), host trong `--watch`, pin do `--install` ghi và
`--uninstall` xoá, self-test + kiểm tra tĩnh trong `test.ps1`. Cờ native
`PIPE_REJECT_REMOTE_CLIENTS` chưa đặt được qua .NET 4.0, nên server dùng đường
lui đã ghi ở §6.1: ACL từ chối Network/Anonymous và
`GetNamedPipeClientComputerName` phải rỗng.

W23g chỉ nối `UsbPage` với pipe, và chỉ khi: gốc là ổ (không phải thư mục),
GUI không elevated, `pipe.enabled=1`. Thao tác trên thư mục vẫn chạy cục bộ như
2.3. `ProtectPage` **không** đi qua pipe: allow-list đã duyệt không có lệnh đổi
chính sách, nên trang này vẫn yêu cầu quyền quản trị và chỉ hiện nút chạy lại
elevated. Hoàn tác một lần dọn qua pipe cũng cần quyền quản trị vì GUI thường
không đọc được vùng cách ly. W23i đã cập nhật `README.md`, `PRIVACY.md` và
`plan.md`; việc còn lại là checklist máy thật ở §11.2. Pipe vẫn tắt mặc định.
