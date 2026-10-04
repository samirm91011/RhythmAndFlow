using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class PlanAdminTests
{
    private static PlanUpsert Plan(string name = "Studio", decimal price = 149, int tier = 2, string? features = "Live classes\nPriority booking", string? status = "ACTIVE") =>
        new(name, "A test plan", price, tier, features, status);

    [Fact]
    public async Task A_new_plan_is_saved_monthly_with_its_features_split_into_a_list()
    {
        using var t = new TestDb();
        var svc = new PlanAdminService(t.Db);

        var (ok, id, error) = await svc.CreateAsync(Plan());

        Assert.True(ok); Assert.Null(error);
        var row = (await svc.ListAsync()).Single(p => p.Id == id);
        Assert.Equal("Studio", row.Name);
        Assert.Equal("MONTHLY", row.BillingFrequency);
        Assert.Equal(["Live classes", "Priority booking"], row.Features.ToArray());
        Assert.Equal("ACTIVE", row.Status);
    }

    [Theory]
    [InlineData("", 149, 2, "Enter a plan name")]
    [InlineData("X", 0, 2, "at least R1")]
    [InlineData("X", 149, 0, "between 1 and 10")]
    [InlineData("X", 149, 11, "between 1 and 10")]
    public async Task Invalid_plans_are_refused_and_nothing_is_saved(string name, decimal price, int tier, string expected)
    {
        using var t = new TestDb();
        var svc = new PlanAdminService(t.Db);

        var (ok, _, error) = await svc.CreateAsync(Plan(name, price, tier));

        Assert.False(ok);
        Assert.Contains(expected, error);
        Assert.Equal(0, await t.Db.Plans.CountAsync());
    }

    [Fact]
    public async Task A_status_other_than_active_or_inactive_is_refused()
    {
        using var t = new TestDb();
        var (ok, _, error) = await new PlanAdminService(t.Db).CreateAsync(Plan(status: "DELETED"));
        Assert.False(ok);
        Assert.Contains("ACTIVE or INACTIVE", error);
    }

    [Fact]
    public async Task A_duplicate_name_is_refused_ignoring_case_but_a_plan_may_keep_its_own_name()
    {
        using var t = new TestDb();
        var svc = new PlanAdminService(t.Db);
        var (_, id, _) = await svc.CreateAsync(Plan("Studio"));

        var (dup, _, dupError) = await svc.CreateAsync(Plan("STUDIO"));
        var (same, _, _) = await svc.UpdateAsync(id, Plan("Studio", 159));

        Assert.False(dup); Assert.Contains("already exists", dupError);
        Assert.True(same);
        Assert.Equal(159m, (await svc.ListAsync()).Single().Price);
    }

    [Fact]
    public async Task Hiding_a_plan_removes_it_from_the_customer_list_but_keeps_it_for_the_admin()
    {
        using var t = new TestDb();
        var svc = new PlanAdminService(t.Db);
        var (_, id, _) = await svc.CreateAsync(Plan());

        var (ok, _, _) = await svc.UpdateAsync(id, Plan(status: "INACTIVE"));

        Assert.True(ok);
        Assert.Equal("INACTIVE", (await svc.ListAsync()).Single().Status);
        Assert.Equal(0, await t.Db.Plans.CountAsync(p => p.Status == "ACTIVE"));   // customers only ever see ACTIVE plans
    }

    [Fact]
    public async Task Editing_an_unknown_plan_is_not_found()
    {
        using var t = new TestDb();
        var (ok, notFound, _) = await new PlanAdminService(t.Db).UpdateAsync(999, Plan());
        Assert.False(ok);
        Assert.True(notFound);
    }

    [Fact]
    public async Task The_admin_list_is_ordered_by_price()
    {
        using var t = new TestDb();
        var svc = new PlanAdminService(t.Db);
        await svc.CreateAsync(Plan("Big", 299)); await svc.CreateAsync(Plan("Small", 99)); await svc.CreateAsync(Plan("Mid", 199));

        Assert.Equal(["Small", "Mid", "Big"], (await svc.ListAsync()).Select(p => p.Name).ToArray());
    }
}
