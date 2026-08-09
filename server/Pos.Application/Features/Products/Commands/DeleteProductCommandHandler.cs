using Pos.Application.Common.Constants;
using Pos.Application.Common.Interfaces;

namespace Pos.Application.Features.Products.Commands;

// Illustrative — adapt to whatever CQRS/MediatR (or plain service) pattern your
// Pos.Application layer already uses. The part that matters for Step 5 is the
// IStorageService.DeleteFolderAsync call: every image for the product is removed
// from Storage in the same operation that removes the product from the catalog,
// so files never go orphaned in the bucket.

public sealed class DeleteProductCommandHandler
{
    private readonly IStorageService _storageService;
    // private readonly IApplicationDbContext _db;  // your existing EF Core context

    public DeleteProductCommandHandler(IStorageService storageService /*, IApplicationDbContext db */)
    {
        _storageService = storageService;
        // _db = db;
    }

    public async Task HandleAsync(Guid productId, CancellationToken cancellationToken)
    {
        // 1. Load the product (existing EF Core logic)
        // var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
        //     ?? throw new NotFoundException(nameof(Product), productId);

        // 2. Remove every stored file under this product's folder
        var folderPrefix = $"{StorageFolders.Products}/{productId}";
        await _storageService.DeleteFolderAsync(folderPrefix, cancellationToken);

        // 3. Remove the product row itself (existing EF Core logic)
        // _db.Products.Remove(product);
        // await _db.SaveChangesAsync(cancellationToken);
    }
}
