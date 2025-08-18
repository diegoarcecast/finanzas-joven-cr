using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using auth.api.Contracts;
using System.Security.Claims;
using System.Text;

namespace auth.api.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _opt;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _opt = options.Value;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
    }

    public (string token, DateTime expiresAt) GenerateToken(IEnumerable<Claim> claims)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpirationMinutes);

        var jwt = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
