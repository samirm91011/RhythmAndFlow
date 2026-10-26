using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>Paid events: tickets, capacity, waiting list, credit, cancellations, refunds, emails and reminders (client feedback of 20 Oct 2026).</summary>
public class EventTests
{
    private static (EventBookingService svc, FakeEmail email) Build(TestDb t)
    {
        var email = new FakeEmail();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        return (new EventBookingService(t.Db, payFast, new NotificationService(t.Db), email, Options.Create(new EventOptions())), email);
    }

    private static ClassSession Event(TestDb t, decimal price = 250, int capacity = 10, double startsInDays = 5, string format = EventKinds.InPerson,
        string kind = EventKinds.Workshop, bool waitlist = false, string? link = null, int closeHours = 24, DateTime? opensAt = null, int earlyHours = 0)
    {
        var c = t.AddClass(capacity, TimeSpan.FromDays(startsInDays));
        c.Name = "Sunrise Workshop"; c.Price = price; c.Format = format; c.Kind = kind; c.WaitlistEnabled = waitlist;
        c.JoinLink = link; c.JoinPlatform = link is null ? null : "Zoom"; c.SalesCloseHours = closeHours; c.SalesOpenAt = opensAt; c.MemberEarlyHours = earlyHours;
        c.Location = format == EventKinds.Online ? "Online" : "Rhythm & Flow Studio";
        t.Db.SaveChanges();
        return c;
    }

    /// <summary>Buys and pays for a ticket the way a real customer would, and returns the confirmed booking.</summary>
    private static async Task<Booking> Bought(EventBookingService svc, TestDb t, User user, ClassSession c, string pf = "PF-1")
    {
        var (r, error) = await svc.TicketAsync(user.Id, c.Id, useCredit: false);
        Assert.Null(error);
        Assert.True(await svc.ApplyPaymentResultAsync(r!.BookingId, "COMPLETE", c.Price, pf));
        return await t.Db.Bookings.AsNoTracking().SingleAsync(b => b.Id == r.BookingId);
    }

    private static void AddCredit(TestDb t, User user, decimal amount, int daysValid = 100)
    {
        t.Db.Credits.Add(new AccountCredit { UserId = user.Id, Amount = amount, Remaining = amount, ExpiresAt = DateTime.UtcNow.AddDays(daysValid), Reason = "Test" });
        t.Db.SaveChanges();
    }

    // ---- Tickets ----

    [Fact]
    public async Task Buying_a_ticket_holds_it_and_returns_a_once_off_PayFast_link_for_the_ticket_price()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250);

        var (r, error) = await svc.TicketAsync(user.Id, c.Id, true);

        Assert.Null(error);
        Assert.False(r!.Confirmed);
        var url = Uri.UnescapeDataString(r.PaymentUrl!);
        Assert.Contains("&amount=250.00&", url);
        Assert.Contains($"&m_payment_id=E{r.BookingId}&", url);
        Assert.DoesNotContain("subscription_type", url);
        var b = await t.Db.Bookings.SingleAsync();
        Assert.Equal(BookingStatus.PendingPayment, b.Status);
        Assert.True(b.HoldUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task A_free_event_does_not_take_a_ticket_and_a_paid_one_cannot_be_booked_the_free_way()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var free = Event(t, 0); var paid = Event(t, 250);

        var (r, error) = await svc.TicketAsync(t.AddUser().Id, free.Id, true);
        Assert.Null(r); Assert.Contains("free", error);

        var bookings = new BookingService(t.Db, Options.Create(new BookingOptions()));
        var (ok, freeError, _) = await bookings.BookAsync(t.AddUser().Id, paid.Id);
        Assert.False(ok); Assert.Contains("needs a ticket", freeError);
    }

    [Fact]
    public async Task A_confirmed_payment_books_the_ticket_and_sends_the_confirmation_with_everything_the_client_listed()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var user = t.AddUser("Thandi Mokoena"); var c = Event(t, 250, kind: EventKinds.Workshop);

        var b = await Bought(svc, t, user, c);

        Assert.Equal(BookingStatus.Booked, b.Status);
        Assert.Equal(250m, b.AmountPaid);
        var mail = Assert.Single(email.Sent);
        Assert.Equal(user.Email, mail.To);
        foreach (var expected in new[]
        {
            "Sunrise Workshop", "South African time", "In person at Rhythm & Flow Studio", "R250.00 (ZAR)", $"RF-E{b.Id:D6}", "Thandi Mokoena", user.Email,
            "info@rhythmandflow.co.za", "+27-83-440-3317", "Practical Information for Your Session", "wellness workshops", "48 hours",
            "Thank you for making space for yourself with Rhythm & Flow. We look forward to moving and flowing with you.",
        })
            Assert.Contains(expected, mail.Body);
        Assert.Contains(t.Db.Notifications, n => n.UserId == user.Id && n.Title == "Booking confirmed");
    }

    [Fact]
    public async Task The_same_payment_notice_twice_confirms_once_and_a_wrong_amount_is_refused()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250);
        var (r, _) = await svc.TicketAsync(user.Id, c.Id, false);

        Assert.False(await svc.ApplyPaymentResultAsync(r!.BookingId, "COMPLETE", 100m, "PF-X"));
        Assert.Equal(BookingStatus.PendingPayment, (await t.Db.Bookings.SingleAsync()).Status);
        Assert.True(await svc.ApplyPaymentResultAsync(r.BookingId, "COMPLETE", 250m, "PF-1"));
        Assert.True(await svc.ApplyPaymentResultAsync(r.BookingId, "COMPLETE", 250m, "PF-1"));

        Assert.Single(email.Sent);
    }

    [Fact]
    public async Task An_online_event_with_a_link_sends_a_separate_joining_email_and_one_without_a_link_says_it_will_follow()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var withLink = Event(t, 150, format: EventKinds.Online, link: "https://zoom.us/j/123");
        var without = Event(t, 150, format: EventKinds.Online);
        var user = t.AddUser();

        var a = await Bought(svc, t, user, withLink, "PF-A");
        Assert.Equal(2, email.Sent.Count);
        Assert.Contains("Joining details", email.Sent[1].Subject);
        Assert.Contains("https://zoom.us/j/123", email.Sent[1].Body);
        Assert.DoesNotContain("https://zoom.us/j/123", email.Sent[0].Body);   // the link comes in its own email
        Assert.NotNull(a.LinkSentAt);

        email.Sent.Clear();
        await Bought(svc, t, user, without, "PF-B");
        var mail = Assert.Single(email.Sent);
        Assert.Contains("A separate email with the Zoom joining link will follow", mail.Body.Replace("platform", "Zoom"));
    }

    [Fact]
    public async Task Adding_the_link_later_emails_everyone_who_already_has_a_ticket_once()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var c = Event(t, 150, format: EventKinds.Online);
        await Bought(svc, t, t.AddUser(), c, "PF-1"); await Bought(svc, t, t.AddUser(), c, "PF-2");
        email.Sent.Clear();

        c.JoinLink = "https://meet.google.com/abc"; c.JoinPlatform = "Google Meet"; t.Db.SaveChanges();
        await svc.SendJoinLinksAsync(c.Id);
        await svc.SendJoinLinksAsync(c.Id);

        Assert.Equal(2, email.Sent.Count);
        Assert.All(email.Sent, m => Assert.Contains("meet.google.com", m.Body));
    }

    // ---- Capacity, sales window, waiting list ----

    [Fact]
    public async Task A_held_ticket_counts_as_taken_and_is_released_when_the_hold_runs_out()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, capacity: 1);
        var (first, _) = await svc.TicketAsync(t.AddUser().Id, c.Id, false);
        Assert.NotNull(first);

        var (second, error) = await svc.TicketAsync(t.AddUser().Id, c.Id, false);
        Assert.Null(second); Assert.Contains("sold out", error);

        await svc.SweepAsync(DateTime.UtcNow.AddHours(2));   // the first customer never paid
        var (third, _) = await svc.TicketAsync(t.AddUser().Id, c.Id, false);
        Assert.NotNull(third);
    }

    [Fact]
    public async Task Ticket_sales_close_at_the_time_set_for_the_event()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, startsInDays: 0.8, closeHours: 24);    // starts in about 19 hours, sales closed 24 hours before

        var (r, error) = await svc.TicketAsync(t.AddUser().Id, c.Id, false);

        Assert.Null(r); Assert.Contains("closed", error);
    }

    [Fact]
    public async Task Members_of_the_second_plan_can_buy_before_sales_open_to_everyone()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, opensAt: DateTime.UtcNow.AddHours(30), earlyHours: 48);
        var member = t.AddUser(); var other = t.AddUser();
        t.AddSubscription(member, t.AddPlan(tier: 2, price: 249), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10));

        var (m, mError) = await svc.TicketAsync(member.Id, c.Id, false);
        var (o, oError) = await svc.TicketAsync(other.Id, c.Id, false);

        Assert.Null(mError); Assert.NotNull(m);
        Assert.Null(o); Assert.Contains("Ticket sales open on", oError);
    }

    [Fact]
    public async Task A_sold_out_event_with_a_waiting_list_offers_the_place_to_the_first_person_when_it_frees_up()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, capacity: 1, waitlist: true);
        var holder = t.AddUser(); var waiting = t.AddUser(); var later = t.AddUser();
        var b = await Bought(svc, t, holder, c);

        var (soldOut, soldOutError) = await svc.TicketAsync(waiting.Id, c.Id, false);
        Assert.Null(soldOut); Assert.Contains("waiting list", soldOutError);
        Assert.True((await svc.JoinWaitlistAsync(waiting.Id, c.Id)).Ok);
        Assert.True((await svc.JoinWaitlistAsync(later.Id, c.Id)).Ok);
        Assert.False((await svc.JoinWaitlistAsync(waiting.Id, c.Id)).Ok);   // only once

        var cancelled = await svc.CancelAsync(holder.Id, b.Id, "CREDIT", null);
        Assert.Null(cancelled.Error);

        Assert.Contains(t.Db.Notifications, n => n.UserId == waiting.Id && n.Title == "A place opened up");
        Assert.DoesNotContain(t.Db.Notifications, n => n.UserId == later.Id && n.Title == "A place opened up");   // one offer at a time
        var (second, _) = await svc.TicketAsync(later.Id, c.Id, false);
        Assert.Null(second);                                          // the place is held for the first person
        var (offer, offerError) = await svc.TicketAsync(waiting.Id, c.Id, false);
        Assert.Null(offerError); Assert.NotNull(offer);
    }

    [Fact]
    public async Task An_offered_waiting_list_place_that_is_not_taken_moves_on_to_the_next_person()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, capacity: 1, waitlist: true);
        var holder = t.AddUser(); var first = t.AddUser(); var second = t.AddUser();
        var b = await Bought(svc, t, holder, c);
        await svc.JoinWaitlistAsync(first.Id, c.Id); await Task.Delay(5); await svc.JoinWaitlistAsync(second.Id, c.Id);
        await svc.CancelAsync(holder.Id, b.Id, "CREDIT", null);

        await svc.SweepAsync(DateTime.UtcNow.AddHours(13));   // the 12-hour offer has run out

        Assert.Contains(t.Db.Notifications, n => n.UserId == second.Id && n.Title == "A place opened up");
    }

    [Fact]
    public async Task A_waiting_list_only_exists_when_the_event_has_one_and_is_full()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var noList = Event(t, 250, capacity: 1);
        var open = Event(t, 250, capacity: 5, waitlist: true);

        await Bought(svc, t, t.AddUser(), noList, "PF-1");
        Assert.Contains("no waiting list", (await svc.JoinWaitlistAsync(t.AddUser().Id, noList.Id)).Error);
        Assert.Contains("places left", (await svc.JoinWaitlistAsync(t.AddUser().Id, open.Id)).Error);
    }

    // ---- Account credit ----

    [Fact]
    public async Task Account_credit_is_used_first_and_given_back_if_the_payment_is_abandoned()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250); AddCredit(t, user, 100);

        var (r, _) = await svc.TicketAsync(user.Id, c.Id, true);

        Assert.Equal(150m, r!.AmountDue); Assert.Equal(100m, r.CreditApplied);
        Assert.Contains("&amount=150.00&", Uri.UnescapeDataString(r.PaymentUrl!));
        Assert.Equal(0m, (await svc.CreditsAsync(user.Id)).Balance);

        Assert.True(await svc.ApplyPaymentResultAsync(r.BookingId, "CANCELLED", 0m, null));
        Assert.Equal(100m, (await svc.CreditsAsync(user.Id)).Balance);
        Assert.Equal(BookingStatus.Cancelled, (await t.Db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Credit_that_covers_the_whole_price_books_the_event_without_PayFast()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250); AddCredit(t, user, 300);

        var (r, error) = await svc.TicketAsync(user.Id, c.Id, true);

        Assert.Null(error);
        Assert.True(r!.Confirmed); Assert.Null(r.PaymentUrl);
        Assert.Equal(50m, (await svc.CreditsAsync(user.Id)).Balance);
        Assert.Single(email.Sent);
        Assert.Contains("including R250.00 account credit", email.Sent[0].Body);
    }

    [Fact]
    public async Task Credit_that_has_expired_is_not_used()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250); AddCredit(t, user, 100, daysValid: -1);

        var (r, _) = await svc.TicketAsync(user.Id, c.Id, true);

        Assert.Equal(250m, r!.AmountDue);
    }

    // ---- Cancelling ----

    [Fact]
    public async Task Cancelling_48_hours_or_more_ahead_gives_the_full_fee_back_as_twelve_months_of_credit()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var b = await Bought(svc, t, user, Event(t, 250, startsInDays: 5));

        var (result, error) = await svc.CancelAsync(user.Id, b.Id, "CREDIT", null);

        Assert.Null(error); Assert.Contains("R250.00", result!.Message);
        var credit = await t.Db.Credits.SingleAsync();
        Assert.Equal(250m, credit.Amount);
        Assert.InRange(credit.ExpiresAt, DateTime.UtcNow.AddMonths(12).AddMinutes(-5), DateTime.UtcNow.AddMonths(12).AddMinutes(5));
        Assert.Equal(BookingStatus.Cancelled, (await t.Db.Bookings.SingleAsync()).Status);
        Assert.Null((await t.Db.Bookings.SingleAsync()).RefundStatus);
    }

    [Fact]
    public async Task Choosing_a_refund_queues_it_for_the_administrator_and_it_is_recorded_when_paid()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); t.AddUser(role: Roles.Admin); var b = await Bought(svc, t, user, Event(t, 250, startsInDays: 5));

        await svc.CancelAsync(user.Id, b.Id, "REFUND", null);

        var refunds = await svc.RefundsAsync();
        var row = Assert.Single(refunds);
        Assert.Equal(250m, row.Amount); Assert.Equal("PF-1", row.PayFastPaymentId);
        Assert.Empty(t.Db.Credits);
        Assert.Contains(t.Db.Notifications, n => n.Title == "Refund to process");

        Assert.True((await svc.MarkRefundedAsync(b.Id)).Ok);
        Assert.Empty(await svc.RefundsAsync());
        Assert.Contains(t.Db.Notifications, n => n.UserId == user.Id && n.Title == "Refund sent");
        Assert.False((await svc.MarkRefundedAsync(b.Id)).Ok);
    }

    [Fact]
    public async Task A_refund_only_returns_the_cash_and_the_credit_part_goes_back_as_credit()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250, startsInDays: 5); AddCredit(t, user, 100);
        var (r, _) = await svc.TicketAsync(user.Id, c.Id, true);
        await svc.ApplyPaymentResultAsync(r!.BookingId, "COMPLETE", 150m, "PF-9");

        await svc.CancelAsync(user.Id, r.BookingId, "REFUND", null);

        Assert.Equal(150m, (await svc.RefundsAsync()).Single().Amount);
        Assert.Equal(100m, (await svc.CreditsAsync(user.Id)).Balance);
    }

    [Fact]
    public async Task Cancelling_less_than_48_hours_ahead_becomes_a_request_and_the_booking_stays()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); t.AddUser(role: Roles.Admin); var b = await Bought(svc, t, user, Event(t, 250, startsInDays: 1, closeHours: 2));

        var (result, error) = await svc.CancelAsync(user.Id, b.Id, "REFUND", "Sick");

        Assert.Null(error); Assert.Equal("REQUESTED", result!.Status);
        Assert.Equal(BookingStatus.Booked, (await t.Db.Bookings.SingleAsync()).Status);
        var req = Assert.Single(await svc.RequestsAsync());
        Assert.Equal(250m, req.TotalPaid); Assert.Equal(125m, req.MaxAmount); Assert.Equal("REFUND", req.Choice);
        Assert.Contains("Sick", t.Db.Notifications.Single(n => n.Title == "Late cancellation request").Body);
        Assert.Contains("already with us", (await svc.CancelAsync(user.Id, b.Id, "REFUND", null)).Error);
    }

    [Fact]
    public async Task An_administrator_can_agree_up_to_half_of_a_late_cancellation_or_decline_it()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var a = t.AddUser(); var d = t.AddUser();
        var c = Event(t, 250, startsInDays: 1, capacity: 5, closeHours: 2);
        var booked = await Bought(svc, t, a, c, "PF-A"); var declined = await Bought(svc, t, d, c, "PF-D");
        await svc.CancelAsync(a.Id, booked.Id, "CREDIT", null); await svc.CancelAsync(d.Id, declined.Id, "CREDIT", null);

        Assert.Contains("no more than R125.00", (await svc.ResolveRequestAsync(booked.Id, true, 200m)).Error);
        Assert.True((await svc.ResolveRequestAsync(booked.Id, true, 125m)).Ok);
        Assert.True((await svc.ResolveRequestAsync(declined.Id, false, null)).Ok);

        Assert.Equal(BookingStatus.Cancelled, (await t.Db.Bookings.FindAsync(booked.Id))!.Status);
        Assert.Equal(125m, (await svc.CreditsAsync(a.Id)).Balance);
        var stays = await t.Db.Bookings.FindAsync(declined.Id);
        Assert.Equal(BookingStatus.Booked, stays!.Status); Assert.Equal("DECLINED", stays.RequestStatus);
        Assert.Empty(await svc.RequestsAsync());
        Assert.Contains(t.Db.Notifications, n => n.UserId == d.Id && n.Body.Contains("info@rhythmandflow.co.za"));
    }

    [Fact]
    public async Task When_the_studio_cancels_a_paid_event_ticket_holders_choose_a_full_refund_or_credit_and_free_bookings_are_just_cancelled()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var paid = t.AddUser(); var c = Event(t, 250, capacity: 5);
        var b = await Bought(svc, t, paid, c);
        var freeUser = t.AddUser();
        t.Db.Bookings.Add(new Booking { UserId = freeUser.Id, ClassId = c.Id, Status = BookingStatus.Booked }); t.Db.SaveChanges();

        c.Status = "CANCELLED"; t.Db.SaveChanges();
        await svc.CancelEventAsync(c);

        var held = await t.Db.Bookings.FindAsync(b.Id);
        Assert.Equal(BookingStatus.EventCancelled, held!.Status); Assert.Equal("EVENT_CANCELLED", held.ChoiceNeeded);
        Assert.Equal(BookingStatus.Cancelled, (await t.Db.Bookings.SingleAsync(x => x.UserId == freeUser.Id)).Status);
        Assert.Contains(t.Db.Notifications, n => n.UserId == paid.Id && n.Title.Contains("choose a refund or credit"));

        Assert.Contains("choose a refund", (await svc.ChooseAsync(paid.Id, b.Id, "KEEP")).Error);
        var (result, error) = await svc.ChooseAsync(paid.Id, b.Id, "REFUND");
        Assert.Null(error); Assert.Contains("R250.00", result!.Message);
        Assert.Equal(250m, (await svc.RefundsAsync()).Single().Amount);
        Assert.Equal(BookingStatus.Cancelled, (await t.Db.Bookings.FindAsync(b.Id))!.Status);
    }

    [Fact]
    public async Task When_a_paid_event_moves_each_ticket_holder_can_keep_the_place_or_take_a_refund_or_credit()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var keeps = t.AddUser(); var leaves = t.AddUser(); var c = Event(t, 250, capacity: 5);
        var a = await Bought(svc, t, keeps, c, "PF-1"); var b = await Bought(svc, t, leaves, c, "PF-2");

        await svc.EventRescheduledAsync(c);

        Assert.Equal("EVENT_RESCHEDULED", (await t.Db.Bookings.FindAsync(a.Id))!.ChoiceNeeded);
        Assert.Equal(BookingStatus.Booked, (await svc.ChooseAsync(keeps.Id, a.Id, "KEEP")).Result!.Status);
        Assert.Null((await t.Db.Bookings.FindAsync(a.Id))!.ChoiceNeeded);
        await svc.ChooseAsync(leaves.Id, b.Id, "CREDIT");
        Assert.Equal(250m, (await svc.CreditsAsync(leaves.Id)).Balance);
        Assert.Contains("nothing to choose", (await svc.ChooseAsync(keeps.Id, a.Id, "KEEP")).Error);
    }

    [Fact]
    public async Task A_payment_that_arrives_after_the_hold_ended_is_never_double_booked_and_is_queued_for_refund()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var late = t.AddUser(); var c = Event(t, 250, capacity: 1);
        var (r, _) = await svc.TicketAsync(late.Id, c.Id, false);
        await svc.SweepAsync(DateTime.UtcNow.AddHours(2));                          // hold released
        await Bought(svc, t, t.AddUser(), c, "PF-OTHER");                            // someone else got the place

        Assert.True(await svc.ApplyPaymentResultAsync(r!.BookingId, "COMPLETE", 250m, "PF-LATE"));

        Assert.NotEqual(BookingStatus.Booked, (await t.Db.Bookings.FindAsync(r.BookingId))!.Status);
        Assert.Contains(await svc.RefundsAsync(), x => x.PayFastPaymentId == "PF-LATE");
        Assert.Contains(t.Db.Notifications, n => n.UserId == late.Id && n.Title == "Your ticket wasn't confirmed");
    }

    // ---- Reminders ----

    [Fact]
    public async Task A_paid_event_gets_a_24_hour_reminder_then_a_1_hour_reminder_each_only_once()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var user = t.AddUser(); var c = Event(t, 150, format: EventKinds.Online, link: "https://zoom.us/j/9", startsInDays: 5);
        await Bought(svc, t, user, c);
        email.Sent.Clear();

        c.StartTime = DateTime.UtcNow.AddHours(20); c.EndTime = c.StartTime.AddHours(1); t.Db.SaveChanges();
        await svc.SendRemindersAsync(DateTime.UtcNow); await svc.SendRemindersAsync(DateTime.UtcNow);
        Assert.Single(email.Sent); Assert.Contains("Tomorrow", email.Sent[0].Subject);

        c.StartTime = DateTime.UtcNow.AddMinutes(40); c.EndTime = c.StartTime.AddHours(1); t.Db.SaveChanges();
        await svc.SendRemindersAsync(DateTime.UtcNow); await svc.SendRemindersAsync(DateTime.UtcNow);
        Assert.Equal(2, email.Sent.Count);
        Assert.Contains("Starting soon", email.Sent[1].Subject);
        Assert.Contains("https://zoom.us/j/9", email.Sent[1].Body);
        Assert.Equal(2, t.Db.Notifications.Count(n => n.UserId == user.Id && n.Kind == "CLASS"));
    }

    [Fact]
    public async Task A_free_class_gets_no_server_reminder_because_the_phone_already_reminds_for_those()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var c = t.AddClass(10, TimeSpan.FromHours(5));
        t.Db.Bookings.Add(new Booking { UserId = t.AddUser().Id, ClassId = c.Id, Status = BookingStatus.Booked }); t.Db.SaveChanges();

        await svc.SendRemindersAsync(DateTime.UtcNow);

        Assert.Empty(email.Sent);
    }

    // ---- What customers see ----

    [Fact]
    public async Task The_class_list_shows_price_format_and_whether_I_can_buy_now()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var open = Event(t, 250, format: EventKinds.Online, link: "https://zoom.us/j/1");
        var full = Event(t, 250, capacity: 1);
        await Bought(svc, t, t.AddUser(), full);
        var bookings = new BookingService(t.Db, Options.Create(new BookingOptions()));

        var list = await bookings.UpcomingAsync(t.AddUser().Id);

        var o = list.Single(x => x.Id == open.Id);
        Assert.Equal(250m, o.Price); Assert.Equal("ONLINE", o.Format); Assert.True(o.CanBuyNow); Assert.Equal("Zoom", o.JoinPlatform);
        var f = list.Single(x => x.Id == full.Id);
        Assert.False(f.CanBuyNow); Assert.Contains("sold out", f.BuyBlockedReason); Assert.Equal(0, f.SpotsLeft);
    }

    [Fact]
    public async Task My_bookings_shows_the_reference_the_joining_link_and_what_I_still_have_to_decide()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var c = Event(t, 250, format: EventKinds.Online, link: "https://zoom.us/j/1");
        var b = await Bought(svc, t, user, c);
        var bookings = new BookingService(t.Db, Options.Create(new BookingOptions()));

        var mine = Assert.Single(await bookings.MineAsync(user.Id));
        Assert.Equal($"RF-E{b.Id:D6}", mine.Reference); Assert.Equal("https://zoom.us/j/1", mine.JoinLink);
        Assert.True(mine.FreeCancellation); Assert.Equal(250m, mine.TotalPaid);

        c.Status = "CANCELLED"; t.Db.SaveChanges(); await svc.CancelEventAsync(c);
        var after = Assert.Single(await bookings.MineAsync(user.Id));
        Assert.Equal("EVENT_CANCELLED", after.ChoiceNeeded); Assert.Null(after.JoinLink);
    }

    [Fact]
    public async Task The_old_free_cancel_refuses_a_paid_ticket_but_still_works_for_a_free_booking()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var paid = await Bought(svc, t, user, Event(t, 250));
        var bookings = new BookingService(t.Db, Options.Create(new BookingOptions()));
        var free = t.AddClass(); var freeBooking = new Booking { UserId = user.Id, ClassId = free.Id, Status = BookingStatus.Booked };
        t.Db.Bookings.Add(freeBooking); t.Db.SaveChanges();

        Assert.Contains("paid ticket", (await bookings.CancelAsync(user.Id, paid.Id)).Error);
        Assert.True((await bookings.CancelAsync(user.Id, freeBooking.Id)).Ok);
    }

    [Fact]
    public void An_event_payment_id_is_told_apart_from_a_subscription_one()
    {
        Assert.True(PayFastService.TryParseEventPaymentId("E42", out var id)); Assert.Equal(42, id);
        Assert.False(PayFastService.TryParseEventPaymentId("42", out _));
        Assert.False(PayFastService.TryParseEventPaymentId("Eabc", out _));
        Assert.Equal("E7", PayFastService.EventPaymentId(7));
    }
}

public class EventAdminTests
{
    private static (ScheduleAdminService svc, EventBookingService events) Build(TestDb t)
    {
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        var notes = new NotificationService(t.Db);
        var events = new EventBookingService(t.Db, payFast, notes, new FakeEmail(), Options.Create(new EventOptions()));
        return (new ScheduleAdminService(t.Db, notes, events), events);
    }

    private static ClassUpsert Upsert(ClassSession c, decimal price = 250, string? kind = "WORKSHOP", string? format = "IN_PERSON", bool waitlist = false,
        string? link = null, DateTime? start = null, int capacity = 10) =>
        new(c.Name, "d", "Deni", c.Location, start ?? c.StartTime, (start ?? c.StartTime).AddHours(1), capacity, kind, format, price, "https://x/p.jpg", 24, null, 0, null, link, null, waitlist);

    [Theory]
    [InlineData(3, "WORKSHOP", "IN_PERSON", false, null, "R0 (free) or at least R5")]
    [InlineData(250, "PARTY", "IN_PERSON", false, null, "class, workshop, retreat or coaching")]
    [InlineData(250, "WORKSHOP", "HYBRID", false, null, "online or in person")]
    [InlineData(0, "CLASS", "IN_PERSON", true, null, "waiting list is for paid events")]
    [InlineData(250, "WORKSHOP", "ONLINE", false, "zoom.us/j/1", "must start with https://")]
    public void Invalid_event_settings_are_refused(double price, string kind, string format, bool waitlist, string? link, string expected)
    {
        var c = new ClassSession { Name = "N", Location = "L", StartTime = DateTime.UtcNow.AddDays(3), EndTime = DateTime.UtcNow.AddDays(3).AddHours(1) };
        var error = ScheduleAdminService.ValidateEvent(Upsert(c, (decimal)price, kind, format, waitlist, link));
        Assert.Contains(expected, error);
    }

    [Fact]
    public async Task A_new_event_saves_every_detail_the_client_asked_to_set()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var start = DateTime.UtcNow.AddDays(10);
        var req = new ClassUpsert("Breath Retreat", "Two days away", "Deni", "Magaliesberg", start, start.AddHours(48), 12, "RETREAT", "IN_PERSON",
            1800, "https://x/poster.jpg", 336, DateTime.UtcNow.AddDays(1), 72, "Zoom", "https://zoom.us/j/5", "Bring a towel", true);

        var (ok, id, error) = await svc.CreateClassAsync(req);

        Assert.True(ok, error);
        var c = await t.Db.Classes.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("RETREAT", c.Kind); Assert.Equal(1800m, c.Price); Assert.Equal(336, c.SalesCloseHours); Assert.Equal(72, c.MemberEarlyHours);
        Assert.Equal("Bring a towel", c.PracticalNote); Assert.True(c.WaitlistEnabled); Assert.Equal("https://x/poster.jpg", c.PosterUrl);
    }

    [Fact]
    public async Task The_price_cannot_change_once_tickets_have_been_sold()
    {
        using var t = new TestDb(); var (svc, events) = Build(t);
        var c = t.AddClass(); c.Price = 250; t.Db.SaveChanges();
        t.Db.Bookings.Add(new Booking { UserId = t.AddUser().Id, ClassId = c.Id, Status = BookingStatus.Booked, AmountPaid = 250 }); t.Db.SaveChanges();

        var (ok, _, error) = await svc.UpdateClassAsync(c.Id, Upsert(c, price: 300));

        Assert.False(ok); Assert.Contains("price can't be changed", error);
    }

    [Fact]
    public async Task Moving_a_paid_event_asks_each_ticket_holder_what_they_want_and_does_not_send_the_plain_update()
    {
        using var t = new TestDb(); var (svc, events) = Build(t);
        var c = t.AddClass(); c.Price = 250; c.Name = "Workshop"; t.Db.SaveChanges();
        var holder = t.AddUser();
        t.Db.Bookings.Add(new Booking { UserId = holder.Id, ClassId = c.Id, Status = BookingStatus.Booked, AmountPaid = 250 }); t.Db.SaveChanges();

        var (ok, _, error) = await svc.UpdateClassAsync(c.Id, Upsert(c, 250, start: c.StartTime.AddDays(1)));

        Assert.True(ok, error);
        Assert.Equal("EVENT_RESCHEDULED", (await t.Db.Bookings.SingleAsync()).ChoiceNeeded);
        Assert.Contains(t.Db.Notifications, n => n.UserId == holder.Id && n.Title == "Event rescheduled: your choice");
        Assert.DoesNotContain(t.Db.Notifications, n => n.Title == "Class updated");
    }

    [Fact]
    public async Task A_free_class_that_moves_still_just_tells_the_people_booked()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = t.AddClass(); var u = t.AddUser();
        t.Db.Bookings.Add(new Booking { UserId = u.Id, ClassId = c.Id, Status = BookingStatus.Booked }); t.Db.SaveChanges();

        await svc.UpdateClassAsync(c.Id, Upsert(c, 0, "CLASS", start: c.StartTime.AddHours(3)));

        Assert.Contains(t.Db.Notifications, n => n.UserId == u.Id && n.Title == "Class updated");
        Assert.Null((await t.Db.Bookings.SingleAsync()).ChoiceNeeded);
    }

    [Fact]
    public async Task Saving_a_joining_link_emails_the_people_who_already_have_tickets()
    {
        using var t = new TestDb();
        var mail = new FakeEmail();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        var notes = new NotificationService(t.Db);
        var events = new EventBookingService(t.Db, payFast, notes, mail, Options.Create(new EventOptions()));
        var svc = new ScheduleAdminService(t.Db, notes, events);
        var c = t.AddClass(); c.Price = 150; c.Format = "ONLINE"; t.Db.SaveChanges();
        t.Db.Bookings.Add(new Booking { UserId = t.AddUser().Id, ClassId = c.Id, Status = BookingStatus.Booked, AmountPaid = 150 }); t.Db.SaveChanges();

        await svc.UpdateClassAsync(c.Id, Upsert(c, 150, "WORKSHOP", "ONLINE", link: "https://zoom.us/j/77"));

        var sent = Assert.Single(mail.Sent);
        Assert.Contains("https://zoom.us/j/77", sent.Body);
    }
}

public class EventSchemaTests
{
    [Fact]
    public async Task A_database_from_before_paid_events_gets_the_new_columns_and_the_credit_table_and_keeps_its_bookings()
    {
        using var t = new TestDb();
        var u = t.AddUser(); var c = t.AddClass();
        t.Db.Bookings.Add(new Booking { UserId = u.Id, ClassId = c.Id, Status = BookingStatus.Booked }); t.Db.SaveChanges();
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Classes\" DROP COLUMN \"Price\"");
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Bookings\" DROP COLUMN \"AmountPaid\"");
        await t.Db.Database.ExecuteSqlRawAsync("DROP TABLE \"Credits\"");

        await SchemaUpgrades.ApplyAsync(t.Db);

        Assert.Equal(0m, (await t.Db.Classes.AsNoTracking().SingleAsync()).Price);
        Assert.Equal(0m, (await t.Db.Bookings.AsNoTracking().SingleAsync()).AmountPaid);
        t.Db.Credits.Add(new AccountCredit { UserId = u.Id, Amount = 10, Remaining = 10, ExpiresAt = DateTime.UtcNow.AddDays(1), Reason = "x" });
        await t.Db.SaveChangesAsync();
        Assert.Equal(1, await t.Db.Credits.CountAsync());
    }
}
