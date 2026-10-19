/* Demo mode (open /app/?demo): an in-memory stand-in for the server, so every screen can be viewed without an account. */
"use strict";

const DEMO = (() => {
  const day = (d, h, m = 0) => { const t = new Date(); t.setDate(t.getDate() + d); t.setHours(h, m, 0, 0); return t.toISOString(); };
  const user = { id: "demo-user", fullName: "Demo Member", username: "demo", email: "demo@example.com", role: "CUSTOMER", about: "Trying out the app." };
  const plans = [
    { id: 1, name: "Flow", description: "Start your practice.", price: 99, billingFrequency: "MONTHLY", tier: 1, features: ["Beginner programme", "Weekly new lessons", "Class booking"] },
    { id: 2, name: "Rhythm", description: "Most popular.", price: 199, billingFrequency: "MONTHLY", tier: 2, features: ["Everything in Flow", "Intermediate programmes", "Progress tracking"] },
    { id: 3, name: "Rhythm & Flow", description: "The full studio.", price: 299, billingFrequency: "MONTHLY", tier: 3, features: ["Everything in Rhythm", "Advanced programmes", "Priority class booking"] },
  ];
  const programmes = [
    { id: 1, name: "Gentle Beginnings", description: "A soft start: breath, balance and easy flows.", minTier: 1, lessonCount: 3 },
    { id: 2, name: "Dance Cardio", description: "Move to the music and lift your mood.", minTier: 2, lessonCount: 2 },
    { id: 3, name: "Deep Stretch", description: "Long holds for release and recovery.", minTier: 3, lessonCount: 1 },
  ];
  const clip = "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4";
  const lessons = [
    { id: 1, programmeId: 1, title: "Morning breath and balance", description: "Wake the body gently with breath-led movement.", category: "Yoga", level: "Beginner", durationSeconds: 600, isPreview: true },
    { id: 2, programmeId: 1, title: "Easy hip opener", description: "Soften tight hips with supported poses.", category: "Stretch", level: "Beginner", durationSeconds: 900, isPreview: false },
    { id: 3, programmeId: 1, title: "Evening wind-down", description: "A calm close to the day.", category: "Meditation", level: "Beginner", durationSeconds: 480, isPreview: false },
    { id: 4, programmeId: 2, title: "Salsa starter", description: "Basic steps and hip rhythm.", category: "Dance", level: "Intermediate", durationSeconds: 1200, isPreview: false },
    { id: 5, programmeId: 2, title: "Barre burn", description: "Low-impact strength at the barre.", category: "Barre", level: "Intermediate", durationSeconds: 1500, isPreview: false },
    { id: 6, programmeId: 3, title: "Full-body deep stretch", description: "Slow, long holds.", category: "Stretch", level: "Advanced", durationSeconds: 1800, isPreview: false },
  ].map((l) => Object.assign(l, { programmeName: programmes.find((p) => p.id === l.programmeId).name, thumbnailUrl: null }));
  const state = {
    tier: 0, subs: [], progress: {}, bookings: [], nextId: 100,
    journal: [{ id: 1, kind: "CHECKIN", mood: "Grounded", text: "", createdAt: day(-1, 8) }, { id: 2, kind: "GRATITUDE", mood: "", text: "A quiet morning and good coffee.", createdAt: day(-2, 20) }],
    notifications: [{ id: 1, kind: "WELCOME", title: "Welcome to Rhythm & Flow", body: "This is demo data. Nothing here is saved.", route: null, createdAt: day(0, 7), read: false }],
    classes: [
      { id: 1, name: "Vinyasa Flow", description: "Breath-linked movement.", coachName: "Denise", location: "Studio A", startTime: day(1, 9), endTime: day(1, 10), capacity: 12, spotsLeft: 5, status: "SCHEDULED" },
      { id: 2, name: "Dance Fusion", description: "Mixed styles, big energy.", coachName: "Amy", location: "Studio B", startTime: day(2, 17, 30), endTime: day(2, 18, 30), capacity: 15, spotsLeft: 9, status: "SCHEDULED" },
      { id: 3, name: "Restorative Stretch", description: "Slow, supported poses.", coachName: "Denise", location: "Studio A", startTime: day(3, 18), endTime: day(3, 19), capacity: 10, spotsLeft: 0, status: "SCHEDULED" },
    ],
  };
  const lessonDto = (l) => {
    const p = state.progress[l.id] || { w: 0, pct: 0 };
    return Object.assign({}, l, { locked: !(l.isPreview || state.tier >= programmes.find((x) => x.id === l.programmeId).minTier), completionPercentage: p.pct, watchTimeSeconds: p.w });
  };
  const classDto = (c) => Object.assign({}, c, { bookedByMe: state.bookings.some((b) => b.classId === c.id && b.status === "CONFIRMED") });
  const bookingDto = (b) => Object.assign({}, b, { canCancel: new Date(b.startTime) - Date.now() > 3600000 });
  const fail = (msg, status = 409) => { throw new ApiError(msg, status); };
  const delay = (v) => new Promise((r) => setTimeout(() => r(JSON.parse(JSON.stringify(v === undefined ? null : v))), 160));

  async function handle(method, path, body) {
    const [p, qs] = path.split("?"); const q = new URLSearchParams(qs || "");
    const key = method + " " + p; let m;
    switch (true) {
      case key === "POST /api/auth/login": case key === "POST /api/auth/register":
        return delay({ token: "demo-token", user });
      case key === "GET /api/auth/me": return delay(user);
      case key === "PUT /api/auth/me": Object.assign(user, { fullName: body.fullName, email: body.email, about: body.about || "" }); return delay(user);
      case key === "POST /api/auth/forgot-password": case key === "POST /api/auth/reset-password": case key === "POST /api/auth/change-password": return delay({ message: "Done (demo)." });
      case key === "GET /api/plans": return delay(plans);
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
        return delay({ url: clip, expiresAt: day(0, 23), resumeSeconds: (state.progress[l.id] || { w: 0 }).w });
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
      case key === "GET /api/subscriptions": return delay(state.subs);
      case key === "GET /api/subscriptions/payments": return delay(state.subs.map((s) => ({ id: s.id, planName: s.planName, amount: s.price, status: "COMPLETE", date: s.startDate, receipt: "RF-" + String(s.id).padStart(6, "0") })));
      case key === "POST /api/subscriptions/checkout": {
        if (state.subs.some((s) => s.grantsAccess)) fail("You already have an active plan. To switch plans, cancel it first (you keep access until the period ends), then choose the new plan.", 400);
        const pl = plans.find((x) => x.id === body.planId); const id = state.nextId++;
        state.subs.push({ id, planId: pl.id, planName: pl.name, tier: pl.tier, price: pl.price, status: "ACTIVE", startDate: new Date().toISOString(), endDate: day(30, 0), grantsAccess: true });
        state.tier = pl.tier; return delay({ subscriptionId: id, paymentUrl: "#/paid" });
      }
      case (m = /^POST \/api\/subscriptions\/(\d+)\/cancel$/.exec(key)) !== null: {
        const s = state.subs.find((x) => x.id === Number(m[1])); if (s) { s.status = "CANCELLED"; }
        return delay({ message: "Cancelled. You keep access until the period ends.", cancelledWithPayFast: false });
      }
      case key === "GET /api/classes": return delay(state.classes.map(classDto));
      case key === "GET /api/bookings": return delay(state.bookings.map(bookingDto));
      case (m = /^POST \/api\/classes\/(\d+)\/book$/.exec(key)) !== null: {
        const c = state.classes.find((x) => x.id === Number(m[1]));
        if (c.spotsLeft <= 0) fail("This class is full.");
        if (state.bookings.some((b) => b.classId === c.id && b.status === "CONFIRMED")) fail("You have already booked this class.");
        c.spotsLeft--; const b = { id: state.nextId++, classId: c.id, className: c.name, coachName: c.coachName, location: c.location, startTime: c.startTime, endTime: c.endTime, status: "CONFIRMED" };
        state.bookings.push(b); return delay(bookingDto(b));
      }
      case (m = /^POST \/api\/bookings\/(\d+)\/cancel$/.exec(key)) !== null: {
        const b = state.bookings.find((x) => x.id === Number(m[1])); if (b) { b.status = "CANCELLED"; const c = state.classes.find((x) => x.id === b.classId); if (c) c.spotsLeft++; }
        return delay(null);
      }
      case key === "GET /api/journal": return delay(state.journal);
      case key === "POST /api/journal": { const e = { id: state.nextId++, kind: body.kind, mood: body.mood || "", text: body.text || "", createdAt: new Date().toISOString() }; state.journal.unshift(e); return delay(e); }
      case (m = /^DELETE \/api\/journal\/(\d+)$/.exec(key)) !== null: state.journal = state.journal.filter((e) => e.id !== Number(m[1])); return delay(null);
      case key === "GET /api/notifications": return delay(state.notifications);
      case key === "GET /api/notifications/unread-count": return delay({ count: state.notifications.filter((n) => !n.read).length });
      case key === "POST /api/notifications/read": state.notifications.forEach((n) => { n.read = true; }); return delay(null);
      case key === "GET /api/account/export": return delay({ user, journal: state.journal, bookings: state.bookings });
      case key === "POST /api/account/delete": if (!body.password) fail("Enter your password.", 400); return delay({ message: "Deleted (demo)." });
      case key === "POST /api/telemetry/errors": return delay(null);
      default: fail("Not available in demo mode.", 404);
    }
  }
  return { handle, user };
})();
