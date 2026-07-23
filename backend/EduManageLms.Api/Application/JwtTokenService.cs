using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EduManageLms.Api.Application;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions settings = options.Value;

    public (string token, DateTime expires) Create(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("fullName", user.FullName)
        };
        if (user.StudentCode is not null) claims.Add(new Claim("studentCode", user.StudentCode));
        if (user.LecturerCode is not null) claims.Add(new Claim("lecturerCode", user.LecturerCode));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public DateTime RefreshExpiry(bool rememberMe = true) =>
        DateTime.UtcNow.AddDays(rememberMe ? settings.RefreshTokenDays : 1);
}
