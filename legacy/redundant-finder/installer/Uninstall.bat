@echo off
setlocal
title Redundant File Finder - uninstall
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
echo.
pause
