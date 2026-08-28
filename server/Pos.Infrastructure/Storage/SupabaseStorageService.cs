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
            _supabaseUrl = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url missing");
            _supabaseKey = config["Supabase:ServiceRoleKey"] ?? throw new InvalidOperationException("Supabase:ServiceRoleKey missing");
            _bucketName = config["Supabase:StorageBucket"] ?? "products";
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
        {
            var url = $"{_supabaseUrl}/storage/v1/object/{_bucketName}/{fileName}";

            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return $"{_supabaseUrl}/storage/v1/object/public/{_bucketName}/{fileName}";
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
                listResponse.EnsureSuccessStatusCode();
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
                    deleteResponse.EnsureSuccessStatusCode();
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
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SignedUrlResponse>(cancellationToken: cancellationToken);
            return result?.SignedUrl ?? throw new InvalidOperationException("Failed to generate signed URL.");
        }

        private record SignedUrlResponse(string SignedUrl);

// Helper record for deserializing the list response
        private record SupabaseFileInfo(string Name, string Id, DateTime CreatedAt, DateTime UpdatedAt);
    }
}