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
    // ── Thông tin AWS VPS ─────────────────────────────────────────────
    private const string DefaultHost     = "13.229.239.111";
    private const int    DefaultPort     = 22;
    private const string DefaultUsername = "ubuntu";
    // ─────────────────────────────────────────────────────────────────

    // Path của file .ppk tạm (giải mã trong RAM, ghi ra temp folder)
    private static string? _tempKeyPath;

    /// <summary>
    /// Trả về VpsConfig. Nếu key đang dùng encrypted mode, SshKeyFile
    /// trỏ tới file tạm đã giải mã trong %TEMP%.
    /// </summary>
    public static VpsConfig GetConfig() => new VpsConfig
    {
        Host       = DefaultHost,
        Port       = DefaultPort,
        Username   = DefaultUsername,
        Password   = "",
        SshKeyFile = _tempKeyPath ?? ResolveKeyPath(),
    };

    /// <summary>
    /// Xác thực group password, giải mã key vào RAM → ghi file tạm.
    /// Gọi khi khởi động nếu mode = Encrypted.
    /// </summary>
    public static bool UnlockWithPassword(string password)
    {
        try
        {
            var keyBytes = KeyManager.DecryptToMemory(password);
            // Xóa file tạm cũ nếu có
            if (_tempKeyPath != null) KeyManager.DeleteTempKey(_tempKeyPath);
            _tempKeyPath = KeyManager.WriteTempKey(keyBytes);
            Array.Clear(keyBytes);
            Logger.Success("Key đã được giải mã thành công.");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Error(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Lỗi giải mã key: {ex.Message}");
            return false;
        }
    }

    /// <summary>Xóa file .ppk tạm khi thoát app.</summary>
    public static void Cleanup()
    {
        if (_tempKeyPath == null) return;
        KeyManager.DeleteTempKey(_tempKeyPath);
        _tempKeyPath = null;
    }

    public static bool IsUnlocked => _tempKeyPath != null && File.Exists(_tempKeyPath);

    public static string ResolveKeyPath()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        var path   = Path.Combine(exeDir, "default_vps.ppk");
        if (File.Exists(path)) return path;
        if (File.Exists("default_vps.ppk")) return "default_vps.ppk";
        return "default_vps.ppk";
    }

    public static bool KeyFileExists()
        => KeyManager.DetectMode() != KeyMode.Missing;

    public static string KeyFileName => KeyManager.DetectMode() == KeyMode.Encrypted
        ? "default_vps.ppk.enc" : "default_vps.ppk";

    public static void PrintInfo()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Default VPS : {DefaultUsername}@{DefaultHost}:{DefaultPort}");
        Console.ResetColor();
        KeyManager.PrintKeyStatus();
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

            var keyMode = KeyManager.DetectMode();

            if (keyMode == KeyMode.Missing)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  ✘ Không tìm thấy file key!");
                Console.WriteLine("    Đặt 'default_vps.ppk' hoặc 'default_vps.ppk.enc'");
                Console.WriteLine("    vào cùng thư mục với SshTunnelManager.exe rồi chạy lại.");
                Console.ResetColor();
            }
            else if (keyMode == KeyMode.Plain)
            {
                // Lần đầu — có file .ppk gốc → hỏi đặt group password rồi tự mã hóa
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  ℹ  Phát hiện file 'default_vps.ppk' chưa mã hóa.");
                Console.WriteLine("     Hãy đặt Group Password để mã hóa key ngay bây giờ.");
                Console.WriteLine("     Password này dùng chung cho cả nhóm.");
                Console.ResetColor();

                string password = SetGroupPasswordInteractive();
                if (!string.IsNullOrEmpty(password))
                {
                    try
                    {
                        KeyManager.EncryptPlainKey(password);
                        DefaultVpsProvider.UnlockWithPassword(password);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n  ✔  Key đã mã hóa và mở khóa thành công!");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("  ⚠  Hãy XÓA file 'default_vps.ppk' gốc sau khi setup xong.");
                        Console.WriteLine("     Chỉ giữ lại 'default_vps.ppk.enc' — file này an toàn khi upload GitHub.");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Mã hóa thất bại: {ex.Message}");
                    }
                }
            }
            else // KeyMode.Encrypted — file .enc đã có → nhập password để unlock
            {
                Console.WriteLine("\n  🔐 Nhập Group Password để mở khóa key VPS:");
                bool unlocked = false;
                for (int i = 1; i <= 3; i++)
                {
                    Console.Write($"  Group Password (lần {i}/3): ");
                    var pwd = PromptPassword("");
                    if (DefaultVpsProvider.UnlockWithPassword(pwd))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("  ✔  Mở khóa thành công!");
                        Console.ResetColor();
                        unlocked = true;
                        break;
                    }
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  ✘  Sai mật khẩu.");
                    Console.ResetColor();
                }
                if (!unlocked)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n  ✘  Sai mật khẩu 3 lần. Chuyển sang Custom VPS.");
                    Console.ResetColor();
                    cfg.VpsMode = VpsMode.Custom;
                }
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
        if (!string.IsNullOrEmpty(label)) Console.Write($"{label}: ");
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

    /// <summary>
    /// Hỏi đặt Group Password mới — yêu cầu nhập 2 lần để xác nhận.
    /// </summary>
    private static string SetGroupPasswordInteractive()
    {
        Console.WriteLine("\n  Đặt Group Password (tối thiểu 6 ký tự):");
        Console.WriteLine("  Password này dùng chung cho cả nhóm — thông báo qua Zalo/gặp trực tiếp.\n");

        while (true)
        {
            Console.Write("  Nhập Group Password  : ");
            var pass = PromptPassword("");
            if (pass.Length < 6)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ✘ Quá ngắn, cần ít nhất 6 ký tự.");
                Console.ResetColor();
                continue;
            }
            Console.Write("  Xác nhận lại         : ");
            var confirm = PromptPassword("");
            if (pass == confirm) return pass;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✘ Hai lần nhập không khớp, thử lại.");
            Console.ResetColor();
        }
    }
}

