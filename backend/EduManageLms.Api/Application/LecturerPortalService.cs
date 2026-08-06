using ClosedXML.Excel;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class LecturerPortalService(MongoContext db, IGradebookService gradebook, ScoreNormalizationService scoreNormalizer) : ILecturerPortalService
{
    public async Task<IReadOnlyCollection<LecturerClassDto>> GetClassesAsync(string lecturerCode, CancellationToken ct)
    {
        var sections = await db.ClassSections.Find(x => x.LecturerCode == lecturerCode && !x.IsDeleted)
            .SortByDescending(x => x.StartDate)
            .ToListAsync(ct);
        return sections.Select(MapClass).ToList();
    }

    public async Task<IReadOnlyCollection<ClassStudentDto>> GetStudentsAsync(string lecturerCode, string classSectionId, CancellationToken ct)
    {
        var section = await RequireSectionAsync(lecturerCode, classSectionId, ct);
        var ids = section.Students.Select(x => x.StudentId).ToList();
        var students = await db.Students.Find(Builders<Student>.Filter.In(x => x.Id, ids) & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .SortBy(x => x.StudentCode)
            .ToListAsync(ct);
        return students.Select(x => new ClassStudentDto(x.Id, x.StudentCode, x.FullName, x.Email, x.AdministrativeClass, x.Status)).ToList();
    }

    public async Task<ClassStatisticsDto> GetStatisticsAsync(string lecturerCode, string classSectionId, CancellationToken ct)
    {
        var section = await RequireSectionAsync(lecturerCode, classSectionId, ct);
        if (!ObjectId.TryParse(section.Id, out var sectionObjectId)) throw new AppException("Id lớp học phần không hợp lệ");

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "isDeleted", false },
                { "academicRecords.semesters.courses.classSectionId", sectionObjectId }
            }),
            Stage("{ $unwind: '$academicRecords' }"),
            Stage("{ $unwind: '$academicRecords.semesters' }"),
            Stage("{ $unwind: '$academicRecords.semesters.courses' }"),
            new BsonDocument("$match", new BsonDocument("academicRecords.semesters.courses.classSectionId", sectionObjectId)),
            Stage("{ $unwind: '$academicRecords.semesters.courses.scores' }"),
            Stage("{ $addFields: { weighted: { $multiply: [ { $divide: [ { $ifNull: ['$academicRecords.semesters.courses.scores.score', 0] }, '$academicRecords.semesters.courses.scores.maxScore' ] }, 10, { $divide: ['$academicRecords.semesters.courses.scores.weight', 100] } ] } } }"),
            Stage("{ $group: { _id: '$_id', studentCode: { $first: '$studentCode' }, fullName: { $first: '$fullName' }, finalScore: { $sum: '$weighted' } } }"),
            Stage($"{{ $addFields: {{ finalScore: {{ $round: ['$finalScore', 2] }}, passed: {{ $gte: ['$finalScore', {section.GradingSchemeSnapshot.PassingScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}] }} }} }}"),
            Stage("{ $facet: { summary: [ { $group: { _id: null, count: { $sum: 1 }, average: { $avg: '$finalScore' }, highest: { $max: '$finalScore' }, lowest: { $min: '$finalScore' }, stdDev: { $stdDevPop: '$finalScore' }, passed: { $sum: { $cond: ['$passed', 1, 0] } }, failed: { $sum: { $cond: ['$passed', 0, 1] } }, scores: { $push: '$finalScore' } } } ], distribution: [ { $bucket: { groupBy: '$finalScore', boundaries: [0, 4, 5.5, 7, 8.5, 10.01], default: 'Khác', output: { count: { $sum: 1 } } } } ], top: [ { $sort: { finalScore: -1 } }, { $limit: 5 } ], risk: [ { $match: { finalScore: { $lt: 5 } } }, { $sort: { finalScore: 1 } }, { $limit: 10 } ] } }"),
        };

        var result = await db.Students.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(ct);
        if (result is null) return EmptyStatistics(section);

        var summaryArray = result.GetValue("summary", new BsonArray()).AsBsonArray;
        if (summaryArray.Count == 0) return EmptyStatistics(section);
        var summary = summaryArray[0].AsBsonDocument;
        var count = summary.GetValue("count", 0).ToInt32();
        var passed = summary.GetValue("passed", 0).ToInt32();
        var failed = summary.GetValue("failed", 0).ToInt32();
        var scores = summary.GetValue("scores", new BsonArray()).AsBsonArray.Select(x => x.ToDouble()).OrderBy(x => x).ToList();
        var median = scores.Count == 0 ? 0 : scores.Count % 2 == 1 ? scores[scores.Count / 2] : (scores[scores.Count / 2 - 1] + scores[scores.Count / 2]) / 2;

        var distributionLabels = new Dictionary<string, string>
        {
            ["0"] = "0–<4",
            ["4"] = "4–<5.5",
            ["5.5"] = "5.5–<7",
            ["7"] = "7–<8.5",
            ["8.5"] = "8.5–10"
        };
        var distribution = result["distribution"].AsBsonArray.Select(x =>
        {
            var doc = x.AsBsonDocument;
            var key = doc["_id"].ToString();
            return new ChartItemDto(distributionLabels.GetValueOrDefault(key, key), doc["count"].ToDouble());
        }).ToList();

        return new ClassStatisticsDto(
            section.Id,
            section.ClassSectionCode,
            section.CourseName,
            count,
            Round(summary.GetValue("average", 0).ToDouble()),
            Round(summary.GetValue("highest", 0).ToDouble()),
            Round(summary.GetValue("lowest", 0).ToDouble()),
            Round(median),
            Round(summary.GetValue("stdDev", 0).ToDouble()),
            passed,
            failed,
            count == 0 ? 0 : Round(passed * 100d / count),
            distribution,
            result["top"].AsBsonArray.Select(MapScoreRow).ToList(),
            result["risk"].AsBsonArray.Select(MapScoreRow).ToList());
    }

    public async Task<IReadOnlyCollection<ClassCloStatisticsDto>> GetCloStatisticsAsync(string lecturerCode, string classSectionId, CancellationToken ct)
    {
        var section = await RequireSectionAsync(lecturerCode, classSectionId, ct);
        if (!ObjectId.TryParse(section.Id, out var sectionObjectId)) throw new AppException("Id lớp học phần không hợp lệ");
        var course = await db.Courses.Find(x => x.Id == section.CourseId && !x.IsDeleted).FirstOrDefaultAsync(ct);
        var thresholds = course?.Clos.ToDictionary(x => x.CloCode, x => x.Threshold, StringComparer.OrdinalIgnoreCase) ?? [];
        var descriptions = course?.Clos.ToDictionary(x => x.CloCode, x => x.Description, StringComparer.OrdinalIgnoreCase) ?? [];

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "isDeleted", false },
                { "academicRecords.semesters.courses.classSectionId", sectionObjectId }
            }),
            Stage("{ $unwind: '$academicRecords' }"),
            Stage("{ $unwind: '$academicRecords.semesters' }"),
            Stage("{ $unwind: '$academicRecords.semesters.courses' }"),
            new BsonDocument("$match", new BsonDocument("academicRecords.semesters.courses.classSectionId", sectionObjectId)),
            Stage("{ $unwind: '$academicRecords.semesters.courses.scores' }"),
            Stage("{ $unwind: '$academicRecords.semesters.courses.scores.cloMappings' }"),
            Stage("{ $addFields: { normalized: { $divide: [ { $ifNull: ['$academicRecords.semesters.courses.scores.score', 0] }, '$academicRecords.semesters.courses.scores.maxScore' ] }, validWeight: { $multiply: ['$academicRecords.semesters.courses.scores.weight', '$academicRecords.semesters.courses.scores.cloMappings.mappingWeight'] } } }"),
            Stage("{ $group: { _id: { studentId: '$_id', cloCode: '$academicRecords.semesters.courses.scores.cloMappings.cloCode' }, weighted: { $sum: { $multiply: ['$normalized', '$validWeight'] } }, totalWeight: { $sum: '$validWeight' } } }"),
            Stage("{ $addFields: { percentage: { $multiply: [ { $divide: ['$weighted', '$totalWeight'] }, 100 ] } } }"),
            Stage("{ $group: { _id: '$_id.cloCode', average: { $avg: '$percentage' }, values: { $push: '$percentage' }, total: { $sum: 1 } } }"),
            Stage("{ $sort: { _id: 1 } }"),
        };

        var docs = await db.Students.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);
        return docs.Select(x =>
        {
            var code = x["_id"].AsString;
            var threshold = thresholds.GetValueOrDefault(code, 50);
            var values = x["values"].AsBsonArray.Select(v => v.ToDouble()).ToList();
            var passed = values.Count(v => v >= threshold);
            return new ClassCloStatisticsDto(
                code,
                descriptions.GetValueOrDefault(code, $"Chuẩn đầu ra {code}"),
                Round(x["average"].ToDouble()),
                threshold,
                passed,
                values.Count,
                values.Count == 0 ? 0 : Round(passed * 100d / values.Count));
        }).ToList();
    }

    public async Task<IReadOnlyCollection<MaterialDto>> GetMaterialsAsync(string lecturerCode, string? classSectionId, CancellationToken ct)
    {
        var filter = Builders<LearningMaterial>.Filter.Eq(x => x.LecturerCode, lecturerCode) & Builders<LearningMaterial>.Filter.Eq(x => x.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(classSectionId)) filter &= Builders<LearningMaterial>.Filter.Eq(x => x.ClassSectionId, classSectionId);
        var items = await db.Materials.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
        return items.Select(MapMaterial).ToList();
    }

    public async Task<MaterialDto> SaveMaterialAsync(string lecturerCode, string? id, MaterialUpsertRequest request, CancellationToken ct)
    {
        var section = await RequireSectionAsync(lecturerCode, request.ClassSectionId, ct);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AppException("Tiêu đề tài liệu là bắt buộc");
        if (string.IsNullOrWhiteSpace(request.ResourceUrl))
            throw new AppException("Đường dẫn tài liệu là bắt buộc");
        if (request.Status is not ("Draft" or "Published" or "Hidden"))
            throw new AppException("Trạng thái tài liệu không hợp lệ");
        var visibleFrom = request.VisibleFrom ?? DateTime.UtcNow;
        if (request.VisibleUntil.HasValue
            && request.VisibleUntil.Value <= visibleFrom)
            throw new AppException("Thời điểm ẩn tài liệu phải sau thời điểm hiển thị");
        LearningMaterial item;
        if (string.IsNullOrWhiteSpace(id))
        {
            item = new LearningMaterial { LecturerCode = lecturerCode, ClassSectionId = section.Id, ClassSectionCode = section.ClassSectionCode, CourseCode = section.CourseCode, CourseName = section.CourseName };
        }
        else
        {
            item = await db.Materials.Find(x => x.Id == id && x.LecturerCode == lecturerCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy tài liệu");
        }
        item.ClassSectionId = section.Id;
        item.ClassSectionCode = section.ClassSectionCode;
        item.CourseCode = section.CourseCode;
        item.CourseName = section.CourseName;
        item.LecturerCode = lecturerCode;
        item.Title = request.Title.Trim();
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.Category = request.Category?.Trim() ?? string.Empty;
        item.Chapter = request.Chapter?.Trim() ?? string.Empty;
        item.ResourceType = request.ResourceType?.Trim() ?? "Link";
        item.ResourceUrl = request.ResourceUrl.Trim();
        item.VisibleFrom = visibleFrom;
        item.VisibleUntil = request.VisibleUntil;
        item.Status = request.Status;
        item.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(id)) await db.Materials.InsertOneAsync(item, cancellationToken: ct);
        else
        {
            var result = await db.Materials.ReplaceOneAsync(
                x => x.Id == item.Id && x.LecturerCode == lecturerCode && !x.IsDeleted,
                item,
                cancellationToken: ct);
            if (result.MatchedCount == 0)
                throw new ConflictException("Tài liệu đã thay đổi hoặc bị xóa. Vui lòng tải lại");
        }
        return MapMaterial(item);
    }

    public async Task DeleteMaterialAsync(string lecturerCode, string id, CancellationToken ct)
    {
        var result = await db.Materials.UpdateOneAsync(
            x => x.Id == id && x.LecturerCode == lecturerCode && !x.IsDeleted,
            Builders<LearningMaterial>.Update.Set(x => x.IsDeleted, true).Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);
        if (result.MatchedCount == 0) throw new NotFoundException("Không tìm thấy tài liệu");
    }

    public async Task<IReadOnlyCollection<AssignmentDto>> GetAssignmentsAsync(string lecturerCode, string? classSectionId, CancellationToken ct)
    {
        var filter = Builders<Assignment>.Filter.Eq(x => x.LecturerCode, lecturerCode) & Builders<Assignment>.Filter.Eq(x => x.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(classSectionId)) filter &= Builders<Assignment>.Filter.Eq(x => x.ClassSectionId, classSectionId);
        var items = await db.Assignments.Find(filter).SortByDescending(x => x.DueAt).ToListAsync(ct);
        var counts = await db.Submissions.Aggregate()
            .Match(x => !x.IsDeleted)
            .Group(x => x.AssignmentId, g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var lookup = counts.ToDictionary(x => x.Id, x => x.Count);
        return items.Select(x => MapAssignment(x, lookup.GetValueOrDefault(x.Id), null)).ToList();
    }

    public async Task<AssignmentDto> SaveAssignmentAsync(string lecturerCode, string? id, AssignmentUpsertRequest request, CancellationToken ct)
    {
        var section = await RequireSectionAsync(lecturerCode, request.ClassSectionId, ct);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AppException("Tiêu đề bài tập là bắt buộc");
        if (request.DueAt <= request.OpenAt) throw new AppException("Hạn nộp phải sau ngày mở");
        if (!double.IsFinite(request.MaxScore) || request.MaxScore <= 0)
            throw new AppException("Điểm tối đa phải lớn hơn 0");
        if (!double.IsFinite(request.LatePenaltyPercent)
            || request.LatePenaltyPercent is < 0 or > 100)
            throw new AppException("Tỷ lệ trừ điểm nộp trễ phải từ 0 đến 100");
        if (request.Status is not ("Draft" or "Open" or "Closed"))
            throw new AppException("Trạng thái bài tập không hợp lệ");
        Assignment item;
        if (string.IsNullOrWhiteSpace(id))
        {
            item = new Assignment { LecturerCode = lecturerCode, ClassSectionId = section.Id, ClassSectionCode = section.ClassSectionCode, CourseCode = section.CourseCode, CourseName = section.CourseName };
        }
        else
        {
            item = await db.Assignments.Find(x => x.Id == id && x.LecturerCode == lecturerCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy bài tập");
            if (item.ClassSectionId != section.Id)
            {
                var hasSubmissions = await db.Submissions.CountDocumentsAsync(
                    x => x.AssignmentId == item.Id && !x.IsDeleted,
                    cancellationToken: ct) > 0;
                if (hasSubmissions)
                    throw new ConflictException(
                        "Không thể chuyển bài tập sang lớp khác vì đã có bài nộp");
            }
        }
        item.ClassSectionId = section.Id;
        item.ClassSectionCode = section.ClassSectionCode;
        item.CourseCode = section.CourseCode;
        item.CourseName = section.CourseName;
        item.LecturerCode = lecturerCode;
        item.Title = request.Title.Trim();
        item.Content = request.Content?.Trim() ?? string.Empty;
        item.AttachmentUrl = request.AttachmentUrl?.Trim() ?? string.Empty;
        item.MaxScore = request.MaxScore;
        item.OpenAt = request.OpenAt;
        item.DueAt = request.DueAt;
        item.AllowLate = request.AllowLate;
        item.LatePenaltyPercent = request.LatePenaltyPercent;
        item.CloCodes = (request.CloCodes ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        item.LinkedComponentId = string.IsNullOrWhiteSpace(request.LinkedComponentId)
            ? null
            : request.LinkedComponentId.Trim();
        item.Status = request.Status;
        item.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(id)) await db.Assignments.InsertOneAsync(item, cancellationToken: ct);
        else
        {
            var result = await db.Assignments.ReplaceOneAsync(
                x => x.Id == item.Id && x.LecturerCode == lecturerCode && !x.IsDeleted,
                item,
                cancellationToken: ct);
            if (result.MatchedCount == 0)
                throw new ConflictException("Bài tập đã thay đổi hoặc bị xóa. Vui lòng tải lại");
        }
        var count = await db.Submissions.CountDocumentsAsync(x => x.AssignmentId == item.Id && !x.IsDeleted, cancellationToken: ct);
        return MapAssignment(item, (int)count, null);
    }

    public async Task DeleteAssignmentAsync(string lecturerCode, string id, CancellationToken ct)
    {
        _ = await RequireAssignmentAsync(lecturerCode, id, ct);
        var submissionCount = await db.Submissions.CountDocumentsAsync(
            x => x.AssignmentId == id && !x.IsDeleted,
            cancellationToken: ct);
        if (submissionCount > 0)
            throw new ConflictException(
                "Không thể xóa bài tập đã có bài nộp. Hãy chuyển trạng thái sang Đã đóng");
        var result = await db.Assignments.UpdateOneAsync(
            x => x.Id == id && x.LecturerCode == lecturerCode && !x.IsDeleted,
            Builders<Assignment>.Update.Set(x => x.IsDeleted, true).Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);
        if (result.MatchedCount == 0) throw new NotFoundException("Không tìm thấy bài tập");
    }

    public async Task<IReadOnlyCollection<SubmissionDto>> GetSubmissionsAsync(string lecturerCode, string assignmentId, CancellationToken ct)
    {
        _ = await RequireAssignmentAsync(lecturerCode, assignmentId, ct);
        var items = await db.Submissions.Find(x => x.AssignmentId == assignmentId && !x.IsDeleted)
            .SortBy(x => x.StudentCode)
            .ToListAsync(ct);
        return items.Select(MapSubmission).ToList();
    }

    public async Task GradeSubmissionAsync(string lecturerCode, string submissionId, GradeSubmissionRequest request, CancellationToken ct)
    {
        var submission = await db.Submissions.Find(x => x.Id == submissionId && !x.IsDeleted).FirstOrDefaultAsync(ct)
                         ?? throw new NotFoundException("Không tìm thấy bài nộp");
        var assignment = await RequireAssignmentAsync(lecturerCode, submission.AssignmentId, ct);
        if (!double.IsFinite(request.Score)
            || request.Score < 0
            || request.Score > assignment.MaxScore)
            throw new AppException($"Điểm phải từ 0 đến {assignment.MaxScore}");
        if (request.Status is not ("Graded" or "NeedsRevision" or "Accepted"))
            throw new AppException("Trạng thái chấm bài không hợp lệ");
        submission.Score = request.Score;
        submission.Feedback = request.Feedback?.Trim() ?? string.Empty;
        submission.ResubmissionAllowed = request.ResubmissionAllowed;
        submission.Status = request.Status;
        submission.GradedAt = DateTime.UtcNow;
        submission.GradedBy = lecturerCode;
        submission.UpdatedAt = DateTime.UtcNow;
        await db.Submissions.ReplaceOneAsync(x => x.Id == submission.Id, submission, cancellationToken: ct);

        if (!string.IsNullOrWhiteSpace(assignment.LinkedComponentId))
        {
            var linkedAssignments = await db.Assignments.Find(x => x.ClassSectionId == assignment.ClassSectionId && x.LinkedComponentId == assignment.LinkedComponentId && !x.IsDeleted).ToListAsync(ct);
            var linkedIds = linkedAssignments.Select(x => x.Id).ToList();
            var graded = await db.Submissions.Find(Builders<Submission>.Filter.In(x => x.AssignmentId, linkedIds) & Builders<Submission>.Filter.Eq(x => x.StudentId, submission.StudentId) & Builders<Submission>.Filter.Ne(x => x.Score, null) & Builders<Submission>.Filter.Eq(x => x.IsDeleted, false)).ToListAsync(ct);
            if (graded.Count > 0)
            {
                var normalizedAverage = graded.Average(x =>
                {
                    var source = linkedAssignments.First(a => a.Id == x.AssignmentId);
                    return (x.Score ?? 0) / source.MaxScore;
                });
                var student = await db.Students.Find(x => x.Id == submission.StudentId && !x.IsDeleted).FirstOrDefaultAsync(ct);
                var course = student?.AcademicRecords.SelectMany(x => x.Semesters).SelectMany(x => x.Courses).FirstOrDefault(x => x.ClassSectionId == assignment.ClassSectionId);
                var component = course?.Scores.FirstOrDefault(x => x.ComponentId == assignment.LinkedComponentId);
                if (student is not null && component is not null)
                {
                    component.Score = Math.Round(normalizedAverage * component.MaxScore, 2);
                    component.Status = "Graded";
                    student.UpdatedAt = DateTime.UtcNow;
                    await db.Students.ReplaceOneAsync(x => x.Id == student.Id, student, cancellationToken: ct);
                }
            }
        }
    }

    public async Task RequestReopenAsync(string lecturerCode, string classSectionId, string reason, CancellationToken ct)
    {
        var section = await RequireSectionAsync(lecturerCode, classSectionId, ct);
        if (section.GradeStatus is not ("Published" or "Locked")) throw new AppException("Chỉ có thể yêu cầu mở lại bảng điểm đã công bố hoặc đã khóa");
        var exists = await db.GradeReopenRequests.Find(x => x.ClassSectionId == classSectionId && x.Status == "Pending" && !x.IsDeleted).Limit(1).FirstOrDefaultAsync(ct) is not null;
        if (exists) throw new AppException("Đã có yêu cầu đang chờ xử lý");
        await db.GradeReopenRequests.InsertOneAsync(new GradeReopenRequest
        {
            ClassSectionId = section.Id,
            ClassSectionCode = section.ClassSectionCode,
            LecturerCode = lecturerCode,
            Reason = reason
        }, cancellationToken: ct);
    }

    public async Task<byte[]> ExportGradebookAsync(string lecturerCode, string classSectionId, CancellationToken ct)
    {
        var book = await gradebook.GetAsync(lecturerCode, classSectionId, ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bang diem");
        sheet.Cell(1, 1).Value = $"BẢNG ĐIỂM {book.ClassSectionCode} - {book.CourseName}";
        sheet.Range(1, 1, 1, book.Components.Count + 5).Merge().Style.Font.SetBold().Font.SetFontSize(15);
        sheet.Cell(3, 1).Value = "Mã SV";
        sheet.Cell(3, 2).Value = "Họ tên";
        var column = 3;
        foreach (var component in book.Components)
        {
            sheet.Cell(3, column++).Value = $"{component.ComponentName} ({component.Weight}%)";
        }
        sheet.Cell(3, column++).Value = "Tổng kết";
        sheet.Cell(3, column++).Value = "Điểm chữ";
        sheet.Cell(3, column).Value = "Kết quả";
        sheet.Row(3).Style.Font.SetBold();
        var row = 4;
        foreach (var student in book.Students)
        {
            sheet.Cell(row, 1).Value = student.StudentCode;
            sheet.Cell(row, 2).Value = student.FullName;
            column = 3;
            foreach (var component in book.Components) sheet.Cell(row, column++).Value = student.Scores.GetValueOrDefault(component.ComponentId);
            sheet.Cell(row, column++).Value = student.FinalScore;
            sheet.Cell(row, column++).Value = student.LetterGrade;
            sheet.Cell(row, column).Value = student.Passed ? "Đạt" : "Không đạt";
            row++;
        }
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ImportPreviewDto> ImportGradesAsync(string lecturerCode, string classSectionId, Microsoft.AspNetCore.Http.IFormFile file, bool commit, string userId, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException("File import rỗng");
        var section = await RequireSectionAsync(lecturerCode, classSectionId, ct);
        var book = await gradebook.GetAsync(lecturerCode, classSectionId, ct);
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var header = sheet.RowsUsed().FirstOrDefault(r => string.Equals(r.Cell(1).GetString().Trim(), "studentCode", StringComparison.OrdinalIgnoreCase));
        if (header is null) throw new AppException("Không tìm thấy dòng tiêu đề studentCode");
        var headers = header.CellsUsed().ToDictionary(x => x.GetString().Trim(), x => x.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        var results = new List<ImportRowResult>();
        var updates = new List<GradeUpdateStudent>();
        foreach (var row in sheet.RowsUsed().Where(x => x.RowNumber() > header.RowNumber()))
        {
            var code = row.Cell(headers["studentCode"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;
            var errors = new List<string>();
            var student = book.Students.FirstOrDefault(x => x.StudentCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (student is null) errors.Add("Sinh viên không thuộc lớp");
            var scores = new Dictionary<string, string?>();
            foreach (var component in book.Components)
            {
                if (!headers.TryGetValue(component.ComponentId, out var componentColumn))
                {
                    errors.Add($"Thiếu cột {component.ComponentId}");
                    continue;
                }

                var cell = row.Cell(componentColumn);
                if (cell.IsEmpty())
                {
                    scores[component.ComponentId] = null;
                    continue;
                }

                var rawScore = cell.GetFormattedString().Trim();
                try
                {
                    var normalized = scoreNormalizer.Normalize(rawScore, (decimal)component.MaxScore);
                    if (normalized.RequiresConfirmation)
                    {
                        errors.Add($"{component.ComponentId}: giá trị {rawScore} cần xác nhận thủ công trước khi import");
                        continue;
                    }

                    scores[component.ComponentId] = rawScore;
                }
                catch (AppException ex)
                {
                    errors.Add($"{component.ComponentId}: {ex.Message}");
                }
            }
            var data = new Dictionary<string, object?> { ["studentCode"] = code, ["fullName"] = student?.FullName };
            foreach (var value in scores) data[value.Key] = value.Value;
            results.Add(new ImportRowResult(row.RowNumber(), errors.Count == 0, errors, data));
            if (student is not null && errors.Count == 0) updates.Add(new GradeUpdateStudent(student.StudentId, scores));
        }
        if (commit)
        {
            if (results.Any(x => !x.Valid)) throw new AppException("File còn dòng không hợp lệ; hãy sửa trước khi import");
            await gradebook.UpdateAsync(lecturerCode, section.Id, new GradeUpdateRequest(updates, false), userId, ct);
        }
        return new ImportPreviewDto(results.Count, results.Count(x => x.Valid), results.Count(x => !x.Valid), results);
    }

    private async Task<ClassSection> RequireSectionAsync(string lecturerCode, string id, CancellationToken ct) =>
        await db.ClassSections.Find(x => x.Id == id && x.LecturerCode == lecturerCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Không tìm thấy lớp học phần được phân công");

    private async Task<Assignment> RequireAssignmentAsync(string lecturerCode, string id, CancellationToken ct) =>
        await db.Assignments.Find(x => x.Id == id && x.LecturerCode == lecturerCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Không tìm thấy bài tập");

    private static LecturerClassDto MapClass(ClassSection x) => new(x.Id, x.ClassSectionCode, x.CourseCode, x.CourseName, x.AcademicYearName, x.SemesterName, x.Students.Count, x.GradeStatus, x.Schedule, x.StartDate, x.EndDate);
    private static MaterialDto MapMaterial(LearningMaterial x) => new(x.Id, x.ClassSectionId, x.ClassSectionCode, x.CourseCode, x.CourseName, x.Title, x.Description, x.Category, x.Chapter, x.ResourceType, x.ResourceUrl, x.VisibleFrom, x.VisibleUntil, x.ViewCount, x.DownloadCount, x.Status);
    private static AssignmentDto MapAssignment(Assignment x, int count, Submission? submission) => new(x.Id, x.ClassSectionId, x.ClassSectionCode, x.CourseCode, x.CourseName, x.Title, x.Content, x.AttachmentUrl, x.MaxScore, x.OpenAt, x.DueAt, x.AllowLate, x.LatePenaltyPercent, x.CloCodes, x.LinkedComponentId, x.Status, count, submission?.Status, submission?.Score, submission?.Feedback, submission?.ResubmissionAllowed ?? false);
    private static SubmissionDto MapSubmission(Submission x) => new(x.Id, x.AssignmentId, x.StudentId, x.StudentCode, x.StudentName, x.TextContent, x.Files, x.SubmittedAt, x.IsLate, x.Status, x.Score, x.Feedback, x.ResubmissionAllowed);
    private static GradebookStudentDto MapScoreRow(BsonValue value)
    {
        var x = value.AsBsonDocument;
        var score = Round(x["finalScore"].ToDouble());
        return new GradebookStudentDto(x["_id"].ToString(), x["studentCode"].AsString, x["fullName"].AsString, new Dictionary<string, double?>(), score, Letter(score), x.GetValue("passed", BsonBoolean.False).AsBoolean);
    }
    private static ClassStatisticsDto EmptyStatistics(ClassSection section) => new(section.Id, section.ClassSectionCode, section.CourseName, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], []);
    private static BsonDocument Stage(string json) => BsonDocument.Parse(json);
    private static double Round(double value) => Math.Round(value, 2);
    private static string Letter(double s) => s >= 8.5 ? "A" : s >= 8 ? "B+" : s >= 7 ? "B" : s >= 6.5 ? "C+" : s >= 5.5 ? "C" : s >= 5 ? "D+" : s >= 4 ? "D" : "F";
}
