using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class StudentAnalyticsService(MongoContext db) : IStudentAnalyticsService
{
    public async Task<IReadOnlyCollection<CourseGradeDto>> GetGradesAsync(
        string studentCode,
        string? year,
        string? semester,
        CancellationToken ct)
    {
        var courses = await LoadCoursesAsync(ct);
        var pipeline = BaseCoursePipeline(studentCode, year, semester);
        pipeline.AddRange(BuildCourseScoreStages(courses));
        pipeline.Add(Stage("""{ "$sort": { "course.courseCode": 1 } }"""));
        pipeline.Add(Stage("""
        {
          "$project": {
            "_id": 0,
            "courseId": "$course.courseId",
            "courseCode": "$course.courseCode",
            "courseName": "$course.courseName",
            "credits": "$course.credits",
            "classSectionCode": "$course.classSectionCode",
            "lecturerName": "$course.lecturer.fullName",
            "scores": 1,
            "finalScore": 1,
            "letterGrade": "$grade.letter",
            "gradePoint": "$grade.point",
            "classification": "$grade.classification",
            "passed": 1,
            "publishedAt": "$course.publishedAt"
          }
        }
        """));

        var documents = await db.Students.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);
        return documents.Select(MapGrade).ToList();
    }

    public async Task<IReadOnlyCollection<TranscriptTermDto>> GetTranscriptAsync(string studentCode, CancellationToken ct)
    {
        var courses = await LoadCoursesAsync(ct);
        var pipeline = BaseCoursePipeline(studentCode, null, null);
        pipeline.AddRange(BuildCourseScoreStages(courses));
        pipeline.Add(Stage("""
        {
          "$group": {
            "_id": {
              "year": "$_id.year",
              "semesterCode": "$_id.semesterCode",
              "semesterName": "$_id.semesterName"
            },
            "courses": {
              "$push": {
                "courseId": "$course.courseId",
                "courseCode": "$course.courseCode",
                "courseName": "$course.courseName",
                "credits": "$course.credits",
                "classSectionCode": "$course.classSectionCode",
                "lecturerName": "$course.lecturer.fullName",
                "scores": "$scores",
                "finalScore": "$finalScore",
                "letterGrade": "$grade.letter",
                "gradePoint": "$grade.point",
                "classification": "$grade.classification",
                "passed": "$passed",
                "publishedAt": "$course.publishedAt"
              }
            },
            "weightedPoints": { "$sum": { "$cond": ["$course.excludeFromGpa", 0, { "$multiply": ["$grade.point", "$course.credits"] }] } },
            "weighted10": { "$sum": { "$cond": ["$course.excludeFromGpa", 0, { "$multiply": ["$finalScore", "$course.credits"] }] } },
            "gpaCredits": { "$sum": { "$cond": ["$course.excludeFromGpa", 0, "$course.credits"] } },
            "totalCredits": { "$sum": "$course.credits" },
            "passedCredits": { "$sum": { "$cond": ["$passed", "$course.credits", 0] } }
          }
        }
        """));
        pipeline.Add(Stage("""
        {
          "$project": {
            "_id": 0,
            "academicYear": "$_id.year",
            "semesterCode": "$_id.semesterCode",
            "semesterName": "$_id.semesterName",
            "courses": 1,
            "gpa": {
              "$round": [
                { "$cond": [{ "$gt": ["$gpaCredits", 0] }, { "$divide": ["$weightedPoints", "$gpaCredits"] }, 0] },
                2
              ]
            },
            "average10": {
              "$round": [
                { "$cond": [{ "$gt": ["$gpaCredits", 0] }, { "$divide": ["$weighted10", "$gpaCredits"] }, 0] },
                2
              ]
            },
            "totalCredits": 1,
            "passedCredits": 1
          }
        }
        """));
        pipeline.Add(Stage("""{ "$sort": { "academicYear": -1, "semesterCode": -1 } }"""));

        var documents = await db.Students.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);
        return documents.Select(MapTerm).ToList();
    }

    public async Task<StudentGpaDto> GetCumulativeGpaAsync(string studentCode, CancellationToken ct)
    {
        var courses = await LoadCoursesAsync(ct);
        var repeatSetting = await db.SystemSettings
            .Find(x => x.Key == "grade.repeatPolicy" && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        var repeatPolicy = repeatSetting?.Value?.ToString() ?? "Latest";

        var pipeline = BaseCoursePipeline(studentCode, null, null);
        pipeline.AddRange(BuildCourseScoreStages(courses));
        pipeline.Add(new BsonDocument("$sort", repeatPolicy.Equals("Highest", StringComparison.OrdinalIgnoreCase)
            ? new BsonDocument { { "course.courseCode", 1 }, { "finalScore", -1 }, { "course.attemptNumber", -1 } }
            : new BsonDocument { { "course.courseCode", 1 }, { "course.attemptNumber", -1 }, { "finalScore", -1 } }));
        pipeline.Add(Stage("""
        {
          "$group": {
            "_id": "$course.courseCode",
            "credits": { "$first": "$course.credits" },
            "excludeFromGpa": { "$first": "$course.excludeFromGpa" },
            "finalScore": { "$first": "$finalScore" },
            "gradePoint": { "$first": "$grade.point" },
            "passed": { "$first": "$passed" }
          }
        }
        """));
        pipeline.Add(Stage("""
        {
          "$group": {
            "_id": null,
            "weightedPoints": { "$sum": { "$cond": ["$excludeFromGpa", 0, { "$multiply": ["$gradePoint", "$credits"] }] } },
            "weightedScore10": { "$sum": { "$cond": ["$excludeFromGpa", 0, { "$multiply": ["$finalScore", "$credits"] }] } },
            "gpaCredits": { "$sum": { "$cond": ["$excludeFromGpa", 0, "$credits"] } },
            "totalCredits": { "$sum": "$credits" },
            "passedCredits": { "$sum": { "$cond": ["$passed", "$credits", 0] } }
          }
        }
        """));
        pipeline.Add(Stage("""
        {
          "$project": {
            "_id": 0,
            "gpa": {
              "$round": [
                { "$cond": [{ "$gt": ["$gpaCredits", 0] }, { "$divide": ["$weightedPoints", "$gpaCredits"] }, 0] },
                2
              ]
            },
            "average10": {
              "$round": [
                { "$cond": [{ "$gt": ["$gpaCredits", 0] }, { "$divide": ["$weightedScore10", "$gpaCredits"] }, 0] },
                2
              ]
            },
            "totalCredits": 1,
            "passedCredits": 1
          }
        }
        """));
        pipeline.Add(Stage("""
        {
          "$addFields": {
            "classification": {
              "$switch": {
                "branches": [
                  { "case": { "$gte": ["$gpa", 3.6] }, "then": "Xuất sắc" },
                  { "case": { "$gte": ["$gpa", 3.2] }, "then": "Giỏi" },
                  { "case": { "$gte": ["$gpa", 2.5] }, "then": "Khá" },
                  { "case": { "$gte": ["$gpa", 2.0] }, "then": "Trung bình" },
                  { "case": { "$gte": ["$gpa", 1.0] }, "then": "Yếu" }
                ],
                "default": "Kém"
              }
            }
          }
        }
        """));

        var document = await db.Students.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(ct);
        return document is null
            ? new StudentGpaDto(0, 0, 0, 0, "Chưa có dữ liệu công bố")
            : new StudentGpaDto(
                document.GetValue("gpa", 0).ToDouble(),
                document.GetValue("average10", 0).ToDouble(),
                document.GetValue("totalCredits", 0).ToInt32(),
                document.GetValue("passedCredits", 0).ToInt32(),
                document.GetValue("classification", "Chưa xếp loại").AsString);
    }

    public async Task<IReadOnlyCollection<CloResultDto>> GetCloAsync(string studentCode, CancellationToken ct)
    {
        var courses = await LoadCoursesAsync(ct);
        var thresholdSwitch = BuildCloValueSwitch(courses, true);
        var descriptionSwitch = BuildCloValueSwitch(courses, false);
        var pipeline = new List<BsonDocument>
        {
            new("$match", new BsonDocument { { "studentCode", studentCode }, { "isDeleted", false } }),
            Stage("""{ "$unwind": "$academicRecords" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses" }"""),
            Stage("""{ "$match": { "academicRecords.semesters.courses.scoreStatus": "Published" } }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores" }"""),
            Stage("""
            {
              "$match": {
                "academicRecords.semesters.courses.scores.score": { "$ne": null },
                "academicRecords.semesters.courses.scores.status": "Graded"
              }
            }
            """),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores.cloMappings" }"""),
            Stage("""
            {
              "$addFields": {
                "normalized": {
                  "$cond": [
                    { "$gt": ["$academicRecords.semesters.courses.scores.maxScore", 0] },
                    { "$divide": ["$academicRecords.semesters.courses.scores.score", "$academicRecords.semesters.courses.scores.maxScore"] },
                    0
                  ]
                },
                "validWeight": {
                  "$multiply": [
                    "$academicRecords.semesters.courses.scores.weight",
                    "$academicRecords.semesters.courses.scores.cloMappings.mappingWeight"
                  ]
                }
              }
            }
            """),
            Stage("""
            {
              "$group": {
                "_id": {
                  "courseCode": "$academicRecords.semesters.courses.courseCode",
                  "courseName": "$academicRecords.semesters.courses.courseName",
                  "cloCode": "$academicRecords.semesters.courses.scores.cloMappings.cloCode"
                },
                "weighted": { "$sum": { "$multiply": ["$normalized", "$validWeight"] } },
                "totalWeight": { "$sum": "$validWeight" },
                "components": { "$addToSet": "$academicRecords.semesters.courses.scores.componentName" }
              }
            }
            """),
            Stage("""
            {
              "$addFields": {
                "percentage": {
                  "$round": [
                    { "$cond": [{ "$gt": ["$totalWeight", 0] }, { "$multiply": [{ "$divide": ["$weighted", "$totalWeight"] }, 100] }, 0] },
                    2
                  ]
                }
              }
            }
            """),
            new("$addFields", new BsonDocument
            {
                { "threshold", thresholdSwitch },
                { "description", descriptionSwitch }
            }),
            Stage("""{ "$addFields": { "passed": { "$gte": ["$percentage", "$threshold"] } } }"""),
            Stage("""
            {
              "$project": {
                "_id": 0,
                "courseCode": "$_id.courseCode",
                "courseName": "$_id.courseName",
                "cloCode": "$_id.cloCode",
                "description": 1,
                "percentage": 1,
                "threshold": 1,
                "passed": 1,
                "components": 1
              }
            }
            """),
            Stage("""{ "$sort": { "courseCode": 1, "cloCode": 1 } }""")
        };

        var documents = await db.Students.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);
        return documents.Select(document => new CloResultDto(
            document["courseCode"].AsString,
            document["courseName"].AsString,
            document["cloCode"].AsString,
            document.GetValue("description", $"Chuẩn đầu ra {document["cloCode"].AsString}").AsString,
            document.GetValue("percentage", 0).ToDouble(),
            document.GetValue("threshold", 50).ToDouble(),
            document.GetValue("passed", false).AsBoolean,
            document["components"].AsBsonArray.Select(x => x.AsString).ToList())).ToList();
    }

    private async Task<IReadOnlyCollection<Course>> LoadCoursesAsync(CancellationToken ct) =>
        await db.Courses.Find(x => !x.IsDeleted).ToListAsync(ct);

    private static List<BsonDocument> BaseCoursePipeline(string studentCode, string? year, string? semester)
    {
        var pipeline = new List<BsonDocument>
        {
            new("$match", new BsonDocument { { "studentCode", studentCode }, { "isDeleted", false } }),
            Stage("""{ "$unwind": "$academicRecords" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses" }""")
        };
        if (!string.IsNullOrWhiteSpace(year))
            pipeline.Add(new BsonDocument("$match", new BsonDocument("academicRecords.academicYearName", year)));
        if (!string.IsNullOrWhiteSpace(semester))
            pipeline.Add(new BsonDocument("$match", new BsonDocument("academicRecords.semesters.semesterCode", semester)));
        pipeline.Add(Stage("""{ "$match": { "academicRecords.semesters.courses.scoreStatus": "Published" } }"""));
        pipeline.Add(Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores" }"""));
        return pipeline;
    }

    private static IEnumerable<BsonDocument> BuildCourseScoreStages(IReadOnlyCollection<Course> courses)
    {
        yield return Stage("""
        {
          "$addFields": {
            "componentWeighted": {
              "$multiply": [
                {
                  "$cond": [
                    { "$gt": ["$academicRecords.semesters.courses.scores.maxScore", 0] },
                    {
                      "$divide": [
                        { "$ifNull": ["$academicRecords.semesters.courses.scores.score", 0] },
                        "$academicRecords.semesters.courses.scores.maxScore"
                      ]
                    },
                    0
                  ]
                },
                10,
                { "$divide": ["$academicRecords.semesters.courses.scores.weight", 100] }
              ]
            }
          }
        }
        """);
        yield return Stage("""
        {
          "$group": {
            "_id": {
              "year": "$academicRecords.academicYearName",
              "semesterCode": "$academicRecords.semesters.semesterCode",
              "semesterName": "$academicRecords.semesters.semesterName",
              "courseCode": "$academicRecords.semesters.courses.courseCode",
              "classSectionId": "$academicRecords.semesters.courses.classSectionId",
              "attemptNumber": "$academicRecords.semesters.courses.attemptNumber"
            },
            "course": { "$first": "$academicRecords.semesters.courses" },
            "scores": { "$push": "$academicRecords.semesters.courses.scores" },
            "rawFinal": { "$sum": "$componentWeighted" }
          }
        }
        """);
        yield return Stage("""{ "$addFields": { "finalScore": { "$round": ["$rawFinal", 2] } } }""");
        yield return new BsonDocument("$addFields", new BsonDocument
        {
            { "grade", BuildGradeSwitch(courses) },
            { "passingScore", BuildPassingScoreSwitch(courses) },
            { "requiredFailures", Stage("""
              {
                "$size": {
                  "$filter": {
                    "input": "$scores",
                    "as": "score",
                    "cond": {
                      "$and": [
                        { "$eq": ["$$score.isRequired", true] },
                        { "$ne": ["$$score.minimumScore", null] },
                        { "$lt": [{ "$ifNull": ["$$score.score", -1] }, "$$score.minimumScore"] }
                      ]
                    }
                  }
                }
              }
              """) }
        });
        yield return Stage("""
        {
          "$addFields": {
            "passed": {
              "$and": [
                { "$gte": ["$finalScore", "$passingScore"] },
                { "$eq": ["$requiredFailures", 0] }
              ]
            }
          }
        }
        """);
    }

    private static BsonDocument BuildGradeSwitch(IReadOnlyCollection<Course> courses)
    {
        var branches = new BsonArray();
        foreach (var course in courses)
        {
            var scale = course.GradeScale.Count > 0 ? course.GradeScale : DefaultScale();
            foreach (var item in scale)
            {
                branches.Add(new BsonDocument
                {
                    { "case", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$course.courseCode", course.CourseCode }),
                            new BsonDocument("$gte", new BsonArray { "$finalScore", item.Min }),
                            new BsonDocument("$lte", new BsonArray { "$finalScore", item.Max + 0.000001 })
                        }) },
                    { "then", new BsonDocument
                        {
                            { "letter", item.Letter },
                            { "point", item.GradePoint },
                            { "classification", item.Classification }
                        } }
                });
            }
        }

        return new BsonDocument("$switch", new BsonDocument
        {
            { "branches", branches },
            { "default", BuildDefaultGradeSwitch() }
        });
    }

    private static BsonDocument BuildDefaultGradeSwitch()
    {
        var branches = new BsonArray(DefaultScale().Select(item => new BsonDocument
        {
            { "case", new BsonDocument("$gte", new BsonArray { "$finalScore", item.Min }) },
            { "then", new BsonDocument
                {
                    { "letter", item.Letter },
                    { "point", item.GradePoint },
                    { "classification", item.Classification }
                } }
        }));
        return new BsonDocument("$switch", new BsonDocument
        {
            { "branches", branches },
            { "default", new BsonDocument { { "letter", "F" }, { "point", 0.0 }, { "classification", "Kém" } } }
        });
    }

    private static BsonDocument BuildPassingScoreSwitch(IReadOnlyCollection<Course> courses)
    {
        var branches = new BsonArray();
        foreach (var course in courses)
        {
            foreach (var scheme in course.GradingSchemes)
            {
                branches.Add(new BsonDocument
                {
                    { "case", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$course.courseCode", course.CourseCode }),
                            new BsonDocument("$eq", new BsonArray { "$course.gradingSchemeVersion", scheme.Version })
                        }) },
                    { "then", scheme.PassingScore }
                });
            }
        }
        return new BsonDocument("$switch", new BsonDocument { { "branches", branches }, { "default", 4.0 } });
    }

    private static BsonValue BuildCloValueSwitch(IReadOnlyCollection<Course> courses, bool threshold)
    {
        var branches = new BsonArray();
        foreach (var course in courses)
        {
            foreach (var clo in course.Clos.Where(x => x.Active))
            {
                branches.Add(new BsonDocument
                {
                    { "case", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$_id.courseCode", course.CourseCode }),
                            new BsonDocument("$eq", new BsonArray { "$_id.cloCode", clo.CloCode })
                        }) },
                    { "then", threshold ? new BsonDouble(clo.Threshold) : new BsonString(clo.Description) }
                });
            }
        }
        return new BsonDocument("$switch", new BsonDocument
        {
            { "branches", branches },
            { "default", threshold ? new BsonDouble(50) : new BsonString("Chuẩn đầu ra học phần") }
        });
    }

    private static IReadOnlyCollection<GradeScaleItem> DefaultScale() =>
    [
        new() { Min = 8.5, Max = 10, Letter = "A", GradePoint = 4.0, Classification = "Giỏi" },
        new() { Min = 8.0, Max = 8.49, Letter = "B+", GradePoint = 3.5, Classification = "Khá" },
        new() { Min = 7.0, Max = 7.99, Letter = "B", GradePoint = 3.0, Classification = "Khá" },
        new() { Min = 6.5, Max = 6.99, Letter = "C+", GradePoint = 2.5, Classification = "Trung bình khá" },
        new() { Min = 5.5, Max = 6.49, Letter = "C", GradePoint = 2.0, Classification = "Trung bình" },
        new() { Min = 5.0, Max = 5.49, Letter = "D+", GradePoint = 1.5, Classification = "Trung bình yếu" },
        new() { Min = 4.0, Max = 4.99, Letter = "D", GradePoint = 1.0, Classification = "Yếu" },
        new() { Min = 0, Max = 3.99, Letter = "F", GradePoint = 0, Classification = "Kém" }
    ];

    private static BsonDocument Stage(string json) => BsonDocument.Parse(json);

    private static TranscriptTermDto MapTerm(BsonDocument document)
    {
        var courses = document["courses"].AsBsonArray.Select(value => MapGrade(value.AsBsonDocument)).ToList();
        return new TranscriptTermDto(
            document.GetValue("academicYear", "").AsString,
            document.GetValue("semesterCode", "").AsString,
            document.GetValue("semesterName", "").AsString,
            courses,
            document.GetValue("gpa", 0).ToDouble(),
            document.GetValue("average10", 0).ToDouble(),
            document.GetValue("totalCredits", 0).ToInt32(),
            document.GetValue("passedCredits", 0).ToInt32());
    }

    private static CourseGradeDto MapGrade(BsonDocument document)
    {
        var scores = document["scores"].AsBsonArray.Select(value =>
        {
            var score = value.AsBsonDocument;
            return new GradeComponentDto(
                score.GetValue("componentId", "").AsString,
                score.GetValue("componentName", "").AsString,
                score.GetValue("weight", 0).ToDouble(),
                score.GetValue("maxScore", 10).ToDouble(),
                score.GetValue("score", BsonNull.Value).IsBsonNull ? null : score["score"].ToDouble(),
                score.GetValue("status", "NotGraded").AsString);
        }).ToList();

        DateTime? publishedAt = null;
        if (document.TryGetValue("publishedAt", out var value) && value.IsValidDateTime)
            publishedAt = value.ToUniversalTime();

        return new CourseGradeDto(
            document.GetValue("courseId", "").ToString(),
            document.GetValue("courseCode", "").AsString,
            document.GetValue("courseName", "").AsString,
            document.GetValue("credits", 0).ToInt32(),
            document.GetValue("classSectionCode", "").AsString,
            document.GetValue("lecturerName", "").AsString,
            scores,
            document.GetValue("finalScore", 0).ToDouble(),
            document.GetValue("letterGrade", "F").AsString,
            document.GetValue("gradePoint", 0).ToDouble(),
            document.GetValue("classification", "Chưa xếp loại").AsString,
            document.GetValue("passed", false).AsBoolean,
            publishedAt);
    }
}
