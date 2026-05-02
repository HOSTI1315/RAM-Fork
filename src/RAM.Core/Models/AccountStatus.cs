namespace RAM.Core.Models;

public enum AccountStatus
{
    Unknown = 0,
    NotInGame = 1,
    InGame = 2,
    Restarting = 3,
    /// <summary>Auth failure: cookie expired, 2FA required again, or revoked.</summary>
    Error = 4,
}
