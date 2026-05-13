using System.Diagnostics;

namespace SshTunnelManager;

/// <summary>
/// Handles launching client tools (PuTTY, mstsc) pointed at the local
/// tunnel ports, and manages custom port-forward profiles.
/// </summary>
public static class ConnectionHandler
{
    // ---- Launch helpers ----

    /// <summary>Open PuTTY targeting the SSH forward tunnel port on Machine A.</summary>
    public static void LaunchPutty(AppConfig cfg)
    {
        var ssh = cfg.Tunnels.FirstOrDefault(t => t.Type == ConnectionType.SSH);
        if (ssh == null) { Logger.Warn("No SSH tunnel configured."); return; }
        LaunchApp("putty.exe", $"-P {ssh.LocalPort} 127.0.0.1",
            "PuTTY", $"Connect PuTTY to localhost:{ssh.LocalPort}");
    }

    /// <summary>Open Windows Remote Desktop (mstsc) targeting the RDP forward port.</summary>
    public static void LaunchRdp(AppConfig cfg)
    {
        var rdp = cfg.Tunnels.FirstOrDefault(t => t.Type == ConnectionType.RDP);
        if (rdp == null) { Logger.Warn("No RDP tunnel configured."); return; }
        LaunchApp("mstsc", $"/v:127.0.0.1:{rdp.LocalPort}",
            "mstsc (RDP)", $"Connect RDP to localhost:{rdp.LocalPort}");
    }

    private static void LaunchApp(string exe, string args, string label, string logMsg)
    {
        Logger.Info(logMsg);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = args,
                UseShellExecute = true,
            });
            Logger.Success($"{label} launched.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Cannot launch {label}: {ex.Message}");
        }
    }

    // ---- Custom port management ----

    public static void AddCustomTunnel(AppConfig cfg)
    {
        Console.WriteLine("\n--- Add Custom Port Forward ---");
        Console.Write("  Tunnel name       : ");
        var name = Console.ReadLine()?.Trim() ?? "custom";

        Console.Write("  Local port (MachineA) : ");
        if (!int.TryParse(Console.ReadLine(), out var local)) { Logger.Warn("Invalid port."); return; }

        Console.Write("  VPS relay port        : ");
        if (!int.TryParse(Console.ReadLine(), out var vps)) { Logger.Warn("Invalid port."); return; }

        Console.Write("  Remote port (MachineB): ");
        if (!int.TryParse(Console.ReadLine(), out var remote)) { Logger.Warn("Invalid port."); return; }

        cfg.Tunnels.Add(new TunnelConfig
        {
            Name       = name,
            Type       = ConnectionType.Custom,
            LocalPort  = local,
            VpsPort    = vps,
            RemotePort = remote,
        });
        ConfigManager.Save(cfg);
        Logger.Success($"Custom tunnel '{name}' added. Restart tunnels to apply.");
    }

    public static void RemoveTunnel(AppConfig cfg)
    {
        if (cfg.Tunnels.Count == 0) { Console.WriteLine("No tunnels configured."); return; }

        Console.WriteLine("\n--- Remove Tunnel ---");
        for (int i = 0; i < cfg.Tunnels.Count; i++)
            Console.WriteLine($"  [{i + 1}] {cfg.Tunnels[i].Name}");

        Console.Write("Select number to remove (0 = cancel): ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > cfg.Tunnels.Count) return;

        var removed = cfg.Tunnels[idx - 1];
        cfg.Tunnels.RemoveAt(idx - 1);
        ConfigManager.Save(cfg);
        Logger.Success($"Tunnel '{removed.Name}' removed.");
    }

    public static void PrintTunnelUsageGuide(AppConfig cfg)
    {
        Console.Clear();
        var sep = "  " + new string('═', 65);

        // ── Header ──────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine(sep);
        Console.WriteLine("  ║          HƯỚNG DẪN SỬ DỤNG SSH TUNNEL MANAGER              ║");
        Console.WriteLine(sep);
        Console.ResetColor();

        // ── Session info ─────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  📋 Session ID : \"{cfg.SessionId}\"");
        Console.WriteLine($"  🖥  Vai trò    : {(cfg.Role == MachineRole.MachineA ? "Máy A — CLIENT (kết nối vào máy bạn bè)" : "Máy B — SERVER (máy đích, được kết nối vào)")}");
        Console.WriteLine($"  🌐 VPS        : {cfg.Vps.Username}@{cfg.Vps.Host}");
        Console.ResetColor();

        if (cfg.Role == MachineRole.MachineB)
        {
            PrintGuideForMachineB(cfg);
        }
        else
        {
            PrintGuideForMachineA(cfg);
        }

        // ── Troubleshooting ───────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n  " + new string('─', 65));
        Console.WriteLine("  ❓ XỬ LÝ SỰ CỐ THƯỜNG GẶP");
        Console.WriteLine("  " + new string('─', 65));
        Console.ResetColor();
        Console.WriteLine("  • Tunnel DOWN liên tục  → Kiểm tra kết nối internet, VPS có hoạt động không");
        Console.WriteLine("  • PuTTY 'Connection refused' → Máy B chưa bật OpenSSH Server");
        Console.WriteLine("  • PuTTY 'Network error'      → Tunnel chưa UP, chờ vài giây rồi thử lại");
        Console.WriteLine("  • Đăng nhập 'Access denied'  → Sai username/password Windows của Máy B");
        Console.WriteLine("  • Host key warning           → Chạy plink.exe thủ công 1 lần để accept key");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Nhấn Enter để quay lại menu...");
        Console.ResetColor();
        Console.ReadLine();
    }

    private static void PrintGuideForMachineB(AppConfig cfg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  " + new string('─', 65));
        Console.WriteLine("  ✅ NHIỆM VỤ CỦA MÁY B (máy này)");
        Console.WriteLine("  " + new string('─', 65));
        Console.ResetColor();
        Console.WriteLine("\n  Máy B đẩy Reverse Tunnel lên VPS để Máy A có thể kết nối vào.");
        Console.WriteLine("  Bạn CHỈ CẦN giữ app này đang chạy — không cần làm gì thêm.\n");

        Console.WriteLine("  Các cổng đang mở trên VPS cho session này:");
        foreach (var t in cfg.Tunnels)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"\n    [{t.Name}] ");
            Console.ResetColor();
            Console.WriteLine($"Máy này (port {t.RemotePort})  →  VPS relay port {t.VpsPort}");
            if (t.Type == ConnectionType.SSH)
                Console.WriteLine($"           Máy A sẽ SSH vào cổng này để điều khiển máy bạn");
            else if (t.Type == ConnectionType.RDP)
                Console.WriteLine($"           Máy A sẽ Remote Desktop vào cổng này");
            else
                Console.WriteLine($"           Máy A sẽ kết nối ứng dụng tùy chỉnh vào cổng này");
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  ⚠  YÊU CẦU TRÊN MÁY B:");
        Console.ResetColor();
        Console.WriteLine("    • OpenSSH Server phải đang chạy (cho kết nối SSH)");
        Console.WriteLine("      Kiểm tra: Get-Service sshd  (phải thấy Running)");
        Console.WriteLine("      Bật:      Start-Service sshd  (chạy PowerShell Admin)");
        Console.WriteLine("    • Tường lửa Windows phải cho phép port 22");
        Console.WriteLine("    • Máy B phải có username + password (không được để trống password)");
        Console.WriteLine("\n  ℹ  Chia sẻ thông tin sau cho người dùng Máy A:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"    Session ID : {cfg.SessionId}");
        Console.WriteLine($"    Username   : (username Windows của máy này)");
        Console.WriteLine($"    Password   : (password Windows của máy này)");
        Console.ResetColor();
    }

    private static void PrintGuideForMachineA(AppConfig cfg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  " + new string('─', 65));
        Console.WriteLine("  ✅ CÁCH KẾT NỐI TỪ MÁY A (máy này)");
        Console.WriteLine("  " + new string('─', 65));
        Console.ResetColor();
        Console.WriteLine("\n  Các cổng local dưới đây được forward xuyên VPS tới Máy B:\n");

        foreach (var t in cfg.Tunnels)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  ┌─── [{t.Name}] " + new string('─', 48 - t.Name.Length) + "┐");
            Console.ResetColor();
            Console.WriteLine($"  │  Luồng: localhost:{t.LocalPort}  →  VPS:{t.VpsPort}  →  MáyB:{t.RemotePort}");

            if (t.Type == ConnectionType.SSH)
            {
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Cách kết nối bằng PuTTY:");
                Console.WriteLine($"  │    1. Mở PuTTY");
                Console.WriteLine($"  │    2. Host Name : 127.0.0.1");
                Console.WriteLine($"  │    3. Port      : {t.LocalPort}");
                Console.WriteLine($"  │    4. Connection type: SSH");
                Console.WriteLine($"  │    5. Click Open → đăng nhập bằng user/pass Windows của Máy B");
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Hoặc chọn [5] → [1] trong menu để mở PuTTY tự động");
            }
            else if (t.Type == ConnectionType.RDP)
            {
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Cách kết nối bằng Remote Desktop:");
                Console.WriteLine($"  │    1. Nhấn Win+R → gõ: mstsc");
                Console.WriteLine($"  │    2. Computer: 127.0.0.1:{t.LocalPort}");
                Console.WriteLine($"  │    3. Click Connect → đăng nhập user/pass Windows Máy B");
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Lưu ý: Máy B phải dùng Windows Pro/Enterprise mới có RDP");
                Console.WriteLine($"  │  Thay thế: Dùng VNC (TightVNC) nếu Máy B dùng Windows Home");
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Hoặc chọn [5] → [2] trong menu để mở mstsc tự động");
            }
            else
            {
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Kết nối ứng dụng bất kỳ tới: localhost:{t.LocalPort}");
                Console.WriteLine($"  │  Ví dụ VNC Viewer: kết nối tới 127.0.0.1:{t.LocalPort}");
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  └" + new string('─', 62) + "┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ⚠  ĐIỀU KIỆN ĐỂ KẾT NỐI THÀNH CÔNG:");
        Console.ResetColor();
        Console.WriteLine("    • Máy B phải đang chạy app này với Role = B, Tunnel RUNNING");
        Console.WriteLine("    • Máy A (máy này) phải đang RUNNING (đã start tunnel)");
        Console.WriteLine("    • Cả 2 máy phải dùng cùng Session ID: \"" + cfg.SessionId + "\"");
        Console.WriteLine("    • Máy B phải có internet (dù khác mạng, khác IP đều được)");
    }
}
