# ShieldGate - dựng và chạy bộ tự kiểm tra lõi trên thư mục tạm.
# Không cần quyền quản trị và không đụng tới USB thật hay cấu hình máy.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Không tìm thấy csc.exe của .NET Framework 4.0." }

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
