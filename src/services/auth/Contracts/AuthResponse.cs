namespace auth.api.Contracts;

public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt
);
