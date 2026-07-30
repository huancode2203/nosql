using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EduManageLms.Api.Common;

public static class AppPermissions
{
    public const string FullAccess = "admin.full_access";
    public const string ResourcesRead = "admin.resources.read";
    public const string ResourcesWrite = "admin.resources.write";
    public const string ResourcesDelete = "admin.resources.delete";
    public const string UsersManagePermissions = "admin.users.permissions";
    public const string UsersManageAvatars = "admin.users.avatars";
    public const string GradesReview = "admin.grades.review";
    public const string GradesPublish = "admin.grades.publish";
    public const string GradesLock = "admin.grades.lock";
    public const string GradesReopen = "admin.grades.reopen";
    public const string BackupsRead = "admin.backups.read";
    public const string BackupsManage = "admin.backups.manage";
    public const string ReportsRead = "admin.reports.read";
    public const string ReportsExport = "admin.reports.export";
    public const string AuditRead = "admin.audit.read";
    public const string NotificationsManage = "admin.notifications.manage";
    public const string SettingsManage = "admin.settings.manage";
    public const string ImportExport = "admin.import_export";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
    [
        FullAccess,
        ResourcesRead,
        ResourcesWrite,
        ResourcesDelete,
        UsersManagePermissions,
        UsersManageAvatars,
        GradesReview,
        GradesPublish,
        GradesLock,
        GradesReopen,
        BackupsRead,
        BackupsManage,
        ReportsRead,
        ReportsExport,
        AuditRead,
        NotificationsManage,
        SettingsManage,
        ImportExport
    ], StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> DefaultsForRole(string role) =>
        role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            ? All.Where(permission => permission != FullAccess).ToArray()
            : [];
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission)
    : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!user.HasPermission(permission))
        {
            context.Result = new ForbidResult();
        }
    }
}
