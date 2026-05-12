@echo off
echo ========================================
echo   SSH Tunnel Manager — Build Script
echo ========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Install from:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/3] Restoring packages...
dotnet restore

echo [2/3] Building self-contained exe...
dotnet publish -c Release -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -o publish\

echo [3/3] Done!
echo.
echo Output: publish\SshTunnelManager.exe
echo.
echo Copy plink.exe into the publish\ folder before distributing.
echo.
pause
