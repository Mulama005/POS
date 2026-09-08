namespace Pos.Domain.Enums;

public enum TaxClass
{
    Standard,   // 16% VAT
    ZeroRated,
    Exempt
}

public enum UnitStatus
{
    InStock,
    Reserved,
    Sold,
    InRepair,
    Returned,
    WrittenOff
}

public enum SaleStatus
{
    Held,       // parked sale, not yet completed
    Completed,
    Voided,
    Refunded,
    PendingApproval
}

public enum PaymentMethod
{
    Cash,
    Mpesa,
    Card,
    Credit // Deni
}

public enum PaymentStatus
{
    Pending,
    Success,
    Failed,
    TimedOut,
    Reversed
}

public enum CreditLedgerEntryType
{
    Charge,     // credit sale increases balance owed
    Payment     // customer payment reduces balance owed
}



public enum InventoryAdjustmentType
{
    Increase,
    Decrease,
    Correction
}

public enum CustomerType
{
    Retail,
    Trade
}

public enum RegisterUserRole
{
    Cashier,
    Manager,
    Admin,
    Technician
}

public enum TillSessionStatus
{
    Open,
    Closed
}