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
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  ┌─────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                   HOW TO USE THIS TUNNEL                       │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        if (cfg.Role == MachineRole.MachineB)
        {
            Console.WriteLine("\n  Machine B (this machine) is the SERVER side.");
            Console.WriteLine("  Action: This app pushes Reverse Tunnel(s) up to VPS.");
            Console.WriteLine("  Your VPS exposes the following relay ports:");
            foreach (var t in cfg.Tunnels)
                Console.WriteLine($"    • {t.Name}: VPS port {t.VpsPort}  ←→  this machine port {t.RemotePort}");
            Console.WriteLine("\n  Nothing else needed here — keep this app running.");
        }
        else
        {
            Console.WriteLine("\n  Machine A (this machine) is the CLIENT side.");
            Console.WriteLine("  The following local ports are forwarded through the VPS to Machine B:");
            foreach (var t in cfg.Tunnels)
            {
                Console.WriteLine($"\n  [{t.Name}]");
                Console.WriteLine($"    localhost:{t.LocalPort}  →  VPS:{t.VpsPort}  →  MachineB:{t.RemotePort}");
                if (t.Type == ConnectionType.SSH)
                    Console.WriteLine($"    Open PuTTY → host=127.0.0.1  port={t.LocalPort}");
                else if (t.Type == ConnectionType.RDP)
                    Console.WriteLine($"    Open mstsc → 127.0.0.1:{t.LocalPort}");
                else
                    Console.WriteLine($"    Connect any app to localhost:{t.LocalPort}");
            }
        }
        Console.WriteLine();
    }
}
