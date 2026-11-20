# ShieldGate - chan goi cong cu he thong bang ten tran trong ma nguon.
# CreateProcess tim tep trong thu muc chua ShieldGate truoc, nen mot reg.exe hay
# schtasks.exe gia nam canh ShieldGate se chay voi quyen admin. Moi loi goi phai
# di qua Native.SysExe (hoac Native.ShellExe voi explorer.exe).
$ErrorActionPreference = 'Stop'
$root = if ($args.Count -gt 0 -and $args[0]) { $args[0] } else { Split-Path -Parent $PSScriptRoot }
$src = Join-Path $root 'src'

$patterns = @('Native\.Run\("', 'Process\.Start\("', 'ProcessStartInfo\("')
$bad = New-Object System.Collections.Generic.List[string]

foreach ($file in Get-ChildItem $src -Recurse -Filter *.cs) {
    foreach ($pattern in $patterns) {
        foreach ($hit in (Select-String -Path $file.FullName -Pattern $pattern)) {
            $text = $hit.Line.Trim()
            if ($text -match '\.exe"') {
                $rel = $file.FullName.Replace($root + [IO.Path]::DirectorySeparatorChar, '')
                $bad.Add(('{0}:{1}: {2}' -f $rel, $hit.LineNumber, $text))
            }
        }
    }
}

if ($bad.Count -gt 0) {
    Write-Host 'Loi: con goi cong cu he thong bang ten tran:' -ForegroundColor Red
    foreach ($line in $bad) { Write-Host ('  ' + $line) }
    exit 1
}
exit 0
