using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AdminAvatarService(
    MongoContext db,
    IWebHostEnvironment environment) : IAdminAvatarService
{
    public async Task<AdminAvatarDto> UploadAsync(
        string userId,
        IFormFile file,
        AdminActor actor,
        CancellationToken ct)
    {
        userId = ValidateUserId(userId);
        if (file.Length <= 0)
            throw new AppException("Tệp ảnh đại diện đang rỗng.");
        if (file.Length > AvatarFileValidator.MaximumBytes)
            throw new AppException("Ảnh đại diện không được vượt quá 5 MB.");

        var user = await db.Users
            .Find(x => x.Id == userId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        await using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var extension = AvatarFileValidator.DetectExtension(bytes);

        var avatarDirectory = Path.Combine(
            environment.ContentRootPath,
            "uploads",
            "avatars");
        Directory.CreateDirectory(avatarDirectory);

        var fileName = $"{user.Id}-{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(avatarDirectory, fileName);
        var avatarUrl = $"/uploads/avatars/{fileName}";

        await File.WriteAllBytesAsync(absolutePath, bytes, ct);
        try
        {
            using var session = await db.Client.StartSessionAsync(
                cancellationToken: ct);
            await session.WithTransactionAsync(
                async (_, token) =>
                {
                    await db.Users.UpdateOneAsync(
                        session,
                        x => x.Id == user.Id && !x.IsDeleted,
                        Builders<User>.Update
                            .Set(x => x.AvatarUrl, avatarUrl)
                            .Set(x => x.UpdatedAt, DateTime.UtcNow),
                        cancellationToken: token);

                    await db.AuditLogs.InsertOneAsync(
                        session,
                        new AuditLog
                        {
                            UserId = actor.UserId,
                            UserName = actor.UserName,
                            Role = actor.Role,
                            Action = "USER_AVATAR_UPDATE",
                            Entity = "users",
                            EntityId = user.Id,
                            Before = new { user.AvatarUrl },
                            After = new { AvatarUrl = avatarUrl },
                            IpAddress = actor.IpAddress,
                            UserAgent = actor.UserAgent
                        },
                        cancellationToken: token);

                    return true;
                },
                new TransactionOptions(
                    readPreference: ReadPreference.Primary,
                    readConcern: ReadConcern.Snapshot,
                    writeConcern: WriteConcern.WMajority),
                ct);
        }
        catch
        {
            File.Delete(absolutePath);
            throw;
        }

        DeleteManagedAvatar(user.AvatarUrl, avatarDirectory);
        return new AdminAvatarDto(user.Id, avatarUrl);
    }

    public async Task<AdminAvatarDto> DeleteAsync(
        string userId,
        AdminActor actor,
        CancellationToken ct)
    {
        userId = ValidateUserId(userId);
        var user = await db.Users
            .Find(x => x.Id == userId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        using var session = await db.Client.StartSessionAsync(
            cancellationToken: ct);
        await session.WithTransactionAsync(
            async (_, token) =>
            {
                await db.Users.UpdateOneAsync(
                    session,
                    x => x.Id == user.Id && !x.IsDeleted,
                    Builders<User>.Update
                        .Set(x => x.AvatarUrl, null)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: token);

                await db.AuditLogs.InsertOneAsync(
                    session,
                    new AuditLog
                    {
                        UserId = actor.UserId,
                        UserName = actor.UserName,
                        Role = actor.Role,
                        Action = "USER_AVATAR_DELETE",
                        Entity = "users",
                        EntityId = user.Id,
                        Before = new { user.AvatarUrl },
                        After = new { AvatarUrl = (string?)null },
                        IpAddress = actor.IpAddress,
                        UserAgent = actor.UserAgent
                    },
                    cancellationToken: token);

                return true;
            },
            new TransactionOptions(
                readPreference: ReadPreference.Primary,
                readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority),
            ct);

        DeleteManagedAvatar(
            user.AvatarUrl,
            Path.Combine(environment.ContentRootPath, "uploads", "avatars"));
        return new AdminAvatarDto(user.Id, null);
    }

    internal static string ValidateUserId(string userId)
    {
        var normalized = userId?.Trim() ?? string.Empty;
        if (!ObjectId.TryParse(normalized, out _))
            throw new AppException("ID tài khoản không hợp lệ.");
        return normalized;
    }

    private static void DeleteManagedAvatar(
        string? avatarUrl,
        string avatarDirectory)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl)
            || !avatarUrl.StartsWith(
                "/uploads/avatars/",
                StringComparison.OrdinalIgnoreCase))
            return;

        var fileName = Path.GetFileName(avatarUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var path = Path.Combine(avatarDirectory, fileName);
        if (File.Exists(path))
            File.Delete(path);
    }
}
