namespace Pos.Application.Common.Constants;

/// <summary>
/// Logical top-level folders inside the single private "pos-files" bucket. Using folder
/// prefixes instead of separate buckets keeps bucket-level policy simple (one bucket, one
/// policy) while still giving each file type its own namespace for cleanup and listing.
/// </summary>
public static class StorageFolders
{
    /// <summary>Product images. Path shape: products/{productId}/{fileName}</summary>
    public const string Products = "products";

    /// <summary>Generated PDF sale receipts. Path shape: receipts/{transactionId}.pdf</summary>
    public const string Receipts = "receipts";

    /// <summary>Exported CSV/PDF reports (sales, inventory, reconciliation, etc.). Path shape: reports/{reportId}.{ext}</summary>
    public const string Reports = "reports";
}
