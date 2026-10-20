@echo off
setlocal
title Redundant File Finder - install
echo Installing Redundant File Finder for the current user...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
if errorlevel 1 (
    echo.
    echo Installation failed. Nothing outside this folder was changed.
) else (
    echo You can close this window.
)
echo.
pause
