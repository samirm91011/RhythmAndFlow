/* Journal, You, plans and payment, subscription, notifications, progress, Explore and settings. */
"use strict";

const AFFIRMATIONS = [
  "I welcome joy into my body today.", "I move with kindness and patience.", "My breath is my anchor.",
  "I am allowed to rest and to grow.", "Small steps still count.", "I listen to what my body needs.", "I am enough, just as I am today.",
];
const EXPLORE = [
  ["Feed Your Soul", "Mindfulness, meditation, emotional wellbeing.", "🌙"],
  ["Nourish Your Body", "Nutrition, food, self-care.", "🍓"],
  ["Open Your Mind", "Personal development, mindset, coaching.", "💡"],
  ["Discover New Journeys", "Travel, experiences, trying something new.", "🧭"],
  ["Coaching & Programmes", "Guided support for your next chapter.", "🧠"],
  ["Shop", "Wellness products for everyday living.", "🛍️"],
];
const KIND = { CHECKIN: "Check-in", REFLECTION: "Reflection", GRATITUDE: "Gratitude" };
const planRow = (p) => `<div class="kv"><span>${esc(p.planName || p.name)}</span><b>${fmtRand(p.price)}/month</b></div>`;

/* ---------------- Journal ---------------- */
route("/journal", async (el) => {
  let tab = "checkin";
  const draw = async () => {
    let body = "";
    if (tab === "checkin") {
      body = `<div class="pad stack"><div class="card tang stack"><h3>Today's reflection</h3><p class="muted small">What's on your mind?</p>
        ${field("ref", "Write freely…", { area: true, max: 2000 })}<button class="btn accent" id="saveref">Save entry</button></div>
        <div class="card tint"><p class="eyebrow">DAILY AFFIRMATION</p><h3 style="margin-top:6px">“${esc(AFFIRMATIONS[new Date().getDate() % AFFIRMATIONS.length])}”</h3></div>
        <div class="card stack"><h3>Gratitude</h3><p class="muted small">What are you grateful for today?</p>${field("grat", "I'm grateful for…", { max: 300, enter: "done" })}<button class="btn secondary" id="savegrat">Add</button></div></div>`;
    } else if (tab === "journal") {
      const items = await api("GET", "/api/journal");
      body = items.length ? `<div class="list">${items.map((j) => `<div class="card"><div class="row between"><span class="pill ${j.kind === "GRATITUDE" ? "tang" : ""}">${esc(KIND[j.kind] || j.kind)}${j.mood ? " · " + esc(j.mood) : ""}</span>
          <button class="iconbtn" style="margin:0" data-del="${j.id}" aria-label="Delete entry">${ic("trash")}</button></div>${j.text ? `<p class="mt-s">${esc(j.text)}</p>` : ""}<p class="small muted mt-s">${fmtDayTime(j.createdAt)}</p></div>`).join("")}</div>`
        : emptyBox("No entries yet", "Your check-ins, reflections and gratitude notes will appear here.", "📓");
    } else if (tab === "meditation") {
      const l = await api("GET", "/api/lessons?category=Meditation");
      body = l.length ? `<div class="list">${l.map(lessonRow).join("")}</div>` : emptyBox("No meditations yet", "Guided meditations will appear here.", "🌙");
    } else {
      body = `<div class="list">${AFFIRMATIONS.map((a) => `<div class="card tint"><h3 style="font-weight:600">“${esc(a)}”</h3></div>`).join("")}</div>`;
    }
    el.innerHTML = `${topbar("Journal", { back: false, sub: "Reconnect with yourself." })}
      <div class="tabsbar" role="tablist">${[["checkin", "Check-in"], ["journal", "Journal"], ["meditation", "Meditation"], ["aff", "Affirmations"]].map(([k, n]) => `<button class="${tab === k ? "on" : ""}" data-tab="${k}">${n}</button>`).join("")}</div>${body}`;
  };
  await draw();
  el.onclick = async (e) => {
    const t = e.target.closest("[data-tab]"), del = e.target.closest("[data-del]");
    try {
      if (t) { tab = t.dataset.tab; el.innerHTML = skeleton(2); await draw(); return; }
      if (del) { if (await dialog({ title: "Delete this entry?", text: "This can't be undone.", ok: "Delete", danger: true })) { await api("DELETE", `/api/journal/${del.dataset.del}`); toast("Entry deleted."); await draw(); } return; }
      if (e.target.id === "saveref") { const text = val("ref").trim(); if (!text) return toast("Write something first."); busy(e.target, true); await api("POST", "/api/journal", { kind: "REFLECTION", mood: "", text }); toast("Saved. Well done for checking in."); await draw(); }
      if (e.target.id === "savegrat") { const text = val("grat").trim(); if (!text) return toast("Write something first."); busy(e.target, true); await api("POST", "/api/journal", { kind: "GRATITUDE", mood: "", text }); toast("Gratitude added."); await draw(); }
    } catch (err) { toast(err.message); }
  };
}, { tab: "journal" });

/* ---------------- You ---------------- */
route("/you", async (el) => {
  const [summary, subs] = await Promise.all([api("GET", "/api/progress/summary"), loadSubs(true)]);
  const u = RF.user || {}; const active = (subs || []).find((s) => s.grantsAccess);
  el.innerHTML = `${topbar("You", { back: false, sub: "Your journey, your way.", right: `<a class="iconbtn right" href="#/settings" aria-label="Settings">${ic("info")}</a>` })}
    <div class="pad stack">
      <a class="card tint row" href="#/edit-profile"><span class="avatar">${esc((u.fullName || "R")[0].toUpperCase())}</span><div class="grow"><h3>${esc(u.fullName || "")}</h3><p class="muted small">${esc(u.email || "")}</p></div>${ic("chev", "chev")}</a>
      <a class="card" href="#/progress"><h3>Your journey</h3><p class="muted">You've shown up for yourself ${summary.sessionsThisMonth} ${summary.sessionsThisMonth === 1 ? "time" : "times"} this month.</p><p class="small" style="color:var(--teal-deep);font-weight:600;margin-top:4px">See my progress</p></a>
      <a class="card navrow" href="${active ? "#/subscription" : "#/plans"}">${ic("card", "lead")}<div class="grow"><h3 style="font-size:19px">${active ? esc(active.planName) + " plan" : "Choose a plan"}</h3><p class="muted small">${active ? "Manage your subscription" : "Unlock every programme"}</p></div>${ic("chev", "chev")}</a>
      <a class="card navrow" href="#/classes">${ic("classes", "lead")}<div class="grow"><h3 style="font-size:19px">My bookings</h3><p class="muted small">Upcoming classes</p></div>${ic("chev", "chev")}</a>
      <a class="card navrow" href="#/explore">${ic("search", "lead")}<div class="grow"><h3 style="font-size:19px">Explore</h3><p class="muted small">A holistic wellness library</p></div>${ic("chev", "chev")}</a>
      ${u.role === "ADMIN" ? `<div class="hint">${ic("info", "sm")}<span>You are signed in as an administrator. The admin tools (classes, lessons, plans, customers) are in the Android app.</span></div>` : ""}
    </div>`;
}, { tab: "you" });

/* ---------------- Plans and payment ---------------- */
route("/plans", async (el) => {
  const [plans, subs] = await Promise.all([api("GET", "/api/plans"), loadSubs(true)]);
  const current = (subs || []).find((s) => s.grantsAccess);
  el.innerHTML = `${topbar("Choose your plan", { sub: "Monthly. Cancel any time." })}<div class="list">${plans.map((p, i) => `<div class="card stack ${i === 1 ? "tint" : ""}">
      <div class="row between"><h3>${esc(p.name)}</h3>${i === 1 ? `<span class="pill solid">Most popular</span>` : ""}</div>
      <div class="price">${fmtRand(p.price)}<small> / month</small></div><p class="muted">${esc(p.description)}</p>
      <div class="stack-sm">${p.features.map((f) => `<div class="feat">${ic("tick")}<span>${esc(f)}</span></div>`).join("")}</div>
      ${current && current.planId === p.id ? `<span class="pill">Your plan</span>` : `<button class="btn accent" data-plan="${p.id}">Subscribe</button>`}</div>`).join("")}</div>`;
  el.onclick = async (e) => {
    const b = e.target.closest("[data-plan]"); if (!b) return;
    busy(b, true, "Opening payment…");
    try { const r = await api("POST", "/api/subscriptions/checkout", { planId: Number(b.dataset.plan) }); RF.subs = null; if (String(r.paymentUrl).startsWith("#")) location.hash = r.paymentUrl; else location.href = r.paymentUrl; }
    catch (err) { busy(b, false); toast(err.message); }
  };
});

route("/paid", async (el) => {
  const subs = await loadSubs(true); const s = (subs || []).find((x) => x.grantsAccess);
  el.innerHTML = `<div class="centerpage"><div class="big-check">${ic("tick")}</div><h2>You're subscribed!</h2><p class="muted">${s ? `Your ${esc(s.planName)} plan is active. Enjoy your practice.` : "Your payment is being confirmed. This can take a minute."}</p><a class="btn accent" style="max-width:320px" href="#/home">Start moving</a></div>`;
});

route("/subscription", async (el) => {
  const subs = await loadSubs(true); const s = (subs || []).find((x) => x.grantsAccess) || (subs || [])[0];
  if (!s) { el.innerHTML = topbar("My subscription") + emptyBox("No plan yet", "Choose a plan to unlock every programme.", "💳") + `<div class="pad"><a class="btn accent" href="#/plans">See plans</a></div>`; return; }
  const live = s.status === "ACTIVE";
  el.innerHTML = `${topbar("My subscription")}<div class="pad stack"><div class="card stack"><div class="row between"><h3>${esc(s.planName)}</h3><span class="pill ${live ? "" : "grey"}">${live ? "Active" : esc(s.status === "CANCELLED" ? "Cancelled" : s.status)}</span></div>
    <p class="muted">${fmtRand(s.price)} per month</p><p>${live ? "Renews" : "Access until"} ${fmtDate(s.endDate)}</p></div>
    ${live ? `<button class="btn danger" id="cancel">Cancel subscription</button><p class="small muted">To switch plans, cancel this one first. You keep access until the period ends, then you can choose a new plan.</p>` : `<a class="btn accent" href="#/plans">Choose a new plan</a>`}
    <a class="card navrow" href="#/payments">${ic("card", "lead")}<div class="grow"><h3 style="font-size:19px">Payment history</h3></div>${ic("chev", "chev")}</a></div>`;
  const c = $("#cancel"); if (c) c.onclick = async () => {
    if (!(await dialog({ title: "Cancel your subscription?", text: "We'll tell PayFast to stop your monthly payments. You'll keep access until the end of the paid period.", ok: "Cancel subscription", cancel: "Keep it", danger: true }))) return;
    busy(c, true, "Cancelling…");
    try { const r = await api("POST", `/api/subscriptions/${s.id}/cancel`); RF.subs = null; await dialog({ title: "Subscription cancelled", text: r.message, ok: "OK", cancel: "" }); go("/subscription"); navigate(); }
    catch (err) { busy(c, false); toast(err.message); }
  };
});

route("/payments", async (el) => {
  const rows = await api("GET", "/api/subscriptions/payments");
  el.innerHTML = topbar("Payment history", { sub: "Your payments and receipts." }) + (rows.length ? `<div class="list">${rows.map((p) => `<div class="card"><div class="row between"><h3 style="font-size:19px">${esc(p.planName)}</h3><b>${fmtRand(p.amount)}</b></div>
    <div class="row between mt-s"><span class="small muted">${fmtDate(p.date)} · Receipt ${esc(p.receipt)}</span><span class="pill ${p.status === "COMPLETE" ? "" : "tang"}">${p.status === "COMPLETE" ? "Paid" : "Didn't go through"}</span></div></div>`).join("")}</div>` : emptyBox("No payments yet", "When you subscribe, each payment and its receipt number will appear here.", "🧾"));
});

/* ---------------- Notifications and progress ---------------- */
const ROUTES = { home: "/home", move: "/move", classes: "/classes", bookings: "/classes", journal: "/journal", you: "/you", plans: "/plans", subscription: "/subscription", notifications: "/notifications" };
route("/notifications", async (el) => {
  const items = await api("GET", "/api/notifications"); const unread = items.filter((n) => !n.read).length;
  el.innerHTML = topbar("Notifications", { sub: unread ? `${unread} unread` : "All caught up", right: unread ? `<button class="linkbtn" id="markall">Mark all read</button>` : "" }) +
    (items.length ? `<div class="list">${items.map((n) => `<a class="card row" ${ROUTES[n.route] ? `href="#${ROUTES[n.route]}"` : ""} style="align-items:flex-start">${n.read ? "" : `<span class="dot"></span>`}<div class="grow"><h3 style="font-size:18px">${esc(n.title)}</h3><p class="muted small">${esc(n.body)}</p><p class="small muted mt-s">${timeAgo(n.createdAt)}</p></div></a>`).join("")}</div>` : emptyBox("Nothing here yet", "Updates about your plan and classes will appear here.", "🔔"));
  const m = $("#markall"); if (m) m.onclick = async () => { try { await api("POST", "/api/notifications/read", { ids: null }); navigate(); } catch (e) { toast(e.message); } };
});

route("/progress", async (el) => {
  const [s, lessons] = await Promise.all([api("GET", "/api/progress/summary"), api("GET", "/api/lessons")]);
  const done = lessons.filter((l) => l.completionPercentage > 0);
  el.innerHTML = `${topbar("My progress", { sub: "Every practice you've started." })}<div class="cards3"><div class="card stat"><b>${s.sessionsThisMonth}</b><span>sessions this month</span></div><div class="card stat"><b>${s.minutesWatched}</b><span>minutes moved</span></div><div class="card stat"><b>${s.completedLessons}</b><span>completed</span></div></div>
    <div class="sect"><h3>Your practices</h3></div>${done.length ? `<div class="list">${done.map(lessonRow).join("")}</div>` : emptyBox("Nothing started yet", "Begin a practice and it will show up here.", "🌱")}`;
});

/* ---------------- Explore ---------------- */
route("/explore", (el) => {
  el.innerHTML = `${topbar("Explore", { sub: "A holistic wellness library." })}<div class="list">${EXPLORE.map(([t, b, e]) => `<a class="card row" href="#/article/${encodeURIComponent(t)}"><span class="thumb" style="width:64px;height:64px;font-size:28px">${e}</span><div class="grow"><h3 style="font-size:20px">${esc(t)}</h3><p class="muted small">${esc(b)}</p></div>${ic("chev", "chev")}</a>`).join("")}</div>`;
});
route("/article/:title", (el, { title }) => {
  const item = EXPLORE.find((x) => x[0] === title) || [title, "", "🌿"];
  el.innerHTML = `${topbar(item[0])}<div class="hero" style="height:160px">${item[2]}</div><div class="pad pt stack"><h2>${esc(item[0])}</h2><p>${esc(item[1])}</p>
    <div class="hint">${ic("info", "sm")}<span>${item[0] === "Shop" ? "The shop is coming soon." : "New articles and guides are on their way."}</span></div></div>`;
});

/* ---------------- Settings, profile, password, account ---------------- */
route("/settings", (el) => {
  const row = (icn, title, sub, href) => `<a class="card navrow" ${href.startsWith("http") ? `href="${href}" target="_blank" rel="noopener"` : `href="${href}"`}>${ic(icn, "lead")}<div class="grow"><h3 style="font-size:19px">${title}</h3><p class="muted small">${sub}</p></div>${ic("chev", "chev")}</a>`;
  el.innerHTML = `${topbar("Settings")}<div class="pad stack">${row("you", "Account", "Personal information", "#/edit-profile")}${row("lock", "Password", "Change your password", "#/change-password")}${row("card", "Subscription", "Plan, billing and receipts", "#/subscription")}${row("bell", "Notifications", "Your updates and alerts", "#/notifications")}
    ${row("policy", "Privacy Policy", "Read it on rhythmandflow.co.za", RF.legal.privacy)}${row("policy", "Terms of Use", "Read it on rhythmandflow.co.za", RF.legal.terms)}
    <button class="card navrow" id="export" style="text-align:left;border:0;width:100%">${ic("download", "lead")}<div class="grow"><h3 style="font-size:19px">Download my data</h3><p class="muted small">Get a copy of everything we hold about you</p></div></button>
    <button class="card navrow" id="delete" style="text-align:left;border:0;width:100%">${ic("trash", "lead")}<div class="grow"><h3 style="font-size:19px">Delete my account</h3><p class="muted small">Erase your account and personal data</p></div></button>
    <p class="small muted center">Rhythm &amp; Flow web app</p><button class="btn danger" id="logout">${ic("logout")} Log out</button></div>`;
  $("#logout").onclick = () => { signOutLocal(); go("/welcome"); };
  $("#export").onclick = async () => {
    try { const data = await api("GET", "/api/account/export"); const a = document.createElement("a"); a.href = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: "application/json" })); a.download = "rhythm-and-flow-my-data.json"; document.body.appendChild(a); a.click(); a.remove(); toast("Your data has been downloaded."); }
    catch (e) { toast(e.message); }
  };
  $("#delete").onclick = async () => {
    const v = await dialog({ title: "Delete your account?", text: "This erases your account and personal data and can't be undone. Enter your password to confirm.", fields: field("dpw", "Password", { type: "password", autocomplete: "current-password" }), ok: "Delete my account", danger: true, check: (x) => (x.dpw ? "" : "Enter your password.") });
    if (!v) return;
    try { await api("POST", "/api/account/delete", { password: v.dpw }); signOutLocal(); toast("Your account has been deleted."); go("/welcome"); } catch (e) { toast(e.message); }
  };
});

route("/edit-profile", (el) => {
  const u = RF.user || {};
  el.innerHTML = `${topbar("Edit profile")}<div class="pad"><form id="f" novalidate>${field("name", "Full name", { value: u.fullName, autocomplete: "name", cap: "words" })}${field("email", "Email", { type: "email", value: u.email, autocomplete: "email" })}${field("about", "About you", { area: true, value: u.about, max: 300 })}
    <div class="formerr hidden" id="ferr"></div><button class="btn accent" id="go" type="submit">Save changes</button></form></div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault(); const fe = $("#ferr"); fe.classList.add("hidden");
    const body = { fullName: val("name").trim(), email: val("email").trim(), about: val("about").trim() };
    setFieldError("name", body.fullName.length >= 2 ? "" : "Enter your full name."); setFieldError("email", /^\S+@\S+\.\S+$/.test(body.email) ? "" : "Enter a valid email address.");
    if ($$(".field.err").length) return; busy($("#go"), true, "Saving…");
    try { RF.user = await api("PUT", "/api/auth/me", body); toast("Profile updated."); history.back(); } catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy($("#go"), false); }
  };
});

route("/change-password", (el) => {
  el.innerHTML = `${topbar("Change password")}<div class="pad"><form id="f" novalidate>${field("cur", "Current password", { type: "password", autocomplete: "current-password" })}${field("nw", "New password (8 or more characters)", { type: "password", autocomplete: "new-password" })}${field("nw2", "Repeat new password", { type: "password", autocomplete: "new-password", enter: "go" })}
    <div class="formerr hidden" id="ferr"></div><button class="btn accent" id="go" type="submit">Change password</button></form></div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault(); const fe = $("#ferr"); fe.classList.add("hidden");
    setFieldError("cur", val("cur") ? "" : "Enter your current password."); setFieldError("nw", val("nw").length >= 8 ? "" : "At least 8 characters."); setFieldError("nw2", val("nw") === val("nw2") ? "" : "The passwords don't match.");
    if ($$(".field.err").length) return; busy($("#go"), true, "Saving…");
    try { const r = await api("POST", "/api/auth/change-password", { currentPassword: val("cur"), newPassword: val("nw") }); if (r && r.token) setSession(r); toast("Password changed."); history.back(); }
    catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy($("#go"), false); }
  };
});
