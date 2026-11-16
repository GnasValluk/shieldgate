# ShieldGate 1.0

ShieldGate removes USB shortcut viruses on Windows. The virus hides folders, replaces them with shortcuts, and runs a payload from a hidden folder. ShieldGate scans the drive, quarantines what you select, and repairs the traces left on the PC.

Portable single `.exe`. No installer, no network access, no telemetry.

## What it detects

On USB drives:

- Shortcuts, scripts and `autorun.inf` that point at hidden folders, drive roots or odd payloads.
- Nameless or hidden folders used to stash data, and hidden executables at drive roots.
- Fake folder executables, `Documents`/`Docusment` lookalike folders with a system CLSID in `desktop.ini`, and known payload bundles (`1.exe`, `opera_elf.dll`, `adback.dat`, `link.dat`, `icudtl.dat`).
- Matching uses shortcut structure, folder markers, file names and SHA256, so renamed payload files still match. `1.exe` is a validly signed Opera binary used as a decoy; a good signature alone does not clear it.

On the PC:

- Run/RunOnce, `Winlogon` Shell/Userinit, `AppInit_DLLs`, `MountPoints2` and the Startup folder.
- Scheduled tasks, WMI event subscriptions and modified `.lnk` files.
- Unsigned executables in AppData/Temp/ProgramData, system process names running from the wrong folder, and processes started from USB or from payload copies.
- Payload copies in user folders and at drive roots.
- Explorer registry: the folder open command and `DelegateExecute`, `.exe` association, hidden-file setting, and policies for Folder Options, the context menu, Task Manager and `regedit`. `HKCU\Software\Classes` overrides that shadow machine keys are included.

## What it does

- Shows findings first. Quarantine runs only on the items you keep selected, and can be undone.
- Data inside a nameless folder moves to the drive root during quarantine. Nothing is overwritten; duplicates get `(1)`.
- Fix removes malicious Run/Winlogon/MountPoints/`.lnk` values, disables tasks, deletes WMI consumers and bindings, and stops related processes. Every action is logged.
- Registry repair runs `reg export` before changing a key.
- **Unhide files** moves data out of nameless folders and clears the hidden attribute across a drive.

## Protection

Optional and off by default.

- Disable AutoRun and block `autorun.inf`.
- Block Windows Script Host. Turn this on only if you do not need VBScript.
- Registry protection: keep checking Explorer keys, repair only damaged ones.
- Background watch: scans a USB drive when it is plugged in, and rechecks removable drive roots every 10 seconds. Only high-confidence items are quarantined automatically. A tray icon shows that it is running.

Registry fixes stay after protection is turned off. Restoring broken values would break Explorer again.

## Run

Get `ShieldGate.exe` from Releases, run it and accept the UAC prompt. First start asks for a data folder and a language (Vietnamese or English). The choice is stored in `%LOCALAPPDATA%\ShieldGate\setup.ini`. Delete that file to run setup again.

1. Open **USB scan**, choose a drive, click **Scan USB**.
2. Clear the items you want to keep, then click **Quarantine**.
3. If data is still hidden, use **Unhide files**.
4. Restore quarantined files from **Overview**.

To carry it on a USB drive, copy `ShieldGate.exe` to the drive. The GUI and protection need administrator rights. `--scan`, `--status` and `--selftest` do not.

## Command line

```
ShieldGate.exe                            GUI
ShieldGate.exe --status                   protection state
ShieldGate.exe --scan E:\                 read-only scan
ShieldGate.exe --install                  install background protection
ShieldGate.exe --install --blockscript    also block Windows Script Host
ShieldGate.exe --uninstall                remove protection, restore Windows
ShieldGate.exe --watch                    background worker, used by the task
ShieldGate.exe --tray                     tray icon worker
ShieldGate.exe --selftest                 self-test in a temp folder
```

- `--scan` exits `0` when clean, `2` when it finds items and `1` on error. It stops after 150,000 files or 300 seconds and reports that the drive was only partly checked.
- `--selftest` writes only inside its temp folder. It covers detection, quarantine, restore, hidden folders, the payload bundle, registry checks, settings and logging. Current result: 22/22.
- Flags work with `--` or `-`.
- `--install`, `--uninstall`, `--watch`, `--tray` and the GUI request admin through UAC.

## Data

Default folder: `C:\ProgramData\ShieldGate`, falling back to `%LOCALAPPDATA%\ShieldGate`.

```
settings.txt        settings and scan counters
guard.cfg           protection choices
last_scan.txt       last machine scan result
log\shieldgate.log  activity log, rotated at 5 MB
quarantine\         quarantined files and manifests
backup\             Windows config and registry backups
bin\ShieldGate.exe  background copy plus SHA256
```

While protection is on, the data folder is limited to Administrators and SYSTEM. Standard users get read access only where needed, not to `quarantine` or `backup`. Nothing leaves the machine.

## Build

Needs Windows and the .NET Framework 4.0 compiler (`csc.exe`). Visual Studio is not required.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1   # dist\ShieldGate.exe
powershell -ExecutionPolicy Bypass -File test.ps1    # self-test
```

The source is C# 5 and WinForms. `src\core` holds the logic, `src\ui` the interface. Every user-facing string is in `src\ui\Strings.cs` in both languages; the log follows the selected language.

CI (`.github\workflows\build.yml`) builds and runs the tests on Windows for every push.

```
src\core\      paths, scanning, quarantine, protection, startup repair
src\ui\        forms, dialogs, setup, strings
tests\         self-test entry point
assets\        icon
tools\         icon generator
legacy\4.8\    old single-file PowerShell version
build.ps1      builds dist\ShieldGate.exe
test.ps1       builds and runs the self-test
```

## Legacy 4.8

`legacy\4.8\ShieldGate_4.8.bat` is the old standalone PowerShell script. It only removes shortcuts and scripts in its own folder. See `legacy\4.8\README.md`.

## Limits

- Tuned on one real sample (`GnasValluk`/`Docusment`), not measured on a wide corpus. Release targets are under 0.5% false positives and under 5% misses; more samples will be added to the repo.
- Not an antivirus. It does not scan the whole system drive and does not replace Defender.
- It does not check services, drivers, IFEO/COM hijacks or Defender exclusions. Registry coverage is limited to Explorer keys and the policies that block repairs.
- A scan can stop early at 150,000 files or 300 seconds.
- The release is unsigned. SmartScreen may warn; compare the SHA256 before running.
- No network, optical or MTP drives.
- Windows 7 or later with .NET Framework 4.0.

## License

MIT. See [LICENSE](LICENSE). Report bugs in Issues; include the Windows version, drive type and the last lines of `shieldgate.log`.
