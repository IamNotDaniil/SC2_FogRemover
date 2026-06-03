@echo off
title Building SC2 Fog Remover
echo ========================================
echo    Building SC2 Fog of War Remover
echo ========================================
echo.
dotnet build -c Release
echo.
if %errorlevel% equ 0 (
    echo [SUCCESS] Build completed!
    echo.
    echo Output: bin\Release\net8.0-windows\SC2_FogRemover.exe
) else (
    echo [ERROR] Build failed!
    echo Make sure .NET 8.0 SDK is installed
)
echo.
pause
