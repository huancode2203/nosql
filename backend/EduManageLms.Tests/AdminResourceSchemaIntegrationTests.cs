using ClosedXML.Excel;
using EduManageLms.Api.Application;
using EduManageLms.Api.Infrastructure;
using EduManageLms.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace EduManageLms.Tests;

public sealed class AdminResourceSchemaIntegrationTests : IAsyncLifetime
{
    private readonly MongoDbContainer mongo = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    private MongoContext db = null!;
    private AdminResourceService resources = null!;
    private ObjectId facultyId;
    private ObjectId programId;
    private ObjectId courseId;
    private ObjectId lecturerId;
    private ObjectId semesterId;

    public async Task InitializeAsync()
    {
        await mongo.StartAsync();
        db = new MongoContext(
            Options.Create(
                new MongoOptions
                {
                    ConnectionString = mongo.GetConnectionString(),
                    DatabaseName = $"schema_{Guid.NewGuid():N}"
                }));
        resources = new AdminResourceService(db);

        await CreateValidatedCollectionsAsync();
        await SeedReferencesAsync();
    }

    public Task DisposeAsync() => mongo.DisposeAsync().AsTask();

    [Fact]
    public async Task PreparedStudentCourseAndSection_PassMongoJsonSchema()
    {
        var student = await resources.PrepareCreateAsync(
            "students",
            new Dictionary<string, object?>
            {
                ["studentCode"] = "SV-SCHEMA-001",
                ["fullName"] = "Sinh viên kiểm thử",
                ["email"] = "schema.student@example.edu.vn",
                ["facultyId"] = facultyId.ToString(),
                ["programId"] = programId.ToString()
            },
            CancellationToken.None);
        var course = await resources.PrepareCreateAsync(
            "courses",
            new Dictionary<string, object?>
            {
                ["courseCode"] = "SCHEMA101",
                ["courseName"] = "Môn kiểm thử schema",
                ["credits"] = 3
            },
            CancellationToken.None);
        var section = await resources.PrepareCreateAsync(
            "class-sections",
            new Dictionary<string, object?>
            {
                ["classSectionCode"] = "SCHEMA101-01",
                ["courseId"] = courseId.ToString(),
                ["lecturerId"] = lecturerId.ToString(),
                ["semesterId"] = semesterId.ToString()
            },
            CancellationToken.None);

        await db.Database.GetCollection<BsonDocument>("students")
            .InsertOneAsync(ToDocument(student));
        await db.Database.GetCollection<BsonDocument>("courses")
            .InsertOneAsync(ToDocument(course));
        await db.Database.GetCollection<BsonDocument>("classSections")
            .InsertOneAsync(ToDocument(section));

        (await db.Database.GetCollection<BsonDocument>("students")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty))
            .Should().Be(1);
        (await db.Database.GetCollection<BsonDocument>("courses")
                .CountDocumentsAsync(
                    Builders<BsonDocument>.Filter.Eq("courseCode", "SCHEMA101")))
            .Should().Be(1);
        (await db.Database.GetCollection<BsonDocument>("classSections")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty))
            .Should().Be(1);
        section["gradingSchemeSnapshot"].Should().BeOfType<BsonDocument>();

        using var importStream = BuildStudentImport();
        var importFile = new FormFile(
            importStream,
            0,
            importStream.Length,
            "file",
            "students.xlsx");
        var preview = await new ImportExportService(db, resources)
            .ImportResourceAsync(
                "students",
                importFile,
                commit: false,
                new AdminActor("admin", "Admin", "Admin", "127.0.0.1", "test"),
                CancellationToken.None);

        preview.ValidRows.Should().Be(1);
        preview.InvalidRows.Should().Be(0);

        var exception = await Assert.ThrowsAsync<MongoWriteException>(
            () => db.Database.GetCollection<BsonDocument>("students")
                .InsertOneAsync(
                    new BsonDocument
                    {
                        ["studentCode"] = "INVALID",
                        ["fullName"] = "Thiếu cấu trúc bắt buộc"
                    }));

        var mapped = ExceptionMiddleware.MapException(exception);

        mapped.Status.Should().Be(400);
        mapped.Message.Should().Be(
            "Dữ liệu chưa đúng cấu trúc bắt buộc. Vui lòng kiểm tra các trường đã nhập.");
        mapped.Message.Should().NotContain("Document failed validation");
    }

    private async Task CreateValidatedCollectionsAsync()
    {
        await CreateCollectionAsync(
            "students",
            new BsonDocument
            {
                ["bsonType"] = "object",
                ["required"] = new BsonArray
                {
                    "studentCode", "fullName", "faculty", "program",
                    "academicRecords"
                },
                ["properties"] = new BsonDocument
                {
                    ["academicRecords"] = new BsonDocument("bsonType", "array")
                }
            });
        await CreateCollectionAsync(
            "courses",
            new BsonDocument
            {
                ["bsonType"] = "object",
                ["required"] = new BsonArray
                {
                    "courseCode", "courseName", "credits", "clos",
                    "gradingSchemes"
                },
                ["properties"] = new BsonDocument
                {
                    ["credits"] = new BsonDocument
                    {
                        ["bsonType"] = new BsonArray { "int", "long" },
                        ["minimum"] = 1
                    },
                    ["clos"] = new BsonDocument("bsonType", "array"),
                    ["gradingSchemes"] = new BsonDocument("bsonType", "array")
                }
            });
        await CreateCollectionAsync(
            "classSections",
            new BsonDocument
            {
                ["bsonType"] = "object",
                ["required"] = new BsonArray
                {
                    "classSectionCode", "courseId", "lecturerId", "students",
                    "gradingSchemeSnapshot", "gradeStatus"
                },
                ["properties"] = new BsonDocument
                {
                    ["students"] = new BsonDocument("bsonType", "array"),
                    ["gradingSchemeSnapshot"] = new BsonDocument(
                        "bsonType",
                        "object"),
                    ["gradeStatus"] = new BsonDocument(
                        "enum",
                        new BsonArray
                        {
                            "Draft", "InProgress", "Submitted", "Published",
                            "Locked", "Reopened"
                        })
                }
            });
    }

    private async Task CreateCollectionAsync(
        string name,
        BsonDocument schema)
    {
        await db.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                ["create"] = name,
                ["validator"] = new BsonDocument("$jsonSchema", schema),
                ["validationLevel"] = "moderate",
                ["validationAction"] = "error"
            });
    }

    private async Task SeedReferencesAsync()
    {
        facultyId = ObjectId.GenerateNewId();
        programId = ObjectId.GenerateNewId();
        courseId = ObjectId.GenerateNewId();
        lecturerId = ObjectId.GenerateNewId();
        semesterId = ObjectId.GenerateNewId();
        var academicYearId = ObjectId.GenerateNewId();

        await db.Database.GetCollection<BsonDocument>("faculties")
            .InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = facultyId,
                    ["facultyCode"] = "CNTT",
                    ["facultyName"] = "Công nghệ thông tin",
                    ["isDeleted"] = false
                });
        await db.Database.GetCollection<BsonDocument>("programs")
            .InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = programId,
                    ["programCode"] = "CNTT2024",
                    ["programName"] = "Công nghệ thông tin 2024",
                    ["requiredCredits"] = 130,
                    ["isDeleted"] = false
                });
        await db.Database.GetCollection<BsonDocument>("courses")
            .InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = courseId,
                    ["courseCode"] = "NOSQL01",
                    ["courseName"] = "Cơ sở dữ liệu NoSQL",
                    ["credits"] = 3,
                    ["clos"] = new BsonArray(),
                    ["gradingSchemes"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["version"] = 2,
                            ["academicYear"] = "2026-2027",
                            ["components"] = new BsonArray
                            {
                                new BsonDocument
                                {
                                    ["componentId"] = "FINAL",
                                    ["name"] = "Cuối kỳ",
                                    ["weight"] = 100,
                                    ["maxScore"] = 10
                                }
                            },
                            ["passingScore"] = 4,
                            ["roundingMode"] = "Normal",
                            ["decimalPlaces"] = 2,
                            ["active"] = true
                        }
                    },
                    ["isDeleted"] = false
                });
        await db.Database.GetCollection<BsonDocument>("lecturers")
            .InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = lecturerId,
                    ["lecturerCode"] = "GV001",
                    ["fullName"] = "Giảng viên kiểm thử",
                    ["isDeleted"] = false
                });
        await db.Database.GetCollection<BsonDocument>("semesters")
            .InsertOneAsync(
                new BsonDocument
                {
                    ["_id"] = semesterId,
                    ["semesterCode"] = "HK1",
                    ["semesterName"] = "Học kỳ 1",
                    ["academicYearId"] = academicYearId,
                    ["academicYearName"] = "2026-2027",
                    ["isDeleted"] = false
                });
    }

    private static BsonDocument ToDocument(
        IReadOnlyDictionary<string, object?> values) =>
        new(values.Select(pair =>
            new BsonElement(
                pair.Key,
                pair.Value switch
                {
                    null => BsonNull.Value,
                    BsonValue bson => bson,
                    _ => BsonValue.Create(pair.Value)
                })));

    private static MemoryStream BuildStudentImport()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("students");
            var headers = new[]
            {
                "studentCode", "fullName", "email", "facultyCode",
                "programCode"
            };
            for (var index = 0; index < headers.Length; index++)
                sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Cell(2, 1).Value = "SV-IMPORT-001";
            sheet.Cell(2, 2).Value = "Sinh viên import";
            sheet.Cell(2, 3).Value = "schema.import@example.edu.vn";
            sheet.Cell(2, 4).Value = "CNTT";
            sheet.Cell(2, 5).Value = "CNTT2024";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }
}
