using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;
using SupabaseStorage = Supabase.Storage;

namespace Pos.Infrastructure.Storage;

/// <summary>
/// IStorageService implementation backed by Supabase Storage. Talks to Storage using the
/// service role key only — this class is the single choke point through which any file in the
/// system is read or written, which is what lets access stay tied to the caller's role instead
/// of a guessable public URL.
/// </summary>
public sealed class SupabaseStorageService : IStorageService
{
    private readonly SupabaseStorage.Client _client;
    private readonly string _bucket;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        IOptions<SupabaseStorageOptions> options,
        ILogger<SupabaseStorageService> logger)
    {
        var settings = options.Value;
        _bucket = settings.Bucket;
        _logger = logger;

        var storageUrl = $"{settings.Url.TrimEnd('/')}/storage/v1";
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {settings.ServiceRoleKey}",
            ["apikey"] = settings.ServiceRoleKey,
        };

        _client = new SupabaseStorage.Client(storageUrl, headers);
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        // Prefix with a GUID so two files with the same original name never collide,
        // while keeping the original name readable for anyone browsing the bucket.
        var storagePath = $"{folder}/{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";

        await using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        try
        {
            await _client.From(_bucket).Upload(
                bytes,
                storagePath,
                new SupabaseStorage.FileOptions
                {
                    ContentType = contentType,
                    Upsert = false,
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {StoragePath} to bucket {Bucket}", storagePath, _bucket);
            throw;
        }

        return storagePath;
    }

    public async Task<string> GetSignedUrlAsync(
        string storagePath,
        int expiresInSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var signedUrl = await _client.From(_bucket).CreateSignedUrl(storagePath, expiresInSeconds);
            return signedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create signed URL for {StoragePath}", storagePath);
            throw;
        }
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.From(_bucket).Remove(new List<string> { storagePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {StoragePath}", storagePath);
            throw;
        }
    }

    public async Task DeleteFolderAsync(string folderPrefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _client.From(_bucket).List(folderPrefix);
            if (entries is null || entries.Count == 0)
            {
                return;
            }

            var paths = entries.Select(e => $"{folderPrefix}/{e.Name}").ToList();
            await _client.From(_bucket).Remove(paths);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete folder {FolderPrefix}", folderPrefix);
            throw;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned.Replace(' ', '-');
    }
}
