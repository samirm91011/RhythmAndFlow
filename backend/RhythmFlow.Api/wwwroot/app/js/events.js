/* Classes and events: booking, paid tickets, the waiting list, my bookings, account credit and cancellations. */
"use strict";

const KIND_LABEL = { CLASS: "Class", WORKSHOP: "Workshop", RETREAT: "Retreat", COACHING: "Coaching" };
const placeOf = (x) => (x.format === "ONLINE" ? `Online${x.joinPlatform ? " on " + x.joinPlatform : ""}` : x.location);
const detail = (icon, text) => `<div class="detail">${ic(icon, "sm")}<span>${esc(text)}</span></div>`;
const POLICY_NOTE = "Cancel at least 48 hours before the event for a full refund or account credit. Closer than that, a refund or credit isn't guaranteed.";

function poster(url) { return url && /^https:\/\//i.test(url) ? `<img class="poster" src="${esc(url)}" alt="" loading="lazy" onerror="this.remove()">` : ""; }

function classCard(c) {
  const paid = c.price > 0;
  const status = c.status === "CANCELLED" ? `<span class="pill grey">Cancelled</span>` : c.bookedByMe ? `<span class="pill">Booked</span>`
    : c.spotsLeft <= 0 ? `<span class="pill grey">Full</span>` : c.spotsLeft <= 5 ? `<span class="pill tang">${c.spotsLeft} spots left</span>` : `<span class="pill">${c.spotsLeft} spots</span>`;
  let action;
  if (c.status === "CANCELLED") action = "";
  else if (c.bookedByMe) action = `<button class="btn secondary" disabled>You're booked in</button>`;
  else if (!paid && c.spotsLeft <= 0) action = `<button class="btn secondary" disabled>Class is full</button>`;
  else if (!paid) action = `<button class="btn accent" data-book="${c.id}">Book this class</button>`;
  else if (c.canBuyNow) action = `<button class="btn accent" data-buy="${c.id}">${c.onWaitlist ? "A place is held for you: buy ticket" : `Buy ticket · ${fmtRand(c.price)}`}</button>`;
  else if (c.onWaitlist) action = `<button class="btn secondary" disabled>On the waiting list</button>`;
  else if (c.spotsLeft <= 0 && c.waitlistEnabled && /sold out/i.test(c.buyBlockedReason || "")) action = `<button class="btn secondary" data-wait="${c.id}">Join the waiting list</button>`;
  else action = `<button class="btn secondary" disabled>${c.spotsLeft <= 0 ? "Sold out" : "Not available"}</button>${c.buyBlockedReason ? `<p class="small muted">${esc(c.buyBlockedReason)}</p>` : ""}`;
  return `<div class="card stack-sm">${poster(c.posterUrl)}
    <div class="row between"><h3>${esc(c.name)}</h3>${status}</div>
    ${paid || c.kind !== "CLASS" || c.format === "ONLINE" ? `<div class="pills" style="margin:0">${c.kind !== "CLASS" ? `<span class="pill grey">${esc(KIND_LABEL[c.kind] || c.kind)}</span>` : ""}<span class="pill grey">${c.format === "ONLINE" ? "Online" : "In person"}</span>${paid ? `<span class="pill">${fmtRand(c.price)}</span>` : ""}</div>` : ""}
    ${detail("classes", `${fmtDay(c.startTime)} · ${fmtTime(c.startTime)} – ${fmtTime(c.endTime)} (SA time)`)}${detail("you", c.coachName)}${detail("place", placeOf(c))}
    ${c.description ? `<p class="small muted">${esc(c.description)}</p>` : ""}
    ${paid && c.memberEarlyHours > 0 && c.salesOpenAt ? `<p class="small muted">Members of the second plan can buy ${c.memberEarlyHours} hours before everyone else, and go first on the waiting list.</p>` : ""}
    ${action}</div>`;
}

/* A significant technical problem can be reported for an online class once it has started and for 48 hours after it ends. */
const canReport = (b) => b.format === "ONLINE" && b.status === "BOOKED" && Date.now() > new Date(b.startTime) && Date.now() < new Date(b.endTime).getTime() + 48 * 3600000;

function bookingCard(b) {
  const pill = b.choiceNeeded ? `<span class="pill tang">Your choice</span>` : b.status === "WAITLISTED" ? `<span class="pill grey">Waiting list</span>`
    : b.requestStatus === "PENDING" ? `<span class="pill">Request sent</span>` : b.requestStatus === "DECLINED" ? `<span class="pill">Booked</span>` : `<span class="pill">Confirmed</span>`;
  let body = "";
  if (b.choiceNeeded) {
    body = `<p>${b.choiceNeeded === "EVENT_CANCELLED" ? "Sorry, this event was cancelled. Choose a full refund or account credit." : "This event moved to a new time. Keep your place, or take a full refund or account credit."}</p>
      ${b.choiceNeeded === "EVENT_RESCHEDULED" ? `<button class="btn" data-choice="KEEP" data-id="${b.id}">Keep my place</button>` : ""}
      <div class="row"><button class="btn secondary" data-choice="REFUND" data-id="${b.id}">Full refund</button><button class="btn secondary" data-choice="CREDIT" data-id="${b.id}">Account credit</button></div>`;
  } else if (b.status === "WAITLISTED") {
    body = `<button class="btn secondary" data-leave="${b.id}">Leave the waiting list</button>`;
  } else {
    body = `${b.joinLink && /^https?:\/\//i.test(b.joinLink) ? `<a class="btn" href="${esc(b.joinLink)}" target="_blank" rel="noopener">Open the joining link</a>` : ""}
      ${canReport(b) ? `<button class="btn secondary" data-report="${b.id}">Report a problem</button>` : ""}
      ${b.requestStatus === "PENDING" ? `<p class="small muted">We've received your cancellation request and will handle it fairly.</p>`
        : b.canCancel ? `<button class="btn secondary" data-cancelticket="${b.id}">${b.totalPaid > 0 ? "Cancel ticket" : "Cancel booking"}</button>`
          : b.totalPaid <= 0 ? `<p class="small muted">Too close to the class start to cancel online.</p>` : ""}`;
  }
  return `<div class="card stack-sm"><div class="row between"><h3>${esc(b.className)}</h3>${pill}</div>
    ${detail("classes", `${fmtDay(b.startTime)} · ${fmtTime(b.startTime)} – ${fmtTime(b.endTime)} (SA time)`)}${detail("you", b.coachName)}${detail("place", placeOf(b))}
    ${b.totalPaid > 0 ? `<p class="small muted">Paid ${fmtRand(b.totalPaid)} · Reference ${esc(b.reference)}</p>` : ""}
    ${b.refundStatus === "REQUESTED" ? `<p class="small" style="color:var(--teal-deep)">Refund on its way. We'll tell you when it's sent.</p>` : ""}
    ${body}</div>`;
}

route("/classes", async (el) => {
  let tab = /[?&]t=mine/.test(location.hash) ? "mine" : "upcoming";
  const draw = async () => {
    const [classes, bookings, credits] = await Promise.all([api("GET", "/api/classes"), api("GET", "/api/bookings"), api("GET", "/api/credits").catch(() => ({ balance: 0, items: [] }))]);
    const shown = bookings.filter((b) => b.status !== "CANCELLED" && b.status !== "COMPLETED" && b.status !== "PENDING_PAYMENT");
    let body;
    if (tab === "upcoming") {
      body = classes.length ? `<div class="list">${classes.map(classCard).join("")}</div>` : emptyBox("No upcoming classes", "New classes will appear here as soon as they are scheduled.", "📅");
    } else {
      const credit = credits.balance > 0 ? `<div class="card tint"><h3 style="font-size:19px">Account credit: ${fmtRand(credits.balance)}</h3>${credits.items[0] ? `<p class="small muted">Valid until ${fmtDate(credits.items[0].expiresAt)}. It is used automatically when you buy a ticket.</p>` : ""}</div>` : "";
      body = `<div class="list">${credit}${shown.length ? shown.map(bookingCard).join("") : (credit ? "" : emptyBox("No bookings yet", "Reserve a class and it will show up here.", "🌿"))}<a class="linkbtn center" href="#/policies">Cancellation and refund policies</a></div>`;
    }
    el.innerHTML = `${topbar("Classes", { back: false, sub: "Reserve your spot and move with the community." })}
      <div class="tabsbar"><button class="${tab === "upcoming" ? "on" : ""}" data-tab="upcoming">Upcoming</button><button class="${tab === "mine" ? "on" : ""}" data-tab="mine">My bookings${shown.length ? ` (${shown.length})` : ""}</button></div>${body}`;
  };
  await draw();
  const done = async (r) => { if (r && r.message) await dialog({ title: "Done", text: r.message, ok: "OK", cancel: "" }); await draw(); };
  el.onclick = async (e) => {
    const t = e.target.closest("[data-tab]"), book = e.target.closest("[data-book]"), buy = e.target.closest("[data-buy]"), wait = e.target.closest("[data-wait]");
    const choice = e.target.closest("[data-choice]"), leave = e.target.closest("[data-leave]"), cancel = e.target.closest("[data-cancelticket]"), report = e.target.closest("[data-report]");
    try {
      if (t) { tab = t.dataset.tab; await draw(); }
      else if (book) {
        busy(book, true, "Booking…"); await api("POST", `/api/classes/${book.dataset.book}/book`); toast("You're booked in. See you there!"); await draw();
      } else if (wait) {
        busy(wait, true, "Joining…"); await api("POST", `/api/classes/${wait.dataset.wait}/waitlist`); toast("You're on the waiting list. We'll tell you if a place opens."); await draw();
      } else if (buy) {
        busy(buy, true, "One moment…");
        const [classes, q] = await Promise.all([api("GET", "/api/classes"), api("GET", `/api/classes/${buy.dataset.buy}/quote`)]);
        busy(buy, false);
        const c = classes.find((x) => String(x.id) === buy.dataset.buy); if (!c) return draw();
        if (!q.canBuy) { toast(q.reason || "Tickets aren't available right now."); return; }
        const ok = await dialog({
          title: "Buy a ticket", ok: q.amountDue === 0 ? "Book with credit" : "Continue to payment", cancel: "Not now",
          fields: `<p><b>${esc(c.name)}</b></p><p class="muted">${fmtDay(c.startTime)} · ${fmtTime(c.startTime)} – ${fmtTime(c.endTime)} (SA time)</p><p>${fmtRand(c.price)} including VAT.</p>
            ${q.creditApplied > 0 ? `<p style="color:var(--teal-deep)">You have ${fmtRand(q.creditAvailable)} account credit. ${fmtRand(q.creditApplied)} will be used, so you pay ${fmtRand(q.amountDue)}.</p>` : ""}
            <p class="small muted">${POLICY_NOTE}</p><p class="small"><a href="#/policies">Read the cancellation policy</a></p>
            ${c.format === "ONLINE" ? `<p class="small muted">A separate email with the joining link will follow.</p>` : ""}`,
        });
        if (!ok) return;
        const r = await api("POST", `/api/classes/${c.id}/ticket`, { useCredit: true });
        if (r.confirmed || !r.paymentUrl) { toast(r.message); tab = "mine"; await draw(); } else toPayFast(r.paymentUrl, "ticket", r.bookingId);   // payment happens on PayFast's own page
      } else if (choice) {
        const r = await api("POST", `/api/bookings/${choice.dataset.id}/choice`, { choice: choice.dataset.choice }); await done(r);
      } else if (report) {
        const v = await dialog({
          title: "Report a problem", ok: "Send report", cancel: "Cancel",
          fields: `<p class="muted">Tell us what went wrong and about how long it lasted. We'll look into it and email you with what we can offer.</p>${field("issue", "What happened?", { area: true, max: 1000 })}`,
          check: (x) => (x.issue.trim().length >= 10 ? "" : "Please tell us what happened and about how long it lasted."),
        });
        if (v) await done(await api("POST", `/api/bookings/${report.dataset.report}/report-issue`, { description: v.issue.trim() }));
      } else if (leave) {
        const r = await api("POST", `/api/bookings/${leave.dataset.leave}/cancel-ticket`, { choice: null, note: null }); await done(r);
      } else if (cancel) {
        const b = (await api("GET", "/api/bookings")).find((x) => String(x.id) === cancel.dataset.cancelticket); if (!b) return draw();
        if (!(b.totalPaid > 0)) {
          if (!(await dialog({ title: "Cancel this booking?", text: `${b.className} on ${fmtDayTime(b.startTime)}. Your spot will be released for someone else.`, ok: "Cancel booking", cancel: "Keep it", danger: true }))) return;
          await api("POST", `/api/bookings/${b.id}/cancel`); toast("Booking cancelled."); return draw();
        }
        const v = await dialog({
          title: "Cancel your ticket?", ok: b.freeCancellation ? "Cancel ticket" : "Send request", cancel: "Keep it", danger: true,
          fields: `<p>${esc(b.className)} on ${fmtDayTime(b.startTime)}. You paid ${fmtRand(b.totalPaid)}.</p>
            ${b.freeCancellation ? `<p style="color:var(--teal-deep)">There are more than 48 hours to go, so you can have the full fee back.</p>`
              : `<p class="muted">This is less than 48 hours before the event, so a refund or credit isn't guaranteed. We'll look at your request fairly. Up to half of the fee can be agreed.</p>`}
            <p class="small muted">${b.freeCancellation ? "I'd like:" : "I'd prefer:"}</p>
            <div class="row"><button type="button" class="chip on" data-pick="CREDIT" data-group="choice">Account credit</button><button type="button" class="chip" data-pick="REFUND" data-group="choice">Refund</button></div>
            <p class="small muted">Account credit can be used towards any other event for 12 months.</p>
            ${b.freeCancellation ? "" : field("note", "Anything we should know? (optional)", { area: true, max: 500 })}
            <p class="small"><a href="#/policies">Read the cancellation policy</a></p>`,
        });
        if (!v) return;
        await done(await api("POST", `/api/bookings/${b.id}/cancel-ticket`, { choice: v.choice || "CREDIT", note: (v.note || "").trim() || null }));
      }
    } catch (err) { toast(err.message); if (book || buy || wait) await draw().catch(() => {}); }
  };
}, { tab: "classes" });

/* Back from PayFast after paying for a ticket: wait for PayFast to confirm it to the server. The app never decides a payment worked. */
function renderTicketPaid(el, bookingId) {
  let stop = false, tries = 0;
  const show = (inner) => { el.innerHTML = `<div class="centerpage">${inner}</div>`; };
  show(`<div class="spinner" aria-hidden="true"></div><h2>Confirming your payment</h2><p class="muted">Finish paying on the PayFast page, then come back here. Your ticket is held for a short time while you pay.</p><a class="btn secondary" style="max-width:320px" href="#/classes">Back to classes</a>`);
  const tick = async () => {
    if (stop) return;
    try {
      const found = (await api("GET", "/api/bookings")).find((b) => String(b.id) === String(bookingId) && b.status === "BOOKED");
      if (found) { pending.clear(); return show(`<div class="big-check">${ic("tick")}</div><h2>You're booked!</h2><p class="muted">${esc(found.className)} on ${fmtDayTime(found.startTime)}. Reference ${esc(found.reference)}. We've emailed your confirmation.</p><a class="btn accent" style="max-width:320px" href="#/classes">View my bookings</a>`); }
    } catch (e) { /* try again */ }
    if (++tries >= 60) return show(`<h2>Still waiting for confirmation</h2><p class="muted">If you completed the payment, it can take a few minutes. Your booking will appear in My bookings, and we'll email you.</p><a class="btn accent" style="max-width:320px" href="#/classes">View my bookings</a>`);
    setTimeout(tick, 3000);
  };
  tick();
  return () => { stop = true; };
}
