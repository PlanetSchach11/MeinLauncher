using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace MeinLauncher.Services;

/// <summary>
/// Sichere Ablage der Microsoft-Session für den aktuellen Windows-Benutzer.
///
/// Die Session (inkl. Refresh-Token) wird mit DPAPI (CryptProtectData/CryptUnprotectData,
/// CurrentUser) verschlüsselt in <c>%APPDATA%\MeinLauncher\session.bin</c> abgelegt.
/// DPAPI bindet die Daten an das Windows-Benutzerkonto – ein anderer Benutzer
/// kann sie nicht entschlüsseln. Es werden keine Daten des offiziellen Launchers gelesen.
/// </summary>
public sealed class MicrosoftSessionStore
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MeinLauncher",
        "session.bin");

    private sealed class StoredSession
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTimeOffset ExpiresAt { get; set; }
        public string MinecraftUuid { get; set; } = "";
        public string MinecraftUsername { get; set; } = "";
        public string? SkinUrl { get; set; }
        public string? CapeUrl { get; set; }
        public string? Xuid { get; set; }
    }

    /// <summary>Speichert die Session DPAPI-verschlüsselt auf dem Datenträger.</summary>
    public void Save(MicrosoftSession session)
    {
        var dto = new StoredSession
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresAt = session.ExpiresAt,
            MinecraftUuid = session.MinecraftUuid,
            MinecraftUsername = session.MinecraftUsername,
            SkinUrl = session.SkinUrl,
            CapeUrl = session.CapeUrl,
            Xuid = session.Xuid,
        };

        var json = JsonSerializer.Serialize(dto);
        var encrypted = Dpapi.Protect(Encoding.UTF8.GetBytes(json));

        var directory = Path.GetDirectoryName(SessionPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(SessionPath, encrypted);
    }

    /// <summary>Lädt die gespeicherte Session oder <c>null</c>, wenn keine vorhanden/lesbar ist.</summary>
    public MicrosoftSession? Load()
    {
        try
        {
            if (!File.Exists(SessionPath))
                return null;

            var encrypted = File.ReadAllBytes(SessionPath);
            var json = Encoding.UTF8.GetString(Dpapi.Unprotect(encrypted));
            var dto = JsonSerializer.Deserialize<StoredSession>(json);

            if (dto is null || string.IsNullOrEmpty(dto.RefreshToken))
                return null;

            return new MicrosoftSession(
                dto.AccessToken,
                dto.RefreshToken,
                dto.ExpiresAt,
                dto.MinecraftUuid,
                dto.MinecraftUsername,
                dto.SkinUrl,
                dto.CapeUrl,
                dto.Xuid);
        }
        catch
        {
            // Beschädigte oder fremde Session ignorieren – kein Browser, kein Fehlerdialog.
            return null;
        }
    }

    /// <summary>Entfernt die gespeicherte Session vom Datenträger.</summary>
    public void Clear()
    {
        try
        {
            if (File.Exists(SessionPath))
                File.Delete(SessionPath);
        }
        catch
        {
            // Löschen ist best effort.
        }
    }
}

/// <summary>
/// Minimale DPAPI-Implementierung über CryptProtectData/CryptUnprotectData –
/// verschlüsselt Daten für den aktuellen Windows-Benutzer (kein Zusatzpaket nötig).
/// </summary>
internal static class Dpapi
{
    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int CbData;
        public IntPtr PbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        out IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Protect(byte[] data)
    {
        var input = new DataBlob { CbData = data.Length, PbData = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.PbData, data.Length);

            if (!CryptProtectData(ref input, "MeinLauncher Microsoft Session", IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out var output))
            {
                throw new InvalidOperationException(
                    $"Verschlüsselung der Anmeldung fehlgeschlagen (Win32-Fehler {Marshal.GetLastWin32Error()}).");
            }

            try
            {
                var result = new byte[output.CbData];
                Marshal.Copy(output.PbData, result, 0, output.CbData);
                return result;
            }
            finally
            {
                LocalFree(output.PbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.PbData);
        }
    }

    public static byte[] Unprotect(byte[] data)
    {
        var input = new DataBlob { CbData = data.Length, PbData = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.PbData, data.Length);

            if (!CryptUnprotectData(ref input, out _, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out var output))
            {
                throw new InvalidOperationException(
                    $"Entschlüsselung der Anmeldung fehlgeschlagen (Win32-Fehler {Marshal.GetLastWin32Error()}).");
            }

            try
            {
                var result = new byte[output.CbData];
                Marshal.Copy(output.PbData, result, 0, output.CbData);
                return result;
            }
            finally
            {
                LocalFree(output.PbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.PbData);
        }
    }
}
