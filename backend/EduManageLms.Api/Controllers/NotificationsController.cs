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
        CancellationToken ct)
    {
        var userId = User.UserId();
        var role = User.RoleName();
        var filter = Builders<Notification>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<Notification>.Filter.Eq(x => x.Status, "Sent") &
                     (Builders<Notification>.Filter.AnyEq(x => x.RecipientIds, userId) |
                      Builders<Notification>.Filter.Eq(x => x.AudienceType, "All") |
                      Builders<Notification>.Filter.Eq(x => x.AudienceType, role));

        var items = await db.Notifications.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Limit(100)
            .ToListAsync(ct);

        var data = items
            .Where(x => isRead is null || x.ReadBy.Contains(userId) == isRead.Value)
            .Select(x => (object)new
            {
                id = x.Id,
                x.Title,
                x.Content,
                x.Type,
                x.Priority,
                x.CreatedAt,
                isRead = x.ReadBy.Contains(userId)
            }).ToList();

        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(data));
    }

    [HttpPut("{id}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(string id, CancellationToken ct)
    {
        var result = await db.Notifications.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            Builders<Notification>.Update.AddToSet(x => x.ReadBy, User.UserId()),
            cancellationToken: ct);
        if (result.MatchedCount == 0) throw new NotFoundException();
        return Ok(ApiResponse<object>.Ok(new { }, "Đã đánh dấu đã đọc"));
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllRead(CancellationToken ct)
    {
        var userId = User.UserId();
        await db.Notifications.UpdateManyAsync(
            x => !x.IsDeleted && x.Status == "Sent",
            Builders<Notification>.Update.AddToSet(x => x.ReadBy, userId),
            cancellationToken: ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã đọc tất cả thông báo"));
    }
}
