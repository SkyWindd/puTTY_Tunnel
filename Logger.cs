namespace SshTunnelManager;

public static class Logger
{
    private static readonly object _lock = new();
    private const string LogFile = "tunnel.log";

    public static bool VerboseConsole { get; set; } = true;

    public static void Info(string msg)    => Write("INFO ", ConsoleColor.Gray,    msg);
    public static void Success(string msg) => Write("OK   ", ConsoleColor.Green,   msg);
    public static void Warn(string msg)    => Write("WARN ", ConsoleColor.Yellow,  msg);
    public static void Error(string msg)   => Write("ERROR", ConsoleColor.Red,     msg);
    public static void Tunnel(string msg)  => Write("TUNL ", ConsoleColor.Cyan,    msg);

    private static void Write(string level, ConsoleColor color, string msg)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {msg}";
        lock (_lock)
        {
            try { File.AppendAllText(LogFile, line + Environment.NewLine); } catch { /* ignore log I/O errors */ }
            if (VerboseConsole)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(line);
                Console.ResetColor();
            }
        }
    }

    public static void PrintLastLines(int n = 30)
    {
        if (!File.Exists(LogFile)) { Console.WriteLine("(no log file yet)"); return; }
        var lines = File.ReadAllLines(LogFile);
        foreach (var l in lines.TakeLast(n)) Console.WriteLine(l);
    }
}
