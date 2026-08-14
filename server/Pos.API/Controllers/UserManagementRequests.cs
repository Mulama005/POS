namespace Pos.Api.Controllers;

public sealed record InviteUserRequest(string Email, string FullName, string Role);
public sealed record AcceptInviteRequest(string UserId, string Token, string Password);
public sealed record ChangeRoleRequest(string Role);
