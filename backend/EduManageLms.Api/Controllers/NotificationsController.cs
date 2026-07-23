using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(MongoContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> List(
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var userId = User.UserId();
        var filter = VisibleToCurrentUser(userId, User.RoleName());
        if (isRead == true) filter &= Builders<Notification>.Filter.AnyEq(x => x.ReadBy, userId);
        if (isRead == false) filter &= Builders<Notification>.Filter.Not(Builders<Notification>.Filter.AnyEq(x => x.ReadBy, userId));

        var items = await db.Notifications.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var data = items.Select(x => (object)new
        {
            id = x.Id,
            x.Title,
            x.Content,
            x.Type,
            x.Priority,
            x.CreatedAt,
            x.DisplayFrom,
            x.ExpiresAt,
            isRead = x.ReadBy.Contains(userId)
        }).ToList();
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(data));
    }

    [HttpPut("{id}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(string id, CancellationToken ct)
    {
        var userId = User.UserId();
        var filter = VisibleToCurrentUser(userId, User.RoleName()) & Builders<Notification>.Filter.Eq(x => x.Id, id);
        var result = await db.Notifications.UpdateOneAsync(
            filter,
            Builders<Notification>.Update.AddToSet(x => x.ReadBy, userId).Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);
        if (result.MatchedCount == 0) throw new NotFoundException("Không tìm thấy thông báo trong phạm vi truy cập");
        return Ok(ApiResponse<object>.Ok(new { }, "Đã đánh dấu đã đọc"));
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllRead(CancellationToken ct)
    {
        var userId = User.UserId();
        var filter = VisibleToCurrentUser(userId, User.RoleName()) &
                     Builders<Notification>.Filter.Not(Builders<Notification>.Filter.AnyEq(x => x.ReadBy, userId));
        await db.Notifications.UpdateManyAsync(
            filter,
            Builders<Notification>.Update.AddToSet(x => x.ReadBy, userId).Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã đọc tất cả thông báo"));
    }

    private static FilterDefinition<Notification> VisibleToCurrentUser(string userId, string role)
    {
        var now = DateTime.UtcNow;
        return Builders<Notification>.Filter.Eq(x => x.IsDeleted, false) &
               Builders<Notification>.Filter.Eq(x => x.Status, "Sent") &
               Builders<Notification>.Filter.Lte(x => x.DisplayFrom, now) &
               (Builders<Notification>.Filter.Eq(x => x.ExpiresAt, null) |
                Builders<Notification>.Filter.Gt(x => x.ExpiresAt, now)) &
               (Builders<Notification>.Filter.AnyEq(x => x.RecipientIds, userId) |
                Builders<Notification>.Filter.Eq(x => x.AudienceType, "All") |
                Builders<Notification>.Filter.Eq(x => x.AudienceType, role));
    }
}
