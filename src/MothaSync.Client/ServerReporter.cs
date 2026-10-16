using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using MothaSync.Shared.Contracts;

namespace MothaSync.Client;

/// <summary>
/// Registers this device with the server (once) and reports sync runs to it.
/// Reporting is skipped when no server is configured. Configured reporting returns false on failure.
/// </summary>
public static class ServerReporter
{
    private const string ProtobufContentType = "application/x-protobuf";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly SemaphoreSlim RegistrationLock = new(1, 1);

    public static bool IsConfigured
    {
        get
        {
            var url = SettingsStore.Load().ServerUrl;
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }
    }

    public enum EnrollmentCheck
    {
        Valid,
        InvalidToken,
        Unreachable
    }

    /// <summary>Verifies the server URL and enrollment token without registering the device.</summary>
    public static async Task<EnrollmentCheck> TestEnrollmentAsync(string serverUrl, string enrollmentToken)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseUri))
        {
            return EnrollmentCheck.Unreachable;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/devices/verify-enrollment"))
            {
                Content = ProtobufContent(new RegisterDeviceRequest { EnrollmentToken = enrollmentToken })
            };
            using var response = await Http.SendAsync(request);
            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => EnrollmentCheck.Valid,
                HttpStatusCode.Unauthorized => EnrollmentCheck.InvalidToken,
                _ => EnrollmentCheck.Unreachable
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return EnrollmentCheck.Unreachable;
        }
    }

    public static async Task<bool> ReportRunAsync(SyncRun run)
    {
        try
        {
            var settings = SettingsStore.Load();
            if (!Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out var baseUri))
            {
                return false;
            }

            var token = await EnsureRegisteredAsync(baseUri);
            if (token is null)
            {
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/runs"))
            {
                Content = ProtobufContent(new ReportSyncRunRequest { Run = run })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await Http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Token no longer known to the server (e.g. device removed): register again next time.
                ClearRegistration();
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task<string?> EnsureRegisteredAsync(Uri baseUri)
    {
        var settings = SettingsStore.Load();
        if (!string.IsNullOrEmpty(settings.DeviceToken))
        {
            return settings.DeviceToken;
        }

        if (string.IsNullOrEmpty(settings.EnrollmentToken))
        {
            return null;
        }

        await RegistrationLock.WaitAsync();
        try
        {
            settings = SettingsStore.Load();
            if (!string.IsNullOrEmpty(settings.DeviceToken))
            {
                return settings.DeviceToken;
            }

            var body = new RegisterDeviceRequest
            {
                EnrollmentToken = settings.EnrollmentToken,
                DeviceName = Environment.MachineName,
                Platform = $"{Environment.OSVersion.Platform}-{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}".ToLowerInvariant(),
                ClientVersion = typeof(ServerReporter).Assembly.GetName().Version?.ToString() ?? string.Empty
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/devices/register"))
            {
                Content = ProtobufContent(body)
            };
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var registered = RegisterDeviceResponse.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync());
            settings = SettingsStore.Load();
            settings.DeviceId = registered.DeviceId;
            settings.DeviceToken = registered.DeviceToken;
            SettingsStore.Save(settings, notify: false);
            return registered.DeviceToken;
        }
        finally
        {
            RegistrationLock.Release();
        }
    }

    private static void ClearRegistration()
    {
        var settings = SettingsStore.Load();
        settings.DeviceId = string.Empty;
        settings.DeviceToken = string.Empty;
        SettingsStore.Save(settings, notify: false);
    }

    private static ByteArrayContent ProtobufContent(IMessage message)
    {
        var content = new ByteArrayContent(message.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);
        return content;
    }
}
