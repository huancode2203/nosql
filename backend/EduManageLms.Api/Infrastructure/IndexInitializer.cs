using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Infrastructure;

public sealed class IndexInitializer(MongoContext db, ILogger<IndexInitializer> log)
{
    private sealed record IndexSpec(
        string Name,
        BsonDocument Keys,
        bool Unique = false,
        TimeSpan? ExpireAfter = null,
        BsonDocument? PartialFilter = null);

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await EnsureIndexesAsync("users",
        [
            UniqueActive("ux_users_username", Key(("username", 1))),
            UniqueActive("ux_users_email", Key(("email", 1))),
            new("ix_users_role_status", Key(("role", 1), ("status", 1)))
        ], ct);
        await EnsureIndexesAsync("students",
        [
            UniqueActive("ux_students_studentCode", Key(("studentCode", 1))),
            new("ix_students_year_semester", Key(("academicRecords.academicYearId", 1), ("academicRecords.semesters.semesterId", 1)))
        ], ct);
        await EnsureIndexesAsync("lecturers", [UniqueActive("ux_lecturers_lecturerCode", Key(("lecturerCode", 1)))], ct);
        await EnsureIndexesAsync("courses", [UniqueActive("ux_courses_courseCode", Key(("courseCode", 1)))], ct);
        await EnsureIndexesAsync("classSections",
        [
            UniqueActive("ux_sections_code", Key(("classSectionCode", 1))),
            new("ix_sections_year_semester_lecturer", Key(("academicYearId", 1), ("semesterId", 1), ("lecturerId", 1))),
            new("ix_sections_studentCode", Key(("students.studentCode", 1)))
        ], ct);
        await EnsureIndexesAsync("notifications",
        [
            new("ix_notifications_recipients", Key(("recipientIds", 1))),
            new("ix_notifications_createdAt", Key(("createdAt", -1)))
        ], ct);
        await EnsureIndexesAsync("auditLogs", [new("ix_audit_createdAt", Key(("createdAt", -1)))], ct);
        await EnsureIndexesAsync("faculties", [UniqueActive("ux_faculties_facultyCode", Key(("facultyCode", 1)))], ct);
        await EnsureIndexesAsync("programs", [UniqueActive("ux_programs_programCode", Key(("programCode", 1)))], ct);
        await EnsureIndexesAsync("academicYears",
        [
            UniqueActive("ux_academicYears_code", Key(("academicYearCode", 1))),
            new("ix_academicYears_current", Key(("isCurrent", 1)))
        ], ct);
        await EnsureIndexesAsync("semesters",
        [
            UniqueActive("ux_semesters_year_code", Key(("academicYearId", 1), ("semesterCode", 1))),
            new("ix_semesters_gradeWindow", Key(("gradeEntryStart", 1), ("gradeEntryEnd", 1)))
        ], ct);
        await EnsureIndexesAsync("materials",
        [
            new("ix_materials_section_createdAt", Key(("classSectionId", 1), ("createdAt", -1))),
            new("ix_materials_lecturerCode", Key(("lecturerCode", 1)))
        ], ct);
        await EnsureIndexesAsync("assignments",
        [
            new("ix_assignments_section_dueAt", Key(("classSectionId", 1), ("dueAt", 1))),
            new("ix_assignments_lecturerCode", Key(("lecturerCode", 1)))
        ], ct);
        await EnsureIndexesAsync("submissions",
        [
            UniqueActive("ux_submissions_assignment_student", Key(("assignmentId", 1), ("studentId", 1))),
            new("ix_submissions_section_status", Key(("classSectionId", 1), ("status", 1)))
        ], ct);
        await EnsureIndexesAsync("examSchedules", [new("ix_examSchedules_section_startAt", Key(("classSectionId", 1), ("startAt", 1)))], ct);
        await EnsureIndexesAsync("systemSettings", [UniqueActive("ux_systemSettings_key", Key(("key", 1)))], ct);
        await EnsureIndexesAsync("gradeReopenRequests", [new("ix_gradeReopen_status_createdAt", Key(("status", 1), ("createdAt", -1)))], ct);
        await EnsureIndexesAsync("passwordResetTokens",
        [
            new("ix_passwordReset_user_active", Key(("userId", 1), ("usedAt", 1))),
            new("ttl_passwordReset_expiresAt", Key(("expiresAt", 1)), ExpireAfter: TimeSpan.Zero)
        ], ct);
        log.LogInformation("MongoDB indexes initialized");
    }

    private async Task EnsureIndexesAsync(string collectionName, IReadOnlyCollection<IndexSpec> specs, CancellationToken ct)
    {
        var collection = db.Database.GetCollection<BsonDocument>(collectionName);
        foreach (var spec in specs)
        {
            var existing = await ReadIndexesAsync(collection, ct);
            var sameName = existing.FirstOrDefault(x => x.GetValue("name", "").AsString == spec.Name);
            if (sameName is not null && IndexMatches(sameName, spec))
                continue;
            if (sameName is not null)
            {
                await collection.Indexes.DropOneAsync(spec.Name, ct);
                log.LogWarning("Dropped conflicting index {IndexName} on {Collection}", spec.Name, collectionName);
                existing = await ReadIndexesAsync(collection, ct);
            }

            var sameKeys = existing.FirstOrDefault(x => KeysEqual(x, spec.Keys));
            if (sameKeys is not null)
            {
                var existingName = sameKeys.GetValue("name", "").AsString;
                if (existingName != "_id_")
                {
                    await collection.Indexes.DropOneAsync(existingName, ct);
                    log.LogWarning("Dropped legacy index {OldName}; replacing with {NewName}", existingName, spec.Name);
                }
            }

            var options = new CreateIndexOptions<BsonDocument>
            {
                Name = spec.Name,
                Unique = spec.Unique,
                ExpireAfter = spec.ExpireAfter,
                PartialFilterExpression = spec.PartialFilter is null
                    ? null
                    : new BsonDocumentFilterDefinition<BsonDocument>(
                        spec.PartialFilter)
            };
            var model = new CreateIndexModel<BsonDocument>(new BsonDocumentIndexKeysDefinition<BsonDocument>(spec.Keys), options);
            await collection.Indexes.CreateOneAsync(model, cancellationToken: ct);
        }
    }

    private static async Task<List<BsonDocument>> ReadIndexesAsync(IMongoCollection<BsonDocument> collection, CancellationToken ct)
    {
        using var cursor = await collection.Indexes.ListAsync(cancellationToken: ct);
        return await cursor.ToListAsync(ct);
    }

    private static bool KeysEqual(BsonDocument index, BsonDocument desired) =>
        index.TryGetValue("key", out var value) && value.IsBsonDocument && value.AsBsonDocument.Equals(desired);

    private static bool IndexMatches(BsonDocument index, IndexSpec spec)
    {
        if (!KeysEqual(index, spec.Keys))
            return false;
        var uniqueValue = index.GetValue("unique", false);
        var unique = uniqueValue.IsBoolean && uniqueValue.AsBoolean;
        if (unique != spec.Unique)
            return false;
        var currentPartial = index.GetValue(
            "partialFilterExpression",
            BsonNull.Value);
        if (spec.PartialFilter is null)
            return currentPartial.IsBsonNull;
        return currentPartial.IsBsonDocument
            && currentPartial.AsBsonDocument.Equals(spec.PartialFilter);
    }

    private static IndexSpec UniqueActive(string name, BsonDocument keys) =>
        new(
            name,
            keys,
            Unique: true,
            PartialFilter: new BsonDocument("isDeleted", false));

    private static BsonDocument Key(params (string Field, int Direction)[] fields)
    {
        var document = new BsonDocument();
        foreach (var field in fields) document.Add(field.Field, field.Direction);
        return document;
    }
}
