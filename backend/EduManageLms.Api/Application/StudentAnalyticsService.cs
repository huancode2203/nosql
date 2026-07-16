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
        var pipeline = new List<BsonDocument>
        {
            new("$match", new BsonDocument
            {
                { "studentCode", studentCode },
                { "isDeleted", false }
            }),
            Stage("""{ "$unwind": "$academicRecords" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses" }""")
        };

        if (!string.IsNullOrWhiteSpace(year))
        {
            pipeline.Add(new("$match", new BsonDocument("academicRecords.academicYearName", year)));
        }

        if (!string.IsNullOrWhiteSpace(semester))
        {
            pipeline.Add(new("$match", new BsonDocument("academicRecords.semesters.semesterCode", semester)));
        }

        pipeline.AddRange(
        [
            Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores" }"""),
            Stage("""
            {
              "$addFields": {
                "componentWeighted": {
                  "$multiply": [
                    { "$divide": [
                      { "$ifNull": ["$academicRecords.semesters.courses.scores.score", 0] },
                      "$academicRecords.semesters.courses.scores.maxScore"
                    ]},
                    10,
                    { "$divide": ["$academicRecords.semesters.courses.scores.weight", 100] }
                  ]
                }
              }
            }
            """),
            Stage("""
            {
              "$group": {
                "_id": {
                  "courseId": "$academicRecords.semesters.courses.courseId",
                  "classSectionId": "$academicRecords.semesters.courses.classSectionId"
                },
                "course": { "$first": "$academicRecords.semesters.courses" },
                "scores": { "$push": "$academicRecords.semesters.courses.scores" },
                "rawFinal": { "$sum": "$componentWeighted" }
              }
            }
            """),
            Stage("""
            {
              "$addFields": {
                "finalScore": { "$round": ["$rawFinal", 2] },
                "grade": {
                  "$switch": {
                    "branches": [
                      { "case": { "$gte": ["$rawFinal", 8.5] }, "then": { "letter": "A",  "point": 4.0 } },
                      { "case": { "$gte": ["$rawFinal", 8.0] }, "then": { "letter": "B+", "point": 3.5 } },
                      { "case": { "$gte": ["$rawFinal", 7.0] }, "then": { "letter": "B",  "point": 3.0 } },
                      { "case": { "$gte": ["$rawFinal", 6.5] }, "then": { "letter": "C+", "point": 2.5 } },
                      { "case": { "$gte": ["$rawFinal", 5.5] }, "then": { "letter": "C",  "point": 2.0 } },
                      { "case": { "$gte": ["$rawFinal", 5.0] }, "then": { "letter": "D+", "point": 1.5 } },
                      { "case": { "$gte": ["$rawFinal", 4.0] }, "then": { "letter": "D",  "point": 1.0 } }
                    ],
                    "default": { "letter": "F", "point": 0.0 }
                  }
                }
              }
            }
            """),
            Stage("""
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
                "passed": { "$gte": ["$finalScore", 4] },
                "publishedAt": "$course.publishedAt"
              }
            }
            """),
            Stage("""{ "$sort": { "courseCode": 1 } }""")
        ]);

        var documents = await db.Students.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);
        return documents.Select(MapGrade).ToList();
    }

    public async Task<StudentGpaDto> GetCumulativeGpaAsync(string studentCode, CancellationToken ct)
    {
        var pipeline = new List<BsonDocument>
        {
            new("$match", new BsonDocument
            {
                { "studentCode", studentCode },
                { "isDeleted", false }
            }),
            Stage("""{ "$unwind": "$academicRecords" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores" }"""),
            Stage("""
            {
              "$addFields": {
                "componentWeighted": {
                  "$multiply": [
                    { "$divide": [
                      { "$ifNull": ["$academicRecords.semesters.courses.scores.score", 0] },
                      "$academicRecords.semesters.courses.scores.maxScore"
                    ]},
                    10,
                    { "$divide": ["$academicRecords.semesters.courses.scores.weight", 100] }
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
                  "attempt": "$academicRecords.semesters.courses.attemptNumber"
                },
                "credits": { "$first": "$academicRecords.semesters.courses.credits" },
                "finalScore": { "$sum": "$componentWeighted" }
              }
            }
            """),
            Stage("""{ "$sort": { "_id.courseCode": 1, "_id.attempt": -1 } }"""),
            // Chính sách mặc định: môn học lại lấy kết quả lần học mới nhất.
            Stage("""
            {
              "$group": {
                "_id": "$_id.courseCode",
                "credits": { "$first": "$credits" },
                "finalScore": { "$first": "$finalScore" }
              }
            }
            """),
            Stage("""
            {
              "$addFields": {
                "gradePoint": {
                  "$switch": {
                    "branches": [
                      { "case": { "$gte": ["$finalScore", 8.5] }, "then": 4.0 },
                      { "case": { "$gte": ["$finalScore", 8.0] }, "then": 3.5 },
                      { "case": { "$gte": ["$finalScore", 7.0] }, "then": 3.0 },
                      { "case": { "$gte": ["$finalScore", 6.5] }, "then": 2.5 },
                      { "case": { "$gte": ["$finalScore", 5.5] }, "then": 2.0 },
                      { "case": { "$gte": ["$finalScore", 5.0] }, "then": 1.5 },
                      { "case": { "$gte": ["$finalScore", 4.0] }, "then": 1.0 }
                    ],
                    "default": 0.0
                  }
                }
              }
            }
            """),
            Stage("""
            {
              "$group": {
                "_id": null,
                "weightedPoints": { "$sum": { "$multiply": ["$gradePoint", "$credits"] } },
                "weightedScore10": { "$sum": { "$multiply": ["$finalScore", "$credits"] } },
                "totalCredits": { "$sum": "$credits" },
                "passedCredits": {
                  "$sum": { "$cond": [{ "$gte": ["$finalScore", 4] }, "$credits", 0] }
                }
              }
            }
            """),
            Stage("""
            {
              "$project": {
                "_id": 0,
                "gpa": { "$round": [{ "$divide": ["$weightedPoints", "$totalCredits"] }, 2] },
                "average10": { "$round": [{ "$divide": ["$weightedScore10", "$totalCredits"] }, 2] },
                "totalCredits": 1,
                "passedCredits": 1
              }
            }
            """),
            Stage("""
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
            """)
        };

        var document = await db.Students.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(ct);
        return document is null
            ? new StudentGpaDto(0, 0, 0, 0, "Chưa có dữ liệu")
            : new StudentGpaDto(
                document["gpa"].ToDouble(),
                document["average10"].ToDouble(),
                document["totalCredits"].ToInt32(),
                document["passedCredits"].ToInt32(),
                document["classification"].AsString);
    }

    public async Task<IReadOnlyCollection<CloResultDto>> GetCloAsync(
        string studentCode,
        CancellationToken ct)
    {
        var pipeline = new List<BsonDocument>
        {
            new("$match", new BsonDocument
            {
                { "studentCode", studentCode },
                { "isDeleted", false }
            }),
            Stage("""{ "$unwind": "$academicRecords" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores" }"""),
            Stage("""{ "$unwind": "$academicRecords.semesters.courses.scores.cloMappings" }"""),
            Stage("""
            {
              "$addFields": {
                "normalized": {
                  "$divide": [
                    { "$ifNull": ["$academicRecords.semesters.courses.scores.score", 0] },
                    "$academicRecords.semesters.courses.scores.maxScore"
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
                    { "$multiply": [{ "$divide": ["$weighted", "$totalWeight"] }, 100] },
                    2
                  ]
                }
              }
            }
            """),
            Stage("""
            {
              "$project": {
                "_id": 0,
                "courseCode": "$_id.courseCode",
                "courseName": "$_id.courseName",
                "cloCode": "$_id.cloCode",
                "description": { "$concat": ["Chuẩn đầu ra ", "$_id.cloCode"] },
                "percentage": 1,
                "threshold": { "$literal": 50 },
                "passed": { "$gte": ["$percentage", 50] },
                "components": 1
              }
            }
            """)
        };

        var documents = await db.Students.Aggregate<BsonDocument>(pipeline).ToListAsync(ct);
        return documents.Select(document => new CloResultDto(
            document["courseCode"].AsString,
            document["courseName"].AsString,
            document["cloCode"].AsString,
            document["description"].AsString,
            document["percentage"].ToDouble(),
            50,
            document["passed"].AsBoolean,
            document["components"].AsBsonArray.Select(x => x.AsString).ToList())).ToList();
    }

    private static BsonDocument Stage(string json) => BsonDocument.Parse(json);

    private static CourseGradeDto MapGrade(BsonDocument document)
    {
        var scores = document["scores"].AsBsonArray.Select(value =>
        {
            var score = value.AsBsonDocument;
            return new GradeComponentDto(
                score["componentId"].AsString,
                score["componentName"].AsString,
                score["weight"].ToDouble(),
                score["maxScore"].ToDouble(),
                score.GetValue("score", BsonNull.Value).IsBsonNull ? null : score["score"].ToDouble(),
                score["status"].AsString);
        }).ToList();

        DateTime? publishedAt = null;
        if (document.TryGetValue("publishedAt", out var value) && value.IsValidDateTime)
        {
            publishedAt = value.ToUniversalTime();
        }

        return new CourseGradeDto(
            document["courseId"].ToString(),
            document["courseCode"].AsString,
            document["courseName"].AsString,
            document["credits"].ToInt32(),
            document["classSectionCode"].AsString,
            document["lecturerName"].AsString,
            scores,
            document["finalScore"].ToDouble(),
            document["letterGrade"].AsString,
            document["gradePoint"].ToDouble(),
            document["passed"].AsBoolean,
            publishedAt);
    }
}
