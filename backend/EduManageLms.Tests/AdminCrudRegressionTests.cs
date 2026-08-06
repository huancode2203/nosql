using EduManageLms.Api.Application;
using FluentAssertions;

namespace EduManageLms.Tests;

public sealed class AdminCrudRegressionTests
{
    [Fact]
    public void CreatingAdmin_RequiresPermissionManagement()
    {
        AdminUserPolicy.RequiresPermissionManagement(
                existingRole: null,
                requestedRole: "Admin",
                permissionsProvided: false,
                creating: true)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ChangingRole_RequiresPermissionManagement()
    {
        AdminUserPolicy.RequiresPermissionManagement(
                existingRole: "Student",
                requestedRole: "Lecturer",
                permissionsProvided: false,
                creating: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void UpdatingSameRole_DoesNotRequirePermissionManagement()
    {
        AdminUserPolicy.RequiresPermissionManagement(
                existingRole: "Student",
                requestedRole: "Student",
                permissionsProvided: false,
                creating: false)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(null, "Admin", false, true)]
    [InlineData("Student", "Admin", false, true)]
    [InlineData("Admin", "Admin", false, false)]
    [InlineData("Student", "Admin", true, false)]
    public void AdminPermissionConfiguration_IsExplicitForNewOrPromotedAdmins(
        string? existingRole,
        string targetRole,
        bool permissionsProvided,
        bool expected)
    {
        AdminUserPolicy.RequiresExplicitAdminPermissions(
                existingRole,
                targetRole,
                permissionsProvided)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData("Student", new[] { "lecturerCode" })]
    [InlineData("Lecturer", new[] { "studentCode" })]
    [InlineData("Admin", new[] { "studentCode", "lecturerCode" })]
    public void RoleNormalization_RemovesObsoleteProfileLinks(
        string role,
        string[] expectedFields)
    {
        AdminUserPolicy.ObsoleteLinkFields(role)
            .Should()
            .BeEquivalentTo(expectedFields);
    }
}
