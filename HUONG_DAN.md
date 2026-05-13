# SSH Tunnel Manager — Hướng Dẫn Sử Dụng

> Ứng dụng giúp 2 máy tính ở 2 mạng LAN khác nhau kết nối với nhau qua SSH tunnel,
> thông qua VPS trung gian. Hoạt động tương tự AnyDesk nhưng dùng SSH thủ công.

---

## Mục lục

1. [Yêu cầu hệ thống](#1-yêu-cầu-hệ-thống)
2. [Chuẩn bị trước khi dùng](#2-chuẩn-bị-trước-khi-dùng)
3. [Cài đặt và chạy lần đầu](#3-cài-đặt-và-chạy-lần-đầu)
4. [Hướng dẫn kết nối SSH](#4-hướng-dẫn-kết-nối-ssh)
5. [Hướng dẫn kết nối RDP](#5-hướng-dẫn-kết-nối-rdp)
6. [Tính năng auto-reconnect](#6-tính-năng-auto-reconnect)
7. [Thêm port tùy chỉnh](#7-thêm-port-tùy-chỉnh)
8. [Xử lý lỗi thường gặp](#8-xử-lý-lỗi-thường-gặp)

---

## 1. Yêu cầu hệ thống

| Thứ | Yêu cầu |
|-----|---------|
| Hệ điều hành | Windows 10/11 (64-bit) |
| .NET Runtime | Không cần — đã nhúng sẵn trong .exe |
| plink.exe | Cần có, đặt cạnh SshTunnelManager.exe |
| File key VPS | default_vps.ppk — đặt cạnh SshTunnelManager.exe |
| Kết nối internet | Cả 2 máy phải có internet |

---

## 2. Chuẩn bị trước khi dùng

### Thư mục cần có đủ 3 file

```
publish\
├── SshTunnelManager.exe    ← file chính
├── plink.exe               ← tải tại: https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe
└── default_vps.ppk         ← file key AWS (nhận từ người phân phối app)
```

> ⚠️ **Lưu ý:** File `default_vps.ppk` KHÔNG được public. Nhận qua Zalo/USB từ người quản lý.

### Bật OpenSSH Server trên Máy B (chỉ cần làm 1 lần)

Mở **PowerShell với quyền Admin** trên Máy B:

```powershell
# Cài OpenSSH Server (nếu chưa có)
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0

# Bật dịch vụ
Start-Service sshd

# Tự khởi động cùng Windows
Set-Service -Name sshd -StartupType Automatic
```

Kiểm tra:
```powershell
Get-Service sshd
# Phải thấy: Status = Running
```

### Đảm bảo Máy B có password

SSH không cho đăng nhập nếu tài khoản Windows không có password. Đặt password nếu chưa có:

```powershell
net user TÊN_USER mật_khẩu_mới
```

---

## 3. Cài đặt và chạy lần đầu

### Chạy app

Double-click `SshTunnelManager.exe` — lần đầu sẽ tự động mở **Setup Wizard**:

```
╔══════════════════════════════════════════════════╗
║        SSH Tunnel Manager — Setup Wizard         ║
╚══════════════════════════════════════════════════╝
```

### Các bước trong Wizard

**Bước 1 — Chọn vai trò máy:**
```
[A] Machine A — Client (máy dùng để kết nối vào máy khác)
[B] Machine B — Server (máy muốn được kết nối vào)
```

**Bước 2 — Nhập Session ID:**
- Session ID là tên ngắn tự đặt, ví dụ: `nhom1`, `alice-bob`, `dev-team`
- **Quan trọng:** Cả 2 máy phải nhập CÙNG Session ID
- Session ID tự động tạo ra port riêng → nhiều nhóm dùng chung VPS không bị đụng nhau

**Bước 3 — Chọn VPS:**
```
[1] Default VPS (AWS — miễn phí, dùng ngay)
[2] Custom VPS  (VPS riêng của bạn)
```

**Bước 4** — Nhấn Enter cho các tùy chọn còn lại (giữ mặc định).

### Sau khi setup xong

Chọn **`[1] Start tunnels`** để bắt đầu kết nối.

---

## 4. Hướng dẫn kết nối SSH

### Sơ đồ hoạt động

```
Máy A ──(Forward Tunnel)──► VPS AWS ◄──(Reverse Tunnel)── Máy B
PuTTY → localhost:XXXXX → 13.229.239.111:XXXXX → OpenSSH:22
```

### Các bước thực hiện

**Máy B** (máy đích — máy muốn được điều khiển):
1. Chạy `SshTunnelManager.exe`
2. Setup Wizard → Role = **B** → Session ID = `tên_session`
3. Chọn **`[1] Start tunnels`**
4. Giữ app đang chạy, không đóng

**Máy A** (máy dùng để kết nối):
1. Chạy `SshTunnelManager.exe`
2. Setup Wizard → Role = **A** → Session ID = `tên_session` *(giống hệt Máy B)*
3. Chọn **`[1] Start tunnels`**
4. Chọn **`[4] Usage guide`** để xem port cụ thể

**Mở PuTTY trên Máy A:**
1. Mở PuTTY
2. Host Name: `127.0.0.1`
3. Port: *(số port hiển thị trong mục `[4]` của app)*
4. Connection type: `SSH`
5. Click **Open**
6. Đăng nhập bằng username/password Windows của **Máy B**

✅ Thấy dấu nhắc lệnh của Máy B là thành công!

---

## 5. Hướng dẫn kết nối RDP

> ⚠️ Máy B phải dùng **Windows 10/11 Pro hoặc Enterprise** mới hỗ trợ RDP Server.
> Windows Home không hỗ trợ — dùng VNC thay thế (xem mục 7).

**Bật Remote Desktop trên Máy B:**
```
Vào Settings → System → Remote Desktop → bật ON
```

Hoặc PowerShell Admin:
```powershell
Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' `
    -Name "fDenyTSConnections" -Value 0
netsh advfirewall firewall set rule group="remote desktop" new enable=yes
```

**Kết nối từ Máy A:**
1. Nhấn `Win+R` → gõ `mstsc` → Enter
2. Computer: `127.0.0.1:XXXXX` *(port RDP trong mục `[4]` của app)*
3. Click **Connect** → đăng nhập user/pass Windows Máy B

Hoặc trong app chọn **`[5] Open PuTTY / RDP`** → **`[2]`** để mở mstsc tự động.

---

## 6. Tính năng auto-reconnect

App tự động kết nối lại khi tunnel bị ngắt (mất mạng, VPS restart...).

- Mặc định: **bật**
- Thời gian chờ trước khi reconnect: 5 giây
- Kiểm tra heartbeat mỗi: 15 giây

Bật/tắt trong menu: chọn **`[9] Toggle auto-reconnect`**

Xem số lần đã reconnect: chọn **`[3] Status`** → cột *Reconnects*.

---

## 7. Thêm port tùy chỉnh

Dùng để forward thêm các dịch vụ khác (VNC, web server, database...).

Trong menu chọn **`[6] Manage custom port forwards`** → **`[1] Add new`**:

| Field | Ví dụ (VNC) | Giải thích |
|-------|-------------|------------|
| Tunnel name | `VNC` | Tên tùy đặt |
| Local port | `5901` | Port mở trên Máy A |
| VPS relay port | `5901` | Port trên VPS làm cầu nối |
| Remote port | `5900` | Port dịch vụ trên Máy B |

Sau khi thêm → Stop tunnels → Start tunnels lại để áp dụng.

**Ví dụ kết nối VNC:**
1. Cài TightVNC Server trên Máy B: https://www.tightvnc.com
2. Thêm custom tunnel port 5900 như trên
3. Mở TightVNC Viewer trên Máy A → kết nối `127.0.0.1:5901`

---

## 8. Xử lý lỗi thường gặp

| Lỗi | Nguyên nhân | Cách sửa |
|-----|-------------|----------|
| `plink.exe not found` | Thiếu file plink.exe | Copy plink.exe vào cùng thư mục với .exe |
| `default_vps.ppk not found` | Thiếu file key | Copy file .ppk vào cùng thư mục, đổi tên thành `default_vps.ppk` |
| Tunnel DOWN liên tục | Mất internet hoặc VPS lỗi | Kiểm tra internet, thử ping VPS |
| `Host key not cached` | Máy chưa từng kết nối VPS | Chạy: `plink.exe -ssh ubuntu@13.229.239.111 -i default_vps.ppk` → nhấn `y` |
| PuTTY `Connection refused` | OpenSSH chưa chạy trên Máy B | Chạy `Start-Service sshd` trên Máy B |
| PuTTY `Network error` | Tunnel chưa UP | Chờ 10 giây, kiểm tra `[3] Status` |
| `Access denied` khi đăng nhập | Sai password | Kiểm tra password Windows Máy B, đảm bảo không để trống |
| 2 máy dùng port khác nhau | Session ID không khớp | Đảm bảo cả 2 máy nhập đúng cùng Session ID |

---

## Cấu trúc kết nối tóm tắt

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  [Máy B - nhà bạn bè]          [VPS AWS]        [Máy A - nhà bạn] │
│                                                                 │
│  SshTunnelManager (Role B)                SshTunnelManager (Role A) │
│  plink -R PORT:localhost:22  ──►  :PORT  ◄──  plink -L PORT:localhost:PORT │
│                                                                 │
│  OpenSSH Server :22          relay           PuTTY → localhost:PORT │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```
