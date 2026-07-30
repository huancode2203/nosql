using System.Security.Cryptography;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AuthService(MongoContext db, JwtTokenService jwt) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        string ipAddress,
        string userAgent,
        CancellationToken ct)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        var user = await db.Users.Find(user =>
                !user.IsDeleted && (user.Email == identifier || user.Username == identifier))
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            await LogAsync(identifier, null, false, "Tài khoản không tồn tại", ipAddress, userAgent, ct);
            throw new AppException("Tên đăng nhập hoặc mật khẩu không đúng", 401);
        }

        if (user.LockedUntil > DateTime.UtcNow || user.Status == "Locked")
            throw new AppException("Tài khoản đang bị khóa", 423);
        if (user.Status != "Active")
            throw new ForbiddenException("Tài khoản đã ngừng hoạt động");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                user.Status = "Locked";
            }
            await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
            await LogAsync(identifier, user.Id, false, "Mật khẩu sai", ipAddress, userAgent, ct);
            throw new AppException("Tên đăng nhập hoặc mật khẩu không đúng", 401);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        if (user.Status == "Locked") user.Status = "Active";
        user.LastLoginAt = DateTime.UtcNow;

        var access = jwt.Create(user);
        var refresh = jwt.NewRefreshToken();
        user.RefreshTokens.RemoveAll(token => !token.IsActive);
        user.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = jwt.Hash(refresh),
            ExpiresAt = jwt.RefreshExpiry(request.RememberMe),
            Device = userAgent
        });
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        await LogAsync(identifier, user.Id, true, null, ipAddress, userAgent, ct);
        return BuildResponse(user, access, refresh);
    }

    public async Task<LoginResponse> RefreshAsync(string token, string userAgent, CancellationToken ct)
    {
        var hash = jwt.Hash(token);
        var user = await db.Users.Find(x => x.RefreshTokens.Any(item => item.TokenHash == hash))
            .FirstOrDefaultAsync(ct) ?? throw new AppException("Refresh token không hợp lệ", 401);
        if (user.Status != "Active" || user.IsDeleted)
            throw new ForbiddenException("Tài khoản không còn hoạt động");

        var old = user.RefreshTokens.First(item => item.TokenHash == hash);
        if (!old.IsActive) throw new AppException("Refresh token đã hết hạn hoặc bị thu hồi", 401);

        old.RevokedAt = DateTime.UtcNow;
        var next = jwt.NewRefreshToken();
        var longSession = old.ExpiresAt - old.CreatedAt > TimeSpan.FromDays(1.5);
        user.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = jwt.Hash(next),
            ExpiresAt = jwt.RefreshExpiry(longSession),
            Device = userAgent
        });
        user.RefreshTokens.RemoveAll(item => !item.IsActive && item.CreatedAt < DateTime.UtcNow.AddDays(-30));

        var access = jwt.Create(user);
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return BuildResponse(user, access, next);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.Find(x => x.Id == userId).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        foreach (var token in user.RefreshTokens.Where(item => item.IsActive)) token.RevokedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
    }

    public async Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct)
    {
        ValidatePassword(newPassword);
        var user = await db.Users.Find(x => x.Id == userId).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new AppException("Mật khẩu hiện tại không đúng");
        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
            throw new AppException("Không được dùng lại mật khẩu hiện tại");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        foreach (var token in user.RefreshTokens.Where(item => item.IsActive)) token.RevokedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
    }

    public async Task<string?> ForgotPasswordAsync(string email, string ipAddress, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(x => !x.IsDeleted && x.Email == normalized).FirstOrDefaultAsync(ct);
        if (user is null) return null;

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await db.PasswordResetTokens.DeleteManyAsync(
            x => x.UserId == user.Id && x.UsedAt == null,
            ct);
        await db.PasswordResetTokens.InsertOneAsync(new PasswordResetToken
        {
            UserId = user.Id,
            Email = user.Email,
            TokenHash = jwt.Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            RequestedFromIp = ipAddress
        }, cancellationToken: ct);
        return code;
    }

    public async Task ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken ct)
    {
        ValidatePassword(newPassword);
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(x => !x.IsDeleted && x.Email == normalized).FirstOrDefaultAsync(ct)
                   ?? throw new AppException("Mã xác nhận không hợp lệ hoặc đã hết hạn");
        var hash = jwt.Hash(code.Trim());
        var resetToken = await db.PasswordResetTokens.Find(x =>
                x.UserId == user.Id && x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .SortByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct) ?? throw new AppException("Mã xác nhận không hợp lệ hoặc đã hết hạn");

        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
            throw new AppException("Không được dùng lại mật khẩu hiện tại");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        if (user.Status == "Locked") user.Status = "Active";
        foreach (var refreshToken in user.RefreshTokens.Where(item => item.IsActive))
            refreshToken.RevokedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        resetToken.UsedAt = DateTime.UtcNow;

        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        await db.PasswordResetTokens.ReplaceOneAsync(x => x.Id == resetToken.Id, resetToken, cancellationToken: ct);
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var hash = jwt.Hash(token);
        var user = await db.Users.Find(x => x.RefreshTokens.Any(item => item.TokenHash == hash)).FirstOrDefaultAsync(ct);
        if (user is null) return;
        var refreshToken = user.RefreshTokens.First(item => item.TokenHash == hash);
        refreshToken.RevokedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
    }

    private static LoginResponse BuildResponse(
        User user,
        (string token, DateTime expires) access,
        string refreshToken) =>
        new(access.token, refreshToken, access.expires,
            new LoginUserDto(
                user.Id,
                user.Username,
                user.Email,
                user.FullName,
                user.Role,
                user.AvatarUrl,
                user.PermissionsConfigured
                    ? user.Permissions
                    : AppPermissions.DefaultsForRole(user.Role)));

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new AppException("Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt");
    }

    private Task LogAsync(
        string identifier,
        string? userId,
        bool success,
        string? reason,
        string ipAddress,
        string userAgent,
        CancellationToken ct) =>
        db.LoginHistories.InsertOneAsync(new LoginHistory
        {
            Identifier = identifier,
            UserId = userId,
            Success = success,
            FailureReason = reason,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, cancellationToken: ct);
}
