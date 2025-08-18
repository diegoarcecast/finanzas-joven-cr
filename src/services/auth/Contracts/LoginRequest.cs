namespace auth.api.Contracts;

public sealed record LoginRequest(
    string Email,
    string Password
);
