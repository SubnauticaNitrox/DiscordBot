using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace NitroxDiscordBot.Core;

/// <summary>
///     Provides the authentication state of the current user. Sourced from Discord OAuth2 API.
/// </summary>
public class DiscordAuthenticationStateProvider : AuthenticationStateProvider
{
    public ProtectedLocalStorage Store { get; }
    public static readonly AuthenticationState DefaultAuthState = new(new ClaimsPrincipal(new ClaimsIdentity((IIdentity)null)));
    public const string AuthenticationScheme = "Discord authentication type";

    public DiscordAuthenticationStateProvider(ProtectedLocalStorage store)
    {
        Store = store;
    }

    /// <summary>
    ///     Provides the current user's authentication state. Sourced from cookies or database.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ProtectedBrowserStorageResult<string> username;
        try
        {
            username = await Store.GetAsync<string>("username");
        }
        catch (InvalidOperationException)
        {
            return DefaultAuthState;
        }
        if (!username.Success || string.IsNullOrWhiteSpace(username.Value))
        {
            return DefaultAuthState;
        }

        return new AuthenticationState(CreatePrincipal(username.Value));
    }

    public static ClaimsPrincipal CreatePrincipal(string name, string role = "moderator")
    {
        return CreatePrincipal((ulong)Random.Shared.NextInt64(), name, role);
    }

    public static ClaimsPrincipal CreatePrincipal(ulong userId, string name, string role = "moderator")
    {
        return CreatePrincipal(userId.ToString(), name, role);
    }

    public static ClaimsPrincipal CreatePrincipal(string userId, string name, string role)
    {
        Claim[] claims =
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };

        ClaimsIdentity identity = new(claims, AuthenticationScheme);
        ClaimsPrincipal user = new(identity);
        return user;
    }
}
