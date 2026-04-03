namespace Purfle.Runtime.Auth;

/// <summary>
/// Status of an auth profile. State machine:
///
///   Unknown ──verify──► Active
///      │                  │
///      │               expired/
///      │               revoked
///      │                  │
///      ▼                  ▼
///   Invalid ◄────────── Expired
///      │                  │
///      │               refresh
///      │                  │
///      └──────────────────┘
///              │
///              ▼
///           Cooldown ──timeout──► Active
/// </summary>
public enum ProfileStatus
{
    /// <summary>Not yet verified.</summary>
    Unknown,

    /// <summary>Credential is valid and ready for use.</summary>
    Active,

    /// <summary>OAuth token expired; needs refresh.</summary>
    Expired,

    /// <summary>Credential was revoked or is invalid.</summary>
    Invalid,

    /// <summary>Rate limited or quota exhausted; retry after cooldown.</summary>
    Cooldown
}
