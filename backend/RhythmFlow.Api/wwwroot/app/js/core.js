/* Rhythm & Flow web app: core helpers, server connection, navigation. No third-party code, no build step. */
"use strict";

const RF = {
  token: null, user: null, subs: null, demo: false,
  legal: { terms: "https://rhythmandflow.co.za/terms-of-use/", privacy: "/privacy.html" },
};
try { RF.token = localStorage.getItem("rf_token"); } catch (e) { /* private mode: stay signed out after a reload */ }
RF.demo = new URLSearchParams(location.search).has("demo");

/* ---------- small helpers ---------- */
const $ = (sel, el = document) => el.querySelector(sel);
const $$ = (sel, el = document) => Array.from(el.querySelectorAll(sel));
const esc = (s) => String(s == null ? "" : s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const store = {
  get(k, d) { try { const v = localStorage.getItem(k); return v == null ? d : JSON.parse(v); } catch (e) { return d; } },
  set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* ignore */ } },
};

const ICONS = {
  home: "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z",
  move: "M20.57 14.86L22 13.43 20.57 12 17 15.57 8.43 7 12 3.43 10.57 2 9.14 3.43 7.71 2 5.57 4.14 4.14 2.71 2.71 4.14l1.43 1.43L2 7.71l1.43 1.43L2 10.57 3.43 12 7 8.43 15.57 17 12 20.57 13.43 22l1.43-1.43L16.29 22l2.14-2.14 1.43 1.43 1.43-1.43-1.43-1.43L22 16.29z",
  classes: "M19 4h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V10h14v10zm0-12H5V6h14v2z",
  journal: "M21 5c-1.11-.35-2.33-.5-3.5-.5-1.95 0-4.05.4-5.5 1.5-1.45-1.1-3.55-1.5-5.5-1.5S2.45 4.9 1 6v14.65c0 .25.25.5.5.5.1 0 .15-.05.25-.05C3.1 20.45 5.05 20 6.5 20c1.95 0 4.05.4 5.5 1.5 1.35-.85 3.8-1.5 5.5-1.5 1.65 0 3.35.3 4.75 1.05.1.05.15.05.25.05.25 0 .5-.25.5-.5V6c-.6-.45-1.25-.75-2-1zm0 13.5c-1.1-.35-2.3-.5-3.5-.5-1.7 0-4.15.65-5.5 1.5V8c1.35-.85 3.8-1.5 5.5-1.5 1.2 0 2.4.15 3.5.5v11.5z",
  you: "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z",
  bell: "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z",
  back: "M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z",
  lock: "M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z",
  play: "M8 5v14l11-7z",
  search: "M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z",
  chev: "M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z",
  heart0: "M16.5 3c-1.74 0-3.41.81-4.5 2.09C10.91 3.81 9.24 3 7.5 3 4.42 3 2 5.42 2 8.5c0 3.78 3.4 6.86 8.55 11.54L12 21.35l1.45-1.32C18.6 15.36 22 12.28 22 8.5 22 5.42 19.58 3 16.5 3zm-4.4 15.55l-.1.1-.1-.1C7.14 14.24 4 11.39 4 8.5 4 6.5 5.5 5 7.5 5c1.54 0 3.04.99 3.57 2.36h1.87C13.46 5.99 14.96 5 16.5 5c2 0 3.5 1.5 3.5 3.5 0 2.89-3.14 5.74-7.9 10.05z",
  heart1: "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z",
  check: "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z",
  tick: "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z",
  eye: "M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z",
  card: "M20 4H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z",
  download: "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z",
  trash: "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z",
  settings: "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z",
  info: "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z",
  policy: "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z",
  logout: "M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z",
  alarm: "M22 5.72l-4.6-3.86-1.29 1.53 4.6 3.86L22 5.72zM7.88 3.39L6.6 1.86 2 5.71l1.29 1.53 4.59-3.85zM12.5 8H11v6l4.75 2.85.75-1.23-4-2.37V8zM12 4c-4.97 0-9 4.03-9 9s4.02 9 9 9c4.97 0 9-4.03 9-9s-4.03-9-9-9zm0 16c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z",
  dash: "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z",
  group: "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z",
  place: "M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z",
  edit: "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z",
  help: "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z",
};
const ic = (name, cls = "") => `<svg class="ic ${cls}" viewBox="0 0 24 24" aria-hidden="true"><path d="${ICONS[name] || ""}"/></svg>`;

/* ---------- formatting ---------- */
/* Every date and time is shown in South African time (UTC+2, no daylight saving), as the client asked, wherever the person is. */
const SA = { timeZone: "Africa/Johannesburg" };
const DAY = Object.assign({ weekday: "short", day: "numeric", month: "short" }, SA);
const fmtDay = (iso) => (iso ? new Date(iso).toLocaleDateString("en-GB", DAY).replace(",", "") : "");
const fmtTime = (iso) => (iso ? new Date(iso).toLocaleTimeString("en-GB", Object.assign({ hour: "2-digit", minute: "2-digit", hour12: false }, SA)) : "");
const fmtDayTime = (iso) => `${fmtDay(iso)} · ${fmtTime(iso)}`;
const fmtDate = (iso) => (iso ? new Date(iso).toLocaleDateString("en-GB", Object.assign({ day: "numeric", month: "short", year: "numeric" }, SA)) : "");
/* an admin types dates and times in South African time too: +02:00 all year */
const saIso = (date, time) => { const d = new Date(`${date}T${time}:00+02:00`); return isNaN(d) ? null : d.toISOString(); };
const saParts = (iso) => { if (!iso) return { date: "", time: "" }; const d = new Date(new Date(iso).getTime() + 2 * 3600000).toISOString(); return { date: d.slice(0, 10), time: d.slice(11, 16) }; };
const fmtRand = (n) => (Number(n) % 1 === 0 ? `R${Number(n)}` : `R${Number(n).toFixed(2)}`);
const durLabel = (s) => (s < 90 ? `${s} secs` : `${Math.floor((s + 30) / 60)} mins`);
function greeting() { const h = new Date().getHours(); return h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening"; }
function timeAgo(iso) {
  const m = Math.floor((Date.now() - new Date(iso).getTime()) / 60000);
  if (m < 1) return "Just now"; if (m < 60) return `${m} min ago`; if (m < 1440) return `${Math.floor(m / 60)} h ago`; if (m < 2880) return "Yesterday";
  return fmtDate(iso);
}

/* ---------- categories: the client's approved list, wording and order (same as the Android app) ---------- */
const CATEGORIES = [
  { name: "Yoga", cls: "yoga", mark: "🧘", desc: "Mindful practices for strength, flexibility, balance and body awareness. Sometimes includes breathwork and meditations." },
  { name: "Barre & Pilates", cls: "barre", mark: "🤸", old: "Barre", desc: "Barre is ballet-inspired movement to develop control, alignment, strength and endurance. Pilates classes will be mat Pilates focused on developing strength, control, alignment and mind-body connection." },
  { name: "Dance", cls: "dance", mark: "💃", desc: "Expressive movement for musicality, coordination, confidence, fitness and enjoyment. Includes styles like Ballet, Jazz, Contemporary, Hip Hop, Latin, Heels, etc." },
  { name: "Stretch & Mobility", cls: "stretch", mark: "stretch", old: "Stretch", desc: "Practices that support range of motion, flexibility, ease of movement and recovery." },
  { name: "Strength & Conditioning", cls: "strength", mark: "🏋️", desc: "Accessible movement to build strength, stability and physical capacity. Can include the use of at-home equipment such as weights or resistance bands for increased difficulty." },
  { name: "Meditation", cls: "meditation", mark: "🌙", desc: "Guided practices for attention, calm and self-awareness." },
  { name: "Breathwork", cls: "breathwork", mark: "🌬️", desc: "Breathing practices to support focus, relaxation and an intentional pause." },
];
const catInfo = (c) => CATEGORIES.find((x) => x.name === c || x.old === c) || { name: c || "", cls: "yoga", mark: "✨", desc: "" };
const catClass = (c) => catInfo(c).cls;
/* The Stretch & Mobility icon is the client's own line drawing, traced from her picture (the same shape as in the Android app). */
const STRETCH_PATH = "M4.6 1.02 L4.81 1 L5.15 1.09 L6.48 1.7 L7.59 2.29 L8.68 3.01 L10.35 4.43 L11.05 5.16 L12.13 6.47 L13.36 8.33 L14.31 10.1 L15.13 12.02 L15.65 13.56 L16.19 15.59 L16.79 18.67 L17.23 21.67 L17.23 22.01 L17.1 22.51 L17.2 22.55 L20.02 22.54 L20.14 22.59 L20.21 22.73 L20.11 22.95 L19.89 22.99 L4.32 23 L4.17 22.98 L4.07 22.89 L4.04 22.69 L4.19 22.55 L7.14 22.51 L7.17 22.44 L7.02 22.02 L7.06 21.54 L9.03 15.9 L9.31 14.88 L9.56 13.56 L9.55 13.45 L9.44 13.39 L7.94 13.39 L6.62 13.21 L6.02 12.92 L5.73 12.55 L5.59 12.02 L5.63 11.44 L5.92 10.36 L6.28 9.48 L6.81 8.65 L8.41 6.75 L8.61 6.41 L8.62 6.29 L8.44 5.8 L8.14 5.34 L7.27 4.43 L6.09 3.63 L4.26 2.78 L3.97 2.53 L3.79 2.01 L3.89 1.54 L4.17 1.19 Z M5.73 4.63 L6.31 4.63 L6.64 4.71 L6.99 4.89 L7.25 5.08 L7.54 5.41 L7.8 6.02 L7.82 6.68 L7.62 7.29 L7.17 7.81 L6.88 8 L6.4 8.18 L5.65 8.17 L4.98 7.89 L4.5 7.38 L4.23 6.73 L4.24 6.07 L4.51 5.42 L5.05 4.89 L5.4 4.71 Z M12.32 14.32 L12.56 14.3 L12.82 14.54 L13.71 16.78 L14.42 19.25 L15.01 21.93 L15.18 22.47 L15.08 22.55 L9.1 22.54 L9.08 22.43 L11.06 17.34 L12.06 14.62 Z M7.83 10.02 L8.07 9.98 L8.73 10.66 L9.53 11.77 L9.5 12.07 L7.77 12 L7.19 11.81 L7.38 10.97 Z";
const catMark = (c) => { const i = catInfo(c); return i.mark === "stretch" ? `<svg class="catsvg" viewBox="0 0 24 24" aria-hidden="true"><path fill-rule="evenodd" d="${STRETCH_PATH}"/></svg>` : i.mark; };

/* ---------- talking to the server ---------- */
class ApiError extends Error { constructor(message, status, data) { super(message); this.status = status; this.data = data || null; this.code = data && data.code ? data.code : null; } }
function friendly(status, body) {
  if (body && typeof body === "object") {
    if (body.error) return body.error;
    const first = body.errors && Object.values(body.errors)[0];
    if (Array.isArray(first) && first[0]) return first[0];
  }
  return { 401: "Please sign in again.", 403: "You don't have access to that.", 404: "We couldn't find that.", 429: "Too many attempts. Please wait a moment." }[status] || `Something went wrong (error ${status}).`;
}
async function api(method, path, body) {
  if (RF.demo) return DEMO.handle(method, path, body);
  let res;
  try {
    res = await fetch(path, {
      method,
      headers: Object.assign({}, body !== undefined ? { "Content-Type": "application/json" } : {}, RF.token ? { Authorization: "Bearer " + RF.token } : {}),
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (e) {
    throw new ApiError("Can't reach the Rhythm & Flow server. Check your internet connection and try again.", 0);
  }
  if (res.ok) {
    if (res.status === 204) return null;
    const ct = res.headers.get("content-type") || "";
    return ct.includes("json") ? res.json() : res.text();
  }
  let parsed = null; try { parsed = await res.json(); } catch (e) { /* not JSON */ }
  if (res.status === 401 && RF.token && path !== "/api/auth/login") { signOutLocal(); }
  throw new ApiError(friendly(res.status, parsed), res.status, parsed);
}
function signOutLocal() {
  RF.token = null; RF.user = null; RF.subs = null; RF.content = null;
  try { localStorage.removeItem("rf_token"); sessionStorage.removeItem("rf_customer_view"); } catch (e) { /* ignore */ }
  if (!isPublic(currentPath())) go("/welcome");
}
/* An administrator lands on the admin side and can flip to the customer view and back. Customers never get either. */
const isAdmin = () => !!(RF.user && RF.user.role === "ADMIN");
const adminView = () => { let c = false; try { c = sessionStorage.getItem("rf_customer_view") === "1"; } catch (e) { /* ignore */ } return isAdmin() && !c; };
function setCustomerView(on) { try { if (on) sessionStorage.setItem("rf_customer_view", "1"); else sessionStorage.removeItem("rf_customer_view"); } catch (e) { /* ignore */ } }
const homePath = () => (adminView() ? "/admin" : "/home");
/* What the person was paying for when they left for PayFast, so the page they come back to can wait for the confirmation. */
const pending = {
  set(kind, id) { try { localStorage.setItem("rf_pending", JSON.stringify({ kind, id, at: Date.now() })); } catch (e) { /* ignore */ } },
  get() { try { const p = JSON.parse(localStorage.getItem("rf_pending")); return p && Date.now() - p.at < 6 * 3600 * 1000 ? p : null; } catch (e) { return null; } },
  clear() { try { localStorage.removeItem("rf_pending"); } catch (e) { /* ignore */ } },
};
/* Sends the browser to PayFast's own payment page. Cards are never typed into this app. */
function toPayFast(url, kind, id) { pending.set(kind, id); if (String(url).startsWith("#")) location.hash = url; else location.href = url; }
function setSession(r) {
  RF.token = r.token; RF.user = r.user;
  try { localStorage.setItem("rf_token", r.token); } catch (e) { /* ignore */ }
}
async function loadSubs(force) {
  if (!RF.subs || force) { try { RF.subs = await api("GET", "/api/subscriptions"); } catch (e) { RF.subs = RF.subs || []; } }
  return RF.subs;
}
/* ---------- the wording the client edits herself (moods, affirmations, quotes, journal prompts) ----------
   The server holds her version. These are the built-in words, used until she saves a change or if the server cannot be reached. */
const ARTICLE_TEXT = "Movement is not just about physical exercise; it is a way to freely express ourselves, connect with others, and find inner balance. Whether you are a beginner or an experienced mover, there is a place for you in this community.\n\nBalance isn't about doing it all. It's about creating space for what matters most. Here are a few simple ways to bring more intention into your everyday life:\n\n•  Start the day with a few slow breaths.\n•  Move in a way that feels kind to your body.\n•  End the day by noting one thing you're grateful for.";
const DEFAULT_CONTENT = {
  moods: [
    { label: "Energised", emoji: "☀️", color: "#FFF1DC" }, { label: "Tired", emoji: "🔋", color: "#E3ECFB" }, { label: "Stressed", emoji: "☁️", color: "#F3E6EE" },
    { label: "Grounded", emoji: "🌿", color: "#DDEFEA" }, { label: "Overwhelmed", emoji: "🌀", color: "#FDE9E1" }, { label: "Happy", emoji: "😊", color: "#FFEFD9" },
    { label: "Low", emoji: "🌙", color: "#E5E8F7" }, { label: "Need some space", emoji: "🤍", color: "#FBE4E3" },
  ],
  affirmations: [
    "I am enough, exactly as I am, and I am always growing.", "My body is strong, and I move with kindness.", "I give myself permission to rest and to rise.",
    "Every small step brings me back to myself.", "I am free to express who I am through movement.", "I welcome joy into my body today.", "I am proud of how far I've come.",
  ],
  homeQuote: "A small check-in can lead to a big shift.",
  rhythmQuote: "Progress isn't about doing more, it's about coming back to yourself.",
  rhythmCards: [{ title: "Morning Reset", subtitle: "10 mins" }, { title: "Nourish Your Body", subtitle: "Recipes" }, { title: "Move & Release", subtitle: "15 mins" }],
  journal: { reflectionTitle: "Today's Reflection", reflectionHint: "What's on your mind?", gratitudeTitle: "Gratitude", gratitudeHint: "What are you grateful for today?" },
  home: { classTitle: "Join a class", classBody: "Move with Deni and the community. Browse upcoming classes and reserve your spot.", bannerTitle: "Build a life that feels good.", bannerBody: "Unlock every programme with a monthly subscription." },
  explore: [
    { title: "Feed Your Soul", blurb: "Mindfulness, meditation, emotional wellbeing.", body: ARTICLE_TEXT }, { title: "Nourish Your Body", blurb: "Nutrition, food, self-care.", body: ARTICLE_TEXT },
    { title: "Open Your Mind", blurb: "Personal development, mindset, coaching.", body: ARTICLE_TEXT }, { title: "Discover New Journeys", blurb: "Travel, experiences, trying something new.", body: ARTICLE_TEXT },
    { title: "Coaching & Programmes", blurb: "Guided support for your next chapter.", body: ARTICLE_TEXT }, { title: "Shop", blurb: "Wellness products for everyday living.", body: "" },
  ],
  shop: { intro: "The Rhythm & Flow shop is coming soon to the app. For now, visit rhythmandflow.co.za to order.", products: [{ name: "Yoga Mat", price: "R650" }, { name: "Stainless Steel Bottle", price: "R450" }, { name: "Resistance Bands", price: "R320" }, { name: "Wellness Journal", price: "R250" }] },
};
const content = () => RF.content || DEFAULT_CONTENT;
async function loadContent() {
  try { const c = await api("GET", "/api/content"); RF.content = Object.assign({}, DEFAULT_CONTENT, c); RF.contentAt = Date.now(); }
  catch (e) { if (!RF.content) RF.content = DEFAULT_CONTENT; }
}
const hasAccess = (subs) => (subs || []).some((s) => s.grantsAccess);

/* ---------- screen pieces ---------- */
function topbar(title, o = {}) {
  return `<header class="top">${o.back === false ? "" : `<button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>`}
    <div class="grow"><h2>${esc(title)}</h2>${o.sub ? `<p class="sub">${esc(o.sub)}</p>` : ""}</div>${o.right || ""}</header>`;
}
const skeleton = (n = 4) => `<div class="list" aria-busy="true" aria-label="Loading">${Array.from({ length: n }, () => `<div class="sk" style="height:96px"></div>`).join("")}</div>`;
const emptyBox = (title, text, emoji = "🌿") => `<div class="empty"><div class="badge-ic" aria-hidden="true">${emoji}</div><h3>${esc(title)}</h3><p class="muted">${esc(text)}</p></div>`;
const errorBox = (msg) => `<div class="err-box"><p>${esc(msg)}</p><button class="linkbtn" data-retry>Try again</button></div>`;
function field(id, label, o = {}) {
  const type = o.type || "text";
  const input = o.area
    ? `<textarea id="${id}" ${o.max ? `maxlength="${o.max}"` : ""} ${o.placeholder ? `placeholder="${esc(o.placeholder)}"` : ""}>${esc(o.value || "")}</textarea>`
    : `<input id="${id}" type="${type}" value="${esc(o.value || "")}" ${o.autocomplete ? `autocomplete="${o.autocomplete}"` : ""} ${o.inputmode ? `inputmode="${o.inputmode}"` : ""} ${o.max ? `maxlength="${o.max}"` : ""} autocapitalize="${o.cap || "off"}" autocorrect="off" spellcheck="false" enterkeyhint="${o.enter || "next"}">`;
  return `<div class="field" id="f-${id}"><label for="${id}">${esc(label)}</label>${input}${type === "password" ? `<button type="button" class="eye" data-eye="${id}" aria-label="Show password">${ic("eye")}</button>` : ""}<div class="msg" hidden></div></div>`;
}
function setFieldError(id, msg) {
  const f = $("#f-" + id); if (!f) return;
  f.classList.toggle("err", !!msg); const m = $(".msg", f); m.hidden = !msg; m.textContent = msg || "";
}
const val = (id) => { const e = $("#" + id); return e ? e.value : ""; };
function busy(btn, on, label) {
  if (!btn) return;
  if (on) { btn.dataset.label = btn.textContent; btn.disabled = true; btn.textContent = label || "Please wait…"; }
  else { btn.disabled = false; btn.textContent = btn.dataset.label || btn.textContent; }
}

/* ---------- toast and dialogs ---------- */
let toastTimer = null;
function toast(msg) {
  const t = $("#toast"); t.textContent = msg; t.hidden = false;
  clearTimeout(toastTimer); toastTimer = setTimeout(() => { t.hidden = true; }, 4200);
}
/** Opens a dialog. Resolves with true/false, or with the field values when fields are given. */
function dialog({ title, text = "", fields = "", ok = "OK", cancel = "Cancel", danger = false, check }) {
  return new Promise((resolve) => {
    const ov = $("#overlay");
    ov.innerHTML = `<div class="dialog" role="dialog" aria-modal="true" aria-label="${esc(title)}"><h3>${esc(title)}</h3>${text ? `<p class="muted">${esc(text)}</p>` : ""}<div class="mt-s stack-sm">${fields}</div><div class="formerr hidden mt-s" id="dlgerr"></div>
      <div class="acts">${cancel ? `<button class="linkbtn" data-x="0">${esc(cancel)}</button>` : ""}<button class="linkbtn ${danger ? "danger" : ""}" data-x="1">${esc(ok)}</button></div></div>`;
    ov.hidden = false;
    const first = $("input,textarea,button[data-x='1']", ov); if (first) first.focus();
    const close = (v) => { ov.hidden = true; ov.innerHTML = ""; ov.onclick = null; resolve(v); };
    ov.onclick = (e) => {
      if (e.target === ov) return close(false);
      const pick = e.target.closest("[data-pick]");          // a choice button: only one in its group stays selected
      if (pick) { $$(`[data-group="${pick.dataset.group}"]`, ov).forEach((b) => b.classList.toggle("on", b === pick)); return; }
      const x = e.target.closest("[data-x]"); if (!x) return;
      if (x.dataset.x === "0") return close(false);
      const values = {}; $$("input,textarea", ov).forEach((i) => { values[i.id] = i.type === "checkbox" ? i.checked : i.value; });
      $$("[data-pick].on", ov).forEach((b) => { values[b.dataset.group] = b.dataset.pick; });
      if (check) { const problem = check(values); if (problem) { const d = $("#dlgerr", ov); d.textContent = problem; d.classList.remove("hidden"); return; } }
      close(fields ? values : true);
    };
  });
}

/* ---------- navigation ---------- */
const routes = [];
let cleanup = null;
function route(pattern, handler, opts = {}) {
  const keys = []; const re = new RegExp("^" + pattern.replace(/:(\w+)/g, (_, k) => { keys.push(k); return "([^/]+)"; }) + "$");
  routes.push({ re, keys, handler, opts });
}
const currentPath = () => (location.hash.replace(/^#/, "") || "/").split("?")[0];
function go(path) { if (currentPath() === path) navigate(); else location.hash = "#" + path; }
const PUBLIC = ["/welcome", "/signin", "/signup", "/forgot"];
/* /verify/<email> is also open to people who are signed out: it is where they confirm their email address */
const isPublic = (path) => PUBLIC.includes(path) || path.startsWith("/verify/");
const TABS = [["home", "Home", "/home"], ["move", "Move", "/move"], ["classes", "Classes", "/classes"], ["journal", "Journal", "/journal"], ["you", "You", "/you"]];
/* what an administrator sees along the bottom instead of the customer tabs */
const ADMIN_TABS = [["a-home", "Dashboard", "/admin", "dash"], ["a-classes", "Classes", "/admin/classes", "classes"], ["a-users", "Customers", "/admin/users", "group"], ["a-plans", "Plans", "/admin/plans", "card"], ["a-account", "Account", "/settings", "you"]];

function drawTabs(active) {
  const nav = $("#tabs");
  if (!active) { nav.hidden = true; return; }
  nav.hidden = false;
  const set = String(active).startsWith("a-") ? ADMIN_TABS : TABS;
  nav.innerHTML = set.map(([k, label, href, icon]) => `<a href="#${href}" class="${k === active ? "on" : ""}" ${k === active ? 'aria-current="page"' : ""}><span class="pillbg">${ic(icon || k)}</span>${label}</a>`).join("");
}
async function navigate() {
  if (typeof cleanup === "function") { try { cleanup(); } catch (e) { /* ignore */ } cleanup = null; }
  $("#overlay").hidden = true;
  const path = currentPath();
  if (!RF.token && !isPublic(path)) { return go("/welcome"); }
  if (RF.token && isPublic(path)) { return go(homePath()); }
  let hit = null, params = {};
  for (const r of routes) { const m = r.re.exec(path); if (m) { hit = r; r.keys.forEach((k, i) => { params[k] = decodeURIComponent(m[i + 1]); }); break; } }
  if (!hit) return go(RF.token ? homePath() : "/welcome");
  if (hit.opts.admin && !isAdmin()) return go("/home");
  if (RF.token && (!RF.content || (path === "/home" && Date.now() - (RF.contentAt || 0) > 120000))) await loadContent();   // her latest wording   // the server also refuses; this just keeps customers off the admin screens
  const el = $("#view");
  el.onclick = null; el.oninput = null;   // each screen sets its own handlers; never keep the previous screen's
  drawTabs(typeof hit.opts.tab === "function" ? hit.opts.tab() : hit.opts.tab || null);
  const draw = async () => {
    el.innerHTML = skeleton(hit.opts.skeleton || 3);
    try { const c = await hit.handler(el, params); if (typeof c === "function") cleanup = c; }
    catch (e) { el.innerHTML = topbar("Oops", { back: true }) + errorBox(e.message || "Something went wrong."); }
    el.scrollTo ? el.scrollTo(0, 0) : null; window.scrollTo(0, 0);
  };
  await draw();
  el.focus({ preventScroll: true });
  el._redraw = draw;
}

/* one click handler for the generic buttons */
document.addEventListener("click", (e) => {
  const back = e.target.closest("[data-back]");
  if (back) { e.preventDefault(); if (history.length > 1) history.back(); else go(RF.token ? homePath() : "/welcome"); return; }
  const retry = e.target.closest("[data-retry]");
  if (retry) { const v = $("#view"); if (v._redraw) v._redraw(); return; }
  const eye = e.target.closest("[data-eye]");
  if (eye) { const i = $("#" + eye.dataset.eye); const show = i.type === "password"; i.type = show ? "text" : "password"; eye.setAttribute("aria-label", show ? "Hide password" : "Show password"); }
});
window.addEventListener("hashchange", navigate);
