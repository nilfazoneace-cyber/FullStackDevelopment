using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BugBoard.Api.Domain;
using BugBoard.Api.Dtos;
using BugBoard.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BugBoard.Tests;

public class BugTests(BugBoardFactory factory) : IClassFixture<BugBoardFactory>
{
    private static async Task<JsonElement> Problem(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    // ------------------------------------------------------------------ create / read

    [Fact]
    public async Task Create_returns_201_with_location_header_and_uses_the_token_user_as_reporter()
    {
        var tester = await factory.Tester1();

        var response = await tester.PostAsJsonAsync("/api/bugs", new { title = "Login fails on Safari", severity = "High", description = "details" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var bug = await response.ReadAsync<BugDto>();
        Assert.Equal(BugStatus.Open, bug.Status);
        Assert.Equal("Tina Tester", bug.ReportedByName);
        Assert.Equal($"/api/bugs/{bug.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(DateTimeKind.Utc, bug.CreatedAt.Kind); // serialized with "Z"
    }

    [Fact]
    public async Task Create_ignores_a_reporter_id_sent_by_the_client()
    {
        var tester = await factory.Tester1();

        var response = await tester.PostAsJsonAsync("/api/bugs", new { title = "Spoofed reporter", severity = "Low", reportedById = 3 });
        var bug = await response.ReadAsync<BugDto>();

        Assert.Equal("Tina Tester", bug.ReportedByName); // not the Lead (id 3)
    }

    [Fact]
    public async Task Create_with_invalid_data_returns_400_with_a_field_errors_map()
    {
        var tester = await factory.Tester1();

        var response = await tester.PostAsJsonAsync("/api/bugs", new { title = "abc", description = new string('x', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var errors = (await Problem(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("description", out _));
        Assert.True(errors.TryGetProperty("severity", out _)); // missing -> required
    }

    [Fact]
    public async Task Create_with_a_title_of_only_spaces_returns_400()
    {
        var tester = await factory.Tester1();

        var response = await tester.PostAsJsonAsync("/api/bugs", new { title = "          ", severity = "Low" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Problem(response)).GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Create_with_an_unknown_severity_returns_400()
    {
        var tester = await factory.Tester1();

        var response = await tester.PostAsJsonAsync("/api/bugs", new { title = "Valid title", severity = "Apocalyptic" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_nonexistent_assignee_returns_400_on_assignedToId()
    {
        var tester = await factory.Tester1();

        var response = await tester.PostAsJsonAsync("/api/bugs", new { title = "Valid title", severity = "Low", assignedToId = 9999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Problem(response)).GetProperty("errors").TryGetProperty("assignedToId", out _));
    }

    [Fact]
    public async Task Get_unknown_bug_returns_404_problem_details()
    {
        var tester = await factory.Tester1();

        var response = await tester.GetAsync("/api/bugs/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, (await Problem(response)).GetProperty("status").GetInt32());
    }

    // ------------------------------------------------------------------ update rules

    [Fact]
    public async Task Reporter_can_update_their_own_bug()
    {
        var tester = await factory.Tester1();
        var bug = await tester.CreateBugAsync();

        var response = await tester.UpdateBugAsync(bug, status: "InProgress", title: "Renamed by reporter");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<BugDto>();
        Assert.Equal("Renamed by reporter", updated.Title);
        Assert.Equal(BugStatus.InProgress, updated.Status);
        Assert.True(updated.UpdatedAt >= bug.UpdatedAt);
    }

    [Fact]
    public async Task A_different_tester_gets_403_when_editing_someone_elses_bug()
    {
        var owner = await factory.Tester1();
        var other = await factory.Tester2();
        var bug = await owner.CreateBugAsync();

        var response = await other.UpdateBugAsync(bug, title: "Hijacked title");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var unchanged = await (await owner.GetAsync($"/api/bugs/{bug.Id}")).ReadAsync<BugDto>();
        Assert.Equal(bug.Title, unchanged.Title);
    }

    [Fact]
    public async Task A_lead_can_edit_a_bug_reported_by_someone_else()
    {
        var owner = await factory.Tester1();
        var lead = await factory.Lead();
        var bug = await owner.CreateBugAsync();

        var response = await lead.UpdateBugAsync(bug, status: "Resolved");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_tester_cannot_set_status_to_Closed_even_on_their_own_bug()
    {
        var tester = await factory.Tester1();
        var bug = await tester.CreateBugAsync();

        var response = await tester.UpdateBugAsync(bug, status: "Closed");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_closed_bug_cannot_be_edited_by_anyone_and_returns_409()
    {
        var tester = await factory.Tester1();
        var lead = await factory.Lead();
        var bug = await tester.CreateBugAsync();
        Assert.Equal(HttpStatusCode.OK, (await lead.UpdateBugAsync(bug, status: "Closed")).StatusCode);

        var byReporter = await tester.UpdateBugAsync(bug, title: "Trying to edit a closed bug");
        var byLead = await lead.UpdateBugAsync(bug, status: "Open");

        Assert.Equal(HttpStatusCode.Conflict, byReporter.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, byLead.StatusCode);
    }

    [Fact]
    public async Task Update_with_invalid_data_returns_400()
    {
        var tester = await factory.Tester1();
        var bug = await tester.CreateBugAsync();

        var response = await tester.UpdateBugAsync(bug, title: "no");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Problem(response)).GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Update_of_a_missing_bug_returns_404()
    {
        var lead = await factory.Lead();
        var fake = new BugDto { Id = 987654, Title = "Does not exist", Severity = Severity.Low, Status = BugStatus.Open };

        var response = await lead.UpdateBugAsync(fake);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ delete rules

    [Fact]
    public async Task A_tester_gets_403_on_delete_and_the_bug_survives()
    {
        var tester = await factory.Tester1();
        var bug = await tester.CreateBugAsync();

        var response = await tester.DeleteAsync($"/api/bugs/{bug.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tester.GetAsync($"/api/bugs/{bug.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_lead_can_delete_a_bug_and_it_is_then_gone()
    {
        var tester = await factory.Tester1();
        var lead = await factory.Lead();
        var bug = await tester.CreateBugAsync();

        var response = await lead.DeleteAsync($"/api/bugs/{bug.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lead.GetAsync($"/api/bugs/{bug.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lead.DeleteAsync($"/api/bugs/{bug.Id}")).StatusCode);
    }

    // ------------------------------------------------------------------ list: filter, search, paging

    [Fact]
    public async Task List_supports_search_filters_and_paging_with_a_total_count()
    {
        var tester = await factory.Tester1();
        var lead = await factory.Lead();
        var tag = Guid.NewGuid().ToString("N")[..8]; // unique per test run so other tests cannot interfere
        for (var i = 1; i <= 5; i++)
        {
            await tester.CreateBugAsync($"Pager {tag} bug {i}", severity: i <= 2 ? "Critical" : "Low");
        }
        var toClose = await tester.CreateBugAsync($"Pager {tag} to be closed", severity: "Critical");
        await lead.UpdateBugAsync(toClose, status: "Closed");

        // paging: 6 matches, 4 per page -> page 2 holds the remaining 2
        var page2 = await (await tester.GetAsync($"/api/bugs?search={tag}&pageSize=4&page=2")).ReadAsync<PagedResult<BugDto>>();
        Assert.Equal(6, page2.Total);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(2, page2.Page);

        // search is case-insensitive
        var upper = await (await tester.GetAsync($"/api/bugs?search={tag.ToUpperInvariant()}")).ReadAsync<PagedResult<BugDto>>();
        Assert.Equal(6, upper.Total);

        // filter by severity
        var critical = await (await tester.GetAsync($"/api/bugs?search={tag}&severity=Critical")).ReadAsync<PagedResult<BugDto>>();
        Assert.Equal(3, critical.Total);
        Assert.All(critical.Items, b => Assert.Equal(Severity.Critical, b.Severity));

        // filter by status
        var closed = await (await tester.GetAsync($"/api/bugs?search={tag}&status=Closed")).ReadAsync<PagedResult<BugDto>>();
        Assert.Equal(1, closed.Total);

        // an empty parameter (what a UI sends for "All") means "no filter", not an error
        var all = await (await tester.GetAsync($"/api/bugs?search={tag}&status=&severity=")).ReadAsync<PagedResult<BugDto>>();
        Assert.Equal(6, all.Total);
    }

    [Fact]
    public async Task Search_treats_percent_literally_instead_of_as_a_wildcard()
    {
        var tester = await factory.Tester1();
        await tester.CreateBugAsync("Discount of 50% not applied");
        await tester.CreateBugAsync("Discount of 500 not applied");

        var result = await (await tester.GetAsync("/api/bugs?search=50%25")).ReadAsync<PagedResult<BugDto>>();

        Assert.Contains(result.Items, b => b.Title.Contains("50%"));
        Assert.DoesNotContain(result.Items, b => b.Title.Contains("500"));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("status=Nonsense")]
    [InlineData("severity=Nonsense")]
    public async Task List_with_invalid_query_parameters_returns_400(string query)
    {
        var tester = await factory.Tester1();

        var response = await tester.GetAsync($"/api/bugs?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ unexpected errors

    [Fact]
    public async Task Unhandled_exceptions_return_500_problem_details_without_a_stack_trace()
    {
        var broken = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.AddScoped<IBugService, ExplodingBugService>()));
        var tester = broken.CreateClient();
        var login = await (await tester.PostAsJsonAsync("/api/auth/login",
            new { email = "tester1@bugboard.local", password = "Tester1Pass!" })).ReadAsync<LoginResponse>();
        tester.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);

        var response = await tester.GetAsync("/api/bugs");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":500", body);
        Assert.DoesNotContain("ExplodingBugService", body); // no class names
        Assert.DoesNotContain(" at ", body);                // no stack trace lines
        Assert.DoesNotContain("secret internal detail", body);
    }

    private sealed class ExplodingBugService : IBugService
    {
        public Task<PagedResult<BugDto>> ListAsync(BugListQuery query, CancellationToken ct) =>
            throw new InvalidOperationException("secret internal detail");
        public Task<ServiceResult<BugDto>> GetAsync(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<ServiceResult<BugDto>> CreateAsync(CreateBugRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<ServiceResult<BugDto>> UpdateAsync(int id, UpdateBugRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<ServiceResult> DeleteAsync(int id, CancellationToken ct) => throw new NotImplementedException();
    }
}
