using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>The rules for what a plan costs and how long one payment covers, kept in one place so checkout, payments and the app agree.</summary>
public static class PlanPricing
{
    public const string Monthly = "MONTHLY";
    public const string Annual = "ANNUAL";

    public static string NormaliseBilling(string? billing) =>
        string.Equals(billing?.Trim(), Annual, StringComparison.OrdinalIgnoreCase) ? Annual : Monthly;

    /// <summary>True while a reduced first-month price is on offer for new subscribers.</summary>
    public static bool LaunchActive(SubscriptionPlan p, DateTime now) =>
        p.LaunchPrice is not null && p.LaunchEndsAt is { } end && end > now;

    /// <summary>The price charged every period once the subscription is running.</summary>
    public static decimal RecurringPrice(SubscriptionPlan p, string billing) =>
        billing == Annual ? p.AnnualPrice ?? p.Price * 12 : p.Price;

    /// <summary>What a subscription brings in per month, so a yearly plan counts as a twelfth of its price.</summary>
    public static decimal MonthlyEquivalent(SubscriptionPlan p, string? billing) =>
        Math.Round(NormaliseBilling(billing) == Annual ? RecurringPrice(p, Annual) / 12m : p.Price, 2);

    public static bool OffersAnnual(SubscriptionPlan p) => p.AnnualPrice is not null;

    public static DateTime AddPeriod(DateTime from, string billing) =>
        billing == Annual ? from.AddYears(1) : from.AddMonths(1);

    public static string PeriodWord(string billing) => billing == Annual ? "year" : "month";
}
