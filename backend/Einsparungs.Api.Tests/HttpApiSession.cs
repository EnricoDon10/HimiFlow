using System.Net.Http.Json;
using Einsparungs.Api.DTOs;

namespace Einsparungs.Api.Tests;

internal sealed class HttpApiSession
{
    private const string CsrfCookieName = "XSRF-TOKEN=";

    public HttpApiSession(HttpClient client)
    {
        Client = client;
    }

    public HttpClient Client { get; }

    public async Task<string> RefreshCsrfAsync()
    {
        using var response = await Client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(CsrfCookieName, StringComparison.Ordinal));
        var encodedToken = setCookie[CsrfCookieName.Length..].Split(';', 2)[0];
        var token = Uri.UnescapeDataString(encodedToken);
        Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        return token;
    }

    public async Task<HttpResponseMessage> LoginAsync(string userName, string password)
    {
        await RefreshCsrfAsync();
        var response = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserName = userName, Password = password });
        if (response.IsSuccessStatusCode)
        {
            await RefreshCsrfAsync();
        }

        return response;
    }

    public Task<HttpResponseMessage> ChangePasswordAsync(string currentPassword, string newPassword) =>
        Client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmPassword = newPassword
            });
}
