/* The administrator's side: dashboard, classes and events, refunds, lessons, programmes, plans, customers, bookings and the error log.
   The server checks the ADMIN role on every call; these screens are only a convenience. */
"use strict";

const A = { admin: true };
const aTab = (k) => () => k;
const aLink = (title, sub, href) => `<a class="card navrow" href="${href}"><div class="grow"><h3 style="font-size:19px">${esc(title)}</h3><p class="muted small">${esc(sub)}</p></div>${ic("chev", "chev")}</a>`;
const stat = (value, label, href) => `<${href ? `a href="${href}"` : "div"} class="card stat"><b>${esc(value)}</b><span>${esc(label)}</span></${href ? "a" : "div"}>`;
/* a row of choice buttons; chipPick() below keeps the one that was tapped */
const chipRow = (group, opts, cur) => `<div class="row wrap" data-chipgroup="${group}">${opts.map(([v, l]) => `<button type="button" class="chip ${String(v) === String(cur) ? "on" : ""}" data-g="${group}" data-v="${esc(v)}">${esc(l)}</button>`).join("")}</div>`;
function chipPick(e, state) {
  const b = e.target.closest("[data-g]"); if (!b) return false;
  state[b.dataset.g] = b.dataset.v;
  $$(`[data-g="${b.dataset.g}"]`).forEach((x) => x.classList.toggle("on", x === b));
  return b.dataset.g;
}
const checkbox = (id, label, on) => `<label class="check"><input type="checkbox" id="${id}" ${on ? "checked" : ""}><span>${esc(label)}</span></label>`;
const isoLocal = saIso;                       // South African time, whatever the admin's own device says
const dateOf = (iso) => saParts(iso).date;
const timeOf = (iso) => saParts(iso).time;
const digits = (s) => String(s || "").replace(/\D/g, "");

/* ---------------- Dashboard ---------------- */
route("/admin", async (el) => {
  const s = await api("GET", "/api/admin/summary");
  el.innerHTML = `${topbar("Admin dashboard", { back: false, sub: RF.user && RF.user.fullName ? `Signed in as ${RF.user.fullName}` : "Manage Rhythm & Flow.", right: `<a class="iconbtn right" href="#/notifications" aria-label="Notifications">${ic("bell")}</a>` })}
    <div class="cards3 mt-s">${stat(s.users, "customers", "#/admin/users")}${stat(s.activeSubscriptions, "active plans", "#/admin/subscriptions")}${stat(s.upcomingClasses, "upcoming classes", "#/admin/classes")}</div>
    <div class="cards3 mt-s">${stat(s.activeBookings, "bookings", "#/admin/bookings")}<div class="card stat" style="grid-column: span 2"><b>${fmtRand(s.monthlyRecurringRevenue)}</b><span>monthly recurring revenue</span></div></div>
    <div class="list">
      ${s.openErrors > 0 ? aLink("Needs attention", `${s.openErrors} open problem(s) in the error log`, "#/admin/errors") : ""}
      <h3 class="mt-s">Manage</h3>
      ${aLink("Classes & events", "Schedule classes, workshops and retreats, set ticket prices, see who is booked", "#/admin/classes")}
      ${aLink("Event requests & refunds", "Late cancellation requests and refunds to pay on PayFast", "#/admin/events")}
      ${aLink("Lessons & videos", "Add, edit or remove lessons", "#/admin/lessons")}
      ${aLink("Programmes", "Add, edit or hide programmes", "#/admin/programmes")}
      ${aLink("Subscription plans", "Add plans, change prices, hide a plan", "#/admin/plans")}
      <h3 class="mt-s">Review</h3>
      ${aLink("Bookings", "Everyone booked into an upcoming class", "#/admin/bookings")}
      ${aLink("Active plans", "Who is subscribed, and to what", "#/admin/subscriptions")}
      ${aLink("Customers", "Find people and switch accounts off", "#/admin/users")}
      ${aLink("Error log", s.openErrors > 0 ? `${s.openErrors} open problem(s) to review` : "Nothing open", "#/admin/errors")}
      <h3 class="mt-s">Customer view</h3>
      <a class="card navrow" href="#/home" id="custview"><div class="grow"><h3 style="font-size:19px">See the app as a customer</h3><p class="muted small">Check what your customers see. Switch back from You → Admin tools.</p></div>${ic("chev", "chev")}</a>
    </div>`;
  $("#custview").onclick = () => setCustomerView(true);
}, { admin: true, tab: aTab("a-home") });

/* ---------------- Classes and events ---------------- */
/* the three levels the client approved */
const LEVELS = ["Beginner", "Intermediate", "Advanced"];
const KINDS = [["CLASS", "Class"], ["WORKSHOP", "Workshop"], ["RETREAT", "Retreat"], ["COACHING", "Coaching"]];
route("/admin/classes", async (el) => {
  const blank = () => ({ id: null, name: "", coach: "Deni", location: "", date: "", time: "", minutes: "60", capacity: "20", description: "All levels welcome.", kind: "CLASS", format: "IN_PERSON", price: "", poster: "", closeHours: "24", opensOn: "", earlyHours: "", platform: "Zoom", joinLink: "", note: "", waitlist: false });
  let f = blank(); let classes = await api("GET", "/api/admin/classes");
  const form = () => {
    const paid = (parseInt(f.price, 10) || 0) > 0;
    return `<div class="card stack" id="cform"><h3>${f.id ? "Edit class" : "Schedule a class"}</h3>
      ${field("c_name", "Class name", { value: f.name, cap: "sentences" })}${field("c_coach", "Coach", { value: f.coach, cap: "words" })}${field("c_loc", "Location (studio or online)", { value: f.location, cap: "sentences" })}
      <div class="row">${field("c_date", "Date", { type: "date", value: f.date })}${field("c_time", "Start time", { type: "time", value: f.time })}</div>
      <div class="row">${field("c_min", "Minutes", { inputmode: "numeric", value: f.minutes })}${field("c_cap", "Capacity", { inputmode: "numeric", value: f.capacity })}</div>
      ${field("c_desc", "Description", { area: true, value: f.description, max: 500 })}
      <p class="small muted">What is it?</p>${chipRow("kind", KINDS, f.kind)}${chipRow("format", f.kind === "RETREAT" ? [["IN_PERSON", "In person"]] : [["IN_PERSON", "In person"], ["ONLINE", "Online"]], f.format)}${f.kind === "RETREAT" ? `<p class="small muted">Retreats are held in person.</p>` : ""}
      ${field("c_price", "Ticket price in rand, VAT included (empty = free booking)", { inputmode: "numeric", value: f.price })}
      ${paid ? `${field("c_poster", "Poster image address (https://…). 1200 x 630 px works best", { value: f.poster })}${field("c_close", "Ticket sales close this many hours before the start", { inputmode: "numeric", value: f.closeHours })}
        ${field("c_opens", "Ticket sales open on (optional)", { type: "date", value: f.opensOn })}${field("c_early", "Members of the second plan can buy this many hours earlier", { inputmode: "numeric", value: f.earlyHours })}
        <p class="small muted">Priority booking: this works together with the 'sales open on' date above. Second-plan members can buy this many hours before that date, and they go first when a place opens on the waiting list.</p>
        ${checkbox("c_wait", "Waiting list when sold out", f.waitlist)}` : ""}
      ${f.format === "ONLINE" ? `${chipRow("platform", [["Zoom", "Zoom"], ["Google Meet", "Google Meet"]], f.platform)}${field("c_link", "Joining link (https://…)", { value: f.joinLink })}<p class="small muted">Ticket holders get the link in a separate email. You can add it later.</p>` : ""}
      ${field("c_note", "Practical note for the confirmation email (optional, replaces the standard one)", { area: true, value: f.note, max: 800 })}
      ${f.id ? `<p class="small muted">Anyone already booked is told if the time or place changes. People who bought a ticket choose to keep it or take a refund or credit.</p>` : ""}
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="csave">${f.id ? "Save changes" : "Add class"}</button>${f.id ? `<button class="btn secondary" id="ccancel">Cancel editing</button>` : ""}</div>`;
  };
  const read = () => Object.assign(f, {
    name: val("c_name"), coach: val("c_coach"), location: val("c_loc"), date: val("c_date"), time: val("c_time"), minutes: val("c_min"), capacity: val("c_cap"), description: val("c_desc"),
    price: val("c_price"), note: val("c_note"),
    poster: $("#c_poster") ? val("c_poster") : f.poster, closeHours: $("#c_close") ? val("c_close") : f.closeHours, opensOn: $("#c_opens") ? val("c_opens") : f.opensOn,
    earlyHours: $("#c_early") ? val("c_early") : f.earlyHours, waitlist: $("#c_wait") ? $("#c_wait").checked : f.waitlist, joinLink: $("#c_link") ? val("c_link") : f.joinLink,
  });
  const list = () => `<h3 class="mt-s">Scheduled classes</h3>${classes.map((c) => `<div class="card stack-sm"><div class="row between"><div class="grow"><a href="#/admin/attendees/${c.id}" style="text-decoration:none;color:inherit"><h3 style="font-size:19px">${esc(c.name)}</h3></a>
      <p class="small muted">${fmtDayTime(c.startTime)} · ${c.capacity - c.spotsLeft}/${c.capacity} booked</p>
      ${c.price > 0 || c.format === "ONLINE" ? `<p class="small muted">${c.price > 0 ? fmtRand(c.price) + " ticket · " : ""}${c.format === "ONLINE" ? "Online" : "In person"}</p>` : ""}
      <a class="small" href="#/admin/attendees/${c.id}">See who is booked</a></div>${c.status === "SCHEDULED" ? "" : `<span class="pill grey">Cancelled</span>`}</div>
      ${c.status === "SCHEDULED" ? `<div class="row wrap"><button class="linkbtn" data-edit="${c.id}">Edit</button>${c.format === "ONLINE" && c.price > 0 ? `<button class="linkbtn" data-links="${c.id}">Send links</button>` : ""}<button class="linkbtn danger" data-cancel="${c.id}">Cancel</button></div>` : ""}</div>`).join("")}`;
  const draw = () => { el.innerHTML = `${topbar("Classes", { back: false })}<div class="pad stack">${form()}${list()}</div>`; };
  draw();
  const reload = async () => { classes = await api("GET", "/api/admin/classes"); draw(); };
  el.oninput = (e) => { if (e.target.id === "c_price") { const was = (parseInt(f.price, 10) || 0) > 0; read(); const now = (parseInt(f.price, 10) || 0) > 0; if (was !== now) { const id = e.target.id, pos = e.target.selectionStart; draw(); const n = $("#" + id); n.focus(); n.setSelectionRange(pos, pos); } } };
  el.onclick = async (e) => {
    try {
      const g = (read(), chipPick(e, f));
      if (g === "kind" && f.kind === "RETREAT") f.format = "IN_PERSON";
      if (g === "format" || g === "kind") { draw(); return; }
      if (g) return;
      const ed = e.target.closest("[data-edit]"), cn = e.target.closest("[data-cancel]"), ln = e.target.closest("[data-links]");
      if (e.target.id === "ccancel") { f = blank(); return draw(); }
      if (ed) {
        const c = classes.find((x) => String(x.id) === ed.dataset.edit); const s = new Date(c.startTime), en = new Date(c.endTime);
        f = { id: c.id, name: c.name, coach: c.coachName, location: c.location, date: dateOf(c.startTime), time: timeOf(c.startTime), minutes: String(Math.round((en - s) / 60000)), capacity: String(c.capacity), description: c.description || "",
          kind: c.kind, format: c.format, price: c.price > 0 ? String(Math.round(c.price)) : "", poster: c.posterUrl || "", closeHours: String(c.salesCloseHours || 24), opensOn: dateOf(c.salesOpenAt), earlyHours: c.memberEarlyHours > 0 ? String(c.memberEarlyHours) : "",
          platform: c.joinPlatform || "Zoom", joinLink: c.joinLink || "", note: c.practicalNote || "", waitlist: !!c.waitlistEnabled };
        draw(); window.scrollTo(0, 0); return;
      }
      if (ln) { await api("POST", `/api/admin/classes/${ln.dataset.links}/send-links`); return toast("Joining links sent to everyone who has not had one."); }
      if (cn) {
        if (!(await dialog({ title: "Cancel this class?", text: "Everyone who booked is told. People who paid for a ticket are offered a full refund or account credit, their choice.", ok: "Cancel class", cancel: "Keep it", danger: true }))) return;
        await api("POST", `/api/admin/classes/${cn.dataset.cancel}/cancel`); toast("Class cancelled."); return reload();
      }
      if (e.target.id === "csave") {
        const fe = $("#ferr"); fe.classList.add("hidden"); const fail = (m) => { fe.textContent = m; fe.classList.remove("hidden"); };
        const start = isoLocal(f.date, f.time), mins = parseInt(f.minutes, 10) || 0, cap = parseInt(f.capacity, 10) || 0, ticket = parseInt(digits(f.price), 10) || 0;
        if (!f.name.trim() || !f.location.trim()) return fail("Enter the class name and the location.");
        if (!start) return fail("Choose the date and the start time.");
        if (mins < 5 || cap < 1) return fail("Enter the class length and capacity.");
        if (ticket > 0 && ticket < 5) return fail("A ticket price must be free or at least R5.");
        const opens = f.opensOn ? isoLocal(f.opensOn, "00:00") : null;
        const body = { name: f.name.trim(), description: f.description, coachName: f.coach.trim(), location: f.location.trim(), startTime: start, endTime: new Date(new Date(start).getTime() + mins * 60000).toISOString(), capacity: cap,
          kind: f.kind, format: f.format, price: ticket, posterUrl: ticket > 0 && f.poster.trim() ? f.poster.trim() : null, salesCloseHours: parseInt(f.closeHours, 10) || 24, salesOpenAt: ticket > 0 ? opens : null,
          memberEarlyHours: ticket > 0 ? parseInt(f.earlyHours, 10) || 0 : 0, joinPlatform: f.format === "ONLINE" ? f.platform : null, joinLink: f.joinLink.trim() || null, practicalNote: f.note.trim() || null, waitlistEnabled: !!f.waitlist && ticket > 0 };
        busy(e.target, true, "Saving…");
        try { if (f.id) await api("PUT", `/api/admin/classes/${f.id}`, body); else await api("POST", "/api/admin/classes", body); toast(f.id ? "Class updated." : "Class scheduled."); f = blank(); await reload(); }
        catch (err) { busy(e.target, false); fail(err.message); }
      }
    } catch (err) { toast(err.message); }
  };
}, { admin: true, tab: aTab("a-classes") });

route("/admin/attendees/:id", async (el, { id }) => {
  const [rows, classes] = await Promise.all([api("GET", `/api/admin/classes/${id}/attendees`), api("GET", "/api/admin/classes")]);
  const paid = (classes.find((c) => String(c.id) === id) || {}).price > 0;
  el.innerHTML = `${topbar("Who is booked", { sub: `${rows.length} ${rows.length === 1 ? "person" : "people"}` })}` + (rows.length ? `<div class="list">${rows.map((a) => `<div class="card stack-sm"><h3 style="font-size:19px">${esc(a.fullName)}</h3><p class="small muted">${esc(a.email)} · booked ${fmtDate(a.bookedAt)}</p><div class="row between"><span class="pill ${a.status === "BOOKED" ? "" : "grey"}">${esc(a.status)}</span>${paid && a.status === "BOOKED" ? `<button class="linkbtn" data-credit="${a.bookingId}" data-name="${esc(a.fullName)}">Give account credit</button>` : ""}</div></div>`).join("")}</div>` : emptyBox("No one yet", "Nobody has booked this class so far.", "📅"));
  el.onclick = async (e) => {
    const c = e.target.closest("[data-credit]"); if (!c) return;
    const v = await dialog({
      title: "Give account credit", ok: "Give credit", cancel: "Cancel",
      fields: `<p class="muted">${esc(c.dataset.name)} will be emailed. The credit is valid for 12 months and is used automatically on their next ticket. The most you can give is what they paid.</p>${field("camt", "Amount (R)", { inputmode: "numeric" })}${field("crs", "Reason", { value: "Online class problem" })}`,
      check: (x) => (parseFloat(x.camt) >= 1 ? "" : "Enter an amount of at least R1."),
    });
    if (!v) return;
    try { await api("POST", `/api/admin/bookings/${c.dataset.credit}/credit`, { amount: parseFloat(v.camt), reason: (v.crs || "").trim() || null }); toast("Credit added. The person has been told."); } catch (err) { toast(err.message); }
  };
}, { admin: true });

/* ---------------- Event requests and refunds ---------------- */
route("/admin/events", async (el) => {
  const draw = async () => {
    const [reqs, refunds] = await Promise.all([api("GET", "/api/admin/event-requests"), api("GET", "/api/admin/refunds")]);
    el.innerHTML = `${topbar("Event requests & refunds")}<div class="pad stack">
      <h3>Late cancellation requests</h3><p class="small muted">These came in less than 48 hours before the event. You can agree up to half of the fee, or decline.</p>
      ${reqs.length ? reqs.map((r) => `<div class="card stack-sm"><h3 style="font-size:19px">${esc(r.eventName)}</h3><p class="small muted">${esc(r.fullName)} · ${esc(r.email)}</p><p class="small muted">${fmtDayTime(r.startTime)} · paid ${fmtRand(r.totalPaid)} · ${esc(r.reference)}</p>
        <p>Asked for: ${r.choice === "REFUND" ? "a refund" : "account credit"}. You can agree up to ${fmtRand(r.maxAmount)}.</p>${field("amt" + r.bookingId, "Amount to agree (R)", { inputmode: "numeric", value: String(Math.round(r.maxAmount)) })}
        <div class="row"><button class="btn" data-approve="${r.bookingId}">Approve</button><button class="btn secondary" data-decline="${r.bookingId}">Decline</button></div></div>`).join("") : `<p class="muted">Nothing waiting.</p>`}
      <h3 class="mt-s">Refunds to pay on PayFast</h3><p class="small muted">Pay each refund from your PayFast dashboard, then mark it here so the customer is told it has been sent.</p>
      ${refunds.length ? refunds.map((r) => `<div class="card stack-sm"><h3 style="font-size:19px">${fmtRand(r.amount)} to ${esc(r.fullName)}</h3><p class="small muted">${esc(r.eventName)} · ${esc(r.reference)}</p><p class="small muted">${esc(r.email)}</p>
        ${r.payFastPaymentId ? `<p class="small muted">PayFast payment: ${esc(r.payFastPaymentId)}</p>` : ""}<button class="btn secondary" data-paid="${r.bookingId}">I've paid this refund</button></div>`).join("") : `<p class="muted">Nothing to pay.</p>`}</div>`;
  };
  await draw();
  el.onclick = async (e) => {
    const ap = e.target.closest("[data-approve]"), de = e.target.closest("[data-decline]"), pd = e.target.closest("[data-paid]");
    try {
      if (ap) { await api("POST", `/api/admin/bookings/${ap.dataset.approve}/resolve`, { approve: true, amount: parseFloat(val("amt" + ap.dataset.approve)) || null }); toast("Approved. The customer has been told."); await draw(); }
      else if (de) { await api("POST", `/api/admin/bookings/${de.dataset.decline}/resolve`, { approve: false, amount: null }); toast("Declined. The customer has been told."); await draw(); }
      else if (pd) { await api("POST", `/api/admin/bookings/${pd.dataset.paid}/refunded`); toast("Marked as refunded. The customer has been told."); await draw(); }
    } catch (err) { toast(err.message); }
  };
}, { admin: true });

/* ---------------- Lessons and programmes ---------------- */
route("/admin/lessons", async (el) => {
  const blank = () => ({ id: null, programmeId: null, title: "", description: "", category: "Yoga", seconds: "", provider: "Remote", url: "", preview: false, level: "Beginner" });
  let f = blank(); let [lessons, programmes] = await Promise.all([api("GET", "/api/admin/lessons"), api("GET", "/api/admin/programmes")]);
  if (!f.programmeId && programmes.length) f.programmeId = String(programmes[0].id);
  const draw = () => {
    el.innerHTML = `${topbar("Lessons & videos", { back: false })}<div class="pad stack"><div class="card stack"><h3>${f.id ? "Edit lesson" : "Add a lesson"}</h3>
      <p class="small muted">Programme</p>${chipRow("programmeId", programmes.map((p) => [p.id, p.name]), f.programmeId)}
      ${field("l_title", "Title", { value: f.title, cap: "sentences" })}${field("l_desc", "Description", { area: true, value: f.description, max: 600 })}
      <p class="small muted">Category</p>${chipRow("category", CATEGORIES.map((c) => [c.name, c.name]), catInfo(f.category).name)}<p class="small muted">${esc(catInfo(f.category).desc)}</p>
      <p class="small muted">Level</p>${chipRow("level", LEVELS.map((l) => [l, l]), f.level)}
      ${field("l_sec", "Length in seconds", { inputmode: "numeric", value: f.seconds })}
      <p class="small muted">Where is the video?</p>${chipRow("provider", [["Remote", "Video file"], ["YouTube", "YouTube"]], f.provider)}
      ${field("l_url", f.provider === "YouTube" ? "YouTube link (https://youtu.be/…)" : "Video address (https://…)", { value: f.url })}
      ${f.provider === "YouTube" ? `<p class="small muted">Play a public YouTube video inside the app. Tick free preview so anyone with an account can watch it.</p>` : ""}
      ${checkbox("l_prev", "Free preview (no subscription needed)", f.preview)}
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="lsave">${f.id ? "Save changes" : "Add lesson"}</button>${f.id ? `<button class="btn secondary" id="lcancel">Cancel editing</button>` : ""}</div>
      <h3 class="mt-s">Existing lessons</h3>${lessons.map((l) => `<div class="card row"><div class="grow"><h3 style="font-size:18px">${esc(l.title)}</h3><p class="small muted">${esc(l.programmeName)} · ${esc(l.category)} · ${durLabel(l.durationSeconds)}</p></div>
        <button class="iconbtn" style="margin:0" data-edit="${l.id}" aria-label="Edit lesson">${ic("edit")}</button><button class="iconbtn" style="margin:0" data-del="${l.id}" aria-label="Delete lesson">${ic("trash")}</button></div>`).join("")}</div>`;
  };
  draw();
  const keep = () => Object.assign(f, { title: val("l_title"), description: val("l_desc"), seconds: val("l_sec"), url: val("l_url"), preview: $("#l_prev").checked });
  el.onclick = async (e) => {
    try {
      keep(); const g = chipPick(e, f); if (g) { if (g === "category" || g === "provider") draw(); return; }
      const ed = e.target.closest("[data-edit]"), del = e.target.closest("[data-del]");
      if (e.target.id === "lcancel") { f = Object.assign(blank(), { programmeId: String(programmes[0].id) }); return draw(); }
      if (ed) {
        const l = lessons.find((x) => String(x.id) === ed.dataset.edit); const yt = /youtube/i.test(l.videoProvider);
        f = { id: l.id, programmeId: String(l.programmeId), title: l.title, description: l.description, category: l.category, seconds: String(l.durationSeconds), provider: yt ? "YouTube" : "Remote", url: yt ? `https://youtu.be/${l.videoReference}` : l.videoReference, preview: l.isPreview, level: LEVELS.includes(l.level) ? l.level : "Beginner" };
        draw(); window.scrollTo(0, 0); return;
      }
      if (del) {
        if (!(await dialog({ title: "Delete this lesson?", text: "Customers will no longer be able to watch it, and their progress on it is removed.", ok: "Delete", cancel: "Keep it", danger: true }))) return;
        await api("DELETE", `/api/admin/lessons/${del.dataset.del}`); toast("Lesson deleted."); lessons = await api("GET", "/api/admin/lessons"); return draw();
      }
      if (e.target.id === "lsave") {
        const fe = $("#ferr"); fe.classList.add("hidden"); const fail = (m) => { fe.textContent = m; fe.classList.remove("hidden"); };
        const secs = parseInt(digits(f.seconds), 10) || 0;
        if (!f.title.trim() || secs < 1 || !/^https?:\/\//i.test(f.url.trim()) || !f.programmeId) return fail("Enter a title, the length in seconds and a video address that starts with https://.");
        const body = { programmeId: Number(f.programmeId), title: f.title.trim(), description: f.description.trim(), category: f.category, level: f.level, durationSeconds: secs, videoProvider: f.provider, videoReference: f.url.trim(), isPreview: !!f.preview };
        busy(e.target, true, "Saving…");
        try { if (f.id) await api("PUT", `/api/admin/lessons/${f.id}`, body); else await api("POST", "/api/admin/lessons", body); toast(f.id ? "Lesson updated." : "Lesson added."); f = Object.assign(blank(), { programmeId: f.programmeId }); lessons = await api("GET", "/api/admin/lessons"); draw(); }
        catch (err) { busy(e.target, false); fail(err.message); }
      }
    } catch (err) { toast(err.message); }
  };
}, { admin: true });

route("/admin/programmes", async (el) => {
  const draw = async () => {
    const rows = await api("GET", "/api/admin/programmes");
    el.innerHTML = `${topbar("Programmes")}<div class="pad stack"><button class="btn accent" data-new>Add programme</button>${rows.map((p) => `<div class="card row"><div class="grow"><h3 style="font-size:19px">${esc(p.name)}</h3><p class="small muted">${p.lessonCount} practices · access level ${p.minTier}</p>${p.active ? "" : `<span class="pill grey">Hidden</span>`}</div><button class="linkbtn" data-edit="${p.id}">Edit</button></div>`).join("")}</div>`;
    el._rows = rows;
  };
  await draw();
  const form = async (p) => {
    const v = await dialog({
      title: p ? "Edit programme" : "Add programme", ok: "Save", cancel: "Cancel",
      fields: `${field("p_name", "Name", { value: p ? p.name : "", cap: "words" })}${field("p_desc", "Description", { area: true, value: p ? p.description : "", max: 500 })}
        <p class="small muted">Access level</p><div class="row">${[1, 2, 3].map((t) => `<button type="button" class="chip ${(p ? p.minTier : 1) === t ? "on" : ""}" data-pick="${t}" data-group="tier">Level ${t}</button>`).join("")}</div>
        ${checkbox("p_active", "Visible to customers", p ? p.active : true)}`,
      check: (x) => (x.p_name.trim() ? "" : "Enter a name."),
    });
    if (!v) return;
    const body = { name: v.p_name.trim(), description: v.p_desc.trim(), minTier: Number(v.tier || (p ? p.minTier : 1)), active: !!v.p_active };
    try { if (p) await api("PUT", `/api/admin/programmes/${p.id}`, body); else await api("POST", "/api/admin/programmes", body); toast("Saved."); await draw(); } catch (err) { toast(err.message); }
  };
  el.onclick = (e) => {
    if (e.target.closest("[data-new]")) return form(null);
    const ed = e.target.closest("[data-edit]"); if (ed) form(el._rows.find((x) => String(x.id) === ed.dataset.edit));
  };
}, { admin: true });

/* ---------------- Plans ---------------- */
route("/admin/plans", async (el) => {
  const draw = async () => {
    const rows = await api("GET", "/api/admin/plans"); el._rows = rows;
    el.innerHTML = `${topbar("Subscription plans", { back: false })}<div class="pad stack"><button class="btn accent" data-new>Add plan</button>${rows.map((p) => `<div class="card row"><div class="grow"><h3 style="font-size:19px">${esc(p.name)}</h3>
      <p class="small muted">${fmtRand(p.price)} / month · access level ${p.tier}</p>${p.annualPrice != null ? `<p class="small muted">${fmtRand(p.annualPrice)} / year</p>` : ""}
      ${p.launchPrice != null ? `<p class="small muted">Launch: ${fmtRand(p.launchPrice)} first month, until ${fmtDate(p.launchEndsAt)}</p>` : ""}${p.trialDays > 0 ? `<p class="small muted">${p.trialDays}-day free trial</p>` : ""}
      ${p.status !== "ACTIVE" ? `<span class="pill grey">Hidden from customers</span>` : ""}</div><button class="linkbtn" data-edit="${p.id}">Edit</button></div>`).join("")}</div>`;
  };
  await draw();
  const form = async (p) => {
    const v = await dialog({
      title: p ? "Edit plan" : "Add plan", ok: "Save", cancel: "Cancel",
      fields: `${field("n", "Name", { value: p ? p.name : "", cap: "words" })}${field("pr", "Price per month (R)", { inputmode: "numeric", value: p ? String(Math.round(p.price)) : "" })}${field("d", "Short description", { area: true, value: p ? p.description : "", max: 300 })}
        <p class="small muted">Access level (a plan unlocks every programme at its level or below)</p><div class="row">${[1, 2, 3].map((t) => `<button type="button" class="chip ${(p ? p.tier : 1) === t ? "on" : ""}" data-pick="${t}" data-group="tier">Level ${t}</button>`).join("")}</div>
        ${field("f", "What's included (one per line)", { area: true, value: p ? p.features.join("\n") : "" })}
        <p class="small muted">Billing options (all optional)</p>${field("an", "Price for a whole year (R). Leave empty for monthly only", { inputmode: "numeric", value: p && p.annualPrice != null ? String(Math.round(p.annualPrice)) : "" })}
        ${field("lp", "Launch price for the first month (R)", { inputmode: "numeric", value: p && p.launchPrice != null ? String(Math.round(p.launchPrice)) : "" })}${field("le", "Launch price ends on", { type: "date", value: p ? dateOf(p.launchEndsAt) : "" })}
        ${field("td", "Free trial days (empty = no trial)", { inputmode: "numeric", value: p && p.trialDays > 0 ? String(p.trialDays) : "" })}
        <p class="small muted">The launch price and the free trial are only for people who have never held a plan.</p>${checkbox("vis", "Visible to customers", p ? p.status !== "INACTIVE" : true)}
        ${p ? `<p class="small muted">Changing a price only affects new subscribers. Existing PayFast subscriptions keep their amount.</p>` : ""}`,
      check: (x) => { if (!x.n.trim() || !(parseFloat(x.pr) >= 1)) return "Enter a name and a price of at least R1."; if (x.lp && !x.le) return "Enter the day the launch price ends."; return ""; },
    });
    if (!v) return;
    const body = { name: v.n.trim(), description: v.d.trim(), price: parseFloat(v.pr), tier: Number(v.tier || (p ? p.tier : 1)), features: v.f.trim(), status: v.vis ? "ACTIVE" : "INACTIVE",
      annualPrice: v.an ? parseFloat(v.an) : null, launchPrice: v.lp ? parseFloat(v.lp) : null, launchEndsAt: v.lp ? isoLocal(v.le, "23:59") : null, trialDays: parseInt(v.td, 10) || 0 };
    try { if (p) await api("PUT", `/api/admin/plans/${p.id}`, body); else await api("POST", "/api/admin/plans", body); toast(p ? "Plan updated." : "Plan added."); await draw(); } catch (err) { toast(err.message); }
  };
  el.onclick = (e) => {
    if (e.target.closest("[data-new]")) return form(null);
    const ed = e.target.closest("[data-edit]"); if (ed) form(el._rows.find((x) => String(x.id) === ed.dataset.edit));
  };
}, { admin: true, tab: aTab("a-plans") });

/* ---------------- Customers, bookings, plans in use, error log ---------------- */
route("/admin/users", async (el) => {
  let term = "", timer = null, seq = 0;
  el.innerHTML = `${topbar("Customers", { back: false, sub: "Find someone, or switch an account off." })}<div class="search"><label class="sr-only" for="q">Search</label>${ic("search")}<div class="field" style="margin:0"><input id="q" type="search" placeholder="Search name, username or email" autocomplete="off"></div></div><div id="res" class="mt-s"></div>`;
  const load = async () => {
    const n = ++seq; const rows = await api("GET", `/api/admin/users?search=${encodeURIComponent(term)}`); if (n !== seq) return;
    $("#res").innerHTML = rows.length ? `<div class="list">${rows.map((u) => `<div class="card row"><div class="grow"><h3 style="font-size:19px">${esc(u.fullName)}</h3><p class="small muted">${esc(u.email)} · @${esc(u.username)}</p>
      <div class="pills">${u.role === "ADMIN" ? `<span class="pill grey">Admin</span>` : ""}${u.plan ? `<span class="pill">${esc(u.plan)}</span>` : ""}${u.status !== "ACTIVE" ? `<span class="pill tang">Switched off</span>` : ""}</div></div>
      ${u.role === "CUSTOMER" ? `<button class="linkbtn ${u.status === "ACTIVE" ? "danger" : ""}" data-toggle="${u.id}" data-name="${esc(u.fullName)}" data-on="${u.status === "ACTIVE" ? 1 : 0}">${u.status === "ACTIVE" ? "Switch off" : "Switch on"}</button>` : ""}</div>`).join("")}</div>` : emptyBox("No one found", "Try a different name or email address.", "🔎");
  };
  await load();
  el.oninput = (e) => { if (e.target.id === "q") { term = e.target.value.trim(); clearTimeout(timer); timer = setTimeout(() => load().catch((x) => toast(x.message)), 300); } };
  el.onclick = async (e) => {
    const t = e.target.closest("[data-toggle]"); if (!t) return; const disabling = t.dataset.on === "1";
    if (!(await dialog({ title: disabling ? `Switch off ${t.dataset.name}?` : `Switch ${t.dataset.name} back on?`, ok: disabling ? "Switch off" : "Switch on", danger: disabling,
      text: disabling ? "They will be signed out everywhere and won't be able to log in. Their data is kept, and any subscription is not cancelled automatically." : "They will be able to log in again." }))) return;
    try { await api("POST", `/api/admin/users/${t.dataset.toggle}/status`, { status: disabling ? "DISABLED" : "ACTIVE" }); toast(disabling ? "Account switched off." : "Account switched back on."); await load(); } catch (err) { toast(err.message); }
  };
  return () => clearTimeout(timer);
}, { admin: true, tab: aTab("a-users") });

route("/admin/subscriptions", async (el) => {
  const rows = await api("GET", "/api/admin/subscriptions");
  el.innerHTML = topbar("Active plans") + (rows.length ? `<div class="list">${rows.map((s) => `<div class="card"><div class="row between"><h3 style="font-size:19px">${esc(s.fullName)}</h3><span class="pill ${s.status === "ACTIVE" ? "" : "grey"}">${esc(s.status)}</span></div>
    <p class="small muted">${esc(s.email)}</p><p class="small">${esc(s.planName)} · ${fmtRand(s.price)}${s.endDate ? ` · ${s.renewing ? "next payment" : "until"} ${fmtDate(s.endDate)}` : ""}</p></div>`).join("")}</div>` : emptyBox("No plans yet", "Nobody has subscribed so far.", "💳"));
}, { admin: true });

route("/admin/bookings", async (el) => {
  const rows = await api("GET", "/api/admin/bookings");
  el.innerHTML = topbar("Bookings") + (rows.length ? `<div class="list">${rows.map((b) => `<div class="card"><h3 style="font-size:19px">${esc(b.className)}</h3><p class="small muted">${fmtDayTime(b.startTime)} · ${esc(b.location)}</p><p class="small">${esc(b.fullName)} · ${esc(b.email)}</p></div>`).join("")}</div>` : emptyBox("No bookings", "Nobody has booked an upcoming class.", "📅"));
}, { admin: true });

route("/admin/errors", async (el) => {
  const draw = async () => {
    const rows = await api("GET", "/api/admin/errors?status=NEW");
    el.innerHTML = topbar("Error log", { sub: rows.length ? `${rows.length} open` : "Nothing open", right: rows.length ? `<button class="linkbtn" id="resall">Resolve all</button>` : "" }) +
      (rows.length ? `<div class="list">${rows.map((x) => `<div class="card stack-sm"><div class="row between"><h3 style="font-size:18px">${esc(x.message)}</h3><span class="pill grey">× ${x.count}</span></div>
        <p class="small muted">${esc(x.source)}${x.route ? " · " + esc(x.route) : ""}${x.userEmail ? " · " + esc(x.userEmail) : ""}${x.appVersion ? " · " + esc(x.appVersion) : ""}</p><p class="small muted">Last seen ${timeAgo(x.lastSeen)}</p>
        ${x.details ? `<details><summary class="small">Details</summary><pre class="small prewrap">${esc(x.details)}</pre></details>` : ""}<button class="btn secondary small" data-res="${x.id}">Mark resolved</button></div>`).join("")}</div>` : emptyBox("All clear", "No problems have been reported.", "✅"));
  };
  await draw();
  el.onclick = async (e) => {
    try {
      const r = e.target.closest("[data-res]");
      if (r) { await api("POST", `/api/admin/errors/${r.dataset.res}/resolve`); await draw(); }
      else if (e.target.id === "resall") { await api("POST", "/api/admin/errors/resolve-all"); await draw(); }
    } catch (err) { toast(err.message); }
  };
}, { admin: true });
