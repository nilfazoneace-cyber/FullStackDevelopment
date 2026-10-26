using BugBoard.Api.Data;
using BugBoard.Api.Domain;
using BugBoard.Api.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BugBoard.Api.Services;

public interface IAuthService
{
    /// <summary>Returns null when the credentials are wrong (the caller maps that to 401).</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserDto?> GetUserAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct);
}

public class AuthService(BugBoardDbContext db, ITokenService tokens) : IAuthService
{
    private static readonly PasswordHasher<User> Hasher = new();

    // A real hash of a throw-away password. Used to burn the same CPU time when the email
    // does not exist, so response time does not reveal which emails are registered.
    private static readonly string DummyHash = Hasher.HashPassword(new User(), "not-a-real-password");

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Always run a verification, whether or not the user exists.
        var result = Hasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? DummyHash, request.Password);

        if (user is null || result == PasswordVerificationResult.Failed)
        {
            return null; // same outcome for "unknown email" and "wrong password"
        }

        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<UserDto?> GetUserAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : ToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.Role))
            .ToListAsync(ct);

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role);
}
