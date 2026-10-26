namespace BugBoard.Api.Domain;

/// <summary>The two roles in the system. Stored in the database and in the JWT "role" claim.</summary>
public enum Role
{
    Tester,
    Lead
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public enum BugStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}
