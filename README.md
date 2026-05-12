# SSH Tunnel Manager

Ứng dụng Windows (.exe) cho phép PuTTY kết nối SSH giữa 2 máy tính có IP private
ở 2 mạng LAN khác nhau, thông qua VPS trung gian — hoạt động tương tự AnyDesk.

## Kiến trúc

```
[Machine B]  plink -R 2222:localhost:22  →  VPS  ←  plink -L 2222:localhost:2222  [Machine A]
                                                           ↑
                                              PuTTY → localhost:2222
```

## Cấu trúc project

```
SshTunnelManager/
├── Program.cs            # CLI menu chính
├── ConfigManager.cs      # Đọc/lưu config.json + Setup wizard
├── PlinkWrapper.cs       # Xây dựng lệnh plink, quản lý Process
├── TunnelMonitor.cs      # Heartbeat + auto-reconnect
├── ConnectionHandler.cs  # SSH / RDP / Custom port profiles
├── Logger.cs             # Ghi log ra console + file
└── SshTunnelManager.csproj
```

## Build

### Yêu cầu
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

### Lệnh build (self-contained .exe)

```powershell
# Windows x64 — single .exe file
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Output: bin\Release\net8.0\win-x64\publish\SshTunnelManager.exe
```

Hoặc chạy `build.bat` đính kèm.

## Triển khai

1. Copy `SshTunnelManager.exe` + `plink.exe` vào cùng một thư mục
2. Chạy lần đầu → wizard tự động khởi động
3. Nhập thông tin VPS, chọn role (A hoặc B), cấu hình port

## Sử dụng nhanh

**Machine B (server đích):**
```
Chạy SshTunnelManager.exe → Role = B → Start tunnels [1]
```

**Machine A (client):**
```
Chạy SshTunnelManager.exe → Role = A → Start tunnels [1]
Mở PuTTY → host=127.0.0.1  port=2222
```

## Ports mặc định

| Tunnel | VPS relay port | Machine B port | Machine A local |
|--------|---------------|----------------|-----------------|
| SSH    | 2222          | 22             | 2222            |
| RDP    | 3389          | 3389           | 3389            |

## Lệnh plink được tạo tự động

**Machine B (Reverse Tunnel):**
```
plink.exe -ssh user@VPS -P 22 -R 2222:localhost:22 -N -batch -pw PASSWORD
```

**Machine A (Forward Tunnel):**
```
plink.exe -ssh user@VPS -P 22 -L 2222:localhost:2222 -N -batch -pw PASSWORD
```

## Cấu hình VPS (GatewayPorts)

Thêm vào `/etc/ssh/sshd_config` trên VPS:
```
GatewayPorts clientspecified
AllowTcpForwarding yes
```
Sau đó: `systemctl restart sshd`
