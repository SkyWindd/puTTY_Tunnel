using Newtonsoft.Json;

namespace SshTunnelManager;

// ============================================================
//  Models
// ============================================================

public enum MachineRole   { MachineA, MachineB }
public enum ConnectionType { SSH = 22, RDP = 3389, Custom = 0 }
public enum VpsMode       { Default, Custom }

public class VpsConfig
{
    public string Host       { get; set; } = "";
    public int    Port       { get; set; } = 22;
    public string Username   { get; set; } = "";
    public string Password   { get; set; } = "";
    public string SshKeyFile { get; set; } = ""; // path to .ppk file
}

public class TunnelConfig
{
    public string         Name       { get; set; } = "default";
    public ConnectionType Type       { get; set; } = ConnectionType.SSH;
    public int            LocalPort  { get; set; } = 2222;
    public int            RemotePort { get; set; } = 22;
    public int            VpsPort    { get; set; } = 2222;
}

public class AppConfig
{
    public MachineRole        Role               { get; set; } = MachineRole.MachineA;
    public VpsMode            VpsMode            { get; set; } = VpsMode.Default;
    public string             SessionId          { get; set; } = "";
    public VpsConfig          CustomVps          { get; set; } = new();
    public List<TunnelConfig> Tunnels            { get; set; } = new();
    public string             PlinkPath          { get; set; } = "plink.exe";
    public int                HeartbeatIntervalSec { get; set; } = 15;
    public int                ReconnectDelaySec  { get; set; } = 5;
    public bool               AutoReconnect      { get; set; } = true;

    // ---- Resolved at runtime (not saved to JSON) ----
    [JsonIgnore]
    public VpsConfig Vps => VpsMode == VpsMode.Default
        ? DefaultVpsProvider.GetConfig()
        : CustomVps;
}

// ============================================================
//  DefaultVpsProvider  — AWS VPS baked in
// ============================================================

public static class DefaultVpsProvider
{
    // ── Thông tin AWS VPS của bạn ──────────────────────────────────────
    private const string DefaultHost     = "13.229.239.111";
    private const int    DefaultPort     = 22;
    private const string DefaultUsername = "ubuntu";
    // Đường dẫn tới file .ppk — đặt cạnh .exe, tên cố định
    private const string DefaultKeyFile  = "default_vps.ppk";
    // ───────────────────────────────────────────────────────────────────

    public static VpsConfig GetConfig() => new VpsConfig
    {
        Host       = DefaultHost,
        Port       = DefaultPort,
        Username   = DefaultUsername,
        Password   = "",
        SshKeyFile = ResolveKeyPath(),
    };

    /// <summary>
    /// Tìm file .ppk cạnh .exe hoặc trong thư mục hiện tại.
    /// </summary>
    public static string ResolveKeyPath()
    {
        // 1. Cạnh file .exe
        var exeDir  = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        var exePath = Path.Combine(exeDir, DefaultKeyFile);
        if (File.Exists(exePath)) return exePath;

        // 2. Thư mục làm việc hiện tại
        if (File.Exists(DefaultKeyFile)) return DefaultKeyFile;

        return DefaultKeyFile; // trả về tên để báo lỗi rõ hơn
    }

    public static bool KeyFileExists() => File.Exists(ResolveKeyPath());

    public static string KeyFileName => DefaultKeyFile;

    public static void PrintInfo()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Default VPS : {DefaultUsername}@{DefaultHost}:{DefaultPort}");
        Console.WriteLine($"  Key file    : {ResolveKeyPath()}  " +
                          (KeyFileExists() ? "✔" : "✘ NOT FOUND"));
        Console.ResetColor();
    }
}

// ============================================================
//  ConfigManager
// ============================================================

public static class ConfigManager
{
    private const string ConfigFile = "config.json";

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigFile))
        {
            Logger.Info("No config found — launching setup wizard.");
            return RunSetupWizard();
        }
        try
        {
            var json = File.ReadAllText(ConfigFile);
            var cfg  = JsonConvert.DeserializeObject<AppConfig>(json)
                       ?? throw new InvalidDataException("Empty config");
            Logger.Info($"Config loaded (session: \"{cfg.SessionId}\", role: {cfg.Role}, vps: {cfg.VpsMode})");
            return cfg;
        }
        catch (Exception ex)
        {
            Logger.Error($"Config load failed: {ex.Message} — re-running wizard.");
            return RunSetupWizard();
        }
    }

    public static void Save(AppConfig cfg)
    {
        var json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
        File.WriteAllText(ConfigFile, json);
        Logger.Info($"Config saved to '{ConfigFile}'");
    }

    // ---- Interactive setup wizard ----

    public static AppConfig RunSetupWizard()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║        SSH Tunnel Manager — Setup Wizard         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.ResetColor();

        var cfg = new AppConfig { PlinkPath = "plink.exe", AutoReconnect = true };

        // ── Step 1: Role ──────────────────────────────────────────────
        Console.WriteLine("\n  Role of THIS machine:");
        Console.WriteLine("  [A] Machine A — Client (opens PuTTY/RDP to remote)");
        Console.WriteLine("  [B] Machine B — Server (the machine you want to reach)");
        Console.Write("  Choice [A/B]: ");
        cfg.Role = Console.ReadLine()?.Trim().ToUpper() == "B"
            ? MachineRole.MachineB : MachineRole.MachineA;

        // ── Step 2: Session ID ────────────────────────────────────────
        Console.WriteLine("\n  Session ID — a short name shared between Machine A and B.");
        Console.WriteLine("  Examples: nhom1, alice-bob, dev-team");
        Console.WriteLine("  Rules: 3–32 chars, letters/numbers/dash/underscore only.");
        string sessionId;
        while (true)
        {
            Console.Write("  Session ID: ");
            sessionId = Console.ReadLine()?.Trim() ?? "";
            if (SessionManager.IsValid(sessionId)) break;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✘ Invalid. Use 3–32 chars, letters/numbers/dash/underscore.");
            Console.ResetColor();
        }
        cfg.SessionId = sessionId;

        // Show derived ports
        SessionManager.PrintSessionInfo(sessionId);

        // ── Step 3: VPS Mode ──────────────────────────────────────────
        Console.WriteLine("\n  VPS Mode:");
        Console.WriteLine("  [1] Default VPS (AWS — built-in, free)");
        Console.WriteLine("  [2] Custom VPS  (your own VPS)");
        Console.Write("  Choice [1/2]: ");
        cfg.VpsMode = Console.ReadLine()?.Trim() == "2" ? VpsMode.Custom : VpsMode.Default;

        if (cfg.VpsMode == VpsMode.Default)
        {
            DefaultVpsProvider.PrintInfo();
            if (!DefaultVpsProvider.KeyFileExists())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  ⚠  Key file '{DefaultVpsProvider.KeyFileName}' not found.");
                Console.WriteLine("     Place it next to SshTunnelManager.exe before starting tunnels.");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("\n  --- Custom VPS Settings ---");
            cfg.CustomVps.Host     = Prompt("  VPS IP / hostname", "");
            cfg.CustomVps.Port     = int.TryParse(Prompt("  VPS SSH port", "22"), out var p) ? p : 22;
            cfg.CustomVps.Username = Prompt("  VPS username", "root");
            Console.Write("  Use SSH key file (.ppk)? (y/N): ");
            if (Console.ReadLine()?.Trim().ToLower() == "y")
                cfg.CustomVps.SshKeyFile = Prompt("  Path to .ppk", "");
            else
                cfg.CustomVps.Password = PromptPassword("  VPS password");
        }

        // ── Step 4: plink path ────────────────────────────────────────
        cfg.PlinkPath = Prompt("\n  Path to plink.exe", "plink.exe");

        // ── Step 5: Auto-reconnect ────────────────────────────────────
        Console.Write("\n  Enable auto-reconnect? (Y/n): ");
        cfg.AutoReconnect = Console.ReadLine()?.Trim().ToLower() != "n";

        // ── Build tunnel list from session ports ──────────────────────
        cfg.Tunnels = BuildTunnelsFromSession(sessionId);

        Save(cfg);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  ✔  Configuration saved.\n");
        Console.ResetColor();
        return cfg;
    }

    /// <summary>
    /// Generate SSH + RDP tunnel configs derived from session ID hash.
    /// </summary>
    public static List<TunnelConfig> BuildTunnelsFromSession(string sessionId)
    {
        var ports = SessionManager.GetSessionPorts(sessionId);
        return new List<TunnelConfig>
        {
            new TunnelConfig
            {
                Name       = "SSH",
                Type       = ConnectionType.SSH,
                LocalPort  = ports.SshPort,
                RemotePort = 22,
                VpsPort    = ports.SshPort,
            },
            new TunnelConfig
            {
                Name       = "RDP",
                Type       = ConnectionType.RDP,
                LocalPort  = ports.RdpPort,
                RemotePort = 3389,
                VpsPort    = ports.RdpPort,
            },
        };
    }

    private static string Prompt(string label, string defaultVal)
    {
        Console.Write($"{label}{(string.IsNullOrEmpty(defaultVal) ? "" : $" [{defaultVal}]")}: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultVal : input;
    }

    private static string PromptPassword(string label)
    {
        Console.Write($"{label}: ");
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
}
