namespace Pos.Application.Features.Products;

public class ReceiveStockRequest
{
    public Guid ProductId { get; set; }
    public List<string> SerialNumbers { get; set; } = new();
    public List<string>? Imei { get; set; } // optional, one per unit
    public DateTime? PurchaseDate { get; set; }
}