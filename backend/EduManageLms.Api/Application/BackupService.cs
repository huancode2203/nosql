using System.Diagnostics;
using System.IO.Compression;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class BackupService(MongoContext db, IOptions<BackupOptions> options) : IBackupService
{
    private readonly BackupOptions settings = options.Value;

    public async Task<Dictionary<string, object>> CreateAsync(string userId, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.Directory);
        var name = $"EduManageLms_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var path = Path.Combine(settings.Directory, name);
        var history = new BackupHistory { FileName = name, PerformedBy = userId };
        await db.BackupHistories.InsertOneAsync(history, cancellationToken: ct);

        try
        {
            var arguments = $"--uri=\"{db.Options.ConnectionString}\" --db=\"{db.Options.DatabaseName}\" --out=\"{path}\"";
            await RunAsync(settings.MongoDumpPath, arguments, ct);
            history.Status = "Success";
            history.CompletedAt = DateTime.UtcNow;
            history.SizeBytes = Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
                : 0;
        }
        catch (Exception ex)
        {
            history.Status = "Failed";
            history.Error = ex.Message;
        }

        history.UpdatedAt = DateTime.UtcNow;
        await db.BackupHistories.ReplaceOneAsync(x => x.Id == history.Id, history, cancellationToken: ct);
        if (history.Status == "Failed") throw new AppException("Backup thất bại: " + history.Error, 500);
        return Map(history).ToDictionary(x => x.Key, x => x.Value!);
    }

    public async Task<IReadOnlyCollection<Dictionary<string, object?>>> ListAsync(CancellationToken ct) =>
        (await db.BackupHistories.Find(x => !x.IsDeleted).SortByDescending(x => x.CreatedAt).ToListAsync(ct))
        .Select(Map)
        .ToList();

    public async Task<(byte[] Content, string FileName)> DownloadAsync(string id, CancellationToken ct)
    {
        var item = await RequireBackupAsync(id, ct);
        var source = Path.Combine(settings.Directory, item.FileName);
        if (!Directory.Exists(source)) throw new NotFoundException("Không tìm thấy thư mục bản sao lưu");
        var temp = Path.Combine(Path.GetTempPath(), $"{item.FileName}-{Guid.NewGuid():N}.zip");
        try
        {
            ZipFile.CreateFromDirectory(source, temp, CompressionLevel.Fastest, false);
            return (await File.ReadAllBytesAsync(temp, ct), item.FileName + ".zip");
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<Dictionary<string, object>> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file, string userId, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException("File sao lưu rỗng");
        if (file.Length > 500L * 1024 * 1024) throw new AppException("File sao lưu vượt quá 500 MB");
        if (!string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase)) throw new AppException("Chỉ chấp nhận file ZIP");

        Directory.CreateDirectory(settings.Directory);
        var name = $"Imported_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        var destination = Path.GetFullPath(Path.Combine(settings.Directory, name));
        Directory.CreateDirectory(destination);
        await using var input = file.OpenReadStream();
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new AppException("File ZIP không an toàn");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var entryStream = entry.Open();
            await using var output = File.Create(target);
            await entryStream.CopyToAsync(output, ct);
        }

        var history = new BackupHistory
        {
            FileName = name,
            PerformedBy = userId,
            Type = "Uploaded",
            Status = "Success",
            CompletedAt = DateTime.UtcNow,
            SizeBytes = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length)
        };
        await db.BackupHistories.InsertOneAsync(history, cancellationToken: ct);
        return Map(history).ToDictionary(x => x.Key, x => x.Value!);
    }

    public async Task DeleteAsync(string id, string userId, CancellationToken ct)
    {
        var item = await RequireBackupAsync(id, ct);
        var path = Path.Combine(settings.Directory, item.FileName);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await db.BackupHistories.ReplaceOneAsync(x => x.Id == item.Id, item, cancellationToken: ct);
        await WriteAuditAsync(userId, "DeleteBackup", id, ct);
    }

    public async Task RestoreAsync(string id, string userId, string confirmation, CancellationToken ct)
    {
        if (!string.Equals(confirmation, "RESTORE", StringComparison.Ordinal)) throw new AppException("Chuỗi xác nhận không hợp lệ");
        var item = await RequireBackupAsync(id, ct);
        await CreateAsync(userId, ct);
        var root = Path.Combine(settings.Directory, item.FileName);
        var databasePath = Path.Combine(root, db.Options.DatabaseName);
        if (!Directory.Exists(databasePath))
        {
            databasePath = Directory.EnumerateDirectories(root, db.Options.DatabaseName, SearchOption.AllDirectories).FirstOrDefault()
                           ?? throw new NotFoundException("Không tìm thấy dữ liệu MongoDB trong bản sao lưu");
        }
        var arguments = $"--uri=\"{db.Options.ConnectionString}\" --db=\"{db.Options.DatabaseName}\" --drop \"{databasePath}\"";
        await RunAsync(settings.MongoRestorePath, arguments, ct);
        await WriteAuditAsync(userId, "Restore", id, ct);
    }

    private async Task<BackupHistory> RequireBackupAsync(string id, CancellationToken ct) =>
        await db.BackupHistories.Find(x => x.Id == id && x.Status == "Success" && !x.IsDeleted).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Không tìm thấy bản sao lưu");

    private async Task WriteAuditAsync(string userId, string action, string id, CancellationToken ct) =>
        await db.AuditLogs.InsertOneAsync(new AuditLog { UserId = userId, Role = "Admin", Action = action, Entity = "Database", EntityId = id }, cancellationToken: ct);

    private static Dictionary<string, object?> Map(BackupHistory item) => new()
    {
        ["id"] = item.Id,
        ["fileName"] = item.FileName,
        ["sizeBytes"] = item.SizeBytes,
        ["status"] = item.Status,
        ["type"] = item.Type,
        ["performedBy"] = item.PerformedBy,
        ["createdAt"] = item.CreatedAt,
        ["completedAt"] = item.CompletedAt,
        ["error"] = item.Error
    };

    private static async Task RunAsync(string fileName, string arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var error = await errorTask;
        var output = await outputTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
    }
}
