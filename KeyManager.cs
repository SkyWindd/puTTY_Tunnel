using System.Security.Cryptography;
using System.Text;

namespace SshTunnelManager;

/// <summary>
/// Mã hóa / giải mã file .ppk bằng AES-256-GCM với password nhóm.
/// File .ppk.enc có thể upload GitHub an toàn.
/// File .ppk giải mã chỉ tồn tại tạm trong RAM, KHÔNG ghi ra đĩa.
/// </summary>
public static class KeyManager
{
    private const string EncryptedFile = "default_vps.ppk.enc";
    private const string PlainFile     = "default_vps.ppk";

    // Số vòng PBKDF2 để derive key từ password
    private const int Pbkdf2Iterations = 200_000;
    private const int SaltSize         = 16;
    private const int NonceSize        = 12; // AES-GCM nonce
    private const int TagSize          = 16; // AES-GCM auth tag

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra trạng thái file key. Trả về mode đang dùng.
    /// </summary>
    public static KeyMode DetectMode()
    {
        var dir = AppDir();
        if (File.Exists(Path.Combine(dir, EncryptedFile))) return KeyMode.Encrypted;
        if (File.Exists(Path.Combine(dir, PlainFile)))     return KeyMode.Plain;
        return KeyMode.Missing;
    }

    /// <summary>
    /// Giải mã file .ppk.enc bằng password, trả về nội dung .ppk dưới dạng bytes trong RAM.
    /// KHÔNG ghi file ra đĩa.
    /// </summary>
    public static byte[] DecryptToMemory(string password)
    {
        var path = Path.Combine(AppDir(), EncryptedFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Không tìm thấy '{EncryptedFile}'");

        var data = File.ReadAllBytes(path);

        // Layout: [salt 16] [nonce 12] [tag 16] [ciphertext ...]
        if (data.Length < SaltSize + NonceSize + TagSize)
            throw new InvalidDataException("File .enc bị lỗi hoặc không hợp lệ.");

        var salt       = data[..SaltSize];
        var nonce      = data[SaltSize..(SaltSize + NonceSize)];
        var tag        = data[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
        var ciphertext = data[(SaltSize + NonceSize + TagSize)..];

        var key = DeriveKey(password, salt);

        try
        {
            using var aes = new AesGcm(key, TagSize);
            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (CryptographicException)
        {
            throw new UnauthorizedAccessException("Sai mật khẩu nhóm hoặc file bị hỏng.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Mã hóa file .ppk thành .ppk.enc bằng password.
    /// Dùng khi setup lần đầu (người quản lý chạy).
    /// </summary>
    public static void EncryptPlainKey(string password)
    {
        var dir       = AppDir();
        var plainPath = Path.Combine(dir, PlainFile);
        var encPath   = Path.Combine(dir, EncryptedFile);

        if (!File.Exists(plainPath))
            throw new FileNotFoundException($"Không tìm thấy '{PlainFile}' để mã hóa.");

        var plaintext = File.ReadAllBytes(plainPath);
        var salt      = RandomBytes(SaltSize);
        var nonce     = RandomBytes(NonceSize);
        var key       = DeriveKey(password, salt);

        try
        {
            using var aes        = new AesGcm(key, TagSize);
            var       ciphertext = new byte[plaintext.Length];
            var       tag        = new byte[TagSize];
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            // Ghi: [salt][nonce][tag][ciphertext]
            using var fs = File.Create(encPath);
            fs.Write(salt);
            fs.Write(nonce);
            fs.Write(tag);
            fs.Write(ciphertext);

            Logger.Success($"Đã mã hóa → '{EncryptedFile}' ({new FileInfo(encPath).Length} bytes)");
            Logger.Info("File này có thể upload GitHub an toàn.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Ghi nội dung .ppk từ RAM ra file tạm, trả về đường dẫn.
    /// File tạm sẽ bị xóa khi gọi DeleteTempKey().
    /// </summary>
    public static string WriteTempKey(byte[] keyBytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"stm_{Guid.NewGuid():N}.ppk");
        File.WriteAllBytes(path, keyBytes);
        // Ẩn file tạm
        File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.Temporary);
        return path;
    }

    /// <summary>Xóa file .ppk tạm khỏi đĩa.</summary>
    public static void DeleteTempKey(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            // Ghi đè bằng 0 trước khi xóa
            var len = (int)new FileInfo(path).Length;
            File.WriteAllBytes(path, new byte[len]);
            File.Delete(path);
            Logger.Info("File key tạm đã được xóa an toàn.");
        }
        catch (Exception ex) { Logger.Warn($"Không xóa được file key tạm: {ex.Message}"); }
    }

    /// <summary>
    /// Hiển thị trạng thái key và hướng dẫn.
    /// </summary>
    public static void PrintKeyStatus()
    {
        var mode = DetectMode();
        Console.ForegroundColor = mode switch
        {
            KeyMode.Encrypted => ConsoleColor.Green,
            KeyMode.Plain     => ConsoleColor.Yellow,
            KeyMode.Missing   => ConsoleColor.Red,
            _                 => ConsoleColor.Gray
        };
        Console.Write($"\n  Key status: ");
        Console.ResetColor();

        switch (mode)
        {
            case KeyMode.Encrypted:
                Console.WriteLine($"✔  '{EncryptedFile}' (đã mã hóa — an toàn)");
                Console.WriteLine("     Nhập group password để sử dụng.");
                break;
            case KeyMode.Plain:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠  '{PlainFile}' (chưa mã hóa — KHÔNG nên upload GitHub)");
                Console.WriteLine("     Dùng menu [8] Setup → mã hóa key để bảo vệ.");
                Console.ResetColor();
                break;
            case KeyMode.Missing:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✘  Không tìm thấy '{PlainFile}' hoặc '{EncryptedFile}'");
                Console.WriteLine("     Đặt file .ppk vào cùng thư mục với SshTunnelManager.exe");
                Console.ResetColor();
                break;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static byte[] DeriveKey(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            32); // 256-bit key

    private static byte[] RandomBytes(int count)
    {
        var buf = new byte[count];
        RandomNumberGenerator.Fill(buf);
        return buf;
    }

    private static string AppDir()
        => Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();
}

public enum KeyMode { Plain, Encrypted, Missing }
