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
    echo      Tai tai: https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html
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
echo  BUOC 3 - Cau hinh VPS (chi can lam 1 lan duy nhat)

echo  --- BUOC 3.1: SSH vao VPS ---
echo  Tren May A, mo PowerShell hoac CMD, chay lenh:
echo.
echo    ssh -i "duong_dan\default_vps.pem" ubuntu@13.229.239.111
echo.
echo  Vi du:
echo    ssh -i "E:\SshTunnelManager\publish\default_vps.pem" ubuntu@13.229.239.111
echo.
echo  Nhap "yes" neu duoc hoi xac nhan ket noi lan dau.
echo.

echo  --- BUOC 3.2: Chinh sua quyen truy cap file .pem  ---
echo    1. Chuot phai file default_vps.pem -^> Properties
echo    2. Chon tab Security -^> Advanced
echo    3. Nhan Disable inheritance
echo    4. Chon "Remove all inherited permissions"
echo    5. Nhan Add -^> Select a principal
echo    6. Nhap ten tai khoan Windows hien tai
echo    7. Tick "Full control"
echo    8. Xoa cac group:
echo         - Users
echo         - Everyone
echo         - Authenticated Users
echo    9. Chi giu lai:
echo         - Tai khoan hien tai
echo         - SYSTEM (neu co)
echo.
echo  Sau do chay lai lenh SSH.
echo.

echo  --- BUOC 3.3: Chinh sua file cau hinh SSH tren VPS ---
echo  Sau khi vao duoc VPS, chay lenh sau de mo file cau hinh:
echo.
echo    sudo nano /etc/ssh/sshd_config
echo.
echo  Tim va sua (hoac them vao cuoi file) 2 dong sau:
echo.
echo    GatewayPorts yes
echo    ^ ^ Cho phep cac may khac ket noi vao port ma May B da mo
echo    ^ ^ tren VPS. Mac dinh VPS chi cho localhost ket noi vao.
echo.
echo    AllowTcpForwarding yes
echo    ^ ^ Cho phep chuyen tiep TCP qua SSH tunnel.
echo    ^ ^ Day la nen tang de tunnel hoat dong.
echo.
echo  Luu file: nhan Ctrl+O, Enter, roi Ctrl+X de thoat.
echo.
echo  --- BUOC 3.4: Khoi dong lai SSH tren VPS ---
echo  Ap dung cau hinh vua sua:
echo.
echo    sudo systemctl restart sshd
echo.
echo  Kiem tra SSH van dang chay sau khi restart:
echo.
echo    sudo systemctl status sshd
echo.
echo  Ket qua phai hien "active (running)" mau xanh la.
echo.
echo  --- BUOC 3.4: Mo port tren Firewall VPS ---
echo  Cho phep cac port tunnel di qua firewall cua VPS:
echo.
echo    sudo ufw allow 10000:19999/tcp
echo.
echo  Lenh nay mo dai port 10000 den 19999, bao gom:
echo    - Port 12007 : SSH den May B
echo    - Port 12008 : RDP den May B
echo    - Port 5901  : VNC den May B
echo    - Va cac port tuy chinh khac trong tuong lai
echo.
echo  Kiem tra firewall da cap nhat chua:
echo.
echo    sudo ufw status
echo    ^ ^neu thay inactive tuc la firewall da tat khong can chinh sua gi them.
echo.
echo  Phai thay cac port 10000:19999 voi trang thai ALLOW.
echo.
echo  --- BUOC 3.5: Thoat VPS ---
echo  Sau khi cau hinh xong, go lenh sau de thoat:
echo.
echo    exit
echo.
echo  [OK] Cau hinh VPS hoan tat. Chi can lam 1 lan duy nhat.
echo.

echo  --- BUOC 3.6: Cache host key cua VPS ---
echo  Can cache host key cua VPS vao PuTTY truoc.
echo.
echo  BUOC 1 - Mo CMD hoac PowerShell:
echo.
echo    plink.exe -ssh ubuntu@13.229.239.111 -P 22 -i "D:\puTTY_Tunnel\publish\default_vps.ppk"
echo.
echo  Neu plink.exe khong nam trong PATH:
echo.
echo    "D:\puTTY_Tunnel\plink.exe" -ssh ubuntu@13.229.239.111 -P 22 -i "D:\puTTY_Tunnel\publish\default_vps.ppk"
echo.
echo  BUOC 2 - Xac nhan host key
echo  Neu hien:
echo.
echo    The server's host key is not cached...
echo    Store key in cache? (y/n)
echo.
echo  Hay go:
echo.
echo    y
echo.
echo  Roi nhan Enter.
echo.
echo  BUOC 3 - Sau khi vao VPS thanh cong:
echo.
echo    exit
echo.
echo  Sau do mo lai tool tunnel.
echo  Luc nay plink se ket noi thanh cong vi host key da duoc cache.
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
echo    - KHONG upload file .pem len GitHub
echo    - Chi gui file key qua Zalo/USB cho nguoi tin cay
echo    - Nen them *.ppk va *.pem vao .gitignore
echo.
echo ==========================================================
echo.
pause