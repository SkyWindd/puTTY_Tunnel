namespace SshTunnelManager;

// ============================================================
//  Program — CLI menu shell
// ============================================================

class Program
{
    private static AppConfig _cfg = null!;
    private static TunnelMonitor _monitor = null!;

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Logger.Warn("Ctrl+C received — shutting down...");
            Shutdown();
            Environment.Exit(0);
        };

        PrintBanner();

        // --setup flag: force re-run wizard
        if (args.Contains("--setup"))
            _cfg = ConfigManager.RunSetupWizard();
        else
            _cfg = ConfigManager.Load();

        // --encrypt-key: tool để mã hóa file .ppk (người quản lý dùng 1 lần)
        if (args.Contains("--encrypt-key"))
        {
            RunEncryptKeyTool();
            return;
        }

        // Validate plink
        if (!PlinkWrapper.ValidatePlinkPath(_cfg.PlinkPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  ⚠  plink.exe not found at '{_cfg.PlinkPath}'.");
            Console.WriteLine("     Place plink.exe next to SshTunnelManager.exe.");
            Console.ResetColor();
        }

        // ── Key unlock flow (khi load config cũ, không qua wizard) ──────
        if (_cfg.VpsMode == VpsMode.Default && !DefaultVpsProvider.IsUnlocked)
        {
            var keyMode = KeyManager.DetectMode();
            if (keyMode == KeyMode.Encrypted)
                UnlockKeyInteractive();
            else if (keyMode == KeyMode.Plain)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  ⚠  File key chưa mã hóa (default_vps.ppk).");
                Console.WriteLine("     Gợi ý: chạy 'SshTunnelManager.exe --encrypt-key' để mã hóa.");
                Console.ResetColor();
            }
            else if (keyMode == KeyMode.Missing)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  ✘ Không tìm thấy file key VPS.");
                Console.WriteLine("    Cần có 'default_vps.ppk' hoặc 'default_vps.ppk.enc'");
                Console.ResetColor();
            }
        }

        _monitor = new TunnelMonitor(_cfg);

        RunMenu();
        Shutdown();
    }

    // ---- Main menu loop ----

    private static void RunMenu()
    {
        while (true)
        {
            PrintMenu();
            Console.Write("Choice: ");
            var choice = Console.ReadLine()?.Trim().ToLower();

            switch (choice)
            {
                case "1": StartTunnels();                               break;
                case "2": StopTunnels();                                break;
                case "3": _monitor.PrintStatus();                       break;
                case "4": ConnectionHandler.PrintTunnelUsageGuide(_cfg);break;
                case "5": OpenClientApp();                              break;
                case "6": ManageCustomTunnels();                        break;
                case "7": ShowLog();                                    break;
                case "8": RunSetup();                                   break;
                case "9": ToggleAutoReconnect();                        break;
                case "0":
                case "q": case "quit": case "exit":
                    return;
                default:
                    Console.WriteLine("  Unknown option — try again.");
                    break;
            }
        }
    }

    // ---- Menu actions ----

    private static void StartTunnels()
    {
        if (_monitor.IsRunning)
        {
            Logger.Warn("Tunnels already running. Stop them first (option 2).");
            return;
        }
        Logger.VerboseConsole = true;
        _monitor.StartAll();
        Logger.Success("All tunnels started. Use option 3 to check status.");
    }

    private static void StopTunnels()
    {
        if (!_monitor.IsRunning) { Logger.Warn("No tunnels running."); return; }
        _monitor.StopAll();
        Logger.Success("All tunnels stopped.");
    }

    private static void OpenClientApp()
    {
        if (_cfg.Role != MachineRole.MachineA)
        {
            Logger.Warn("This option is only available on Machine A (client side).");
            return;
        }
        Console.WriteLine("\n  Open client app:");
        Console.WriteLine("  [1] PuTTY  (SSH)");
        Console.WriteLine("  [2] mstsc  (RDP)");
        Console.Write("Choice: ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1": ConnectionHandler.LaunchPutty(_cfg); break;
            case "2": ConnectionHandler.LaunchRdp(_cfg);   break;
            default:  Console.WriteLine("Cancelled.");     break;
        }
    }

    private static void ManageCustomTunnels()
    {
        Console.WriteLine("\n  Custom tunnel management:");
        Console.WriteLine("  [1] Add new custom port forward");
        Console.WriteLine("  [2] Remove a tunnel");
        Console.Write("Choice: ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1": ConnectionHandler.AddCustomTunnel(_cfg);  break;
            case "2": ConnectionHandler.RemoveTunnel(_cfg);     break;
            default:  Console.WriteLine("Cancelled.");          break;
        }
    }

    private static void ShowLog()
    {
        Console.Write("Show last how many lines? [30]: ");
        int.TryParse(Console.ReadLine(), out var n);
        Logger.PrintLastLines(n < 1 ? 30 : n);
    }

    private static void RunSetup()
    {
        if (_monitor.IsRunning)
        {
            Console.Write("Tunnels are running. Stop them first? (y/N): ");
            if (Console.ReadLine()?.Trim().ToLower() != "y") return;
            _monitor.StopAll();
        }
        _monitor.Dispose();
        _cfg    = ConfigManager.RunSetupWizard();
        _monitor = new TunnelMonitor(_cfg);
    }

    private static void ToggleAutoReconnect()
    {
        _cfg.AutoReconnect = !_cfg.AutoReconnect;
        ConfigManager.Save(_cfg);
        Logger.Info($"AutoReconnect is now: {(_cfg.AutoReconnect ? "ON" : "OFF")}");
    }

    private static void Shutdown()
    {
        if (_monitor?.IsRunning == true)
        {
            Logger.Info("Stopping all tunnels on exit...");
            _monitor.StopAll();
        }
        _monitor?.Dispose();
        // Xóa file .ppk tạm khỏi đĩa
        DefaultVpsProvider.Cleanup();
    }

    /// <summary>Hỏi group password và giải mã key, cho phép thử 3 lần.</summary>
    private static void UnlockKeyInteractive()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n  🔐 Key VPS đã được mã hóa — cần nhập Group Password để tiếp tục.");
        Console.ResetColor();

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Console.Write($"  Nhập Group Password (lần {attempt}/3): ");
            var password = ReadPassword();

            if (DefaultVpsProvider.UnlockWithPassword(password))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✔  Key đã được mở khóa thành công!\n");
                Console.ResetColor();
                return;
            }

            if (attempt < 3)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ✘  Sai mật khẩu, thử lại...");
                Console.ResetColor();
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n  ✘  Sai mật khẩu 3 lần. Không thể dùng Default VPS.");
        Console.WriteLine("     Chuyển sang Custom VPS hoặc kiểm tra lại mật khẩu nhóm.");
        Console.ResetColor();
    }

    /// <summary>
    /// Tool mã hóa key — chạy với flag --encrypt-key.
    /// Người quản lý dùng 1 lần, sau đó phân phối file .ppk.enc.
    /// </summary>
    private static void RunEncryptKeyTool()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╔══════════════════════════════════════════════════╗");
        Console.WriteLine("  ║         Công cụ Mã hóa File Key VPS             ║");
        Console.WriteLine("  ╚══════════════════════════════════════════════════╝\n");
        Console.ResetColor();

        Console.WriteLine("  Tool này mã hóa 'default_vps.ppk' → 'default_vps.ppk.enc'");
        Console.WriteLine("  File .enc có thể upload GitHub an toàn.");
        Console.WriteLine("  File .ppk gốc SAU KHI MÃ HÓA nên xóa khỏi thư mục publish.\n");

        if (KeyManager.DetectMode() == KeyMode.Missing)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✘ Không tìm thấy 'default_vps.ppk'.");
            Console.WriteLine("     Đặt file .ppk vào cùng thư mục với .exe rồi chạy lại.");
            Console.ResetColor();
            Console.ReadLine();
            return;
        }

        if (KeyManager.DetectMode() == KeyMode.Encrypted)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ℹ  File 'default_vps.ppk.enc' đã tồn tại.");
            Console.Write("     Mã hóa lại? (y/N): ");
            Console.ResetColor();
            if (Console.ReadLine()?.Trim().ToLower() != "y") return;
        }

        Console.WriteLine("\n  Đặt Group Password cho nhóm của bạn:");
        Console.WriteLine("  (Tất cả thành viên sẽ cần nhập password này khi chạy app)\n");

        string password, confirm;
        while (true)
        {
            Console.Write("  Nhập Group Password  : ");
            password = ReadPassword();
            if (password.Length < 6)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ✘ Password phải có ít nhất 6 ký tự.");
                Console.ResetColor();
                continue;
            }
            Console.Write("  Xác nhận lại         : ");
            confirm = ReadPassword();
            if (password == confirm) break;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✘ Hai lần nhập không khớp, thử lại.");
            Console.ResetColor();
        }

        try
        {
            KeyManager.EncryptPlainKey(password);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ✔  Mã hóa thành công!");
            Console.WriteLine("  ✔  File 'default_vps.ppk.enc' đã sẵn sàng phân phối.");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  ⚠  Tiếp theo:");
            Console.WriteLine("     1. XÓA file 'default_vps.ppk' khỏi thư mục publish\\");
            Console.WriteLine("     2. Giữ lại 'default_vps.ppk.enc'");
            Console.WriteLine("     3. Thông báo Group Password cho thành viên qua kênh riêng (Zalo, gặp trực tiếp)");
            Console.WriteLine("     4. KHÔNG ghi Group Password vào GitHub hay chat công khai");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Logger.Error($"Mã hóa thất bại: {ex.Message}");
        }

        Console.WriteLine("\n  Nhấn Enter để thoát...");
        Console.ReadLine();
    }

    private static string ReadPassword()
    {
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); }
            else { sb.Append(key.KeyChar); Console.Write("*"); }
        }
        Console.WriteLine();
        return sb.ToString();
    }

    // ---- UI helpers ----

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  ╔═══════════════════════════════════════════════════════╗
  ║         SSH Tunnel Manager  v1.0  (plink-based)       ║
  ║   Connect two private-IP machines via VPS relay       ║
  ╚═══════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    private static void PrintMenu()
    {
        var role    = _cfg?.Role.ToString() ?? "?";
        var running = _monitor?.IsRunning == true;
        var status  = running ? "● RUNNING" : "○ STOPPED";
        var vpsTag  = _cfg?.VpsMode == VpsMode.Default ? "Default AWS" : $"Custom ({_cfg?.Vps.Host})";
        var session = string.IsNullOrEmpty(_cfg?.SessionId) ? "(none)" : $"\"{_cfg.SessionId}\"";

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  ┌─────────────────────────────────────────────────────┐");
        Console.ForegroundColor = running ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write($"  │  {status,-10}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Role: {role,-10}  Session: {session,-14}│");
        Console.WriteLine($"  │  VPS: {vpsTag,-46}│");
        Console.WriteLine($"  └─────────────────────────────────────────────────────┘");
        Console.ResetColor();

        Console.WriteLine("  [1] Start tunnels");
        Console.WriteLine("  [2] Stop tunnels");
        Console.WriteLine("  [3] Status / liveness");
        Console.WriteLine("  [4] Usage guide (how to connect)");
        if (_cfg?.Role == MachineRole.MachineA)
            Console.WriteLine("  [5] Open PuTTY / RDP");
        Console.WriteLine("  [6] Manage custom port forwards");
        Console.WriteLine("  [7] View log");
        Console.WriteLine("  [8] Setup wizard (change session / VPS / role)");
        Console.WriteLine($"  [9] Toggle auto-reconnect (currently: {(_cfg?.AutoReconnect == true ? "ON" : "OFF")})");
        Console.WriteLine("  [0] Quit");
    }
}
