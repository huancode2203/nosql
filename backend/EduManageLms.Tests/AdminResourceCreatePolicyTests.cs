using EduManageLms.Api.Application;
using FluentAssertions;
using MongoDB.Bson;

namespace EduManageLms.Tests;

public sealed class AdminResourceCreatePolicyTests
{
    [Fact]
    public void StructuralDefaults_MatchRequiredMongoStructures()
    {
        var student = new Dictionary<string, object?>();
        var course = new Dictionary<string, object?>();
        var section = new Dictionary<string, object?>();

        AdminResourceCreatePolicy.ApplyStructuralDefaults("students", student);
        AdminResourceCreatePolicy.ApplyStructuralDefaults("courses", course);
        AdminResourceCreatePolicy.ApplyStructuralDefaults("class-sections", section);

        student["academicRecords"].Should().BeOfType<BsonArray>();
        course["clos"].Should().BeOfType<BsonArray>();
        course["gradingSchemes"].Should().BeOfType<BsonArray>();
        section["students"].Should().BeOfType<BsonArray>();
        section["gradeStatus"].Should().Be("Draft");
    }

    [Fact]
    public void StudentRequiredFields_IncludeSchemaRelationshipsAndRecords()
    {
        AdminResourceCreatePolicy.RequiredFields("students")
            .Should()
            .Contain(new[] { "faculty", "program", "academicRecords" });
    }

    [Fact]
    public void ClassSectionRequiredFields_IncludeSnapshotAndEnrollmentList()
    {
        AdminResourceCreatePolicy.RequiredFields("class-sections")
            .Should()
            .Contain(
                new[]
                {
                    "students", "gradingSchemeSnapshot", "gradeStatus"
                });
    }
}
