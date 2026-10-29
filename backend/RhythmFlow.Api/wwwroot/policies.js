/* Shows the studio's policies on a public page. The wording is read from the server (the same text the app shows), so it is never out of date. */
"use strict";
(async function () {
  const box = document.getElementById("policies"), toc = document.getElementById("toc");
  try {
    const res = await fetch("/api/policies");
    if (!res.ok) throw new Error("status " + res.status);
    const list = await res.json();
    box.textContent = "";
    list.forEach((p) => {
      const sec = document.createElement("section"); sec.id = p.id;
      const h = document.createElement("h2"); h.textContent = p.title; sec.appendChild(h);
      String(p.body || "").split(/\n\s*\n/).forEach((para) => {
        const text = para.trim(); if (!text) return;
        const lines = text.split("\n").map((l) => l.trim()).filter(Boolean);
        if (lines.length && /^\d+\.\s/.test(lines[0])) {
          const sub = document.createElement("h3"); sub.textContent = lines[0]; sec.appendChild(sub);
          lines.slice(1).forEach((l) => { const el = document.createElement("p"); el.textContent = l; sec.appendChild(el); });
        } else lines.forEach((l) => { const el = document.createElement("p"); el.textContent = l; sec.appendChild(el); });
      });
      box.appendChild(sec);
      const a = document.createElement("a"); a.href = "#" + p.id; a.textContent = p.title; toc.appendChild(a);
    });
  } catch (e) {
    box.textContent = "The policies could not be loaded right now. Please try again, or email info@rhythmandflow.co.za.";
  }
})();
