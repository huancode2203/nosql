using EduManageLms.Api.Domain;

namespace EduManageLms.Api.Application;

public sealed record GradeEvaluation(double FinalScore, string LetterGrade, double GradePoint, string Classification, bool Passed);

public static class GradePolicy
{
    private static readonly IReadOnlyCollection<GradeScaleItem> DefaultScale =
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

    public static double CalculateFinal(GradingSchemeVersion scheme, IReadOnlyDictionary<string, double?> scores)
    {
        var raw = scheme.Components.Sum(component =>
        {
            var value = scores.GetValueOrDefault(component.ComponentId) ?? 0;
            if (component.MaxScore <= 0) return 0;
            return value / component.MaxScore * 10 * component.Weight / 100;
        });

        return Round(raw, scheme.RoundingMode, scheme.DecimalPlaces);
    }

    public static GradeEvaluation Evaluate(
        GradingSchemeVersion scheme,
        IReadOnlyCollection<GradeScaleItem>? scale,
        IReadOnlyDictionary<string, double?> scores)
    {
        var finalScore = CalculateFinal(scheme, scores);
        var item = ResolveScale(scale, finalScore);
        var passed = finalScore >= scheme.PassingScore && MeetsComponentConditions(scheme, scores);
        return new GradeEvaluation(finalScore, item.Letter, item.GradePoint, item.Classification, passed);
    }

    public static GradeScaleItem ResolveScale(IReadOnlyCollection<GradeScaleItem>? scale, double score)
    {
        var source = scale is { Count: > 0 } ? scale : DefaultScale;
        return source
            .OrderByDescending(x => x.Min)
            .FirstOrDefault(x => score >= x.Min && score <= x.Max + 0.000001)
            ?? source.OrderBy(x => x.Min).First();
    }

    public static bool MeetsComponentConditions(
        GradingSchemeVersion scheme,
        IReadOnlyDictionary<string, double?> scores)
    {
        foreach (var component in scheme.Components)
        {
            if (!component.MinimumScore.HasValue) continue;
            if (!component.IsRequired && !component.IsFinalCondition) continue;
            var score = scores.GetValueOrDefault(component.ComponentId);
            if (!score.HasValue || score.Value < component.MinimumScore.Value) return false;
        }
        return true;
    }

    public static void ValidateForPublish(
        GradingSchemeVersion scheme,
        IReadOnlyDictionary<string, double?> scores)
    {
        foreach (var component in scheme.Components.Where(x => x.IsRequired || x.IsFinalCondition))
        {
            if (!scores.GetValueOrDefault(component.ComponentId).HasValue)
            {
                throw new Common.AppException($"Thiếu điểm bắt buộc: {component.Name}");
            }
        }
    }

    private static double Round(double value, string mode, int decimals)
    {
        decimals = Math.Clamp(decimals, 0, 4);
        var factor = Math.Pow(10, decimals);
        return mode?.Trim().ToLowerInvariant() switch
        {
            "none" => value,
            "floor" or "down" => Math.Floor(value * factor) / factor,
            "ceiling" or "up" => Math.Ceiling(value * factor) / factor,
            _ => Math.Round(value, decimals, MidpointRounding.AwayFromZero)
        };
    }
}
