using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Storage
{
    public class SupabaseStorageService : IStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private readonly string _bucketName;

        public SupabaseStorageService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _supabaseUrl = RequireConfigValue(config, "Supabase:Url");
            _supabaseKey = RequireConfigValue(config, "Supabase:ServiceRoleKey");
            _bucketName = config["Supabase:StorageBucket"] ?? "products";
        }

        private static string RequireConfigValue(IConfiguration config, string key)
        {
            var value = config[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{key} is missing or empty. Set it via " +
                    $"'dotnet user-secrets set \"{key}\" \"...\" --project Pos.API'.");
            }
            return value;
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType = "application/octet-stream", CancellationToken cancellationToken = default)
        {
            var url = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{fileName}";

            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            await EnsureSuccessOrThrowWithBodyAsync(response, "upload", cancellationToken);

            return $"{_supabaseUrl}/storage/v1/object/public/{_bucketName}/{fileName}";
        }

        /// <summary>
        /// response.EnsureSuccessStatusCode() throws only the status code, discarding
        /// Supabase's actual error body (e.g. "Bucket not found", an RLS policy rejection,
        /// or a malformed key) — the one piece of information that actually explains a 400.
        /// This reads and includes it instead, so a failure is diagnosable from the
        /// exception message alone rather than requiring a guess.
        /// </summary>
        private static async Task EnsureSuccessOrThrowWithBodyAsync(
            HttpResponseMessage response, string action, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Supabase Storage {action} failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
        
        public async Task DeleteFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            // Ensure folder path ends with a slash for listing
            var prefix = folderPath.EndsWith("/") ? folderPath : folderPath + "/";

            // Step 1: List all files in the folder
            var listUrl = $"{_supabaseUrl}/storage/v1/object/list/{_bucketName}?prefix={Uri.EscapeDataString(prefix)}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);

            var listResponse = await _httpClient.GetAsync(listUrl, cancellationToken);
            if (!listResponse.IsSuccessStatusCode)
            {
                // If the folder doesn't exist or is empty, treat as success
                if (listResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return;
                await EnsureSuccessOrThrowWithBodyAsync(listResponse, "list", cancellationToken);
            }

            var files = await listResponse.Content.ReadFromJsonAsync<List<SupabaseFileInfo>>(cancellationToken: cancellationToken);
            if (files == null || !files.Any())
                return;

            // Step 2: Delete each file
            foreach (var file in files)
            {
                var deleteUrl = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{file.Name}";
                var deleteResponse = await _httpClient.DeleteAsync(deleteUrl, cancellationToken);
                // Ignore 404s – file might already be gone
                if (deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                    await EnsureSuccessOrThrowWithBodyAsync(deleteResponse, "delete", cancellationToken);
            }
        }
        
        public async Task<string> GetSignedUrlAsync(string filePath, int expiresInSeconds = 60, CancellationToken cancellationToken = default)
        {
            // Supabase Storage signed URL endpoint
            var url = $"{_supabaseUrl}/storage/v1/object/sign/{_bucketName}/{filePath}";

            var payload = new { expiresIn = expiresInSeconds };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            await EnsureSuccessOrThrowWithBodyAsync(response, "sign", cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<SignedUrlResponse>(cancellationToken: cancellationToken);
            return result?.SignedUrl ?? throw new InvalidOperationException("Failed to generate signed URL.");
        }

        private record SignedUrlResponse(string SignedUrl);

// Helper record for deserializing the list response
        private record SupabaseFileInfo(string Name, string Id, DateTime CreatedAt, DateTime UpdatedAt);
    }
}