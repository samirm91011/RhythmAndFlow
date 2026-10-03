using Microsoft.Extensions.Options;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class BookingTests
{
    private static BookingService Service(TestDb t, int cutoffHours = 2) =>
        new(t.Db, Options.Create(new BookingOptions { CancelCutoffHours = cutoffHours }));

    [Fact]
    public async Task A_customer_can_book_an_available_class()
    {
        using var t = new TestDb();
        var cls = t.AddClass(capacity: 5); var user = t.AddUser();
        var (ok, error, booking) = await Service(t).BookAsync(user.Id, cls.Id);
        Assert.True(ok, error);
        Assert.Equal("BOOKED", booking!.Status);
        Assert.Single(await Service(t).MineAsync(user.Id));
    }

    [Fact]
    public async Task A_full_class_cannot_be_booked()
    {
        using var t = new TestDb();
        var cls = t.AddClass(capacity: 1);
        await Service(t).BookAsync(t.AddUser().Id, cls.Id);
        var (ok, error, _) = await Service(t).BookAsync(t.AddUser().Id, cls.Id);
        Assert.False(ok);
        Assert.Contains("full", error);
    }

    [Fact]
    public async Task The_same_class_cannot_be_booked_twice_by_one_customer()
    {
        using var t = new TestDb();
        var cls = t.AddClass(); var user = t.AddUser();
        await Service(t).BookAsync(user.Id, cls.Id);
        var (ok, error, _) = await Service(t).BookAsync(user.Id, cls.Id);
        Assert.False(ok);
        Assert.Contains("already", error);
    }

    [Fact]
    public async Task A_class_that_has_started_cannot_be_booked()
    {
        using var t = new TestDb();
        var cls = t.AddClass(startsIn: TimeSpan.FromMinutes(-10));
        var (ok, _, _) = await Service(t).BookAsync(t.AddUser().Id, cls.Id);
        Assert.False(ok);
    }

    [Fact]
    public async Task Cancelling_frees_the_place_for_someone_else()
    {
        using var t = new TestDb();
        var cls = t.AddClass(capacity: 1); var first = t.AddUser(); var second = t.AddUser();
        var (_, _, booking) = await Service(t).BookAsync(first.Id, cls.Id);
        var cancel = await Service(t).CancelAsync(first.Id, booking!.Id);
        Assert.True(cancel.Ok, cancel.Error);
        Assert.True((await Service(t).BookAsync(second.Id, cls.Id)).Ok);
    }

    [Fact]
    public async Task Booking_cannot_be_cancelled_inside_the_cut_off_window()
    {
        using var t = new TestDb();
        var cls = t.AddClass(startsIn: TimeSpan.FromMinutes(90)); var user = t.AddUser();
        var (_, _, booking) = await Service(t, cutoffHours: 2).BookAsync(user.Id, cls.Id);
        var cancel = await Service(t, cutoffHours: 2).CancelAsync(user.Id, booking!.Id);
        Assert.False(cancel.Ok);
        Assert.Contains("2 hours", cancel.Error);
    }

    [Fact]
    public async Task One_customer_cannot_cancel_another_customers_booking()
    {
        using var t = new TestDb();
        var cls = t.AddClass(); var owner = t.AddUser(); var other = t.AddUser();
        var (_, _, booking) = await Service(t).BookAsync(owner.Id, cls.Id);
        Assert.False((await Service(t).CancelAsync(other.Id, booking!.Id)).Ok);
    }

    [Fact]
    public async Task Upcoming_list_reports_spots_left_and_whether_i_booked()
    {
        using var t = new TestDb();
        var cls = t.AddClass(capacity: 3); var me = t.AddUser(); var other = t.AddUser();
        await Service(t).BookAsync(me.Id, cls.Id);
        await Service(t).BookAsync(other.Id, cls.Id);
        var item = Assert.Single(await Service(t).UpcomingAsync(me.Id));
        Assert.Equal(1, item.SpotsLeft);
        Assert.True(item.BookedByMe);
        Assert.False((await Service(t).UpcomingAsync(t.AddUser().Id)).Single().BookedByMe);
    }
}
