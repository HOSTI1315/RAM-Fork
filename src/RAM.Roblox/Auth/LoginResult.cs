namespace RAM.Roblox.Auth;

public abstract record LoginResult
{
    public sealed record Success(string Cookie, ulong UserId, string Username, string DisplayName) : LoginResult;

    public sealed record TwoFactorRequired(
        ulong UserId,
        string ChallengeId,
        TwoFactorMediaType MediaType) : LoginResult;

    public sealed record Failed(string Reason, int? StatusCode = null) : LoginResult;
}

public enum TwoFactorMediaType
{
    Email,
    Authenticator,
    SecurityKey,
}
