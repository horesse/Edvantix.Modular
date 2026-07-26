namespace EDV.Modules.Billing.Contracts;

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Void = 3
}

public enum SubscriptionStatus
{
    Active = 0,
    Suspended = 1,
    Cancelled = 2
}

public enum InvoiceLineItemKind
{
    BaseFee = 0,
    Overage = 1,
    Adjustment = 2
}

public enum PlanInterval
{
    Monthly = 0,
    Yearly = 1
}

public enum InvoicePurpose
{
    // Usage=0 одновременно служит значением столбца по умолчанию (существующие строки получают
    // значение Usage; Subscription=1 всегда записывается явно). НЕ меняйте порядок значений —
    // если Subscription станет 0, снова проявится ошибка EF с пропущенным значением по умолчанию.
    Usage = 0,
    Subscription = 1,
    Topup = 2
}

public enum WalletStatus
{
    Active = 0,
    Frozen = 1
}

public enum WalletTransactionKind
{
    Topup = 0,
    MessageCharge = 1,
    Adjustment = 2
}

public enum TopupRequestStatus
{
    Pending = 0,
    Invoiced = 1,
    Completed = 2,
    Rejected = 3,
    Cancelled = 4
}
