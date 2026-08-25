using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MeinLauncher.Services;

/// <summary>Phasen des Microsoft-Logins – für verständliche Statusmeldungen in der UI.</summary>
public enum MicrosoftLoginStage
{
    /// <summary>Der Browser wird gerade geöffnet.</summary>
    OpeningBrowser,

    /// <summary>Der Browser ist offen, wir warten auf den Redirect mit dem Authorization-Code.</summary>
    WaitingForBrowser,

    /// <summary>Der Code ist da – MSA-Token wird getauscht und XBL/XSTS/Minecraft werden geprüft.</summary>
    CheckingMinecraft,
}

/// <summary>
/// Ergebnis einer erfolgreichen Microsoft-Anmeldung für Minecraft: Java Edition.
/// Enthält das Minecraft-Spieler-Token sowie die Profildaten des Kontos.
/// </summary>
public sealed class MicrosoftSession
{
    public MicrosoftSession(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        string minecraftUuid,
        string minecraftUsername,
        string? skinUrl = null,
        string? capeUrl = null,
        string? xuid = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
        MinecraftUuid = minecraftUuid;
        MinecraftUsername = minecraftUsername;
        SkinUrl = skinUrl;
        CapeUrl = capeUrl;
        Xuid = xuid;
    }

    /// <summary>Minecraft-Spieler-Token (Bearer) für api.minecraftservices.com.</summary>
    public string AccessToken { get; }

    /// <summary>MSA-Refresh-Token für die spätere Erneuerung ohne Browser.</summary>
    public string RefreshToken { get; }

    /// <summary>Ablaufzeitpunkt des Spieler-Tokens (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>UUID des Minecraft-Kontos (ohne Bindestriche).</summary>
    public string MinecraftUuid { get; }

    /// <summary>Anzeigename des Minecraft-Kontos.</summary>
    public string MinecraftUsername { get; }

    public string? SkinUrl { get; }

    public string? CapeUrl { get; }

    /// <summary>Xbox User ID (XUID) für das Spieler-Token beim Start (--xuid).</summary>
    public string? Xuid { get; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}

/// <summary>
/// Microsoft-Anmeldung für Minecraft: Java Edition – ohne den offiziellen Launcher.
///
/// Ablauf:
///   1. Authorization-Code + PKCE (S256) mit Redirect über http://localhost (Loopback).
///      Die Anmeldung läuft im Browser – es werden keine Passwörter gelesen.
///   2. Token-Austausch am Microsoft Identity Endpoint (consumers).
///   3. Kette XBL → XSTS → Minecraft-Spieler-Token → Profil (api.minecraftservices.com).
///
/// Die Microsoft-Client-ID ist fest im Quellcode hinterlegt und wird nicht in
/// Einstellungen gespeichert. Damit der Login funktioniert, muss die Azure-
/// App-Registrierung für „Accounts in any organizational directory and personal
/// Microsoft accounts" (Unternehmens- + Privat-Konten) konfiguriert sein.
/// </summary>
public sealed class MicrosoftAuthService
{
    /// <summary>
    /// Hardcoded Azure Application (client) ID. This is the app registration that
    /// authenticates against Microsoft's consumers endpoint. The Azure app MUST be
    /// configured to support "Accounts in any organizational directory and personal
    /// Microsoft accounts" for the OAuth flow to work.
    /// </summary>
    internal const string MicrosoftClientId = "5b5d32fa-8a71-4a43-8384-7cdc76ad459d";

    private static readonly HttpClient Http = CreateHttpClient();

    private const string AuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XblUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string MinecraftLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string MinecraftProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

    private const string Scope = "XboxLive.signin offline_access";

    /// <summary>Wartezeit auf den Browser-Redirect, bevor der Login abgebrochen wird.</summary>
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(3);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KulkaClient/0.1.0");
        return client;
    }

    /// <summary>
    /// Startet die Anmeldung im Browser und führt die komplette Kette bis zum
    /// Minecraft-Spieler-Token aus. Wirft bei Abbruch oder Fehler eine Exception.
    /// </summary>
    /// <param name="progress">Optionaler Fortschritt (Browser öffnen → warten → Kette prüfen).</param>
    public async Task<MicrosoftSession> LoginAsync(
        IProgress<MicrosoftLoginStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(LoginTimeout);

        // Loopback-Listener mit freiem Port – umgeht URL-ACL, kein Admin nötig.
        // "localhost" kann je nach Browser zuerst als ::1 (IPv6) aufgelöst werden –
        // Dual-Mode (IPv6 mit IPv4-Mapping) deckt beide Fälle ab, mit IPv4-Fallback.
        using var listener = CreateLoopbackListener();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUri = $"http://localhost:{port}/";

        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");

        var authUrl = AuthorizeUrl +
            "?client_id=" + Uri.EscapeDataString(MicrosoftClientId) +
            "&response_type=code" +
            "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
            "&scope=" + Uri.EscapeDataString(Scope) +
            "&code_challenge=" + challenge +
            "&code_challenge_method=S256" +
            "&response_mode=query" +
            "&prompt=select_account" +
            "&state=" + state;

        progress?.Report(MicrosoftLoginStage.OpeningBrowser);
        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
        progress?.Report(MicrosoftLoginStage.WaitingForBrowser);
        AccountDiagnostics.Log("LoginAsync: Browser geöffnet, warte auf Redirect …");

        var (code, error, returnedState) = await WaitForAuthorizationCodeAsync(listener, cts.Token);

        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(
                $"Microsoft-Anmeldung abgebrochen: {DescribeOAuthError(error)}");

        if (string.IsNullOrEmpty(code))
            throw new OperationCanceledException("Zeitüberschreitung bei der Microsoft-Anmeldung.");

        if (!string.Equals(returnedState, state, StringComparison.Ordinal))
            throw new InvalidOperationException("Ungültiger Zustand bei der Microsoft-Anmeldung.");

        AccountDiagnostics.Log("LoginAsync: Authorization-Code empfangen – Austausch gegen MSA-Token.");
        progress?.Report(MicrosoftLoginStage.CheckingMinecraft);

        var (msaAccessToken, refreshToken, expiresAt) =
            await ExchangeCodeAsync(MicrosoftClientId, code, verifier, redirectUri, cts.Token);

        AccountDiagnostics.Log("LoginAsync: MSA-Token erhalten – XBL/XSTS/Minecraft-Kette.");
        return await BuildMinecraftSessionAsync(msaAccessToken, refreshToken, expiresAt, cts.Token);
    }

    /// <summary>
    /// Erneuert eine abgelaufene Session über das gespeicherte Refresh-Token –
    /// ohne den Browser zu öffnen. Wirft eine <see cref="InvalidOperationException"/>
    /// mit "invalid_grant", wenn das Refresh-Token nicht mehr gültig ist.
    /// </summary>
    public async Task<MicrosoftSession> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Kein Refresh-Token vorhanden.");

        AccountDiagnostics.Log("RefreshAsync: Erneuere Token über Refresh-Token …");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = MicrosoftClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = Scope,
        });

        using var response = await Http.PostAsync(TokenUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(body);
        if (!response.IsSuccessStatusCode)
        {
            var error = GetString(doc.RootElement, "error") ?? "unbekannt";
            var description = GetString(doc.RootElement, "error_description") ?? "";
            throw new InvalidOperationException($"Token-Erneuerung fehlgeschlagen ({error}): {description}");
        }

        var accessToken = GetString(doc.RootElement, "access_token")
            ?? throw new InvalidOperationException("Kein access_token in der Antwort.");

        // Microsoft kann Refresh-Tokens rotieren – den neuesten Wert übernehmen.
        var newRefreshToken = GetString(doc.RootElement, "refresh_token") ?? refreshToken;
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresElement) &&
                        expiresElement.ValueKind == JsonValueKind.Number
            ? expiresElement.GetInt32()
            : 3600;

        return await BuildMinecraftSessionAsync(
            accessToken, newRefreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn), cancellationToken);
    }

    // ---------------------------------------------------------------- Loopback-Redirect

    private static TcpListener CreateLoopbackListener()
    {
        try
        {
            var dualListener = new TcpListener(IPAddress.IPv6Any, 0);
            dualListener.Server.DualMode = true;
            dualListener.Start();
            return dualListener;
        }
        catch (SocketException)
        {
            var ipv4Listener = new TcpListener(IPAddress.Loopback, 0);
            ipv4Listener.Start();
            return ipv4Listener;
        }
    }

    private static async Task<(string? Code, string? Error, string? State)> WaitForAuthorizationCodeAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                continue;
            }

            using (client)
            using (var stream = client.GetStream())
            {
                string requestText;
                try
                {
                    requestText = await ReadRequestAsync(stream, cancellationToken);
                }
                catch
                {
                    continue;
                }

                var pathAndQuery = GetPathAndQuery(requestText);
                var queryIndex = pathAndQuery.IndexOf('?');
                if (queryIndex >= 0)
                {
                    var query = ParseQuery(pathAndQuery[(queryIndex + 1)..]);
                    query.TryGetValue("error", out var error);
                    query.TryGetValue("code", out var code);
                    query.TryGetValue("state", out var state);

                    var success = string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code);
                    await SendResponseAsync(stream, success, cancellationToken);
                    return (code, error, state);
                }

                // Favicon & Co. ignorieren und auf den echten Redirect warten.
                await SendResponseAsync(stream, false, cancellationToken);
            }
        }

        return (null, null, null);
    }

    private static async Task<string> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var sb = new StringBuilder();
        while (sb.Length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
                break;

            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (sb.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                break;
        }

        return sb.ToString();
    }

    private static string GetPathAndQuery(string requestText)
    {
        var firstLineEnd = requestText.IndexOf("\r\n", StringComparison.Ordinal);
        var firstLine = firstLineEnd < 0 ? requestText : requestText[..firstLineEnd];
        var parts = firstLine.Split(' ');
        return parts.Length >= 2 ? parts[1] : "/";
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[pair] = "";
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..eq]);
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static async Task SendResponseAsync(Stream stream, bool success, CancellationToken cancellationToken)
    {
        var body = success
            ? "<!DOCTYPE html><html><body style=\"font-family:sans-serif;text-align:center;margin-top:80px\">" +
              "<h2>Anmeldung erfolgreich!</h2><p>Du kannst dieses Fenster schlie&szlig;en.</p></body></html>"
            : "<!DOCTYPE html><html><body style=\"font-family:sans-serif;text-align:center;margin-top:80px\">" +
              "<p>Du kannst dieses Fenster schlie&szlig;en.</p></body></html>";

        var header = "HTTP/1.1 200 OK\r\n" +
                     "Content-Type: text/html; charset=utf-8\r\n" +
                     "Content-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n" +
                     "Connection: close\r\n\r\n";

        var bytes = Encoding.UTF8.GetBytes(header + body);
        await stream.WriteAsync(bytes.AsMemory(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- Token-Austausch (MSA)

    private static async Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)>
        ExchangeCodeAsync(string clientId, string code, string verifier, string redirectUri, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier,
            ["scope"] = Scope,
        });

        using var response = await Http.PostAsync(TokenUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(body);
        if (!response.IsSuccessStatusCode)
        {
            var error = GetString(doc.RootElement, "error") ?? "unbekannt";
            var description = GetString(doc.RootElement, "error_description") ?? "";
            throw new InvalidOperationException(
                $"Token-Austausch fehlgeschlagen ({error}): {description}");
        }

        var accessToken = GetString(doc.RootElement, "access_token")
            ?? throw new InvalidOperationException("Kein access_token in der Antwort.");
        var refreshToken = GetString(doc.RootElement, "refresh_token") ?? "";
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresElement) &&
                        expiresElement.ValueKind == JsonValueKind.Number
            ? expiresElement.GetInt32()
            : 3600;

        return (accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    // ---------------------------------------------------------------- XBL → XSTS → Minecraft

    private static async Task<MicrosoftSession> BuildMinecraftSessionAsync(
        string msaAccessToken,
        string refreshToken,
        DateTimeOffset msaExpiresAt,
        CancellationToken cancellationToken)
    {
        // 1) Xbox Live
        using var xblResponse = await PostJsonAsync(XblUrl, new
        {
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = "d=" + msaAccessToken,
            },
        }, cancellationToken);

        var xblBody = await xblResponse.Content.ReadAsStringAsync(cancellationToken);
        using var xblDoc = JsonDocument.Parse(xblBody);
        var xblToken = GetString(xblDoc.RootElement, "Token")
            ?? throw new InvalidOperationException("Kein Xbox-Live-Token erhalten.");
        var uhs = GetXuiClaim(xblDoc.RootElement, "uhs")
            ?? throw new InvalidOperationException("Keine Xbox UserHash erhalten.");

        // 2) XSTS (spielt gegen api.minecraftservices.com)
        var xstsRequestBody = JsonSerializer.Serialize(new
        {
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT",
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xblToken },
            },
        });

        using var xstsRequest = new HttpRequestMessage(HttpMethod.Post, XstsUrl)
        {
            Content = new StringContent(xstsRequestBody, Encoding.UTF8, "application/json"),
        };
        using var xstsResponse = await Http.SendAsync(xstsRequest, cancellationToken);
        var xstsBody = await xstsResponse.Content.ReadAsStringAsync(cancellationToken);

        using var xstsDoc = JsonDocument.Parse(xstsBody);
        if (!xstsResponse.IsSuccessStatusCode)
        {
            var xErr = GetString(xstsDoc.RootElement, "XErr");
            throw new InvalidOperationException(
                xErr is null
                    ? $"XSTS-Anmeldung fehlgeschlagen ({(int)xstsResponse.StatusCode})."
                    : $"XSTS-Anmeldung fehlgeschlagen: {DescribeXErr(xErr)}");
        }

        var xstsToken = GetString(xstsDoc.RootElement, "Token")
            ?? throw new InvalidOperationException("Kein XSTS-Token erhalten.");
        var xuid = GetXuiClaim(xstsDoc.RootElement, "xid");

        // 3) Minecraft-Spieler-Token
        var mcLoginRequestBody = JsonSerializer.Serialize(new
        {
            identityToken = $"XBL3.0 x={uhs};{xstsToken}",
        });

        using var mcLoginRequest = new HttpRequestMessage(HttpMethod.Post, MinecraftLoginUrl)
        {
            Content = new StringContent(mcLoginRequestBody, Encoding.UTF8, "application/json"),
        };
        using var mcLoginResponse = await Http.SendAsync(mcLoginRequest, cancellationToken);
        var mcLoginBody = await mcLoginResponse.Content.ReadAsStringAsync(cancellationToken);

        using var mcLoginDoc = JsonDocument.Parse(mcLoginBody);
        if (!mcLoginResponse.IsSuccessStatusCode)
        {
            var error = GetString(mcLoginDoc.RootElement, "error") ?? "";
            var errorMessage = GetString(mcLoginDoc.RootElement, "errorMessage") ?? "";
            throw new InvalidOperationException(
                $"Minecraft-Login fehlgeschlagen ({(int)mcLoginResponse.StatusCode}): {error} {errorMessage}");
        }

        var minecraftAccessToken = GetString(mcLoginDoc.RootElement, "access_token")
            ?? throw new InvalidOperationException("Kein Minecraft-Zugriffstoken erhalten.");

        // 4) Profil (Name, UUID, Skin)
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, MinecraftProfileUrl);
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", minecraftAccessToken);
        using var profileResponse = await Http.SendAsync(profileRequest, cancellationToken);
        var profileBody = await profileResponse.Content.ReadAsStringAsync(cancellationToken);

        using var profileDoc = JsonDocument.Parse(profileBody);
        if (!profileResponse.IsSuccessStatusCode)
        {
            if (profileResponse.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException(
                    "Für dieses Microsoft-Konto ist kein Minecraft-Profil vorhanden.");

            throw new InvalidOperationException(
                $"Minecraft-Profil konnte nicht geladen werden ({(int)profileResponse.StatusCode}).");
        }

        var uuid = GetString(profileDoc.RootElement, "id") ?? "";
        var username = GetString(profileDoc.RootElement, "name") ?? "";

        string? skinUrl = null;
        if (profileDoc.RootElement.TryGetProperty("skins", out var skins) &&
            skins.ValueKind == JsonValueKind.Array && skins.GetArrayLength() > 0)
        {
            skinUrl = GetString(skins[0], "url");
        }

        string? capeUrl = null;
        if (profileDoc.RootElement.TryGetProperty("capes", out var capes) &&
            capes.ValueKind == JsonValueKind.Array && capes.GetArrayLength() > 0)
        {
            capeUrl = GetString(capes[0], "url");
        }

        return new MicrosoftSession(
            minecraftAccessToken,
            refreshToken,
            msaExpiresAt,
            uuid,
            username,
            skinUrl,
            capeUrl,
            xuid);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(string url, object body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Http.PostAsync(url, content, cancellationToken);
    }

    private static string? GetXuiClaim(JsonElement root, string claim)
    {
        if (!root.TryGetProperty("DisplayClaims", out var claims) ||
            !claims.TryGetProperty("xui", out var xui) ||
            xui.ValueKind != JsonValueKind.Array ||
            xui.GetArrayLength() == 0)
        {
            return null;
        }

        return GetString(xui[0], claim);
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            _ => value.ToString(),
        };
    }

    // ---------------------------------------------------------------- PKCE

    private static string CreateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ---------------------------------------------------------------- Fehlertexte

    private static string DescribeOAuthError(string error) => error switch
    {
        "access_denied" => "Anmeldung im Browser abgebrochen.",
        "invalid_scope" => "Ungültiger Umfang.",
        "invalid_client" => "Die Microsoft-Anmeldung ist vorübergehend nicht verfügbar – bitte später erneut versuchen.",
        _ => error,
    };

    private static string DescribeXErr(string xErr) => xErr switch
    {
        "2148916227" => "Das Konto ist noch nicht erwachsen verifiziert (0x8015DC0D).",
        "2148916233" => "Dieses Microsoft-Konto besitzt kein Minecraft (0x8015DC12).",
        "2148916235" => "Der Zugriff ist gesperrt – bitte im Browser anmelden und erneut versuchen (0x8015DC03).",
        "2148916236" => "Das Microsoft-Konto ist nicht überprüft (0x8015DC08).",
        "2148916238" => "Es gab zu viele Anmeldeversuche – bitte später erneut versuchen (0x8015DC10).",
        _ => xErr,
    };
}
