namespace Googlesheets.Models;

public record Customer(
    string CustomerNumber,
    string Billing_Method,
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
        $"{CustomerNumber},{Billing_Method},{Date},{SubscriptionCost},{LaborCost},{ServiceCost},{DrivingCost},{Discount},{TotalTimeInHours},{Comments}{Environment.NewLine}";
}