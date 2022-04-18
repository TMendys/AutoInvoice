namespace AutoInvoice;

public record Customer(
    string CustomerNumber,
    bool Email,
    DateOnly Date,
    decimal? SubscriptionCost,
    decimal? LaborCost,
    decimal? ServiceCost,
    decimal? DrivingCost,
    decimal? Discount,
    int TotalTimeInHours,
    string? Comments)
{
    public override string ToString() =>
        $"{CustomerNumber},{Email},{Date},{SubscriptionCost},{LaborCost},{ServiceCost},{DrivingCost},{Discount},{TotalTimeInHours},{Comments}{Environment.NewLine}";
}