using System.IO.Compression;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Purfle.IdentityHub.Api.Services;

public sealed class BackupService
{
    private readonly string _storageRoot;
    private readonly string? _connectionString;
    private readonly string _containerName;

    public BackupService(string storageRoot, string? connectionString, string containerName = "purfle-backups")
    {
        _storageRoot = storageRoot;
        _connectionString = connectionString;
        _containerName = containerName;
    }

    /// <summary>
    /// Create a zip archive of all JSON data files under the storage root.
    /// </summary>
    public async Task<Stream> CreateBackupAsync(CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (Directory.Exists(_storageRoot))
            {
                var files = Directory.GetFiles(_storageRoot, "*.json", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(_storageRoot, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var fileStream = File.OpenRead(file);
                    await fileStream.CopyToAsync(entryStream, ct);
                }
            }
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Restore data from a zip archive, replacing existing files.
    /// </summary>
    public async Task RestoreAsync(Stream zipStream, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;

            // Prevent path traversal
            var destPath = Path.GetFullPath(Path.Combine(_storageRoot, entry.FullName));
            if (!destPath.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Path traversal detected: {entry.FullName}");

            var dir = Path.GetDirectoryName(destPath);
            if (dir != null) Directory.CreateDirectory(dir);

            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(destPath);
            await entryStream.CopyToAsync(fileStream, ct);
        }
    }

    /// <summary>
    /// Upload a backup stream to Azure Blob Storage.
    /// </summary>
    public async Task PushToAzureAsync(Stream stream, CancellationToken ct = default)
    {
        var client = GetBlobContainerClient();
        await client.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName = $"backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        var blob = client.GetBlobClient(blobName);

        stream.Position = 0;
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = "application/zip" }, cancellationToken: ct);
    }

    /// <summary>
    /// List available backups in Azure Blob Storage.
    /// </summary>
    public async Task<List<BackupInfo>> ListAzureBackupsAsync(CancellationToken ct = default)
    {
        var client = GetBlobContainerClient();
        var backups = new List<BackupInfo>();

        if (!await client.ExistsAsync(ct))
            return backups;

        await foreach (var blob in client.GetBlobsAsync(cancellationToken: ct))
        {
            backups.Add(new BackupInfo
            {
                Name = blob.Name,
                Size = blob.Properties.ContentLength ?? 0,
                CreatedAt = blob.Properties.CreatedOn?.UtcDateTime ?? DateTime.MinValue
            });
        }

        return backups.OrderByDescending(b => b.CreatedAt).ToList();
    }

    /// <summary>
    /// Download a backup from Azure Blob Storage.
    /// </summary>
    public async Task<Stream> PullFromAzureAsync(string blobName, CancellationToken ct = default)
    {
        var client = GetBlobContainerClient();
        var blob = client.GetBlobClient(blobName);
        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);

        var ms = new MemoryStream();
        await response.Value.Content.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    private BlobContainerClient GetBlobContainerClient()
    {
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not configured.");

        return new BlobContainerClient(_connectionString, _containerName);
    }
}

public sealed class BackupInfo
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}
