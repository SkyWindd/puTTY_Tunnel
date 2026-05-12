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

        // Validate plink
        if (!PlinkWrapper.ValidatePlinkPath(_cfg.PlinkPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  ⚠  plink.exe not found at '{_cfg.PlinkPath}'.");
            Console.WriteLine("     Place plink.exe next to SshTunnelManager.exe.");
            Console.ResetColor();
        }

        // Validate default VPS key file
        if (_cfg.VpsMode == VpsMode.Default && !DefaultVpsProvider.KeyFileExists())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  ⚠  Default VPS key '{DefaultVpsProvider.KeyFileName}' not found.");
            Console.WriteLine("     Place the .ppk file next to SshTunnelManager.exe.");
            Console.ResetColor();
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
