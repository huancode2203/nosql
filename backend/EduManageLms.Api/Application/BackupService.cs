using System.Diagnostics;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class BackupService(
    MongoContext db,
    IOptions<BackupOptions> options) : IBackupService
{
    private readonly BackupOptions backupOptions = options.Value;

    public async Task<Dictionary<string, object>> CreateAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(backupOptions.Directory);

        var backupName = $"EduManageLms_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var backupPath = Path.Combine(backupOptions.Directory, backupName);

        var history = new BackupHistory
        {
            FileName = backupName,
            PerformedBy = userId,
            Status = "Pending"
        };

        await db.BackupHistories.InsertOneAsync(
            history,
            cancellationToken: cancellationToken);

        try
        {
            await RunProcessAsync(
                backupOptions.MongoDumpPath,
                [
                    $"--uri={db.Options.ConnectionString}",
                    $"--db={db.Options.DatabaseName}",
                    $"--out={backupPath}"
                ],
                cancellationToken);

            history.Status = "Success";
            history.CompletedAt = DateTime.UtcNow;
            history.SizeBytes = Directory.Exists(backupPath)
                ? Directory.EnumerateFiles(backupPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length)
                : 0;
        }
        catch (Exception exception)
        {
            history.Status = "Failed";
            history.Error = exception.Message;
            history.CompletedAt = DateTime.UtcNow;
        }

        history.UpdatedAt = DateTime.UtcNow;

        await db.BackupHistories.ReplaceOneAsync(
            item => item.Id == history.Id,
            history,
            cancellationToken: cancellationToken);

        if (history.Status == "Failed")
        {
            throw new AppException($"Backup tháº¥t báº¡i: {history.Error}", 500);
        }

        return new Dictionary<string, object>
        {
            ["id"] = history.Id,
            ["fileName"] = history.FileName,
            ["sizeBytes"] = history.SizeBytes,
            ["status"] = history.Status
        };
    }

    public async Task<IReadOnlyCollection<Dictionary<string, object?>>> ListAsync(
        CancellationToken cancellationToken)
    {
        var histories = await db.BackupHistories
            .Find(FilterDefinition<BackupHistory>.Empty)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return histories
            .Select(item => new Dictionary<string, object?>
            {
                ["id"] = item.Id,
                ["fileName"] = item.FileName,
                ["sizeBytes"] = item.SizeBytes,
                ["status"] = item.Status,
                ["performedBy"] = item.PerformedBy,
                ["createdAt"] = item.CreatedAt,
                ["completedAt"] = item.CompletedAt,
                ["error"] = item.Error
            })
            .ToList();
    }

    public async Task RestoreAsync(
        string id,
        string userId,
        string confirmation,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, "RESTORE", StringComparison.Ordinal))
        {
            throw new AppException("Chuá»—i xÃ¡c nháº­n khÃ´ng há»£p lá»‡");
        }

        var backup = await db.BackupHistories
            .Find(item => item.Id == id && item.Status == "Success")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("KhÃ´ng tÃ¬m tháº¥y báº£n sao lÆ°u há»£p lá»‡");

        // Táº¡o báº£n sao lÆ°u an toÃ n trÆ°á»›c khi phá»¥c há»“i dá»¯ liá»‡u cÅ©.
        await CreateAsync(userId, cancellationToken);

        var databaseBackupPath = Path.Combine(
            backupOptions.Directory,
            backup.FileName,
            db.Options.DatabaseName);

        if (!Directory.Exists(databaseBackupPath))
        {
            throw new NotFoundException("ThÆ° má»¥c dá»¯ liá»‡u cá»§a báº£n sao lÆ°u khÃ´ng tá»“n táº¡i");
        }

        await RunProcessAsync(
            backupOptions.MongoRestorePath,
            [
                $"--uri={db.Options.ConnectionString}",
                $"--db={db.Options.DatabaseName}",
                "--drop",
                databaseBackupPath
            ],
            cancellationToken);

        await db.AuditLogs.InsertOneAsync(
            new AuditLog
            {
                UserId = userId,
                Role = "Admin",
                Action = "Restore",
                Entity = "Database",
                EntityId = id,
                Result = "Success",
                Note = $"Phá»¥c há»“i tá»« báº£n sao lÆ°u {backup.FileName}"
            },
            cancellationToken: cancellationToken);
    }

    private static async Task RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException($"KhÃ´ng thá»ƒ khá»Ÿi Ä‘á»™ng tiáº¿n trÃ¬nh {executable}");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(standardError)
                ? standardOutput
                : standardError;

            throw new InvalidOperationException(
                $"Tiáº¿n trÃ¬nh {executable} tháº¥t báº¡i vá»›i mÃ£ {process.ExitCode}: {detail}");
        }
    }
}