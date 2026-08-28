using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Application.Common.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
        Task DeleteFolderAsync(string folderPath, CancellationToken cancellationToken = default);
        Task<string> GetSignedUrlAsync(string filePath, int expiresInSeconds = 60, CancellationToken cancellationToken = default);
    }
}