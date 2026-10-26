namespace BugBoard.Api.Domain;

/// <summary>Database entity for a defect. Timestamps are always UTC.</summary>
public class Bug
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Severity Severity { get; set; }
    public BugStatus Status { get; set; }

    public int ReportedById { get; set; }
    public User? ReportedBy { get; set; }

    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
