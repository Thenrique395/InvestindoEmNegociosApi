namespace InvestindoEmNegocio.Domain.Enums;

public enum BillingCheckoutStatus
{
    Draft = 1,
    Pending = 2,
    RequiresAction = 3,
    Paid = 4,
    Failed = 5,
    Expired = 6,
    Refunded = 7,
    Cancelled = 8
}
