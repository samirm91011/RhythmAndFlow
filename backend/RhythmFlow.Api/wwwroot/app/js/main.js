/* Home, Move (programmes and lessons), lesson detail and the video player. Classes and events are in events.js.
   The wording and layout follow the Android app screen for screen. */
"use strict";

const MOODS = [
  ["Energised", "☀️", "#FFF1DC"], ["Tired", "🔋", "#E3ECFB"], ["Stressed", "☁️", "#F3E6EE"],
  ["Grounded", "🌿", "#DDEFEA"], ["Overwhelmed", "🌀", "#FDE9E1"], ["Happy", "😊", "#FFEFD9"],
  ["Low", "🌙", "#E5E8F7"], ["Need some space", "🤍", "#FBE4E3"],
];
const CATS = ["All", ...CATEGORIES.map((c) => c.name)];
/* what the person has saved with the heart on a practice (kept on this device, like the app) */
const saved = {
  all() { return store.get("saved_lessons", []); },
  has(id) { return saved.all().includes(String(id)); },
  toggle(id) { const s = saved.all().filter((x) => x !== String(id)); if (s.length === saved.all().length) s.push(String(id)); store.set("saved_lessons", s); return saved.has(id); },
};

function lessonRow(l) {
  const pct = Math.round(l.completionPercentage || 0);
  return `<a class="card lesson" href="#/lesson/${l.id}">
    <div class="thumb ${catClass(l.category)}"><span aria-hidden="true">${catMark(l.category)}</span>${l.locked ? `<span class="lockbadge" title="Locked">${ic("lock")}</span>` : ""}</div>
    <div class="grow"><h3 style="font-size:19px">${esc(l.title)}</h3>
      <p class="muted small clamp2">${esc(l.description)}</p>
      <div class="pills"><span class="pill">${durLabel(l.durationSeconds)}</span><span class="pill grey">${esc(l.category)}</span>${l.isPreview ? `<span class="pill tang">Preview</span>` : ""}</div>
      ${pct > 0 ? `<div class="bar"><i style="width:${Math.min(pct, 100)}%"></i></div>` : ""}</div>
    <span class="round ${l.locked ? "off" : ""}">${ic(l.locked ? "lock" : "play")}</span></a>`;
}

async function bellButton() {
  let n = 0; try { n = (await api("GET", "/api/notifications/unread-count")).count; } catch (e) { /* ignore */ }
  return `<a class="iconbtn right" href="#/notifications" aria-label="Notifications${n ? `, ${n} unread` : ""}">${ic("bell")}${n ? `<span class="badge">${n > 9 ? "9+" : n}</span>` : ""}</a>`;
}

const rhythmCards = () => `<div class="cards3">
  <a class="rhythm plain tang" href="#/move"><h3>Morning Reset</h3><span>10 mins</span></a>
  <a class="rhythm plain tint" href="#/explore"><h3>Nourish Your Body</h3><span>Recipes</span></a>
  <a class="rhythm plain grey" href="#/move"><h3>Move &amp; Release</h3><span>15 mins</span></a></div>`;

/* ---------------- Home ---------------- */
route("/home", async (el) => {
  const [lessons, summary, subs, bell, bookings] = await Promise.all([api("GET", "/api/lessons"), api("GET", "/api/progress/summary"), loadSubs(true), bellButton(), api("GET", "/api/bookings").catch(() => [])]);
  const first = (RF.user && RF.user.fullName || "").split(" ")[0] || "";
  const resume = lessons.filter((l) => !l.locked && l.watchTimeSeconds > 0 && l.completionPercentage < 95).sort((a, b) => b.completionPercentage - a.completionPercentage)[0];
  const next = bookings.filter((b) => b.status === "BOOKED" && new Date(b.startTime) > new Date()).sort((a, b) => new Date(a.startTime) - new Date(b.startTime))[0];
  el.innerHTML = `<div class="top"><img src="img/logo.png" alt="Rhythm &amp; Flow" style="width:52px;height:52px"><div class="grow"></div>${bell}</div>
    <div class="greet"><h1>${esc(greeting())}${first ? ", " + esc(first) : ""}</h1><p class="muted mt-s">Take a moment to check in with yourself. How are you feeling today?</p></div>
    <div class="moods mt" role="group" aria-label="Mood">${MOODS.map(([m, e, bg]) => `<button class="mood" data-mood="${esc(m)}" style="background:${bg}"><span class="em" aria-hidden="true">${e}</span>${esc(m)}</button>`).join("")}</div>
    <div class="pad mt">${next
      ? `<a class="card tint" href="#/classes?t=mine"><span class="pill solid" style="background:var(--teal-deep)">Your next class</span><h3 style="margin-top:8px">${esc(next.className)}</h3><p class="muted small">${fmtDayTime(next.startTime)} · ${esc(next.location)}</p></a>`
      : `<a class="card tang" href="#/classes"><h3>Join a class</h3><p class="muted small">Move with Deni and the community. Browse upcoming classes and reserve your spot.</p></a>`}</div>
    ${resume ? `<div class="pad mt"><a class="card" href="#/lesson/${resume.id}"><span class="pill">Continue where you left off</span><h3 style="margin-top:8px">${esc(resume.title)}</h3>
      <p class="muted small">${Math.round(resume.completionPercentage)}% done · about ${Math.max(0, Math.ceil((resume.durationSeconds - resume.watchTimeSeconds) / 60))} min left</p><div class="bar"><i style="width:${Math.round(resume.completionPercentage)}%"></i></div></a></div>` : ""}
    <div class="sect"><h3>Your Rhythm Today</h3><a class="linkbtn" href="#/rhythm">See All</a></div>
    ${rhythmCards()}
    <div class="cards3 mt"><a class="card stat" href="#/progress"><b>${summary.sessionsThisMonth}</b><span>sessions this month</span></a><a class="card stat" href="#/progress"><b>${summary.minutesWatched}</b><span>minutes moved</span></a><a class="card stat" href="#/progress"><b>${summary.completedLessons}</b><span>completed</span></a></div>
    ${hasAccess(subs) ? "" : `<div class="pad mt"><div class="banner"><h2>Build a life that feels good.</h2><p>Unlock every programme with a monthly subscription.</p><a class="btn accent small" style="display:inline-flex" href="#/plans">See plans</a></div></div>`}
    <div class="pad mt"><div class="card"><h3 class="muted center" style="font-weight:600">“A small check-in can lead to a big shift.”</h3></div></div>
    <div style="height:16px"></div>`;
  el.onclick = async (e) => {
    const b = e.target.closest("[data-mood]"); if (!b) return;
    const mood = b.dataset.mood; $$(".mood").forEach((x) => x.classList.toggle("on", x === b));
    try { await api("POST", "/api/journal", { kind: "CHECKIN", mood, text: "" }); toast(`Thanks for checking in. You're feeling ${mood} today.`); } catch (err) { toast(err.message); }
  };
}, { tab: "home" });

route("/rhythm", (el) => {
  el.innerHTML = `${topbar("Your Rhythm Today", { sub: "Small steps that add up." })}${rhythmCards()}
    <div class="pad mt"><div class="banner" style="background:linear-gradient(135deg,var(--teal-deep),var(--teal))"><h2>Build a life that feels good.</h2><p></p><a class="btn small" style="display:inline-flex;background:#fff;color:var(--teal-deep);box-shadow:none" href="#/explore">Explore Today</a></div>
    <div class="card mt"><h3 class="muted center" style="font-weight:600">“Progress isn’t about doing more, it’s about coming back to yourself.”</h3></div></div>`;
});

/* ---------------- Move ---------------- */
route("/move", async (el) => {
  const qs = new URLSearchParams((location.hash.split("?")[1]) || "");
  let cat = qs.get("c") && CATS.includes(qs.get("c")) ? qs.get("c") : "All", term = "";
  const programmes = await api("GET", "/api/programmes");
  el.innerHTML = `${topbar("Move", { back: false, sub: "Practices to help you feel good." })}
    ${programmes.length ? `<div class="sect" style="margin-top:0"><h3>Programmes</h3></div><div class="progcards">${programmes.map((p) => `<a class="card progcard ${p.locked ? "mist" : "tint"}" href="#/programme/${p.id}"><h3 class="clamp2">${esc(p.name)}${p.locked ? ` <span class="muted">${ic("lock", "sm")}</span>` : ""}</h3><p class="muted small">${p.lessonCount} ${p.lessonCount === 1 ? "practice" : "practices"}</p></a>`).join("")}</div>` : ""}
    <div class="chips" role="group" aria-label="Category">${CATS.map((c) => `<button class="chip ${c === cat ? "on" : ""}" data-cat="${esc(c)}">${esc(c)}</button>`).join("")}</div>
    <p class="small muted pad" id="catdesc" style="margin-bottom:8px">${cat === "All" ? "" : esc(catInfo(cat).desc)}</p>
    <div class="search"><label class="sr-only" for="q">Search practices</label>${ic("search")}<div class="field" style="margin:0"><input id="q" type="search" placeholder="Search practices…" enterkeyhint="search" autocomplete="off"></div></div>
    <div id="res" class="mt-s">${skeleton(3)}</div>`;
  let timer = null, seq = 0;
  const load = async () => {
    const n = ++seq;
    try {
      const l = await api("GET", `/api/lessons?category=${encodeURIComponent(cat)}&q=${encodeURIComponent(term)}`);
      if (n !== seq) return;
      $("#res").innerHTML = l.length ? `<div class="list">${l.map(lessonRow).join("")}</div>` : emptyBox("Nothing here yet", "Try a different category or search.", "🔎");
    } catch (e) { if (n === seq) $("#res").innerHTML = errorBox(e.message); }
  };
  el.oninput = (e) => { if (e.target.id === "q") { term = e.target.value.trim(); clearTimeout(timer); timer = setTimeout(load, 300); } };
  el.onclick = (e) => {
    const c = e.target.closest("[data-cat]"); if (c) { cat = c.dataset.cat; $$(".chip").forEach((x) => x.classList.toggle("on", x === c)); $("#catdesc").textContent = cat === "All" ? "" : catInfo(cat).desc; load(); }
    if (e.target.closest("[data-retry]")) load();
  };
  load();
  return () => { clearTimeout(timer); el.oninput = null; };
}, { tab: "move" });

route("/programme/:id", async (el, { id }) => {
  const [programmes, lessons, plans] = await Promise.all([api("GET", "/api/programmes"), api("GET", `/api/lessons?programmeId=${id}`), api("GET", "/api/plans")]);
  const p = programmes.find((x) => String(x.id) === id); if (!p) throw new ApiError("We couldn't find that programme.", 404);
  const plan = plans.filter((x) => x.tier >= p.minTier).sort((a, b) => a.tier - b.tier)[0];
  el.innerHTML = `${topbar(p.name, { sub: `${p.lessonCount} ${p.lessonCount === 1 ? "practice" : "practices"}` })}<div class="pad stack">
    ${p.description ? `<p class="muted">${esc(p.description)}</p>` : ""}
    ${p.locked ? `<div class="card tang stack-sm"><h3 style="font-size:18px">Included in the ${esc(plan ? plan.name : "paid")} plan and above.</h3><p class="muted small">Subscribe to unlock every practice in this programme.</p><a class="btn" href="#/plans">View plans</a></div>` : `<div><span class="pill">Included in your plan</span></div>`}</div>
    ${lessons.length ? `<div class="list">${lessons.map(lessonRow).join("")}</div>` : emptyBox("No practices yet", "New practices will appear here.")}`;
});

/* ---------------- Lesson ---------------- */
route("/lesson/:id", async (el, { id }) => {
  const l = await api("GET", `/api/lessons/${id}`);
  const related = (await api("GET", `/api/lessons?category=${encodeURIComponent(l.category)}`)).filter((x) => x.id !== l.id).slice(0, 5);
  const pct = Math.round(l.completionPercentage || 0);
  let tab = "About";
  const paint = () => {
    const body = tab === "About" ? `<p class="muted">Part of ${esc(l.programmeName)}. ${esc(l.description)}</p>`
      : tab === "Guide" ? `<div class="stack-sm muted">${["Find a quiet space and roll out your mat.", "Move at your own pace. There is no wrong way to do this.", "Stop if anything hurts and rest.", "Finish with a few deep breaths and notice how you feel."].map((t) => `<p>•&nbsp; ${t}</p>`).join("")}</div>`
        : related.length ? `<div class="list" style="padding:0">${related.map(lessonRow).join("")}</div>` : `<p class="muted">More practices are coming soon.</p>`;
    $("#lbody").innerHTML = `<div class="tabsbar">${["About", "Guide", "Related"].map((t) => `<button class="${t === tab ? "on" : ""}" data-ltab="${t}">${t}</button>`).join("")}</div>${body}`;
  };
  el.innerHTML = `<div class="hero ${catClass(l.category)}"><button class="iconbtn" data-back aria-label="Back">${ic("back")}</button><button class="iconbtn fav" id="heart" aria-label="${saved.has(l.id) ? "Remove from saved" : "Save"}">${ic(saved.has(l.id) ? "heart1" : "heart0")}</button><span aria-hidden="true">${catMark(l.category)}</span></div>
    <div class="sheet pad pt stack"><h2>${esc(l.title)}</h2>
    <div class="pills" style="margin:0"><span class="pill">${durLabel(l.durationSeconds)}</span><span class="pill">${esc(l.level)}</span><span class="pill">${esc(l.category)}</span></div>
    <p class="muted">${esc(l.description)}</p>
    ${l.locked ? `<div class="card tang"><h3 style="font-size:18px">Included with a subscription</h3><p class="muted small">Subscribe to ${esc(l.programmeName)} and everything in your plan.</p></div><a class="btn accent" href="#/plans">View plans</a>`
      : `<h3>What you'll need</h3><div class="needs">${["Yoga mat", "Water", "Open space"].map((t) => `<div class="card">${t}</div>`).join("")}</div>
        <a class="btn" href="#/play/${l.id}">${l.watchTimeSeconds > 0 && pct < 95 ? "Resume Practice" : "Begin Practice"}</a>`}
    <div id="lbody"></div></div>`;
  paint();
  el.onclick = (e) => {
    const t = e.target.closest("[data-ltab]"); if (t) { tab = t.dataset.ltab; paint(); return; }
    if (e.target.closest("#heart")) { const on = saved.toggle(l.id); $("#heart").innerHTML = ic(on ? "heart1" : "heart0"); $("#heart").setAttribute("aria-label", on ? "Remove from saved" : "Save"); }
  };
});

route("/play/:id", async (el, { id }) => {
  const l = await api("GET", `/api/lessons/${id}`);
  el.innerHTML = `<section class="player"><button class="iconbtn back" data-back aria-label="Back">${ic("back")}</button><div class="msg" id="pmsg">Loading video…</div></section>`;
  const sec = $(".player");
  let pb;
  try { pb = await api("GET", `/api/lessons/${id}/playback`); }
  catch (e) { $("#pmsg").innerHTML = `<p>${esc(e.message)}</p>${e.status === 403 ? `<a class="btn accent small" href="#/plans">View plans</a>` : `<button class="btn secondary small" data-retry>Try again</button>`}`; return; }
  if (pb.youtubeId) {
    // A free practice from the studio's YouTube channel plays in YouTube's own player. YouTube needs to see where it is embedded,
    // which is why the page sends its address (referrerpolicy) and the safety header allows the frame.
    $("#pmsg").remove();
    sec.insertAdjacentHTML("beforeend", `<div class="ytwrap"><iframe title="${esc(l.title)}" src="https://www.youtube.com/embed/${encodeURIComponent(pb.youtubeId)}?rel=0&modestbranding=1&playsinline=1${pb.resumeSeconds > 3 ? "&start=" + pb.resumeSeconds : ""}"
      referrerpolicy="strict-origin-when-cross-origin" allow="autoplay; encrypted-media; picture-in-picture; fullscreen" allowfullscreen></iframe></div>
      <p class="ytnote">${esc(l.title)}<br><a href="#/done/${l.id}">I've finished</a> · <a href="https://www.youtube.com/watch?v=${encodeURIComponent(pb.youtubeId)}" target="_blank" rel="noopener">Not playing? Watch on YouTube</a></p>`);
    return;
  }
  const v = document.createElement("video");
  v.src = pb.url; v.controls = true; v.playsInline = true; v.setAttribute("playsinline", ""); v.preload = "metadata";
  v.setAttribute("controlsList", "nodownload noremoteplayback"); v.setAttribute("disablePictureInPicture", ""); v.oncontextmenu = (e) => e.preventDefault();
  $("#pmsg").remove(); sec.appendChild(v);
  v.addEventListener("loadedmetadata", () => { if (pb.resumeSeconds > 3) { try { v.currentTime = pb.resumeSeconds; } catch (e) { /* ignore */ } } });
  v.addEventListener("error", () => { v.remove(); const m = document.createElement("div"); m.className = "msg"; m.innerHTML = `<p>This video couldn't be played. Check your internet connection and try again.</p><button class="btn secondary small" data-retry>Try again</button>`; sec.appendChild(m); });
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

route("/done/:id", (el) => {
  let mood = null;
  const moods = ["Calmer", "More energised", "Lighter", "Stronger", "More present", "Other"];
  el.innerHTML = `<div class="centerpage" style="justify-content:flex-start;padding-top:48px"><div class="big-check">${ic("tick")}</div><h1>Practice Complete!</h1><p class="muted">You showed up for yourself. How do you feel now?</p>
    <div class="pills" style="justify-content:center">${moods.map((m) => `<button class="chip" data-m="${m}">${m}</button>`).join("")}</div>
    <div style="width:100%;text-align:left" class="mt-s">${field("note", "What did your body need today?", { area: true, max: 2000 })}</div>
    <button class="btn accent" style="max-width:420px" id="save">Save to Journal</button><a class="linkbtn" href="#/home">Back to Home</a></div>`;
  el.onclick = async (e) => {
    const b = e.target.closest("[data-m]"); if (b) { mood = b.dataset.m; $$("[data-m]").forEach((x) => x.classList.toggle("on", x === b)); return; }
    if (e.target.id !== "save") return;
    busy(e.target, true, "Saving…");
    try { await api("POST", "/api/journal", { kind: "REFLECTION", mood, text: val("note").trim() || null }); toast("Saved to your journal."); go("/home"); } catch (err) { busy(e.target, false); toast(err.message); }
  };
});
