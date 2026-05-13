@echo off
chcp 65001 >nul
echo.
echo ==========================================================
echo    SSH Tunnel Manager -- Build Script
echo ==========================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo  [LOI] Chua cai .NET 8 SDK!
    echo.
    echo  Tai tai: https://dotnet.microsoft.com/download/dotnet/8.0
    echo  Chon: .NET 8.0 SDK - Windows x64 Installer
    echo  Cai xong restart CMD va chay lai file nay.
    echo.
    pause
    exit /b 1
)

echo  [OK] .NET SDK da san sang
echo.
echo  [1/3] Dang restore packages...
dotnet restore >nul 2>&1

echo  [2/3] Dang build file .exe...
dotnet publish -c Release -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -o publish\

if errorlevel 1 (
    echo  [LOI] Build that bai!
    pause
    exit /b 1
)

echo  [3/3] Build hoan tat!
echo.
echo ==========================================================
echo                    BUILD THANH CONG
echo ==========================================================
echo.
echo  Output: publish\SshTunnelManager.exe
echo.

if exist publish\plink.exe (
    echo  [OK] plink.exe da co san trong publish\
) else (
    echo  [!] THIEU: plink.exe chua co trong publish\
    echo      Tai tai: https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe
    echo      Copy vao thu muc: publish\
)

if exist publish\default_vps.ppk (
    echo  [OK] default_vps.ppk da co san trong publish\
) else (
    echo  [!] THIEU: default_vps.ppk chua co trong publish\
    echo      Copy file .ppk AWS vao thu muc: publish\
    echo      Doi ten thanh: default_vps.ppk
)

echo.
echo ==========================================================
echo   HUONG DAN SETUP SAU KHI BUILD
echo ==========================================================
echo.
echo  BUOC 1 - Chuan bi thu muc publish\ gom du 3 file:
echo    publish\
echo    +-- SshTunnelManager.exe   (vua build xong)
echo    +-- plink.exe              (tai tu putty.org)
echo    +-- default_vps.ppk        (file key AWS)
echo.
echo  BUOC 2 - Bat OpenSSH Server tren may dich (May B):
echo    Mo PowerShell Admin, chay:
echo    ^> Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
echo    ^> Start-Service sshd
echo    ^> Set-Service -Name sshd -StartupType Automatic
echo.
echo  BUOC 3 - Cau hinh VPS (chi lam 1 lan):
echo    SSH vao VPS, them vao /etc/ssh/sshd_config:
echo      GatewayPorts yes
echo      AllowTcpForwarding yes
echo    Sau do: sudo systemctl restart sshd
echo    Mo port: sudo ufw allow 10000:19999/tcp
echo.
echo  BUOC 4 - Chay app:
echo    May B (may dich): chay SshTunnelManager.exe - Role B - Start
echo    May A (may ban):  chay SshTunnelManager.exe - Role A - Start
echo    Ca 2 may phai nhap CUNG Session ID
echo.
echo  BUOC 5 - Ket noi SSH:
echo    Mo PuTTY - Host: 127.0.0.1 - Port: (xem trong app muc [4])
echo    Dang nhap bang username/password Windows cua May B
echo.
echo  LUU Y BAO MAT:
echo    - KHONG upload file .ppk len GitHub
echo    - Chi gui file .ppk qua Zalo/USB cho nguoi tin cay
echo.
echo ==========================================================
echo.
pause
