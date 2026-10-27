/* Plans and payment (monthly, yearly, launch price, free trial, changing plan), subscription, payment history and policies. */
"use strict";

/* Statuses that mean the person has held a plan before, so the launch price and the free trial are no longer for them. */
const HELD_BEFORE = ["ACTIVE", "CANCELLED", "EXPIRED", "PAST_DUE"];
const isFuture = (iso) => !!iso && new Date(iso).getTime() > Date.now();
const billingWord = (b) => (b === "ANNUAL" ? "year" : "month");
const priceLine = (price, b) => `${fmtRand(price)} per ${billingWord(b)}`;

function planCard(plan, billing, isNew, label, enabled) {
  const yearly = billing === "ANNUAL" && plan.annualPrice != null;
  const launch = !yearly && isNew && plan.launchPrice != null ? plan.launchPrice : null;
  const trial = isNew && plan.trialDays > 0;
  let price;
  if (yearly) {
    const saving = plan.price * 12 - plan.annualPrice;
    price = `<div class="price">${fmtRand(plan.annualPrice)}<small> / year</small></div><p class="muted">That's ${fmtRand(Math.round(plan.annualPrice / 12))} a month, paid once a year.</p>${saving > 0 ? `<p style="color:var(--teal-deep)">You save ${fmtRand(saving)} compared with paying monthly.</p>` : ""}`;
  } else if (launch != null) {
    price = `<div class="price">${fmtRand(launch)}<small> first month</small></div><p class="muted">Then ${fmtRand(plan.price)} per month. Launch price ends ${fmtDate(plan.launchEndsAt)}.</p>`;
  } else price = `<div class="price">${fmtRand(plan.price)}<small> / month</small></div>`;
  return `<div class="card stack">
    <div class="row between"><h3>${esc(plan.name)}</h3>${trial ? `<span class="pill solid">${plan.trialDays}-day free trial</span>` : ""}</div>
    ${price}
    ${trial ? `<p class="muted">Free for ${plan.trialDays} days, then ${priceLine(yearly ? plan.annualPrice : plan.price, billing)}. Cancel before then and you pay nothing.</p>` : ""}
    <p class="muted">${esc(plan.description)}</p>
    <div class="stack-sm">${plan.features.map((f) => `<div class="feat">${ic("tick")}<span>${esc(f)}</span></div>`).join("")}</div>
    ${enabled ? `<button class="btn accent" data-plan="${plan.id}">${esc(label)}</button>` : `<button class="btn secondary" disabled>${esc(label)}</button>`}</div>`;
}

route("/plans", async (el) => {
  const [plans, subs] = await Promise.all([api("GET", "/api/plans"), loadSubs(true)]);
  const current = subs.find((s) => s.status === "ACTIVE" && s.grantsAccess);
  const waiting = subs.find((s) => s.status === "ACTIVE" && !s.grantsAccess && isFuture(s.startDate));   // a change already confirmed, not started yet
  const isNew = !subs.some((s) => HELD_BEFORE.includes(s.status));
  const anyYearly = plans.some((p) => p.annualPrice != null);
  let yearly = false;
  const draw = () => {
    el.innerHTML = `${topbar("Choose your plan", { sub: current ? "Change your plan any time." : "Cancel any time." })}<div class="list">
      ${current ? `<div class="card tint"><h3 style="font-size:19px">You're on the ${esc(current.planName)} plan (${priceLine(current.price, current.billing)}).</h3></div>` : ""}
      ${waiting ? `<div class="card tint"><h3 style="font-size:19px">Your change to ${esc(waiting.planName)} starts on ${fmtDate(waiting.startDate)}.</h3></div>` : ""}
      ${anyYearly ? `<div class="row"><button class="chip ${yearly ? "" : "on"}" data-bill="MONTHLY">Monthly</button><button class="chip ${yearly ? "on" : ""}" data-bill="ANNUAL">Yearly</button></div>` : ""}
      ${plans.map((p) => {
        const billing = yearly && p.annualPrice != null ? "ANNUAL" : "MONTHLY";
        const same = current && current.planId === p.id && current.billing === billing;
        const label = same ? "Your current plan" : current && current.planId === p.id ? (billing === "ANNUAL" ? "Switch to yearly" : "Switch to monthly")
          : current ? "Change to this plan" : isNew && p.trialDays > 0 ? `Start ${p.trialDays}-day free trial` : "Subscribe";
        return planCard(p, billing, isNew, label, !same && !waiting);
      }).join("")}
      <p class="small muted center">Payments are processed securely by PayFast. Rhythm &amp; Flow never sees or stores your card details.</p>
      <a class="linkbtn center" href="#/policies">Cancellation and renewal policies</a></div>`;
  };
  draw();
  el.onclick = async (e) => {
    const bill = e.target.closest("[data-bill]"), b = e.target.closest("[data-plan]");
    if (bill) { yearly = bill.dataset.bill === "ANNUAL"; return draw(); }
    if (!b) return;
    const plan = plans.find((p) => String(p.id) === b.dataset.plan); const billing = yearly && plan.annualPrice != null ? "ANNUAL" : "MONTHLY";
    try {
      if (!current) {
        busy(b, true, "Opening payment…");
        const r = await api("POST", "/api/subscriptions/checkout", { planId: plan.id, billing });
        RF.subs = null; return toPayFast(r.paymentUrl, "sub", r.subscriptionId);
      }
      busy(b, true, "One moment…");
      const p = await api("GET", `/api/subscriptions/${current.id}/change-preview?planId=${plan.id}&billing=${billing}`);
      busy(b, false);
      const diff = p.priceDifference;
      const ok = await dialog({
        title: "Change your plan?", ok: "Confirm change", cancel: "Not now",
        fields: `<p>From: ${esc(p.currentPlan)}, ${priceLine(p.currentPrice, p.currentBilling)}</p><p>To: ${esc(p.newPlan)}, ${priceLine(p.newPrice, p.newBilling)}</p>
          <p>${diff > 0 ? `Price difference: ${fmtRand(diff)} more per month` : diff < 0 ? `Price difference: ${fmtRand(-diff)} less per month` : "Price difference: none per month"}</p>
          <p>Starts on: ${fmtDate(p.effectiveDate)}</p><p class="muted">${esc(p.accessImpact)}</p>
          <p class="small muted">You'll finish setting up the new plan on the PayFast page. Your current plan stops renewing, and you keep it until ${fmtDate(p.effectiveDate)}.</p>`,
      });
      if (!ok) return;
      const r = await api("POST", `/api/subscriptions/${current.id}/change`, { planId: plan.id, billing });
      RF.subs = null; toPayFast(r.paymentUrl, "sub", r.subscriptionId);
    } catch (err) { busy(b, false); toast(err.message); }
  };
});

/* Back from PayFast. A subscription's payment is confirmed to the server by PayFast, so wait for that and then tell the person. */
route("/paid", (el) => {
  const p = pending.get();
  if (p && p.kind === "ticket") return renderTicketPaid(el, p.id);
  let stop = false, tries = 0;
  const show = (inner) => { el.innerHTML = `<div class="centerpage">${inner}</div>`; };
  const waitingView = () => show(`<div class="spinner" aria-hidden="true"></div><h2>Confirming your payment</h2><p class="muted">Finish paying on the PayFast page, then come back here. We'll activate your plan as soon as PayFast confirms it.</p><a class="btn secondary" style="max-width:320px" href="#/plans">Back</a>`);
  waitingView();
  const tick = async () => {
    if (stop) return;
    let s = null;
    try { const subs = await loadSubs(true); s = p ? subs.find((x) => x.id === p.id) : subs.find((x) => x.status === "ACTIVE"); } catch (e) { /* try again */ }
    if (s && s.status === "ACTIVE") {
      pending.clear(); const later = isFuture(s.startDate);
      return show(`<div class="big-check">${ic("tick")}</div><h2>${later ? "Your new plan is set up" : s.inTrial ? "Your free trial has started" : "You're subscribed!"}</h2>
        <p class="muted">${later ? `${esc(s.planName)} starts on ${fmtDate(s.startDate)}. Until then you keep your current plan.`
          : s.inTrial ? `Enjoy ${esc(s.planName)} free until ${fmtDate(s.endDate)}. After that ${priceLine(s.price, s.billing)} is charged unless you cancel.` : `Your ${esc(s.planName)} plan is active. Enjoy your practice.`}</p>
        <a class="btn accent" style="max-width:320px" href="#/home">Start moving</a>`);
    }
    if (s && (s.status === "CANCELLED" || s.status === "FAILED")) { pending.clear(); return show(`<h2>Payment not completed</h2><p class="muted">No payment was taken. You can try again from the plans screen.</p><a class="btn accent" style="max-width:320px" href="#/plans">Back to plans</a>`); }
    if (++tries >= 60) return show(`<h2>Still waiting for confirmation</h2><p class="muted">If you completed the payment, it can take a few minutes. Your subscription will activate automatically.</p><a class="btn accent" style="max-width:320px" href="#/home">Back to home</a>`);
    setTimeout(tick, 3000);
  };
  tick();
  return () => { stop = true; };
});

route("/subscription", async (el) => {
  const all = await loadSubs(true); const subs = all.filter((s) => s.status !== "PENDING" && s.status !== "FAILED");
  const live = subs.find((s) => s.status === "ACTIVE" && s.grantsAccess);
  const cards = subs.map((s) => {
    const later = s.status === "ACTIVE" && isFuture(s.startDate);
    const pill = s.inTrial ? "Free trial" : later ? "Starts soon" : s.status.charAt(0) + s.status.slice(1).toLowerCase().replace("_", " ");
    const line = s.inTrial ? `Free until ${fmtDate(s.endDate)}, then ${priceLine(s.price, s.billing)}.` : later ? `Starts ${fmtDate(s.startDate)}.`
      : s.status === "CANCELLED" && s.endDate ? `Access until ${fmtDate(s.endDate)}` : s.renewing && s.endDate ? `Next payment of ${fmtRand(s.price)} on ${fmtDate(s.endDate)}` : s.endDate ? `Renews ${fmtDate(s.endDate)}` : "";
    return `<div class="card stack-sm"><div class="row between"><h3>${esc(s.planName)}</h3><span class="pill ${s.status === "ACTIVE" ? "" : "grey"}">${esc(pill)}</span></div>
      <p class="muted">${priceLine(s.price, s.billing)}</p>${line ? `<p class="small muted">${line}</p>` : ""}
      ${s.status === "ACTIVE" && !later ? `<button class="btn danger" data-cancel="${s.id}">Cancel subscription</button>` : ""}</div>`;
  }).join("");
  el.innerHTML = `${topbar("My subscription")}<div class="pad stack">
    ${subs.length ? cards : emptyBox("No subscription yet", "Choose a plan to unlock every programme.", "💳") + `<a class="btn accent" href="#/plans">View plans</a>`}
    ${live ? `<a class="btn secondary" href="#/plans">Change plan</a><p class="small muted">Changing plan shows you the price difference and the start day before anything is confirmed. You keep your current plan until the end of the period you've paid for.</p>`
      : subs.length && !subs.some((s) => s.status === "ACTIVE") ? `<a class="btn accent" href="#/plans">Choose a new plan</a>` : ""}
    ${subs.length ? `<a class="btn secondary" href="#/payments">Payment history</a>` : ""}
    <a class="linkbtn center" href="#/policies">Cancellation and renewal policies</a></div>`;
  el.onclick = async (e) => {
    const c = e.target.closest("[data-cancel]"); if (!c) return;
    const s = subs.find((x) => String(x.id) === c.dataset.cancel);
    if (!(await dialog({ title: "Cancel your subscription?", text: `We'll tell PayFast to stop your ${s.billing === "ANNUAL" ? "yearly" : "monthly"} payments. You'll keep access to your plan until ${fmtDate(s.endDate)}.`, ok: "Cancel subscription", cancel: "Keep it", danger: true }))) return;
    busy(c, true, "Cancelling…");
    try { const r = await api("POST", `/api/subscriptions/${s.id}/cancel`); RF.subs = null; await dialog({ title: "Subscription cancelled", text: r.message, ok: "OK", cancel: "" }); navigate(); }
    catch (err) { busy(c, false); toast(err.message); }
  };
});

route("/payments", async (el) => {
  const rows = await api("GET", "/api/subscriptions/payments");
  el.innerHTML = topbar("Payment history", { sub: "Your payments and receipts." }) + (rows.length ? `<div class="list">${rows.map((p) => `<div class="card"><div class="row between"><h3 style="font-size:19px">${esc(p.planName)}</h3><b>${fmtRand(p.amount)}</b></div>
    <div class="row between mt-s"><span class="small muted">${fmtDate(p.date)} · Receipt ${esc(p.receipt)}</span><span class="pill ${p.status === "COMPLETE" ? "" : "tang"}">${p.status === "COMPLETE" ? "Paid" : "Didn't go through"}</span></div></div>`).join("")}</div>` : emptyBox("No payments yet", "When you subscribe, each payment and its receipt number will appear here.", "🧾"));
});

/* The client's own policy wording, read from the server so the app, the web version and the emails all say the same thing. */
route("/policies", async (el) => {
  const rows = await api("GET", "/api/policies");
  el.innerHTML = `${topbar("Policies", { sub: "Cancellation, renewal and events." })}<div class="list">${rows.map((p) => `<details class="card policy"><summary><h3 style="font-size:19px">${esc(p.title)}</h3><span class="read">Read</span></summary><p class="muted prewrap">${esc(p.body)}</p></details>`).join("")}</div>`;
});
