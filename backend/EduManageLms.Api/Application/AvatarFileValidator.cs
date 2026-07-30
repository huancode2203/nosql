using EduManageLms.Api.Common;

namespace EduManageLms.Api.Application;

public static class AvatarFileValidator
{
    public const long MaximumBytes = 5L * 1024 * 1024;

    public static string DetectExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
            return ".png";

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
            return ".jpg";

        if (bytes.Length >= 12
            && bytes[0] == (byte)'R'
            && bytes[1] == (byte)'I'
            && bytes[2] == (byte)'F'
            && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W'
            && bytes[9] == (byte)'E'
            && bytes[10] == (byte)'B'
            && bytes[11] == (byte)'P')
            return ".webp";

        throw new AppException(
            "Ảnh đại diện phải là tệp PNG, JPEG hoặc WebP hợp lệ.");
    }
}
