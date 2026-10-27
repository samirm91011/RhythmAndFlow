/* Journal, You, notifications, progress, Explore, shop and settings. Plans are in plans.js, classes in events.js, admin in admin.js.
   The wording and layout follow the Android app screen for screen. */
"use strict";

const AFFIRMATIONS = [
  "I am enough, exactly as I am, and I am always growing.",
  "My body is strong, and I move with kindness.",
  "I give myself permission to rest and to rise.",
  "Every small step brings me back to myself.",
  "I am free to express who I am through movement.",
  "I welcome joy into my body today.",
  "I am proud of how far I've come.",
];
const EXPLORE = [
  ["Feed Your Soul", "Mindfulness, meditation, emotional wellbeing.", "🌙", "linear-gradient(135deg,var(--teal-deep),var(--teal))"],
  ["Nourish Your Body", "Nutrition, food, self-care.", "🍽️", "linear-gradient(135deg,var(--tang),#EE8E6E)"],
  ["Open Your Mind", "Personal development, mindset, coaching.", "💡", "linear-gradient(135deg,#3D4F6B,#7C94B8)"],
  ["Discover New Journeys", "Travel, experiences, trying something new.", "🧭", "linear-gradient(135deg,var(--teal),#7DB8AD)"],
  ["Coaching & Programmes", "Guided support for your next chapter.", "🧠", "linear-gradient(135deg,#2B3A37,var(--teal))"],
  ["Shop", "Wellness products for everyday living.", "🛍️", "linear-gradient(135deg,#000,#444)"],
];
const INTENTIONS = ["Move more", "Reduce stress", "Improve sleep", "Build confidence", "Create balance", "Nourish my body", "Connect with myself", "Make time for myself"];
const KIND = { CHECKIN: "Check-in", REFLECTION: "Reflection", GRATITUDE: "Gratitude" };
const favs = {
  all() { return store.get("fav_affirmations", []); },
  has(a) { return favs.all().includes(a); },
  toggle(a) { const s = favs.all().filter((x) => x !== a); if (s.length === favs.all().length) s.push(a); store.set("fav_affirmations", s); },
};

/* ---------------- Journal ---------------- */
route("/journal", async (el) => {
  let tab = "checkin";
  const draw = async () => {
    let body = "";
    if (tab === "checkin") {
      body = `<div class="pad stack"><div class="card tang stack"><h3>Today's Reflection</h3><p class="muted small">What's on your mind?</p>
        ${field("ref", "Write freely…", { area: true, max: 2000 })}<button class="btn" id="saveref" disabled>Save Entry</button></div>
        <div class="card"><p class="muted" style="font-weight:600">Daily Affirmation</p><h3 style="margin-top:6px">“${esc(AFFIRMATIONS[dayOfYear() % AFFIRMATIONS.length])}”</h3></div>
        <div class="card stack"><h3>Gratitude</h3><p class="muted small">What are you grateful for today?</p><div class="row">${field("grat", "I'm grateful for…", { max: 300, enter: "done" })}<button class="iconbtn" style="margin:0 0 14px;background:var(--teal-deep);color:#fff" id="savegrat" aria-label="Add gratitude" disabled>+</button></div></div></div>`;
    } else if (tab === "journal") {
      const items = await api("GET", "/api/journal");
      body = items.length ? `<div class="list">${items.map((j) => `<div class="card"><div class="row between"><div class="pills" style="margin:0"><span class="pill">${esc(KIND[j.kind] || j.kind)}</span>${j.mood ? `<span class="pill tang">${esc(j.mood)}</span>` : ""}</div>
          <button class="iconbtn" style="margin:0" data-del="${j.id}" aria-label="Delete entry">${ic("trash")}</button></div>${j.text ? `<p class="mt-s">${esc(j.text)}</p>` : ""}<p class="small muted mt-s">${fmtDayTime(j.createdAt)}</p></div>`).join("")}</div>`
        : emptyBox("Your journal is empty", "Check-ins, reflections and gratitude notes will appear here.", "📓");
    } else if (tab === "meditation") {
      const l = await api("GET", "/api/lessons?category=Meditation");
      body = l.length ? `<div class="list">${l.map(lessonRow).join("")}</div>` : emptyBox("No meditations yet", "Guided meditations will appear here.", "🌙");
    } else {
      body = `<div class="list">${AFFIRMATIONS.map((a) => `<div class="card row"><h3 class="grow" style="font-size:19px;font-weight:600">“${esc(a)}”</h3><button class="iconbtn" style="margin:0;color:var(--tang)" data-fav="${esc(a)}" aria-label="Favourite">${ic(favs.has(a) ? "heart1" : "heart0")}</button></div>`).join("")}</div>`;
    }
    el.innerHTML = `${topbar("Journal", { back: false, sub: "Reconnect with yourself." })}
      <div class="tabsbar" role="tablist">${[["checkin", "Check-in"], ["journal", "Journal"], ["meditation", "Meditation"], ["aff", "Affirmations"]].map(([k, n]) => `<button class="${tab === k ? "on" : ""}" data-tab="${k}">${n}</button>`).join("")}</div>${body}`;
  };
  await draw();
  el.oninput = (e) => { if (e.target.id === "ref") $("#saveref").disabled = !e.target.value.trim(); if (e.target.id === "grat") $("#savegrat").disabled = !e.target.value.trim(); };
  el.onclick = async (e) => {
    const t = e.target.closest("[data-tab]"), del = e.target.closest("[data-del]"), fav = e.target.closest("[data-fav]");
    try {
      if (t) { tab = t.dataset.tab; el.innerHTML = skeleton(2); await draw(); return; }
      if (fav) { favs.toggle(fav.dataset.fav); fav.innerHTML = ic(favs.has(fav.dataset.fav) ? "heart1" : "heart0"); return; }
      if (del) { if (await dialog({ title: "Delete this entry?", text: "This can't be undone.", ok: "Delete", danger: true })) { await api("DELETE", `/api/journal/${del.dataset.del}`); await draw(); } return; }
      if (e.target.id === "saveref") { const text = val("ref").trim(); if (!text) return; busy(e.target, true); await api("POST", "/api/journal", { kind: "REFLECTION", mood: null, text }); toast("Saved. Well done for checking in."); await draw(); }
      if (e.target.id === "savegrat") { const text = val("grat").trim(); if (!text) return; busy(e.target, true); await api("POST", "/api/journal", { kind: "GRATITUDE", mood: null, text }); toast("Saved. Well done for checking in."); await draw(); }
    } catch (err) { toast(err.message); }
  };
}, { tab: "journal" });
const dayOfYear = () => Math.floor((Date.now() - new Date(new Date().getFullYear(), 0, 0)) / 86400000);

/* ---------------- You ---------------- */
route("/you", async (el) => {
  const [summary, subs] = await Promise.all([api("GET", "/api/progress/summary"), loadSubs(true)]);
  const u = RF.user || {}; const active = (subs || []).find((s) => s.grantsAccess);
  let tab = "Overview"; let savedLessons = null;
  const chosen = () => store.get("intentions", []), prefsChosen = () => store.get("practices", []);
  const paint = async () => {
    let body = "";
    if (tab === "Overview") {
      body = `<a class="card tint row" href="#/edit-profile"><span class="avatar">${esc((u.fullName || "R")[0].toUpperCase())}</span><div class="grow"><h3>${esc(u.fullName || "")}</h3><p class="muted small">${esc(u.email || "")}</p></div>${ic("chev", "chev")}</a>
        <a class="card" href="#/progress"><h3>Your Journey</h3><p class="muted">You've shown up for yourself ${summary.sessionsThisMonth} ${summary.sessionsThisMonth === 1 ? "time" : "times"} this month.</p><p class="small" style="color:var(--teal-deep);font-weight:600;margin-top:4px">See my progress</p></a>
        <a class="card navrow" href="${active ? "#/subscription" : "#/plans"}">${ic("card", "lead")}<div class="grow"><h3 style="font-size:19px">${active ? esc(active.planName) + " plan" : "Choose a plan"}</h3><p class="muted small">${active ? "Manage your subscription" : "Unlock every programme"}</p></div>${ic("chev", "chev")}</a>
        <a class="card navrow" href="#/classes?t=mine">${ic("classes", "lead")}<div class="grow"><h3 style="font-size:19px">My bookings</h3><p class="muted small">Upcoming classes</p></div>${ic("chev", "chev")}</a>
        ${isAdmin() ? `<a class="card navrow" href="#/admin" data-adminview>${ic("dash", "lead")}<div class="grow"><h3 style="font-size:19px">Admin tools</h3><p class="muted small">Manage lessons, classes and plans</p></div>${ic("chev", "chev")}</a>` : ""}`;
    } else if (tab === "Intentions") {
      body = `<h3>How do you want to feel?</h3><p class="muted">Select your wellness intentions:</p><div class="needs" style="grid-template-columns:1fr 1fr">${INTENTIONS.map((i) => `<label class="check"><input type="checkbox" data-int="${esc(i)}" ${chosen().includes(i) ? "checked" : ""}><span>${esc(i)}</span></label>`).join("")}</div>`;
    } else if (tab === "Preferences") {
      body = `<h3>Favourite practices</h3><div class="row wrap">${CATEGORIES.map((c) => `<button class="chip ${prefsChosen().includes(c.name) ? "on" : ""}" data-pref="${esc(c.name)}">${esc(c.name)}</button>`).join("")}</div>`;
    } else {
      if (!savedLessons) { const all = await api("GET", "/api/lessons"); savedLessons = all.filter((l) => saved.has(l.id)); }
      body = savedLessons.length ? `<div class="stack">${savedLessons.map(lessonRow).join("")}</div>` : emptyBox("Nothing saved yet", "Tap the heart on a practice to save it here.", "🤍");
    }
    el.innerHTML = `${topbar("You", { back: false, sub: "Your journey, your way.", right: `<a class="iconbtn right" href="#/settings" aria-label="Settings">${ic("info")}</a>` })}
      <div class="tabsbar">${["Overview", "Intentions", "Preferences", "Saved"].map((t) => `<button class="${t === tab ? "on" : ""}" data-ytab="${t}">${t}</button>`).join("")}</div><div class="pad stack">${body}</div>`;
  };
  await paint();
  el.onclick = async (e) => {
    const t = e.target.closest("[data-ytab]"), p = e.target.closest("[data-pref]");
    if (e.target.closest("[data-adminview]")) setCustomerView(false);
    if (t) { tab = t.dataset.ytab; await paint(); }
    if (p) { const s = prefsChosen().filter((x) => x !== p.dataset.pref); if (s.length === prefsChosen().length) s.push(p.dataset.pref); store.set("practices", s); p.classList.toggle("on"); }
  };
  el.onchange = (e) => { const i = e.target.closest("[data-int]"); if (i) { const s = chosen().filter((x) => x !== i.dataset.int); if (i.checked) s.push(i.dataset.int); store.set("intentions", s); } };
}, { tab: "you" });

/* ---------------- Notifications and progress ---------------- */
const ROUTES = { home: "/home", move: "/move", classes: "/classes", bookings: "/classes?t=mine", journal: "/journal", you: "/you", plans: "/plans", subscription: "/subscription", notifications: "/notifications", admin: "/admin", "admin/errors": "/admin/errors" };
const NOTE_ICON = { PAYMENT: "card", SUBSCRIPTION: "card", CLASS: "classes", BOOKING: "classes", ADMIN_ERROR: "info" };
route("/notifications", async (el) => {
  const items = await api("GET", "/api/notifications"); const unread = items.filter((n) => !n.read).length;
  el.innerHTML = topbar("Notifications", { sub: unread ? `${unread} unread` : "", right: unread ? `<button class="linkbtn" id="markall">Mark all read</button>` : "" }) +
    (items.length ? `<div class="list">${items.map((n) => `<a class="card row ${n.read ? "" : "tint"}" data-note="${n.id}" ${ROUTES[n.route] ? `href="#${ROUTES[n.route]}"` : 'href="javascript:void(0)"'} style="align-items:flex-start">
      <span class="round" style="background:#fff;color:${n.kind === "ADMIN_ERROR" ? "var(--tang)" : "var(--teal-deep)"}">${ic(NOTE_ICON[n.kind] || "bell")}</span><div class="grow"><h3 style="font-size:18px;font-weight:${n.read ? 500 : 700}">${esc(n.title)}</h3><p class="muted small">${esc(n.body)}</p><p class="small muted mt-s">${timeAgo(n.createdAt)}</p></div>${n.read ? "" : `<span class="dot"></span>`}</a>`).join("")}</div>`
      : emptyBox("You're all caught up", "Updates about your plan, payments and classes will appear here.", "🔔"));
  const m = $("#markall"); if (m) m.onclick = async () => { try { await api("POST", "/api/notifications/read", { ids: null }); navigate(); } catch (e) { toast(e.message); } };
  el.addEventListener("click", (e) => { const n = e.target.closest("[data-note]"); if (n) api("POST", "/api/notifications/read", { ids: [Number(n.dataset.note)] }).catch(() => {}); });
});

route("/progress", async (el) => {
  const [s, lessons] = await Promise.all([api("GET", "/api/progress/summary"), api("GET", "/api/lessons")]);
  const started = lessons.filter((l) => !l.locked && l.watchTimeSeconds > 0);
  const inProgress = started.filter((l) => l.completionPercentage < 95).sort((a, b) => b.completionPercentage - a.completionPercentage), finished = started.filter((l) => l.completionPercentage >= 95);
  el.innerHTML = `${topbar("My progress", { sub: "Every practice you've started." })}<div class="cards3"><div class="card stat"><b>${s.sessionsThisMonth}</b><span>sessions this month</span></div><div class="card stat"><b>${s.minutesWatched}</b><span>minutes moved</span></div><div class="card stat"><b>${s.completedLessons}</b><span>completed</span></div></div>
    ${started.length ? "" : emptyBox("Nothing started yet", "Pick a practice in Move and your progress will show up here.", "🌱")}
    ${inProgress.length ? `<div class="sect"><h3>In progress</h3></div><div class="list">${inProgress.map(lessonRow).join("")}</div>` : ""}
    ${finished.length ? `<div class="sect"><h3>Completed</h3></div><div class="list">${finished.map(lessonRow).join("")}</div>` : ""}`;
});

/* ---------------- Explore and shop ---------------- */
route("/explore", (el) => {
  el.innerHTML = `${topbar("Explore", { sub: "A holistic wellness library." })}<div class="list">${EXPLORE.map(([t, b, e, bg]) => `<a class="card row" href="#/${t === "Shop" ? "shop" : "article/" + encodeURIComponent(t)}"><span class="thumb" style="width:64px;height:64px;font-size:28px;background:${bg}"><span style="filter:brightness(0) invert(1)">${e}</span></span><div class="grow"><h3 style="font-size:20px">${esc(t)}</h3><p class="muted small">${esc(b)}</p></div>${ic("chev", "chev")}</a>`).join("")}</div>`;
});
route("/article/:title", (el, { title }) => {
  const item = EXPLORE.find((x) => x[0] === title) || [title, "", "🌿", "linear-gradient(135deg,var(--teal-deep),var(--teal))"];
  el.innerHTML = `<div class="hero" style="height:200px;background:${item[3]}"><button class="iconbtn" data-back aria-label="Back">${ic("back")}</button><span style="filter:brightness(0) invert(1);font-size:64px">${item[2]}</span></div>
    <div class="pad pt stack"><h2>${esc(item[0])}</h2><p class="small muted" style="letter-spacing:1px">MINDSET  •  5 MIN READ</p>
    <p class="muted">Movement is not just about physical exercise; it is a way to freely express ourselves, connect with others, and find inner balance. Whether you are a beginner or an experienced mover, there is a place for you in this community.</p>
    <p class="muted">Balance isn't about doing it all. It's about creating space for what matters most. Here are a few simple ways to bring more intention into your everyday life:</p>
    <div class="stack-sm muted"><p>•&nbsp; Start the day with a few slow breaths.</p><p>•&nbsp; Move in a way that feels kind to your body.</p><p>•&nbsp; End the day by noting one thing you're grateful for.</p></div>
    <p class="small muted">Full articles from Rhythm &amp; Flow are coming soon.</p></div>`;
});
route("/shop", (el) => {
  const products = [["Yoga Mat", "R650"], ["Stainless Steel Bottle", "R450"], ["Resistance Bands", "R320"], ["Wellness Journal", "R250"]];
  el.innerHTML = `${topbar("Shop", { sub: "Wellness products for everyday living." })}<div class="pad"><div class="card tang"><p>The Rhythm &amp; Flow shop is coming soon to the app. For now, visit rhythmandflow.co.za to order.</p></div></div>
    <div class="cards3 mt" style="grid-template-columns:1fr 1fr">${products.map(([n, p]) => `<div class="card"><div class="sk" style="height:90px;animation:none;display:grid;place-items:center;font-size:30px">🛍️</div><h3 style="font-size:17px;margin-top:8px">${n}</h3><p style="color:var(--teal-deep);font-weight:600">${p}</p></div>`).join("")}</div>`;
});

/* ---------------- Settings, profile, password, account ---------------- */
route("/settings", (el) => {
  const row = (icn, title, sub, href) => `<a class="card navrow" ${href.startsWith("http") ? `href="${href}" target="_blank" rel="noopener"` : `href="${href}"`}>${ic(icn, "lead")}<div class="grow"><h3 style="font-size:19px">${title}</h3><p class="muted small">${sub}</p></div>${ic("chev", "chev")}</a>`;
  const btnRow = (id, icn, title, sub) => `<button class="card navrow" id="${id}" style="text-align:left;border:0;width:100%">${ic(icn, "lead")}<div class="grow"><h3 style="font-size:19px">${title}</h3><p class="muted small">${sub}</p></div></button>`;
  const admin = adminView();
  el.innerHTML = `${topbar("Settings", { back: !admin })}<div class="pad stack">${row("you", "Account", "Personal information", "#/edit-profile")}${row("lock", "Password", "Change your password", "#/change-password")}${admin ? "" : row("card", "Subscription", "Plan, billing and receipts", "#/subscription")}${row("bell", "Notifications", "Your updates and alerts", "#/notifications")}
    ${row("policy", "Policies", "Cancellation, renewal and events", "#/policies")}${row("lock", "Privacy Policy", "Read it on rhythmandflow.co.za/privacy-policy", RF.legal.privacy)}${row("policy", "Terms of Use", "Read it on rhythmandflow.co.za/terms-of-use", RF.legal.terms)}
    ${btnRow("help", "help", "Help &amp; Support", "Get answers and contact us")}${btnRow("export", "download", "Download my data", "Get a copy of everything we hold about you")}${btnRow("delete", "trash", "Delete my account", "Erase your account and personal data")}${btnRow("about", "info", "About", "Version and credits")}
    <button class="btn danger" id="logout">${ic("logout")} Log Out</button></div>`;
  $("#logout").onclick = () => { signOutLocal(); go("/welcome"); };
  $("#help").onclick = () => dialog({ title: "Help & Support", text: "Visit rhythmandflow.co.za to get in touch with the Rhythm & Flow team.", ok: "OK", cancel: "" });
  $("#about").onclick = () => dialog({ title: "Rhythm & Flow", text: "Web version\n\nHeadings use Cormorant Garamond and text uses Open Sans, both under the SIL Open Font Licence 1.1.\n\nQuestions? Visit rhythmandflow.co.za.", ok: "OK", cancel: "" });
  $("#export").onclick = async () => {
    try { const data = await api("GET", "/api/account/export"); const a = document.createElement("a"); a.href = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: "application/json" })); a.download = "rhythm-and-flow-my-data.json"; document.body.appendChild(a); a.click(); a.remove(); }
    catch (e) { dialog({ title: "Couldn't get your data", text: e.message || "Something went wrong. Please try again.", ok: "OK", cancel: "" }); }
  };
  $("#delete").onclick = async () => {
    const v = await dialog({ title: "Delete your account?", text: "This permanently erases your profile, journal, bookings, progress and notifications. Any active subscription is cancelled first. Payment records are kept without your name, because we must keep financial records. This can't be undone.", fields: field("dpw", "Your password", { type: "password", autocomplete: "current-password" }), ok: "Delete forever", cancel: "Keep my account", danger: true, check: (x) => (x.dpw ? "" : "Enter your password.") });
    if (!v) return;
    try { await api("POST", "/api/account/delete", { password: v.dpw }); signOutLocal(); go("/welcome"); } catch (e) { toast(e.message); }
  };
}, { tab: () => (adminView() ? "a-account" : null) });

route("/edit-profile", (el) => {
  const u = RF.user || {};
  el.innerHTML = `${topbar("Edit Profile")}<div class="pad"><form id="f" novalidate>${field("name", "Full name", { value: u.fullName, autocomplete: "name", cap: "words" })}${field("email", "Email", { type: "email", value: u.email, autocomplete: "email" })}${field("about", "About me", { area: true, value: u.about, max: 300 })}
    <div class="formerr hidden" id="ferr"></div><button class="btn accent" id="go" type="submit">Save Changes</button></form></div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault(); const fe = $("#ferr"); fe.classList.add("hidden");
    const body = { fullName: val("name").trim(), email: val("email").trim(), about: val("about").trim() };
    setFieldError("name", body.fullName.length >= 2 ? "" : "Enter your full name."); setFieldError("email", /^\S+@\S+\.\S+$/.test(body.email) ? "" : "Enter a valid email address.");
    if ($$(".field.err").length) return; busy($("#go"), true, "Saving…");
    try { RF.user = await api("PUT", "/api/auth/me", body); toast("Profile updated."); history.back(); } catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy($("#go"), false); }
  };
});

route("/change-password", (el) => {
  el.innerHTML = `${topbar("Change password", { sub: "Choose something others can't guess." })}<div class="pad"><form id="f" novalidate>${field("cur", "Current password", { type: "password", autocomplete: "current-password" })}${field("nw", "New password", { type: "password", autocomplete: "new-password" })}${field("nw2", "Confirm new password", { type: "password", autocomplete: "new-password", enter: "go" })}
    <div class="formerr hidden" id="ferr"></div><button class="btn accent" id="go" type="submit">Change password</button></form></div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault(); const fe = $("#ferr"); fe.classList.add("hidden");
    setFieldError("cur", val("cur") ? "" : "Enter your current password."); setFieldError("nw", val("nw").length >= 8 ? "" : "At least 8 characters."); setFieldError("nw2", val("nw") === val("nw2") ? "" : "The passwords don't match.");
    if ($$(".field.err").length) return; busy($("#go"), true, "Saving…");
    try { const r = await api("POST", "/api/auth/change-password", { currentPassword: val("cur"), newPassword: val("nw") }); if (r && r.token) setSession(r); toast("Password changed."); history.back(); }
    catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy($("#go"), false); }
  };
});
