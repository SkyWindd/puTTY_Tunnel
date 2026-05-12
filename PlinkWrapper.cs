using System.Diagnostics;

namespace SshTunnelManager;

/// <summary>
/// Wraps a single plink.exe process representing one SSH tunnel.
/// </summary>
public class PlinkProcess : IDisposable
{
    public string TunnelName { get; }
    public bool IsRunning => _process is { HasExited: false };

    private Process? _process;
    private readonly string _arguments;
    private readonly string _plinkPath;
    private bool _disposed;

    public PlinkProcess(string tunnelName, string plinkPath, string arguments)
    {
        TunnelName  = tunnelName;
        _plinkPath  = plinkPath;
        _arguments  = arguments;
    }

    public bool Start()
    {
        Stop(); // kill any stale process first
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _plinkPath,
                Arguments              = _arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            _process = new Process { StartInfo = psi };
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) Logger.Tunnel($"[{TunnelName}] {e.Data}"); };
            _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) Logger.Warn($"[{TunnelName}] STDERR: {e.Data}"); };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Logger.Success($"[{TunnelName}] plink started (PID {_process.Id}) → {_arguments}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"[{TunnelName}] Failed to start plink: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        if (_process == null) return;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(2000);
                Logger.Info($"[{TunnelName}] plink stopped (PID {_process.Id})");
            }
        }
        catch (Exception ex) { Logger.Warn($"[{TunnelName}] Stop error: {ex.Message}"); }
        finally { _process.Dispose(); _process = null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

// ============================================================
//  PlinkWrapper  — builds command-line arguments and spawns tunnels
// ============================================================

public static class PlinkWrapper
{
    /// <summary>
    /// Build plink args for Machine B — Reverse Tunnel:
    ///   plink -ssh user@VPS -R VpsPort:localhost:RemotePort -N -batch [-pw pass | -i key]
    /// </summary>
    public static string BuildReverseArgs(VpsConfig vps, TunnelConfig tunnel)
    {
        var auth = BuildAuth(vps);
        return $"-ssh {vps.Username}@{vps.Host} -P {vps.Port} " +
               $"-R {tunnel.VpsPort}:localhost:{tunnel.RemotePort} " +
               $"-N -batch {auth}";
    }

    /// <summary>
    /// Build plink args for Machine A — Forward Tunnel:
    ///   plink -ssh user@VPS -L LocalPort:localhost:VpsPort -N -batch [-pw pass | -i key]
    /// </summary>
    public static string BuildForwardArgs(VpsConfig vps, TunnelConfig tunnel)
    {
        var auth = BuildAuth(vps);
        return $"-ssh {vps.Username}@{vps.Host} -P {vps.Port} " +
               $"-L {tunnel.LocalPort}:localhost:{tunnel.VpsPort} " +
               $"-N -batch {auth}";
    }

    private static string BuildAuth(VpsConfig vps)
    {
        if (!string.IsNullOrWhiteSpace(vps.SshKeyFile))
            return $"-i \"{vps.SshKeyFile}\"";
        if (!string.IsNullOrWhiteSpace(vps.Password))
            return $"-pw \"{vps.Password}\"";
        return ""; // rely on ssh-agent / pageant
    }

    /// <summary>
    /// Verify that plink.exe exists and is executable.
    /// </summary>
    public static bool ValidatePlinkPath(string path)
    {
        if (File.Exists(path)) return true;

        // Try PATH
        var envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in envPath.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir, path);
            if (File.Exists(full)) return true;
        }
        return false;
    }
}
