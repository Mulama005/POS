namespace Pos.Api.Controllers;

// Property names match Daraja's actual callback JSON shape (case-insensitive binding
// handles "CheckoutRequestID" vs "CheckoutRequestId" automatically — ASP.NET Core's
// default JSON options are case-insensitive on the way in).

public sealed class DarajaCallbackEnvelope
{
    public DarajaCallbackBody? Body { get; set; }
}

public sealed class DarajaCallbackBody
{
    public DarajaStkCallback? StkCallback { get; set; }
}

public sealed class DarajaStkCallback
{
    public string? MerchantRequestId { get; set; }
    public string? CheckoutRequestId { get; set; }
    public int ResultCode { get; set; }
    public string? ResultDesc { get; set; }
    public DarajaCallbackMetadata? CallbackMetadata { get; set; }
}

public sealed class DarajaCallbackMetadata
{
    public List<DarajaCallbackItem>? Item { get; set; }
}

public sealed class DarajaCallbackItem
{
    public string? Name { get; set; }
    public object? Value { get; set; }
}