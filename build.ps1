# ShieldGate - dựng một tệp ShieldGate.exe duy nhất từ src\core (lõi) và src\ui (giao diện).
# Yêu cầu: Windows có .NET Framework 4.0 (csc.exe v4.0.30319). Không cần Visual Studio.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Không tìm thấy csc.exe của .NET Framework 4.0." }

New-Item -ItemType Directory -Force -Path 'dist' | Out-Null
$icon = Join-Path $root 'assets\app.ico'
$makico = Join-Path $root 'dist\_makico.exe'

# Cong kiem tra an toan: khong cho goi cong cu he thong bang ten tran.
& (Join-Path $root 'tools\check-bare-tools.ps1') $root
if ($LASTEXITCODE -ne 0) { throw 'Bare system tool names are not allowed.' }

# Biểu tượng có sẵn trong assets. Nếu thiếu thì vẽ lại từ mã nguồn.
if (-not (Test-Path $icon)) {
    try {
        & $csc /nologo /target:exe /platform:x86 /codepage:65001 /out:$makico `
            /r:System.Drawing.dll tools\MakeIcon.cs src\ui\Icons.cs | Out-Null
        if ($LASTEXITCODE -eq 0) { & $makico $icon | Out-Null }
    } catch {
        Write-Host 'Bỏ qua biểu tượng: ' $_.Exception.Message
    }
}

$sources = @(Get-ChildItem 'src\core\*.cs' | ForEach-Object { $_.FullName }) +
           @(Get-ChildItem 'src\ui\*.cs' | ForEach-Object { $_.FullName })

$out = Join-Path $root 'dist\ShieldGate.exe'
$refs = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll',
          '/r:System.Windows.Forms.dll', '/r:System.Management.dll', '/r:System.Xml.dll')

$build = @('/nologo', '/target:winexe', '/platform:x86', '/codepage:65001',
           '/optimize+', '/debug-', '/win32manifest:src\core\app.manifest',
           ('/out:' + $out)) + $refs

if (Test-Path $icon) { $build += ('/win32icon:' + $icon) }

& $csc @build @sources
if ($LASTEXITCODE -ne 0) { throw "Dựng thất bại (csc exit $LASTEXITCODE)." }

Remove-Item $makico -ErrorAction SilentlyContinue
Remove-Item 'dist\_engine.dll' -ErrorAction SilentlyContinue

$file = Get-Item $out
$hash = (Get-FileHash $out -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host ''
Write-Host ('Xong: ' + $file.FullName)
Write-Host ('Dung lượng: ' + [math]::Round($file.Length / 1KB, 0) + ' KB')
Write-Host ('SHA256: ' + $hash)
