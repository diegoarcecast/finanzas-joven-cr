namespace auth.api.Contracts;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Key { get; set; } = default!;
    public int ExpirationMinutes { get; set; } = 60; // <- NUEVO (default 60 min)

}