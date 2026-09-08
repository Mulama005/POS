namespace Pos.Application.Features.Products;

/// Receiving request for a non-serialized (bulk) product with no per-unit serial numbers 
public class ReceiveBulkStockRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}