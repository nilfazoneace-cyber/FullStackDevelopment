namespace BugBoard.Api.Infrastructure;

public static class Policies
{
    /// <summary>Named authorization policy: the caller's "role" claim must be "Lead".</summary>
    public const string LeadOnly = "LeadOnly";
}
