using System.Linq.Expressions;
using BugBoard.Api.Data;
using BugBoard.Api.Domain;
using BugBoard.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BugBoard.Api.Services;

public interface IBugService
{
    Task<PagedResult<BugDto>> ListAsync(BugListQuery query, CancellationToken ct);
    Task<ServiceResult<BugDto>> GetAsync(int id, CancellationToken ct);
    Task<ServiceResult<BugDto>> CreateAsync(CreateBugRequest request, CancellationToken ct);
    Task<ServiceResult<BugDto>> UpdateAsync(int id, UpdateBugRequest request, CancellationToken ct);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>
/// All business rules live here (NOT in the controller and NOT in React):
///  - only the reporter or a Lead may edit
///  - only a Lead may set status to Closed
///  - a Closed bug cannot be edited (409)
/// </summary>
public class BugService(BugBoardDbContext db, ICurrentUser currentUser, TimeProvider clock) : IBugService
{
    // Entity -> DTO conversion written as an expression so EF Core can translate it to SQL
    // and fetch ONLY these columns (including the joined display names).
    private static readonly Expression<Func<Bug, BugDto>> ToDto = b => new BugDto
    {
        Id = b.Id,
        Title = b.Title,
        Description = b.Description,
        Severity = b.Severity,
        Status = b.Status,
        ReportedById = b.ReportedById,
        ReportedByName = b.ReportedBy!.DisplayName,
        AssignedToId = b.AssignedToId,
        AssignedToName = b.AssignedTo != null ? b.AssignedTo.DisplayName : null,
        CreatedAt = b.CreatedAt,
        UpdatedAt = b.UpdatedAt
    };

    public async Task<PagedResult<BugDto>> ListAsync(BugListQuery query, CancellationToken ct)
    {
        IQueryable<Bug> bugs = db.Bugs.AsNoTracking();

        if (query.Status is { } status)
        {
            bugs = bugs.Where(b => b.Status == status);
        }
        if (query.Severity is { } severity)
        {
            bugs = bugs.Where(b => b.Severity == severity);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            bugs = bugs.Where(b => EF.Functions.Like(b.Title, pattern, "\\"));
        }

        var total = await bugs.CountAsync(ct); // count BEFORE paging

        var items = await bugs
            .OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id) // stable order for paging
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(ct);

        return new PagedResult<BugDto>(items, total, query.Page, query.PageSize);
    }

    public async Task<ServiceResult<BugDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await db.Bugs.AsNoTracking().Where(b => b.Id == id).Select(ToDto).FirstOrDefaultAsync(ct);
        return dto is null ? ServiceResult<BugDto>.NotFound($"Bug {id} was not found.") : ServiceResult<BugDto>.Ok(dto);
    }

    public async Task<ServiceResult<BugDto>> CreateAsync(CreateBugRequest request, CancellationToken ct)
    {
        var title = request.Title.Trim();
        if (title.Length < 5)
        {
            return ServiceResult<BugDto>.Invalid("title", "Title must be at least 5 characters (ignoring spaces at the ends).");
        }

        if (request.AssignedToId is { } assigneeId && !await db.Users.AnyAsync(u => u.Id == assigneeId, ct))
        {
            return ServiceResult<BugDto>.Invalid("assignedToId", "The selected assignee does not exist.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var bug = new Bug
        {
            Title = title,
            Description = request.Description?.Trim(),
            Severity = request.Severity!.Value,
            Status = BugStatus.Open, // new bugs always start Open
            ReportedById = currentUser.Id, // taken from the token, so you cannot report "as someone else"
            AssignedToId = request.AssignedToId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Bugs.Add(bug);
        await db.SaveChangesAsync(ct);

        return await GetAsync(bug.Id, ct);
    }

    public async Task<ServiceResult<BugDto>> UpdateAsync(int id, UpdateBugRequest request, CancellationToken ct)
    {
        var bug = await db.Bugs.FirstOrDefaultAsync(b => b.Id == id, ct); // tracked: we will modify it
        if (bug is null)
        {
            return ServiceResult<BugDto>.NotFound($"Bug {id} was not found.");
        }

        // Order of checks matters: who you are -> state of the bug -> what you ask for -> data validity.
        if (!currentUser.IsLead && bug.ReportedById != currentUser.Id)
        {
            return ServiceResult<BugDto>.Forbidden("Only the reporter or a Lead can edit this bug.");
        }
        if (bug.Status == BugStatus.Closed)
        {
            return ServiceResult<BugDto>.Conflict("This bug is Closed and can no longer be edited.");
        }
        if (request.Status == BugStatus.Closed && !currentUser.IsLead)
        {
            return ServiceResult<BugDto>.Forbidden("Only a Lead can set the status to Closed.");
        }

        var title = request.Title.Trim();
        if (title.Length < 5)
        {
            return ServiceResult<BugDto>.Invalid("title", "Title must be at least 5 characters (ignoring spaces at the ends).");
        }
        if (request.AssignedToId is { } assigneeId && !await db.Users.AnyAsync(u => u.Id == assigneeId, ct))
        {
            return ServiceResult<BugDto>.Invalid("assignedToId", "The selected assignee does not exist.");
        }

        bug.Title = title;
        bug.Description = request.Description?.Trim();
        bug.Severity = request.Severity!.Value;
        bug.Status = request.Status!.Value;
        bug.AssignedToId = request.AssignedToId;
        bug.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct)
    {
        // The "Lead only" rule is enforced by [Authorize(Policy = "LeadOnly")] on the controller action,
        // so by the time we are here the caller is already known to be a Lead.
        var bug = await db.Bugs.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bug is null)
        {
            return ServiceResult.NotFound($"Bug {id} was not found.");
        }

        db.Bugs.Remove(bug);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // In SQL LIKE, % and _ are wildcards. Escape them so a search for "50%" means literally "50%".
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
