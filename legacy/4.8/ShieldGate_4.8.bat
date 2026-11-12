@echo off
setlocal enabledelayedexpansion
title [!] KHOI PHUC USB & DIET VIRUS SHORTCUT v4.8
chcp 65001 >nul
color 0B
cd /d "%~dp0"

:: Phien ban 4.8 - Cap nhat an toan, khong chay o o he thong C:
:: Giu nguyen autorun.inf (bo xoa de an toan voi USB co giai qua su dung autorun)
:: Tu xin quyen Admin nhung van giu nguyen thu muc USB

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)
cd /d "%~dp0"

:: BAO VE: khong cho chay o o he thong C: de tranh xoa nhay file quan trong
for %%a in ("%CD%") do set "drive=%%~da"
if /i "%drive%"=="C:" (
    echo [!] CANH BAO: Ban dang tai o he thong C:.
    echo [!] Script chi chay duoc o O USB de an toan.
    echo.
    pause
    exit /b
)

cls
echo ==========================================================
echo    TU DONG KHOI PHUC FILE AN & DIET SHORTCUT VIRUS v4.8
echo    Thu muc dang xu ly: %CD%
echo ==========================================================
echo.

echo  [1/3] Dang hien lai file/folder bi an...
attrib -s -h -r /s /d *.* >nul 2>&1

echo  [2/3] Dang xoa file Shortcut (.lnk)...
del /f /q /s *.lnk >nul 2>&1

echo  [3/3] Dang xoa ma doc virus (.vbs, .vbe)... (Giu nguyen autorun.inf)
del /f /q /s *.vbs >nul 2>&1
del /f /q /s *.vbe >nul 2>&1
:: KHONG xoa autorun.inf de bo tro giai phap an toan cua ban

echo.
echo ==========================================================
echo     HOAN THANH! USB DA SACH VA AN TOAN (v4.8)
echo ==========================================================
echo.
echo Bam phim bat ky de dong cua so...
pause >nul
