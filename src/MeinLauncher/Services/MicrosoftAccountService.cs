using System;
using System.Threading;
using System.Threading.Tasks;

namespace MeinLauncher.Services;

/// <summary>
/// Zentraler Zustand für das Microsoft-Konto: verwaltet die aktuelle Session,
/// die sichere Speicherung (DPAPI) und das Wiederherstellen per Refresh-Token –
/// ganz ohne den offiziellen Launcher.
///
/// Wichtig: Es existiert GENAU EINE Instanz (erzeugt in App.axaml.cs) und sie wird
/// von allen ViewModels geteilt. Der Zustand ist damit überall der gleiche.
/// </summary>
public sealed class MicrosoftAccountService
{
    private readonly MicrosoftAuthService _auth = new();
    private readonly MicrosoftSessionStore _store = new();

    /// <summary>Aktuell angemeldete Session, oder <c>null</c> wenn nicht angemeldet.</summary>
    public MicrosoftSession? CurrentSession { get; private set; }

    /// <summary>Wird gefeuert, wenn sich die Session geändert hat (Login/Restore/Löschen).</summary>
    public event Action? SessionChanged;

    /// <summary>Browser-Login inkl. kompletter Kette; speichert die Session danach sicher ab.</summary>
    public async Task<MicrosoftSession> LoginAsync(
        string clientId,
        IProgress<MicrosoftLoginStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AccountDiagnostics.Log("LoginAsync: Browser-Login gestartet.");

        var session = await _auth.LoginAsync(clientId, progress, cancellationToken);

        AccountDiagnostics.Log(
            $"LoginAsync: Session erhalten ({session.MinecraftUsername}, " +
            $"Xuid: {(string.IsNullOrEmpty(session.Xuid) ? "leer" : "vorhanden")}).");

        try
        {
            _store.Save(session);
            AccountDiagnostics.Log("LoginAsync: Session per DPAPI gespeichert.");
        }
        catch (Exception ex)
        {
            // Speichern ist Best-Effort – der Login bleibt für diese Sitzung gültig.
            AccountDiagnostics.Log($"LoginAsync: Speichern fehlgeschlagen (Login bleibt gültig): {ex.Message}");
        }

        SetCurrent(session);
        AccountDiagnostics.Log("LoginAsync: Session im AccountService gesetzt.");
        return session;
    }

    /// <summary>
    /// Stellt die Session wieder her – bevorzugt die bereits in diesem Prozess
    /// angemeldete Session (In-Memory), sonst die gespeicherte Session vom Datenträger.
    /// Wenn das Minecraft-Token abgelaufen ist oder die XUID fehlt (ältere Sessions),
    /// wird die Session über das Refresh-Token erneuert (kein Browser).
    ///
    /// Liefert <c>null</c>, wenn keine Session verfügbar ist. Wird das Refresh-Token
    /// von Microsoft abgelehnt (invalid_grant), wird die alte Session verworfen.
    /// Netzwerk-/Dienstfehler werden durchgereicht – die gespeicherte Session bleibt erhalten.
    /// </summary>
    public async Task<MicrosoftSession?> RestoreAsync(string clientId)
    {
        // 1) Frisch angemeldete/geladene Session IMMER zuerst verwenden. Dadurch sieht
        //    „Spielen“ einen soeben abgeschlossenen Login sofort – unabhängig davon, ob
        //    die Client-ID oder die Session auf dem Datenträger bereits persistiert wurde.
        if (CurrentSession is { } current &&
            !current.IsExpired &&
            !string.IsNullOrEmpty(current.AccessToken))
        {
            AccountDiagnostics.Log(
                $"RestoreAsync: In-Memory-Session verwendet ({current.MinecraftUsername}, " +
                $"Xuid: {(string.IsNullOrEmpty(current.Xuid) ? "leer" : "vorhanden")}).");
            return current;
        }

        AccountDiagnostics.Log($"RestoreAsync: ClientId vorhanden: {!string.IsNullOrWhiteSpace(clientId)}");

        // 2) Gespeicherte Session vom Datenträger – sie ist auch ohne Client-ID nutzbar,
        //    solange das Token noch gültig ist.
        var stored = _store.Load();
        AccountDiagnostics.Log($"RestoreAsync: Gespeicherte Session: {(stored is null ? "keine" : "vorhanden")}");
        if (stored is null)
            return null;

        AccountDiagnostics.Log(
            $"RestoreAsync: Gespeicherte Session abgelaufen: {stored.IsExpired}, " +
            $"Xuid: {(string.IsNullOrEmpty(stored.Xuid) ? "leer" : "vorhanden")}.");

        if (!stored.IsExpired && !string.IsNullOrEmpty(stored.AccessToken) && !string.IsNullOrEmpty(stored.Xuid))
        {
            SetCurrent(stored);
            AccountDiagnostics.Log("RestoreAsync: Gespeicherte Session ist gültig – direkt verwendet.");
            return stored;
        }

        // 3) Erneuerung per Refresh-Token ist nur mit hinterlegter Client-ID möglich.
        if (string.IsNullOrWhiteSpace(clientId))
        {
            AccountDiagnostics.Log("RestoreAsync: Erneuerung nötig, aber keine Client-ID vorhanden – Abbruch.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(stored.RefreshToken))
        {
            AccountDiagnostics.Log("RestoreAsync: Kein Refresh-Token vorhanden – Abbruch.");
            return null;
        }

        try
        {
            AccountDiagnostics.Log("RestoreAsync: Erneuere Session über Refresh-Token …");
            var refreshed = await _auth.RefreshAsync(clientId, stored.RefreshToken, CancellationToken.None);
            _store.Save(refreshed);
            SetCurrent(refreshed);
            AccountDiagnostics.Log(
                $"RestoreAsync: Erneuert und gespeichert ({refreshed.MinecraftUsername}).");
            return refreshed;
        }
        catch (Exception ex) when (IsInvalidGrant(ex))
        {
            // Refresh-Token ist dauerhaft ungültig – Session verwerfen, Browser-Login nötig.
            AccountDiagnostics.Log("RestoreAsync: Refresh-Token dauerhaft ungültig (invalid_grant) – Session verworfen.");
            Clear();
            return null;
        }
    }

    /// <summary>Meldet das Konto ab und löscht die gespeicherte Session.</summary>
    public void Clear()
    {
        AccountDiagnostics.Log("Clear: Konto abgemeldet, gespeicherte Session gelöscht.");
        _store.Clear();
        SetCurrent(null);
    }

    private void SetCurrent(MicrosoftSession? session)
    {
        CurrentSession = session;
        SessionChanged?.Invoke();
    }

    private static bool IsInvalidGrant(Exception ex) =>
        ex.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase);
}
