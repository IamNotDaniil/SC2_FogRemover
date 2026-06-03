@echo off
title SC2 Fog Remover
echo ========================================
echo    SC2 Fog of War Remover v2.0
echo ========================================
echo.
echo 1. Make sure StarCraft II is running
echo 2. This program requires Administrator rights
echo.
pause
cd /d "%~dp0bin\Release\net8.0-windows"
if exist "SC2_FogRemover.exe" (
    powershell -Command "Start-Process -FilePath 'SC2_FogRemover.exe' -Verb RunAs -WorkingDirectory '%~dp0bin\Release\net8.0-windows'"
) else (
    echo [ERROR] SC2_FogRemover.exe not found!
    echo Please run build.bat first.
    pause
)
