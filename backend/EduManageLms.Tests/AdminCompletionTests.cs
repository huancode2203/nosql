using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace EduManageLms.Tests;

public sealed class AdminCompletionTests
{
    [Fact]
    public void AdminPermissions_AreUnique_AndIncludeAvatarManagement()
    {
        AppPermissions.All.Should().OnlyHaveUniqueItems();
        AppPermissions.All.Should().Contain(
            AppPermissions.UsersManageAvatars);

        var defaults = AppPermissions.DefaultsForRole("Admin");
        defaults.Should().Contain(AppPermissions.UsersManageAvatars);
        defaults.Should().NotContain(AppPermissions.FullAccess);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg")]
    [InlineData(
        new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        },
        ".png")]
    [InlineData(
        new byte[]
        {
            0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0,
            0x57, 0x45, 0x42, 0x50
        },
        ".webp")]
    public void AvatarValidator_DetectsSupportedImageSignatures(
        byte[] bytes,
        string expectedExtension)
    {
        AvatarFileValidator.DetectExtension(bytes)
            .Should()
            .Be(expectedExtension);
    }

    [Fact]
    public void AvatarValidator_RejectsContentThatIsNotAnImage()
    {
        var action = () => AvatarFileValidator.DetectExtension(
            "not-an-image"u8);

        action.Should()
            .Throw<AppException>()
            .WithMessage("*PNG, JPEG hoặc WebP*");
    }

    [Fact]
    public void LegacyCourseDocuments_IgnoreUnknownFields()
    {
        var document = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["courseCode"] = "TEST101",
            ["courseName"] = "Môn kiểm thử",
            ["legacyCourseField"] = "old-value",
            ["clos"] = new BsonArray
            {
                new BsonDocument
                {
                    ["cloCode"] = "CLO1",
                    ["name"] = "Chuẩn đầu ra",
                    ["legacyCloField"] = true
                }
            },
            ["gradingSchemes"] = new BsonArray
            {
                new BsonDocument
                {
                    ["version"] = 1,
                    ["legacySchemeField"] = 123,
                    ["components"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["componentId"] = "FINAL",
                            ["name"] = "Cuối kỳ",
                            ["weight"] = 100,
                            ["legacyComponentField"] = "ignored"
                        }
                    }
                }
            }
        };

        var course = BsonSerializer.Deserialize<Course>(document);

        course.CourseCode.Should().Be("TEST101");
        course.Clos.Should().ContainSingle();
        course.GradingSchemes.Should().ContainSingle();
        course.GradingSchemes[0].Components.Should().ContainSingle();
    }
}
