using System.Security.Claims;

namespace auth.api.Auth;

public interface IJwtTokenGenerator
{
    (string token, DateTime expiresAt) GenerateToken(IEnumerable<Claim> claims);
}
