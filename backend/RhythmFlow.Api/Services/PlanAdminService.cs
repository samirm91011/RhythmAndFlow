using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>Lets administrators add, edit and hide subscription plans. Plans are never deleted: people may hold them and payments refer to them.</summary>
public class PlanAdminService(AppDbContext db)
{
    public static readonly string[] Statuses = ["ACTIVE", "INACTIVE"];

    public async Task<List<AdminPlanDto>> ListAsync() =>
        (await db.Plans.AsNoTracking().ToListAsync()).OrderBy(p => p.Price).ThenBy(p => p.Id).Select(ToDto).ToList();

    public async Task<(bool Ok, int Id, string? Error)> CreateAsync(PlanUpsert r)
    {
        var error = Validate(r);
        if (error is not null) return (false, 0, error);
        if (await NameTakenAsync(r.Name, null)) return (false, 0, "A plan with that name already exists.");

        var plan = new SubscriptionPlan { BillingFrequency = "MONTHLY" };
        Apply(plan, r);
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return (true, plan.Id, null);
    }

    public async Task<(bool Ok, bool NotFound, string? Error)> UpdateAsync(int id, PlanUpsert r)
    {
        var plan = await db.Plans.FindAsync(id);
        if (plan is null) return (false, true, null);
        var error = Validate(r);
        if (error is not null) return (false, false, error);
        if (await NameTakenAsync(r.Name, id)) return (false, false, "A plan with that name already exists.");

        Apply(plan, r);
        await db.SaveChangesAsync();
        return (true, false, null);
    }

    private async Task<bool> NameTakenAsync(string name, int? exceptId)
    {
        var n = name.Trim().ToLower();
        return await db.Plans.AnyAsync(p => p.Name.ToLower() == n && p.Id != exceptId);
    }

    private static string? Validate(PlanUpsert r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Enter a plan name.";
        if (r.Price < 1) return "The price must be at least R1.";
        if (r.Tier is < 1 or > 10) return "Access level must be between 1 and 10.";
        if (!string.IsNullOrWhiteSpace(r.Status) && !Statuses.Contains(r.Status.ToUpperInvariant())) return "Status must be ACTIVE or INACTIVE.";
        // PayFast does not take recurring amounts under R5.
        if (r.AnnualPrice is < 5) return "The yearly price must be at least R5, or left empty for a monthly-only plan.";
        if (r.LaunchPrice is < 5) return "The launch price must be at least R5, or left empty for no launch offer.";
        if (r.LaunchPrice is { } lp && lp >= r.Price) return "The launch price must be lower than the normal monthly price.";
        if (r.LaunchPrice is not null && r.LaunchEndsAt is null) return "Choose the day the launch price ends.";
        if (r.TrialDays is < 0 or > 60) return "Free trial days must be between 0 and 60.";
        return null;
    }

    private static void Apply(SubscriptionPlan p, PlanUpsert r)
    {
        p.Name = r.Name.Trim();
        p.Description = r.Description?.Trim() ?? "";
        p.Price = r.Price;
        p.Tier = r.Tier;
        p.Features = string.Join('|', SplitFeatures(r.Features));
        p.Status = string.IsNullOrWhiteSpace(r.Status) ? "ACTIVE" : r.Status.ToUpperInvariant();
        p.AnnualPrice = r.AnnualPrice;
        p.LaunchPrice = r.LaunchPrice;
        p.LaunchEndsAt = r.LaunchPrice is null ? null : r.LaunchEndsAt;
        p.TrialDays = r.TrialDays;
    }

    /// <summary>Features are stored as one text value separated by "|"; accept new lines too and drop blanks.</summary>
    public static IEnumerable<string> SplitFeatures(string? text) =>
        (text ?? "").Split(['|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static AdminPlanDto ToDto(SubscriptionPlan p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.BillingFrequency, p.Tier, SplitFeatures(p.Features).ToList(), p.Status,
            p.AnnualPrice, p.LaunchPrice, p.LaunchEndsAt, p.TrialDays);
}
