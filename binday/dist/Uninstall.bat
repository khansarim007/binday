@echo off
setlocal
title BinDay - uninstall
echo Removing BinDay for the current user...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
if errorlevel 1 (
    echo.
    echo Uninstall failed. Nothing outside this folder was changed.
) else (
    echo You can close this window.
)
endlocal
