using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.SignalR;
namespace EduManageLms.Api.Hubs; [Authorize] public sealed class NotificationHub:Hub { public override async Task OnConnectedAsync(){var uid=Context.UserIdentifier;if(uid is not null)await Groups.AddToGroupAsync(Context.ConnectionId,$"user:{uid}");await base.OnConnectedAsync();} }
