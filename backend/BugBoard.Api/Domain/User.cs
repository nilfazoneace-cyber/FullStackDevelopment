namespace BugBoard.Api.Domain;

/// <summary>
/// Database entity. NEVER returned from the API directly (it contains PasswordHash).
/// Controllers return <see cref="Dtos.UserDto"/> instead.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
}
