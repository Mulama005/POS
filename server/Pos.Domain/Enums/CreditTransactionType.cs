namespace Pos.Domain.Enums;

public enum CreditTransactionType
{
    CreditSale = 0,  // increases balance owed
    Payment = 1,     // decreases balance owed
}
