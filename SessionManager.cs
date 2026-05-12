using System.Security.Cryptography;
using System.Text;

namespace SshTunnelManager;

/// <summary>
/// Converts a human-readable Session ID (e.g. "nhom1", "alice")
/// into a deterministic, unique port number in the range 10000–19999.
/// Same Session ID → same port on every machine → Machine A and B always match.
/// </summary>
public static class SessionManager
{
    private const int PortRangeStart = 10000;
    private const int PortRangeEnd   = 19999;
    private const int PortRangeSize  = PortRangeEnd - PortRangeStart + 1; // 10000 slots

    /// <summary>
    /// Hash a session ID string to a port number.
    /// Uses SHA256 so distribution is uniform and collisions are extremely rare.
    /// </summary>
    public static int SessionIdToPort(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be empty.");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId.Trim().ToLower()));
        // Take first 4 bytes as uint, mod into range
        var value = (int)(BitConverter.ToUInt32(bytes, 0) % PortRangeSize);
        return PortRangeStart + value;
    }

    /// <summary>
    /// Returns all three ports derived from a session ID:
    ///   SSH  → base port
    ///   RDP  → base port + 1
    ///   HTTP → base port + 2
    /// This keeps all services of one session tightly grouped.
    /// </summary>
    public static SessionPorts GetSessionPorts(string sessionId)
    {
        var basePort = SessionIdToPort(sessionId);
        // Ensure we don't overflow the range
        if (basePort + 2 > PortRangeEnd) basePort -= 2;
        return new SessionPorts(sessionId, basePort, basePort + 1, basePort + 2);
    }

    public static void PrintSessionInfo(string sessionId)
    {
        var ports = GetSessionPorts(sessionId);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  Session ID : \"{sessionId}\"");
        Console.WriteLine($"  ┌──────────────────────────────────────┐");
        Console.WriteLine($"  │  Service │ VPS relay port │ Use for  │");
        Console.WriteLine($"  ├──────────────────────────────────────┤");
        Console.WriteLine($"  │  SSH     │   {ports.SshPort,-13} │ port 22  │");
        Console.WriteLine($"  │  RDP     │   {ports.RdpPort,-13} │ port 3389│");
        Console.WriteLine($"  │  Custom  │   {ports.CustomPort,-13} │ any port │");
        Console.WriteLine($"  └──────────────────────────────────────┘");
        Console.WriteLine($"\n  ⚠  Share this Session ID with your partner.");
        Console.WriteLine($"     Both machines MUST use the same Session ID.");
        Console.ResetColor();
    }

    /// <summary>
    /// Validate that a session ID is safe to use (no special chars that break plink args).
    /// </summary>
    public static bool IsValid(string sessionId) =>
        !string.IsNullOrWhiteSpace(sessionId) &&
        sessionId.Length is >= 3 and <= 32 &&
        sessionId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
}

public record SessionPorts(string SessionId, int SshPort, int RdpPort, int CustomPort);
