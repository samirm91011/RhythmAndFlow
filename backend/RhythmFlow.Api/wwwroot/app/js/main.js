/* Home, Move (programmes and lessons), lesson detail, video player, Classes. */
"use strict";

const MOODS = [
  ["Energised", "☀️", "#FFF1DC"], ["Tired", "🔋", "#E3ECFB"], ["Stressed", "☁️", "#F3E6EE"],
  ["Grounded", "🌿", "#DDEFEA"], ["Overwhelmed", "🌀", "#FDE9E1"], ["Happy", "😊", "#FFEFD9"],
  ["Low", "🌙", "#E5E8F7"], ["Need some space", "🤍", "#FBE4E3"],
];
const CATS = ["All", "Yoga", "Dance", "Barre", "Stretch", "Meditation"];

function lessonRow(l) {
  const pct = Math.round(l.completionPercentage || 0);
  return `<a class="card lesson" href="#/lesson/${l.id}">
    <div class="thumb ${catClass(l.category)}"><span aria-hidden="true">${catIcon(l.category)}</span>${l.locked ? `<span class="lockbadge" title="Locked">${ic("lock")}</span>` : ""}</div>
    <div class="grow"><h3 class="clamp2" style="font-size:19px">${esc(l.title)}</h3>
      <p class="muted small">${esc(l.category)} · ${esc(l.level)} · ${durLabel(l.durationSeconds)}</p>
      <div class="pills">${l.isPreview ? `<span class="pill tang">Free preview</span>` : ""}${l.locked ? `<span class="pill grey">Locked</span>` : ""}${pct >= 90 ? `<span class="pill">Done</span>` : pct > 0 ? `<span class="pill">${pct}% watched</span>` : ""}</div></div>
    <span class="round ${l.locked ? "off" : ""}">${ic(l.locked ? "lock" : "play")}</span></a>`;
}

async function bellButton() {
  let n = 0; try { n = (await api("GET", "/api/notifications/unread-count")).count; } catch (e) { /* ignore */ }
  return `<a class="iconbtn right" href="#/notifications" aria-label="Notifications${n ? `, ${n} unread` : ""}">${ic("bell")}${n ? `<span class="badge">${n > 9 ? "9+" : n}</span>` : ""}</a>`;
}

/* ---------------- Home ---------------- */
route("/home", async (el) => {
  const [lessons, summary, subs, bell] = await Promise.all([api("GET", "/api/lessons"), api("GET", "/api/progress/summary"), loadSubs(true), bellButton()]);
  const first = (RF.user && RF.user.fullName || "").split(" ")[0] || "there";
  const resume = lessons.filter((l) => !l.locked && l.completionPercentage > 0 && l.completionPercentage < 90).sort((a, b) => b.watchTimeSeconds - a.watchTimeSeconds)[0];
  const sub = hasAccess(subs);
  el.innerHTML = `<div class="top"><div class="grow"><p class="eyebrow">${esc(greeting().toUpperCase())}</p></div>${bell}</div>
    <div class="greet"><h1>Hello, ${esc(first)}</h1><p class="muted mt-s">How are you feeling today?</p></div>
    <div class="moods mt-s" role="group" aria-label="Mood">${MOODS.map(([m, e, bg]) => `<button class="mood" data-mood="${esc(m)}" style="background:${bg}"><span class="em" aria-hidden="true">${e}</span>${esc(m)}</button>`).join("")}</div>
    <div class="pad mt">${resume ? `<a class="banner card" style="display:block;text-decoration:none" href="#/lesson/${resume.id}"><p class="eyebrow" style="color:#9ED6CB">CONTINUE</p><h2>${esc(resume.title)}</h2><p>${Math.round(resume.completionPercentage)}% watched · ${durLabel(resume.durationSeconds)}</p><span class="btn accent small" style="display:inline-flex">Resume</span></a>`
      : sub ? `<a class="card tint" href="#/move"><h3>Pick a session</h3><p class="muted small">Browse programmes in Move.</p></a>`
        : `<a class="card tang" href="#/plans"><h3>Unlock every lesson</h3><p class="muted small">Choose a plan to watch the full programmes.</p></a>`}</div>
    <div class="sect"><h3>Your month</h3><a class="linkbtn" href="#/progress">My progress</a></div>
    <div class="cards3"><a class="card stat" href="#/progress"><b>${summary.sessionsThisMonth}</b><span>Sessions</span></a><a class="card stat" href="#/progress"><b>${summary.minutesWatched}</b><span>Minutes</span></a><a class="card stat" href="#/progress"><b>${summary.completedLessons}</b><span>Completed</span></a></div>
    <div class="sect"><h3>Find your rhythm</h3></div>
    <div class="cards3">${[["Yoga", "#DDEFEA"], ["Dance", "#FDE9E1"], ["Stretch", "#E3F1EE"]].map(([c, bg]) => `<a class="rhythm" style="background:${bg}" href="#/move?c=${c}"><h3>${c}</h3><span>${catIcon(c)} Explore</span></a>`).join("")}</div>
    <div style="height:16px"></div>`;
  el.onclick = async (e) => {
    const b = e.target.closest("[data-mood]"); if (!b) return;
    const mood = b.dataset.mood; $$(".mood").forEach((x) => x.classList.toggle("on", x === b));
    try { await api("POST", "/api/journal", { kind: "CHECKIN", mood, text: "" }); toast(`Check-in saved: ${mood}`); } catch (err) { toast(err.message); }
  };
}, { tab: "home" });

/* ---------------- Move ---------------- */
route("/move", async (el) => {
  const qs = new URLSearchParams((location.hash.split("?")[1]) || "");
  let cat = qs.get("c") && CATS.includes(qs.get("c")) ? qs.get("c") : "All", term = "";
  const [programmes] = await Promise.all([api("GET", "/api/programmes")]);
  el.innerHTML = `${topbar("Move", { back: false, sub: "Programmes and lessons" })}
    <div class="search"><label class="sr-only" for="q">Search lessons</label>${ic("search")}<div class="field" style="margin:0"><input id="q" type="search" placeholder="Search lessons" enterkeyhint="search" autocomplete="off"></div></div>
    <div class="chips mt-s" role="group" aria-label="Category">${CATS.map((c) => `<button class="chip ${c === cat ? "on" : ""}" data-cat="${c}">${c}</button>`).join("")}</div>
    <div id="prog">${programmes.length ? `<div class="sect" style="margin-top:6px"><h3>Programmes</h3></div><div class="progcards">${programmes.map((p) => `<a class="card progcard ${p.locked ? "mist" : "tint"}" href="#/programme/${p.id}"><h3 class="clamp2">${esc(p.name)}</h3><p class="muted small">${p.lessonCount} lessons ${p.locked ? "· 🔒" : ""}</p></a>`).join("")}</div>` : ""}</div>
    <div class="sect" style="margin-top:6px"><h3>Lessons</h3></div><div id="res">${skeleton(3)}</div>`;
  let timer = null, seq = 0;
  const load = async () => {
    const n = ++seq;
    try {
      const l = await api("GET", `/api/lessons?category=${encodeURIComponent(cat)}&q=${encodeURIComponent(term)}`);
      if (n !== seq) return;
      $("#res").innerHTML = l.length ? `<div class="list">${l.map(lessonRow).join("")}</div>` : emptyBox("Nothing found", "Try another category or search word.", "🔎");
    } catch (e) { if (n === seq) $("#res").innerHTML = errorBox(e.message); }
  };
  el.oninput = (e) => { if (e.target.id === "q") { term = e.target.value.trim(); clearTimeout(timer); timer = setTimeout(load, 300); } };
  el.onclick = (e) => {
    const c = e.target.closest("[data-cat]"); if (c) { cat = c.dataset.cat; $$(".chip").forEach((x) => x.classList.toggle("on", x === c)); load(); }
    if (e.target.closest("[data-retry]")) load();
  };
  load();
  return () => { clearTimeout(timer); el.oninput = null; };
}, { tab: "move" });

route("/programme/:id", async (el, { id }) => {
  const [programmes, lessons] = await Promise.all([api("GET", "/api/programmes"), api("GET", `/api/lessons?programmeId=${id}`)]);
  const p = programmes.find((x) => String(x.id) === id); if (!p) throw new ApiError("We couldn't find that programme.", 404);
  el.innerHTML = `${topbar(p.name)}<div class="pad"><p class="muted">${esc(p.description)}</p>
    ${p.locked ? `<a class="card tang mt-s" href="#/plans"><h3>Plan required</h3><p class="muted small">This programme is part of a higher plan. Tap to see plans.</p></a>` : ""}</div>
    <div class="sect"><h3>${lessons.length} lessons</h3></div><div class="list">${lessons.map(lessonRow).join("")}</div>`;
});

/* ---------------- Lesson ---------------- */
route("/lesson/:id", async (el, { id }) => {
  const l = await api("GET", `/api/lessons/${id}`);
  const pct = Math.round(l.completionPercentage || 0);
  el.innerHTML = `<div class="hero ${catClass(l.category)}"><button class="iconbtn" data-back aria-label="Back">${ic("back")}</button><span aria-hidden="true">${catIcon(l.category)}</span></div>
    <div class="pad pt stack"><div><h2>${esc(l.title)}</h2><p class="muted small mt-s">${esc(l.programmeName)}</p></div>
    <div class="pills" style="margin:0"><span class="pill">${esc(l.category)}</span><span class="pill">${esc(l.level)}</span><span class="pill grey">${durLabel(l.durationSeconds)}</span>${l.isPreview ? `<span class="pill tang">Free preview</span>` : ""}</div>
    <p>${esc(l.description)}</p>
    ${pct > 0 ? `<div><div class="small muted">${pct >= 90 ? "Completed" : pct + "% watched"}</div><div class="bar"><i style="width:${pct}%"></i></div></div>` : ""}
    ${l.locked ? `<div class="hint">${ic("lock", "sm")}<span>This lesson is part of a paid plan.</span></div><a class="btn accent" href="#/plans">See plans</a>`
      : `<a class="btn accent" href="#/play/${l.id}">${ic("play")} ${l.watchTimeSeconds > 5 && pct < 90 ? "Resume" : "Start"} lesson</a>`}</div>`;
});

route("/play/:id", async (el, { id }) => {
  const l = await api("GET", `/api/lessons/${id}`);
  el.innerHTML = `<section class="player"><button class="iconbtn back" data-back aria-label="Back">${ic("back")}</button><div class="msg" id="pmsg">Loading video…</div></section>`;
  const sec = $(".player");
  let pb;
  try { pb = await api("GET", `/api/lessons/${id}/playback`); }
  catch (e) { $("#pmsg").innerHTML = `<p>${esc(e.message)}</p><button class="btn secondary small" data-retry>Try again</button>`; return; }
  const v = document.createElement("video");
  v.src = pb.url; v.controls = true; v.playsInline = true; v.setAttribute("playsinline", ""); v.preload = "metadata";
  v.setAttribute("controlsList", "nodownload noremoteplayback"); v.setAttribute("disablePictureInPicture", ""); v.oncontextmenu = (e) => e.preventDefault();
  $("#pmsg").remove(); sec.appendChild(v);
  v.addEventListener("loadedmetadata", () => { if (pb.resumeSeconds > 3) { try { v.currentTime = pb.resumeSeconds; } catch (e) { /* ignore */ } } });
  v.addEventListener("error", () => { v.remove(); const m = document.createElement("div"); m.className = "msg"; m.innerHTML = `<p>This video couldn't be played. Check your connection and try again.</p><button class="btn secondary small" data-retry>Try again</button>`; sec.appendChild(m); });
  let last = 0, sent = 0;
  const report = async (done) => {
    const t = Math.floor(v.currentTime || 0); if (!done && Math.abs(t - sent) < 5) return; sent = t;
    try { await api("POST", "/api/progress", { lessonId: Number(id), watchTimeSeconds: t }); } catch (e) { /* silent: progress is best effort */ }
  };
  v.addEventListener("timeupdate", () => { const t = Math.floor(v.currentTime); if (t - last >= 15) { last = t; report(); } });
  v.addEventListener("pause", () => report());
  v.addEventListener("ended", async () => { await report(true); go("/done/" + id); });
  return () => { try { report(); v.pause(); v.removeAttribute("src"); v.load(); } catch (e) { /* ignore */ } };
});

route("/done/:id", async (el, { id }) => {
  el.innerHTML = `<div class="centerpage"><div class="big-check">${ic("tick")}</div><h2>Well done</h2><p class="muted">How do you feel after that session?</p>
    <div class="pills" style="justify-content:center">${["Calmer", "More energised", "Lighter", "Stronger", "More present"].map((m) => `<button class="chip" data-m="${m}">${m}</button>`).join("")}</div>
    <a class="btn accent" style="max-width:320px" href="#/home">Back to home</a></div>`;
  el.onclick = async (e) => {
    const b = e.target.closest("[data-m]"); if (!b) return;
    $$("[data-m]").forEach((x) => x.classList.toggle("on", x === b));
    try { await api("POST", "/api/journal", { kind: "REFLECTION", mood: b.dataset.m, text: "After a lesson" }); toast("Saved to your journal."); } catch (err) { toast(err.message); }
  };
});

/* ---------------- Classes ---------------- */
route("/classes", async (el) => {
  let tab = "upcoming";
  const draw = async () => {
    const [classes, bookings] = await Promise.all([api("GET", "/api/classes"), api("GET", "/api/bookings")]);
    const mine = bookings.filter((b) => b.status === "CONFIRMED");
    const body = tab === "upcoming"
      ? (classes.length ? `<div class="list">${classes.map((c) => `<div class="card stack-sm"><div class="row between"><h3>${esc(c.name)}</h3>${c.status === "CANCELLED" ? `<span class="pill grey">Cancelled</span>` : c.bookedByMe ? `<span class="pill">Booked</span>` : c.spotsLeft <= 0 ? `<span class="pill grey">Full</span>` : `<span class="pill tang">${c.spotsLeft} spots left</span>`}</div>
        <p class="small muted">${fmtDayTime(c.startTime)} – ${fmtTime(c.endTime)}</p><p class="small">${esc(c.coachName)} · ${esc(c.location)}</p>${c.description ? `<p class="small muted">${esc(c.description)}</p>` : ""}
        ${c.bookedByMe || c.status === "CANCELLED" ? "" : `<button class="btn ${c.spotsLeft <= 0 ? "disabled" : "accent"} small" data-book="${c.id}" ${c.spotsLeft <= 0 ? "disabled" : ""}>Book this class</button>`}</div>`).join("")}</div>` : emptyBox("No classes yet", "New classes will appear here.", "📅"))
      : (mine.length ? `<div class="list">${mine.map((b) => `<div class="card stack-sm"><h3>${esc(b.className)}</h3><p class="small muted">${fmtDayTime(b.startTime)} – ${fmtTime(b.endTime)}</p><p class="small">${esc(b.coachName)} · ${esc(b.location)}</p>
        ${b.canCancel ? `<button class="btn danger small" data-cancel="${b.id}">Cancel booking</button>` : `<p class="small muted">Too late to cancel online (under 1 hour).</p>`}</div>`).join("")}</div>` : emptyBox("No bookings", "Book a class from the Upcoming tab.", "🌿"));
    el.innerHTML = `${topbar("Classes", { back: false, sub: "Book your spot" })}<div class="tabsbar"><button class="${tab === "upcoming" ? "on" : ""}" data-tab="upcoming">Upcoming</button><button class="${tab === "mine" ? "on" : ""}" data-tab="mine">My bookings${mine.length ? ` (${mine.length})` : ""}</button></div>${body}`;
  };
  await draw();
  el.onclick = async (e) => {
    const t = e.target.closest("[data-tab]"), b = e.target.closest("[data-book]"), c = e.target.closest("[data-cancel]");
    try {
      if (t) { tab = t.dataset.tab; await draw(); }
      else if (b) { busy(b, true, "Booking…"); await api("POST", `/api/classes/${b.dataset.book}/book`); toast("You're booked. See you there!"); await draw(); }
      else if (c) { if (await dialog({ title: "Cancel booking?", text: "Your spot will be released.", ok: "Cancel booking", cancel: "Keep it", danger: true })) { await api("POST", `/api/bookings/${c.dataset.cancel}/cancel`); toast("Booking cancelled."); await draw(); } }
    } catch (err) { toast(err.message); if (b) busy(b, false); }
  };
}, { tab: "classes" });
