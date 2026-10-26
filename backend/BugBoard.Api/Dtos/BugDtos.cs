using System.ComponentModel.DataAnnotations;
using BugBoard.Api.Domain;

namespace BugBoard.Api.Dtos;

/// <summary>Body of POST /api/bugs. New bugs always start as Open, so there is no Status here.</summary>
public class CreateBugRequest
{
    [Required, StringLength(120, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    // Nullable + [Required] so a missing value is rejected instead of silently becoming "Low".
    [Required]
    public Severity? Severity { get; set; }

    public int? AssignedToId { get; set; }
}

/// <summary>Body of PUT /api/bugs/{id}. A full replacement of the editable fields.</summary>
public class UpdateBugRequest
{
    [Required, StringLength(120, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Severity? Severity { get; set; }

    [Required]
    public BugStatus? Status { get; set; }

    public int? AssignedToId { get; set; }
}

/// <summary>Query string of GET /api/bugs.</summary>
public class BugListQuery
{
    public BugStatus? Status { get; set; }
    public Severity? Severity { get; set; }

    [StringLength(120)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}

/// <summary>What the API returns for a bug. Includes display names so the UI needs no extra calls.</summary>
public class BugDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Severity Severity { get; set; }
    public BugStatus Status { get; set; }
    public int ReportedById { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
