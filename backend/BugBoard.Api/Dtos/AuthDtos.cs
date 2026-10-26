using System.ComponentModel.DataAnnotations;
using BugBoard.Api.Domain;

namespace BugBoard.Api.Dtos;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Safe public view of a user. Deliberately has NO PasswordHash property.</summary>
public record UserDto(int Id, string Email, string DisplayName, Role Role);

public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
