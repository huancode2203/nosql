using System.Security.Claims;
namespace EduManageLms.Api.Common;
public static class ClaimsPrincipalExtensions{
 public static string UserId(this ClaimsPrincipal user)=>user.FindFirstValue(ClaimTypes.NameIdentifier)??throw new UnauthorizedAccessException();
 public static string RoleName(this ClaimsPrincipal user)=>user.FindFirstValue(ClaimTypes.Role)??string.Empty;
 public static string? StudentCode(this ClaimsPrincipal user)=>user.FindFirstValue("studentCode");
 public static string? LecturerCode(this ClaimsPrincipal user)=>user.FindFirstValue("lecturerCode");
}
