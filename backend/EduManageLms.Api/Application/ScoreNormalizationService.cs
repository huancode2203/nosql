using System.Globalization;
using System.Text.RegularExpressions;
using EduManageLms.Api.Common;

namespace EduManageLms.Api.Application;

public sealed record ScoreNormalizationResult(
    string RawInput,
    decimal? NormalizedValue,
    string NormalizationType,
    bool RequiresConfirmation,
    string? Warning);

public sealed class ScoreNormalizationService
{
    private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex DecimalOnly = new(@"^\d+(?:[\.,]\d+)?$", RegexOptions.Compiled);

    public ScoreNormalizationResult Normalize(string? rawInput, decimal maxScore = 10m)
    {
        var raw = (rawInput ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (raw.Length == 0)
            return new(raw, null, "Empty", false, null);

        if (raw.StartsWith('-') || raw.Contains('e') || raw.Contains('E'))
            throw new AppException("Điểm không được âm hoặc ở dạng số khoa học.");

        var statusCode = raw.ToUpperInvariant();
        if (statusCode is "V" or "ABSENT")
            return new(raw, null, "Absent", false, null);
        if (statusCode is "M" or "EXEMPT")
            return new(raw, null, "Exempt", false, null);

        if (raw == "0700")
        {
            return Validate(
                raw,
                7.1m,
                "BusinessException",
                true,
                "Hệ thống đã chuẩn hóa “0700” thành “7,1”. Vui lòng xác nhận trước khi lưu.",
                maxScore);
        }

        var text = raw.Replace(',', '.');
        if (!DecimalOnly.IsMatch(text))
            throw new AppException($"Giá trị “{raw}” không phải điểm hợp lệ.");

        if (text.Contains('.'))
        {
            if (!decimal.TryParse(
                    text,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var decimalValue))
            {
                throw new AppException($"Không thể đọc điểm “{raw}”.");
            }

            return Validate(raw, decimalValue, "Decimal", false, null, maxScore);
        }

        if (raw == "0") return Validate(raw, 0m, "ExactInteger", false, null, maxScore);
        if (raw == "1") return Validate(raw, 1m, "ExactInteger", false, null, maxScore);
        if (raw == "10") return Validate(raw, 10m, "ExactInteger", false, null, maxScore);

        if (!DigitsOnly.IsMatch(raw))
            throw new AppException($"Giá trị “{raw}” không hợp lệ.");

        var shortened = raw;
        while (shortened.Length > 2 && shortened.EndsWith('0'))
            shortened = shortened[..^1];

        if (shortened == "10")
            return Validate(raw, 10m, "TrailingZeroShorthand", false, null, maxScore);

        if (shortened.Length == 2)
        {
            var shorthand = decimal.Parse(shortened, CultureInfo.InvariantCulture) / 10m;
            var type = shortened == raw ? "Shorthand" : "TrailingZeroShorthand";
            var note = shortened == raw
                ? null
                : $"Đã chuẩn hóa từ {raw} thành {shorthand:0.##}.";

            return Validate(raw, shorthand, type, false, note, maxScore);
        }

        if (decimal.TryParse(
                shortened,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer)
            && integer >= 0m
            && integer <= 10m)
        {
            return Validate(raw, integer, "ExactInteger", false, null, maxScore);
        }

        throw new AppException($"Điểm “{raw}” không hợp lệ.");
    }

    private static ScoreNormalizationResult Validate(
        string raw,
        decimal value,
        string type,
        bool requiresConfirmation,
        string? warning,
        decimal maxScore)
    {
        var upperBound = Math.Min(10m, maxScore);
        if (value < 0m || value > upperBound)
            throw new AppException($"Điểm phải nằm trong khoảng 0 đến {upperBound:0.##}.");

        return new(raw, value, type, requiresConfirmation, warning);
    }
}
