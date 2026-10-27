/* Demo mode (open /app/?demo): an in-memory stand-in for the server, so every screen can be viewed without an account.
   Sign in with any email to see the customer side, or with the username "admin" to see the admin side. The email code is 123456. */
"use strict";

const DEMO = (() => {
  const day = (d, h, m = 0) => { const t = new Date(); t.setDate(t.getDate() + d); t.setHours(h, m, 0, 0); return t.toISOString(); };
  const customer = { id: "demo-user", fullName: "Demo Member", username: "demo", email: "demo@example.com", role: "CUSTOMER", about: "Trying out the app." };
  const admin = { id: "demo-admin", fullName: "Demo Admin", username: "admin", email: "admin@example.com", role: "ADMIN", about: "" };
  let user = customer;
  const plans = [
    { id: 1, name: "Move & Breathe", description: "Your space to move, pause and reconnect with yourself, at your own pace.", price: 149, billingFrequency: "MONTHLY", tier: 1, annualPrice: 1490, launchPrice: 99, launchEndsAt: day(40, 23), trialDays: 7,
      features: ["Full on-demand video library", "Yoga, barre, dance, mobility and stretching", "Meditation, breathwork and strength classes"] },
    { id: 2, name: "Move, Breathe & Grow", description: "Build a more intentional practice through movement, mindful breathing and guided programmes.", price: 249, billingFrequency: "MONTHLY", tier: 2, annualPrice: 2490, launchPrice: null, launchEndsAt: null, trialDays: 0,
      features: ["Everything in Move & Breathe", "Access to all structured programmes", "A more structured way to establish consistency"] },
  ];
  const programmes = [
    { id: 1, name: "Gentle Beginnings", description: "A soft start: breath, balance and easy flows.", minTier: 1, lessonCount: 3, active: true },
    { id: 2, name: "Dance Cardio", description: "Move to the music and lift your mood.", minTier: 2, lessonCount: 2, active: true },
    { id: 5, name: "Free Practices", description: "Free practices from the Rhythm & Flow YouTube channel.", minTier: 0, lessonCount: 2, active: true },
  ];
  const lessons = [
    { id: 1, programmeId: 1, title: "Morning breath and balance", description: "Wake the body gently with breath-led movement.", category: "Yoga", level: "Beginner", durationSeconds: 600, isPreview: false },
    { id: 2, programmeId: 1, title: "Easy hip opener", description: "Soften tight hips with supported poses.", category: "Stretch & Mobility", level: "Beginner", durationSeconds: 900, isPreview: false },
    { id: 3, programmeId: 1, title: "Evening wind-down", description: "A calm close to the day.", category: "Meditation", level: "Beginner", durationSeconds: 480, isPreview: false },
    { id: 4, programmeId: 2, title: "Salsa starter", description: "Basic steps and hip rhythm.", category: "Dance", level: "Intermediate", durationSeconds: 1200, isPreview: false },
    { id: 5, programmeId: 2, title: "Barre burn", description: "Low-impact strength at the barre.", category: "Barre & Pilates", level: "Intermediate", durationSeconds: 1500, isPreview: false },
    { id: 6, programmeId: 2, title: "Full-body strength", description: "Strength and stability with simple moves.", category: "Strength & Conditioning", level: "Intermediate", durationSeconds: 1800, isPreview: false },
    { id: 7, programmeId: 1, title: "Balancing breathwork", description: "Breathing practices to support focus and relaxation.", category: "Breathwork", level: "Beginner", durationSeconds: 640, isPreview: false },
    { id: 11, programmeId: 5, title: "10 Minute Stretch and Mobility Morning Routine", description: "A morning routine for flexibility, circulation and mental clarity.", category: "Stretch & Mobility", level: "Beginner", durationSeconds: 596, isPreview: true, youtubeId: "NFixPh6y39U" },
    { id: 12, programmeId: 5, title: "Mindfulness Check-In", description: "A short guided meditation.", category: "Meditation", level: "Beginner", durationSeconds: 968, isPreview: true, youtubeId: "mdyAXSmZx3w" },
  ].map((l) => Object.assign(l, { programmeName: programmes.find((p) => p.id === l.programmeId).name, thumbnailUrl: null }));
  const clip = "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4";
  const state = {
    tier: 0, subs: [], progress: {}, bookings: [], credit: 0, nextId: 100, emailVerified: false, signedUp: null,
    journal: [{ id: 1, kind: "CHECKIN", mood: "Grounded", text: "", createdAt: day(-1, 8) }, { id: 2, kind: "GRATITUDE", mood: "", text: "A quiet morning and good coffee.", createdAt: day(-2, 20) }],
    notifications: [{ id: 1, kind: "WELCOME", title: "Welcome to Rhythm & Flow", body: "This is demo data. Nothing here is saved.", route: null, createdAt: day(0, 7), read: false }],
    classes: [
      { id: 1, name: "Vinyasa Flow", description: "Breath-linked movement.", coachName: "Deni", location: "Studio A", startTime: day(1, 9), endTime: day(1, 10), capacity: 12, spotsLeft: 5, status: "SCHEDULED", kind: "CLASS", format: "IN_PERSON", price: 0 },
      { id: 2, name: "Dance Fusion Workshop", description: "Mixed styles, big energy.", coachName: "Deni", location: "Studio B", startTime: day(6, 17, 30), endTime: day(6, 19), capacity: 15, spotsLeft: 9, status: "SCHEDULED", kind: "WORKSHOP", format: "IN_PERSON", price: 250, canBuyNow: true, waitlistEnabled: true },
      { id: 3, name: "Online Mindfulness Evening", description: "Slow, supported practice from home.", coachName: "Deni", location: "Online", startTime: day(8, 18), endTime: day(8, 19), capacity: 10, spotsLeft: 0, status: "SCHEDULED", kind: "WORKSHOP", format: "ONLINE", joinPlatform: "Zoom", joinLink: "https://zoom.us/j/demo", price: 120, canBuyNow: false, buyBlockedReason: "This event is sold out.", waitlistEnabled: true },
    ],
    policies: [
      { id: "subscription", title: "Subscription Cancellation Policy", body: "You can cancel at any time. Your plan stays active until the end of the period you have paid for.\nThis is demo wording; the real policies come from the server." },
      { id: "events", title: "Event Cancellation and Refund Policy", body: "Cancel at least 48 hours before an event for a full refund or account credit." },
    ],
  };
  const tierOf = (p) => programmes.find((x) => x.id === p).minTier;
  const lessonDto = (l) => { const p = state.progress[l.id] || { w: 0, pct: 0 }; return Object.assign({}, l, { locked: !(l.isPreview || state.tier >= tierOf(l.programmeId)), completionPercentage: p.pct, watchTimeSeconds: p.w }); };
  const classDto = (c) => Object.assign({ canBuyNow: true, onWaitlist: state.bookings.some((b) => b.classId === c.id && b.status === "WAITLISTED"), waitlistEnabled: false, memberEarlyHours: 0, salesCloseHours: 24 }, c,
    { bookedByMe: state.bookings.some((b) => b.classId === c.id && b.status === "BOOKED") });
  const bookingDto = (b) => Object.assign({ canCancel: new Date(b.startTime) - Date.now() > 3600000, freeCancellation: new Date(b.startTime) - Date.now() > 48 * 3600000, requestStatus: null, choiceNeeded: null, refundStatus: null, totalPaid: 0, format: "IN_PERSON", kind: "CLASS" }, b);
  const fail = (msg, status = 409, extra) => { throw new ApiError(msg, status, Object.assign({ error: msg }, extra || {})); };
  const delay = (v) => new Promise((r) => setTimeout(() => r(JSON.parse(JSON.stringify(v === undefined ? null : v))), 140));
  const activeSub = () => state.subs.find((s) => s.status === "ACTIVE" && s.grantsAccess);

  async function handle(method, path, body) {
    const [p, qs] = path.split("?"); const q = new URLSearchParams(qs || "");
    const key = method + " " + p; let m;
    switch (true) {
      case key === "POST /api/auth/register":
        state.signedUp = body.email; state.emailVerified = false;
        return delay({ email: body.email, verificationRequired: true, message: "We've emailed you a 6-digit code." });
      case key === "POST /api/auth/verify-email":
        if (body.code !== "123456") fail("That code isn't valid or has expired. Please request a new one.", 400);
        state.emailVerified = true; return delay({ message: "Email confirmed." });
      case key === "POST /api/auth/resend-verification": return delay({ message: "If that address needs a code, one is on its way." });
      case key === "POST /api/auth/login":
        if (state.signedUp && !state.emailVerified && body.identifier === state.signedUp) fail("Please confirm your email address first.", 403, { code: "EMAIL_NOT_VERIFIED", email: state.signedUp });
        user = /^admin/i.test(body.identifier) ? admin : customer; return delay({ token: "demo-token", user });
      case key === "GET /api/auth/me": return delay(user);
      case key === "PUT /api/auth/me": Object.assign(user, { fullName: body.fullName, email: body.email, about: body.about || "" }); return delay(user);
      case key === "POST /api/auth/forgot-password": case key === "POST /api/auth/reset-password": case key === "POST /api/auth/change-password": return delay({ message: "Done (demo)." });
      case key === "GET /api/plans": return delay(plans);
      case key === "GET /api/policies": return delay(state.policies);
      case key === "GET /api/programmes": return delay(programmes.map((x) => Object.assign({}, x, { locked: state.tier < x.minTier })));
      case key === "GET /api/lessons": {
        let r = lessons.slice();
        if (q.get("programmeId")) r = r.filter((l) => l.programmeId === Number(q.get("programmeId")));
        if (q.get("category") && q.get("category") !== "All") r = r.filter((l) => l.category === q.get("category"));
        if (q.get("q")) r = r.filter((l) => (l.title + l.description).toLowerCase().includes(q.get("q").toLowerCase()));
        return delay(r.map(lessonDto));
      }
      case (m = /^GET \/api\/lessons\/(\d+)\/playback$/.exec(key)) !== null: {
        const l = lessons.find((x) => x.id === Number(m[1])); if (lessonDto(l).locked) fail("An active subscription is required to watch this lesson.", 403);
        return delay(l.youtubeId ? { url: "", expiresAt: day(0, 23), resumeSeconds: 0, youtubeId: l.youtubeId } : { url: clip, expiresAt: day(0, 23), resumeSeconds: (state.progress[l.id] || { w: 0 }).w });
      }
      case (m = /^GET \/api\/lessons\/(\d+)$/.exec(key)) !== null: return delay(lessonDto(lessons.find((x) => x.id === Number(m[1]))));
      case key === "POST /api/progress": {
        const l = lessons.find((x) => x.id === body.lessonId); const pct = Math.min(100, Math.round(body.watchTimeSeconds * 100 / l.durationSeconds));
        state.progress[l.id] = { w: body.watchTimeSeconds, pct };
        return delay({ lessonId: l.id, watchTimeSeconds: body.watchTimeSeconds, completionPercentage: pct, completed: pct >= 90 });
      }
      case key === "GET /api/progress/summary": {
        const v = Object.values(state.progress);
        return delay({ completedLessons: v.filter((x) => x.pct >= 90).length, inProgressLessons: v.filter((x) => x.pct > 0 && x.pct < 90).length, sessionsThisMonth: v.length, minutesWatched: Math.round(v.reduce((a, x) => a + x.w, 0) / 60) });
      }
      /* ----- subscriptions ----- */
      case key === "GET /api/subscriptions": return delay(state.subs);
      case key === "GET /api/subscriptions/payments": return delay(state.subs.map((s) => ({ id: s.id, planName: s.planName, amount: s.price, status: "COMPLETE", date: s.startDate, receipt: "RF-" + String(s.id).padStart(6, "0") })));
      case key === "POST /api/subscriptions/checkout": {
        if (activeSub()) fail("You already have an active plan. Use Change plan instead.", 400);
        const pl = plans.find((x) => x.id === body.planId); const id = state.nextId++; const annual = body.billing === "ANNUAL";
        state.subs.push({ id, planId: pl.id, planName: pl.name, tier: pl.tier, price: annual ? pl.annualPrice : pl.price, billing: annual ? "ANNUAL" : "MONTHLY", status: "ACTIVE", startDate: new Date().toISOString(), endDate: day(annual ? 365 : 30, 0), grantsAccess: true, renewing: true, inTrial: !annual && pl.trialDays > 0 });
        state.tier = pl.tier; return delay({ subscriptionId: id, paymentUrl: "#/paid" });
      }
      case (m = /^GET \/api\/subscriptions\/(\d+)\/change-preview$/.exec(key)) !== null: {
        const cur = activeSub(); const pl = plans.find((x) => x.id === Number(q.get("planId"))); const annual = q.get("billing") === "ANNUAL";
        return delay({ currentPlan: cur.planName, currentPrice: cur.price, currentBilling: cur.billing, newPlan: pl.name, newPrice: annual ? pl.annualPrice : pl.price, newBilling: annual ? "ANNUAL" : "MONTHLY",
          priceDifference: pl.price - plans.find((x) => x.id === cur.planId).price, effectiveDate: cur.endDate, accessImpact: `You keep ${cur.planName} until the end of the period you have paid for. From then you move to ${pl.name}.` });
      }
      case (m = /^POST \/api\/subscriptions\/(\d+)\/change$/.exec(key)) !== null: {
        const cur = activeSub(); const pl = plans.find((x) => x.id === body.planId); const id = state.nextId++; cur.status = "CANCELLED"; cur.renewing = false;
        state.subs.push({ id, planId: pl.id, planName: pl.name, tier: pl.tier, price: pl.price, billing: body.billing || "MONTHLY", status: "ACTIVE", startDate: cur.endDate, endDate: day(60, 0), grantsAccess: false, renewing: true, inTrial: false });
        return delay({ subscriptionId: id, paymentUrl: "#/paid" });
      }
      case (m = /^POST \/api\/subscriptions\/(\d+)\/cancel$/.exec(key)) !== null: {
        const s = state.subs.find((x) => x.id === Number(m[1])); if (s) { s.status = "CANCELLED"; s.renewing = false; }
        return delay({ message: "Cancelled. You keep access until the period ends.", cancelledWithPayFast: false });
      }
      /* ----- classes, tickets, bookings ----- */
      case key === "GET /api/classes": return delay(state.classes.map(classDto));
      case key === "GET /api/bookings": return delay(state.bookings.filter((b) => b.status !== "CANCELLED").map(bookingDto));
      case key === "GET /api/credits": return delay({ balance: state.credit, items: state.credit > 0 ? [{ id: 1, amount: state.credit, remaining: state.credit, issuedAt: day(0, 8), expiresAt: day(365, 0), reason: "Cancelled ticket" }] : [] });
      case (m = /^POST \/api\/classes\/(\d+)\/book$/.exec(key)) !== null: {
        const c = state.classes.find((x) => x.id === Number(m[1]));
        if (c.spotsLeft <= 0) fail("This class is full.");
        if (state.bookings.some((b) => b.classId === c.id && b.status === "BOOKED")) fail("You have already booked this class.");
        c.spotsLeft--; const b = { id: state.nextId++, classId: c.id, className: c.name, coachName: c.coachName, location: c.location, startTime: c.startTime, endTime: c.endTime, status: "BOOKED", reference: "RF-E" + String(state.nextId).padStart(6, "0") };
        state.bookings.push(b); return delay(bookingDto(b));
      }
      case (m = /^GET \/api\/classes\/(\d+)\/quote$/.exec(key)) !== null: {
        const c = state.classes.find((x) => x.id === Number(m[1])); const used = Math.min(state.credit, c.price);
        return delay({ price: c.price, creditAvailable: state.credit, creditApplied: used, amountDue: c.price - used, canBuy: c.canBuyNow !== false, reason: c.buyBlockedReason || null });
      }
      case (m = /^POST \/api\/classes\/(\d+)\/ticket$/.exec(key)) !== null: {
        const c = state.classes.find((x) => x.id === Number(m[1])); if (c.canBuyNow === false) fail(c.buyBlockedReason || "Tickets aren't available.");
        const used = Math.min(state.credit, c.price); state.credit -= used; c.spotsLeft--;
        const b = { id: state.nextId++, classId: c.id, className: c.name, coachName: c.coachName, location: c.location, startTime: c.startTime, endTime: c.endTime, status: "BOOKED", totalPaid: c.price, format: c.format, kind: c.kind, reference: "RF-E" + String(state.nextId).padStart(6, "0"), joinPlatform: c.joinPlatform || null, joinLink: c.joinLink || null };
        state.bookings.push(b); const due = c.price - used;
        return delay({ bookingId: b.id, reference: b.reference, confirmed: due === 0, paymentUrl: due === 0 ? null : "#/paid", amountDue: due, creditApplied: used, message: due === 0 ? "You're booked with your account credit." : "Continue to payment." });
      }
      case (m = /^POST \/api\/classes\/(\d+)\/waitlist$/.exec(key)) !== null: {
        const c = state.classes.find((x) => x.id === Number(m[1])); state.bookings.push({ id: state.nextId++, classId: c.id, className: c.name, coachName: c.coachName, location: c.location, startTime: c.startTime, endTime: c.endTime, status: "WAITLISTED", format: c.format });
        return delay(null);
      }
      case (m = /^POST \/api\/bookings\/(\d+)\/cancel$/.exec(key)) !== null: {
        const b = state.bookings.find((x) => x.id === Number(m[1])); if (b) { b.status = "CANCELLED"; const c = state.classes.find((x) => x.id === b.classId); if (c) c.spotsLeft++; }
        return delay(null);
      }
      case (m = /^POST \/api\/bookings\/(\d+)\/cancel-ticket$/.exec(key)) !== null: {
        const b = state.bookings.find((x) => x.id === Number(m[1])); b.status = "CANCELLED"; const c = state.classes.find((x) => x.id === b.classId); if (c) c.spotsLeft++;
        if (b.totalPaid > 0) { state.credit += b.totalPaid; return delay({ message: `Cancelled. ${fmtRand(b.totalPaid)} has been added to your account credit.`, status: "CANCELLED" }); }
        return delay({ message: "You have left the waiting list.", status: "CANCELLED" });
      }
      case (m = /^POST \/api\/bookings\/(\d+)\/choice$/.exec(key)) !== null: return delay({ message: "Thanks, we've noted your choice.", status: "BOOKED" });
      case (m = /^POST \/api\/bookings\/(\d+)\/report-issue$/.exec(key)) !== null:
        if (String(body.description || "").trim().length < 10) fail("Please tell us what happened and about how long it lasted.", 400);
        return delay({ message: "Thank you. We've received your report and will email you with what we can offer.", status: "REPORTED" });
      case (m = /^POST \/api\/admin\/bookings\/(\d+)\/credit$/.exec(key)) !== null:
        if (!(body.amount >= 1)) fail("Enter an amount of at least R1.", 400);
        return delay(null);
      case key === "GET /api/journal": return delay(state.journal);
      case key === "POST /api/journal": { const e = { id: state.nextId++, kind: body.kind, mood: body.mood || "", text: body.text || "", createdAt: new Date().toISOString() }; state.journal.unshift(e); return delay(e); }
      case (m = /^DELETE \/api\/journal\/(\d+)$/.exec(key)) !== null: state.journal = state.journal.filter((e) => e.id !== Number(m[1])); return delay(null);
      case key === "GET /api/notifications": return delay(state.notifications);
      case key === "GET /api/notifications/unread-count": return delay({ count: state.notifications.filter((n) => !n.read).length });
      case key === "POST /api/notifications/read": state.notifications.forEach((n) => { n.read = true; }); return delay(null);
      case key === "GET /api/account/export": return delay({ user, journal: state.journal, bookings: state.bookings });
      case key === "POST /api/account/delete": if (!body.password) fail("Enter your password.", 400); return delay({ message: "Deleted (demo)." });
      case key === "POST /api/telemetry/errors": return delay(null);
      /* ----- admin (read-only samples; saving is not available in the demo) ----- */
      case key === "GET /api/admin/summary": return delay({ users: 24, activeSubscriptions: 11, upcomingClasses: state.classes.length, activeBookings: state.bookings.length, monthlyRecurringRevenue: 2139, openErrors: 0 });
      case key === "GET /api/admin/classes": return delay(state.classes.map(classDto));
      case (m = /^GET \/api\/admin\/classes\/(\d+)\/attendees$/.exec(key)) !== null: return delay([{ bookingId: 1, userId: "u1", fullName: "Amy Example", email: "amy@example.com", bookedAt: day(-1, 9), status: "BOOKED" }]);
      case key === "GET /api/admin/lessons": return delay(lessons.map((l) => ({ id: l.id, programmeId: l.programmeId, programmeName: l.programmeName, title: l.title, description: l.description, category: l.category, level: l.level, durationSeconds: l.durationSeconds, videoProvider: l.youtubeId ? "YouTube" : "Remote", videoReference: l.youtubeId || clip, isPreview: l.isPreview })));
      case key === "GET /api/admin/programmes": return delay(programmes.map((x) => ({ id: x.id, name: x.name, description: x.description, minTier: x.minTier, active: x.active, lessonCount: x.lessonCount })));
      case key === "GET /api/admin/plans": return delay(plans.map((x) => Object.assign({ status: "ACTIVE" }, x)));
      case key === "GET /api/admin/users": return delay([{ id: "u1", fullName: "Amy Example", username: "amy", email: "amy@example.com", role: "CUSTOMER", status: "ACTIVE", createdAt: day(-9, 9), plan: "Move & Breathe" }, { id: "u2", fullName: "Ben Example", username: "ben", email: "ben@example.com", role: "CUSTOMER", status: "ACTIVE", createdAt: day(-3, 9), plan: null }]);
      case key === "GET /api/admin/subscriptions": return delay([{ id: 1, userId: "u1", fullName: "Amy Example", email: "amy@example.com", planName: "Move & Breathe", price: 149, status: "ACTIVE", startDate: day(-9, 9), endDate: day(21, 9), renewing: true }]);
      case key === "GET /api/admin/bookings": return delay([]);
      case key === "GET /api/admin/event-requests": return delay([]);
      case key === "GET /api/admin/refunds": return delay([]);
      case key === "GET /api/admin/errors": return delay([]);
      case /^(POST|PUT|DELETE) \/api\/admin\//.test(key): fail("Saving changes isn't available in the demo.", 400);
      default: fail("Not available in demo mode.", 404);
    }
  }
  return { handle, get user() { return user; } };
})();
