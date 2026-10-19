/* Start-up: restore the session if there is one, then show the first screen. */
"use strict";

(async function boot() {
  if (RF.token && !RF.user) {
    try { RF.user = await api("GET", "/api/auth/me"); } catch (e) { /* a 401 clears the token inside api() */ }
  }
  if (!location.hash || location.hash === "#") location.hash = RF.token ? "#/home" : "#/welcome";
  else navigate();
  if (RF.demo) {
    const b = document.createElement("div"); b.textContent = "Demo data"; b.setAttribute("aria-hidden", "true");
    b.style.cssText = "position:fixed;right:8px;bottom:90px;background:rgba(0,0,0,.55);color:#fff;font:12px system-ui;padding:3px 10px;border-radius:20px;z-index:40;pointer-events:none";
    document.body.appendChild(b);
  }
})();
