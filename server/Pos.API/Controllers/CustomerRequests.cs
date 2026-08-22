namespace Pos.Api.Controllers;

public sealed record CreateCustomerRequest(string FullName, string PhoneNumber, string? Email);
public sealed record RecordCreditSaleRequest(decimal Amount, Guid? RelatedSaleId, string? Notes);
public sealed record RecordPaymentRequest(decimal Amount, string PaymentMethod, string? Notes);
