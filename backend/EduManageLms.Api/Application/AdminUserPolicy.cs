namespace EduManageLms.Api.Application;

internal static class AdminUserPolicy
{
    internal static bool RequiresPermissionManagement(
        string? existingRole,
        string? requestedRole,
        bool permissionsProvided,
        bool creating)
    {
        if (permissionsProvided)
            return true;
        if (string.IsNullOrWhiteSpace(requestedRole))
            return false;
        if (creating)
            return IsAdmin(requestedRole);

        return !string.IsNullOrWhiteSpace(existingRole)
               && !existingRole.Equals(
                   requestedRole,
                   StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RequiresExplicitAdminPermissions(
        string? existingRole,
        string? targetRole,
        bool permissionsProvided)
    {
        if (permissionsProvided || !IsAdmin(targetRole))
            return false;

        return string.IsNullOrWhiteSpace(existingRole)
               || !IsAdmin(existingRole);
    }

    internal static IReadOnlyCollection<string> ObsoleteLinkFields(
        string? targetRole) => targetRole?.ToLowerInvariant() switch
    {
        "student" => ["lecturerCode"],
        "lecturer" => ["studentCode"],
        "admin" => ["studentCode", "lecturerCode"],
        _ => []
    };

    internal static bool IsAdmin(string? role) =>
        role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
}
