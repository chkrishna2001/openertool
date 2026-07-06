using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Opener.Models;
using Spectre.Console;

namespace Opener.Services;

public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class DeviceCodeResponse
{
    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 5;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class OAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; } = string.Empty;
}

[JsonSerializable(typeof(OAuthTokenResponse))]
[JsonSerializable(typeof(DeviceCodeResponse))]
[JsonSerializable(typeof(OAuthErrorResponse))]
internal partial class OAuthJsonContext : JsonSerializerContext { }

public interface IGraphAuthService
{
    Task<string?> GetAccessTokenAsync();
    Task StartDeviceCodeAuthAsync(string? customClientId);
    Task<bool> ValidateClientCredentialsAsync(string tenantId, string clientId, string clientSecret);
}

public class GraphAuthService : IGraphAuthService
{
    // Microsoft's default public client ID (same as Azure CLI or similar tools can use, but we can default to a standard one or allow user to supply it)
    private const string DefaultPublicClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46"; // Microsoft Azure CLI client ID is widely used for device code auth
    private readonly IStorageService _storageService;

    public GraphAuthService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var keys = _storageService.GetKeys();
        
        // Check for Client Credentials configuration
        var tenantId = keys.Find(k => k.Key == "__provider_graph_tenant_id")?.Value;
        var clientId = keys.Find(k => k.Key == "__provider_graph_client_id")?.Value;
        var clientSecret = keys.Find(k => k.Key == "__provider_graph_client_secret")?.Value;

        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
        {
            return await GetClientCredentialsTokenAsync(tenantId, clientId, clientSecret);
        }

        // Check for Device Code Flow configuration (Refresh Token)
        var refreshToken = keys.Find(k => k.Key == "__provider_graph_refresh_token")?.Value;
        var deviceClientId = keys.Find(k => k.Key == "__provider_graph_device_client_id")?.Value;
        if (string.IsNullOrEmpty(deviceClientId))
        {
            deviceClientId = DefaultPublicClientId;
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            return await RefreshDeviceTokenAsync(deviceClientId, refreshToken);
        }

        return null;
    }

    private async Task<string?> GetClientCredentialsTokenAsync(string tenantId, string clientId, string clientSecret)
    {
        using var client = new HttpClient();
        var url = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        
        var requestData = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "scope", "https://graph.microsoft.com/.default" }
        };

        var response = await client.PostAsync(url, new FormUrlEncodedContent(requestData));
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var tokenData = JsonSerializer.Deserialize(content, OAuthJsonContext.Default.OAuthTokenResponse);
            return tokenData?.AccessToken;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to get token via Client Credentials flow. Status: {response.StatusCode}[/]");
            AnsiConsole.WriteLine(content);
            return null;
        }
    }

    private async Task<string?> RefreshDeviceTokenAsync(string clientId, string refreshToken)
    {
        using var client = new HttpClient();
        var url = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

        var requestData = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", clientId },
            { "refresh_token", refreshToken },
            { "scope", "offline_access Mail.Send Calendars.ReadWrite User.Read" }
        };

        var response = await client.PostAsync(url, new FormUrlEncodedContent(requestData));
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var tokenData = JsonSerializer.Deserialize(content, OAuthJsonContext.Default.OAuthTokenResponse);
            if (tokenData != null)
            {
                if (!string.IsNullOrEmpty(tokenData.RefreshToken))
                {
                    // Update stored refresh token if a new one is issued
                    SaveSystemKey("__provider_graph_refresh_token", tokenData.RefreshToken);
                }
                return tokenData.AccessToken;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Stored Microsoft Graph session has expired or is invalid. Please run 'o config auth-graph' to re-authenticate.[/]");
        }

        return null;
    }

    public async Task StartDeviceCodeAuthAsync(string? customClientId)
    {
        var clientId = !string.IsNullOrEmpty(customClientId) ? customClientId : DefaultPublicClientId;
        using var client = new HttpClient();
        var deviceCodeUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/devicecode";

        var requestData = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "scope", "offline_access Mail.Send Calendars.ReadWrite User.Read" }
        };

        AnsiConsole.MarkupLine("[yellow]Requesting device code from Microsoft...[/]");
        var response = await client.PostAsync(deviceCodeUrl, new FormUrlEncodedContent(requestData));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine($"[red]Failed to retrieve device code. Status: {response.StatusCode}[/]");
            AnsiConsole.WriteLine(content);
            return;
        }

        var deviceCodeResponse = JsonSerializer.Deserialize(content, OAuthJsonContext.Default.DeviceCodeResponse);
        if (deviceCodeResponse == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to parse device code response.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[bold green]Authentication Required[/]");
        AnsiConsole.MarkupLine($"1. Open your browser and navigate to: [link]{deviceCodeResponse.VerificationUri}[/]");
        AnsiConsole.MarkupLine($"2. Enter the following code: [bold yellow]{deviceCodeResponse.UserCode}[/]");
        AnsiConsole.MarkupLine("[dim]Waiting for authentication... Press Ctrl+C to cancel.[/]");

        var tokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        var tokenRequestData = new Dictionary<string, string>
        {
            { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" },
            { "client_id", clientId },
            { "device_code", deviceCodeResponse.DeviceCode }
        };

        int interval = deviceCodeResponse.Interval;
        if (interval < 5) interval = 5;

        var expiryTime = DateTime.UtcNow.AddSeconds(deviceCodeResponse.ExpiresIn);

        while (DateTime.UtcNow < expiryTime)
        {
            await Task.Delay(interval * 1000);

            var tokenResponse = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequestData));
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenData = JsonSerializer.Deserialize(tokenContent, OAuthJsonContext.Default.OAuthTokenResponse);
                if (tokenData != null)
                {
                    SaveSystemKey("__provider_graph_refresh_token", tokenData.RefreshToken ?? string.Empty);
                    SaveSystemKey("__provider_graph_device_client_id", clientId);
                    
                    // Clear client credential details if they exist to avoid confusion
                    DeleteSystemKey("__provider_graph_tenant_id");
                    DeleteSystemKey("__provider_graph_client_id");
                    DeleteSystemKey("__provider_graph_client_secret");

                    AnsiConsole.MarkupLine("[green]✔ Successfully authenticated with Microsoft Graph![/]");
                    return;
                }
            }

            var errorData = JsonSerializer.Deserialize(tokenContent, OAuthJsonContext.Default.OAuthErrorResponse);
            if (errorData != null)
            {
                if (errorData.Error == "authorization_pending")
                {
                    // Still waiting
                    continue;
                }
                else if (errorData.Error == "authorization_declined" || errorData.Error == "bad_verification_code")
                {
                    AnsiConsole.MarkupLine($"[red]Authorization failed: {errorData.ErrorDescription}[/]");
                    return;
                }
                else if (errorData.Error == "expired_token")
                {
                    AnsiConsole.MarkupLine("[red]Device code has expired. Please try again.[/]");
                    return;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error during polling: {errorData.ErrorDescription}[/]");
                    return;
                }
            }
        }

        AnsiConsole.MarkupLine("[red]Authentication timed out. Please try again.[/]");
    }

    public async Task<bool> ValidateClientCredentialsAsync(string tenantId, string clientId, string clientSecret)
    {
        var token = await GetClientCredentialsTokenAsync(tenantId, clientId, clientSecret);
        return !string.IsNullOrEmpty(token);
    }

    private void SaveSystemKey(string key, string value)
    {
        var keys = _storageService.GetKeys();
        var existing = keys.Find(k => k.Key == key);
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            keys.Add(new OKey
            {
                Key = key,
                Value = value,
                KeyType = OKeyType.Data,
                Description = "System Credential Key (Internal)"
            });
        }
        _storageService.SaveKeys(keys);
    }

    private void DeleteSystemKey(string key)
    {
        var keys = _storageService.GetKeys();
        var existing = keys.Find(k => k.Key == key);
        if (existing != null)
        {
            keys.Remove(existing);
            _storageService.SaveKeys(keys);
        }
    }
}
