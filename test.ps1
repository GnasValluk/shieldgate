# ShieldGate - dựng và chạy bộ tự kiểm tra lõi trên thư mục tạm.
# Không cần quyền quản trị và không đụng tới USB thật hay cấu hình máy.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Không tìm thấy csc.exe của .NET Framework 4.0." }

# W21: không được đọc mã phím. Kiểm tra tĩnh để không vô tình thành keylogger.
$forbidden = @('RAWKEYBOARD', 'SetWindowsHookEx', 'GetAsyncKeyState',
               'GetKeyboardState', 'MapVirtualKey')
$sourceFiles = @(Get-ChildItem 'src' -Recurse -Filter *.cs | ForEach-Object { $_.FullName })
$hits = @(Select-String -Path $sourceFiles -Pattern $forbidden -SimpleMatch)
if ($hits.Count -gt 0) {
    $list = ($hits | ForEach-Object { $_.Path + ':' + $_.LineNumber }) -join ', '
    throw "Forbidden key-reading API in source: $list"
}

# W23: pipe server phải có ACL và xác thực client; không được tạo server ở nơi khác.
$pipeServer = Get-Content 'src\core\PipeServer.cs' -Raw
foreach ($need in @('PipeSecurity', 'BuiltinAdministratorsSid', 'InteractiveSid',
                    'NetworkSid', 'AnonymousSid', 'PipeAccessRule')) {
    if ($pipeServer -notmatch [regex]::Escape($need)) {
        throw "Pipe server is missing $need"
    }
}
$auth = Get-Content 'src\core\PipeAuth.cs' -Raw
foreach ($need in @('GetNamedPipeClientProcessId', 'GetNamedPipeClientSessionId',
                    'QueryFullProcessImageNameW', 'ConvertSidToStringSidW')) {
    if ($auth -notmatch [regex]::Escape($need)) {
        throw "Pipe auth is missing $need"
    }
}
$pipeHits = @(Select-String -Path $sourceFiles -Pattern 'NamedPipeServerStream' -SimpleMatch |
              Where-Object { $_.Path -notlike '*\PipeServer.cs' })
if ($pipeHits.Count -gt 0) {
    $list = ($pipeHits | ForEach-Object { $_.Path + ':' + $_.LineNumber }) -join ', '
    throw "NamedPipeServerStream outside PipeServer.cs: $list"
}

New-Item -ItemType Directory -Force -Path 'dist' | Out-Null

$sources = @(Get-ChildItem 'src\core\*.cs' | ForEach-Object { $_.FullName }) +
           @((Join-Path $root 'src\ui\Strings.cs'),
             (Join-Path $root 'src\ui\SelfTest.cs'),
             (Join-Path $root 'tests\TestMain.cs'))

$out = Join-Path $root 'dist\ShieldGate.Tests.exe'
$refs = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Management.dll', '/r:System.Xml.dll')

$build = @('/nologo', '/target:exe', '/platform:x86', '/codepage:65001',
           '/optimize+', '/debug-', ('/out:' + $out)) + $refs

& $csc @build @sources
if ($LASTEXITCODE -ne 0) { throw "Dựng bộ kiểm tra thất bại (csc exit $LASTEXITCODE)." }

& $out
exit $LASTEXITCODE
