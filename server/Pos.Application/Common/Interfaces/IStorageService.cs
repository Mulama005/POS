namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Abstraction over file storage, implemented in Pos.Infrastructure against Supabase Storage.
/// Application/business logic depends only on this interface — never on the Supabase SDK directly —
/// so the provider can be swapped later without touching anything outside Pos.Infrastructure.
/// All access is private: every file lives behind this service, never behind a public bucket URL.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file into the given logical folder (see <see cref="StorageFolders"/>) and returns
    /// the storage path to persist on the owning entity (e.g. Product.ImagePath).
    /// </summary>
    Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a short-lived signed URL for downloading a private file. Callers decide the
    /// expiry; default is short (5 minutes) because these links are meant to be used immediately,
    /// not bookmarked or shared.
    /// </summary>
    Task<string> GetSignedUrlAsync(
        string storagePath,
        int expiresInSeconds = 300,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single file. Used when a product is removed from the catalog, a report is
    /// purged, etc.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every file under a folder prefix — e.g. all images for a given product ID.
    /// </summary>
    Task DeleteFolderAsync(string folderPrefix, CancellationToken cancellationToken = default);
}
