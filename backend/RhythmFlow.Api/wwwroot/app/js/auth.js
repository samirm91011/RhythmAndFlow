/* Welcome, sign in, sign up and forgot password. */
"use strict";

route("/welcome", (el) => {
  el.innerHTML = `<section class="welcome">
    <img class="logo" src="img/logo.png" alt="Rhythm &amp; Flow">
    <p class="eyebrow">RHYTHM &amp; FLOW</p>
    <h1>Move. Breathe. Flow.</h1>
    <p class="muted">Movement, classes and reflection, in your pocket.</p>
    <div class="stack" style="width:100%;max-width:340px;margin-top:12px">
      <a class="btn accent" href="#/signup">Create account</a>
      <a class="btn secondary" href="#/signin">Sign in</a>
    </div>
    ${RF.demo ? `<p class="small muted">Demo data: nothing is saved.</p>` : ""}
  </section>`;
});

route("/signin", (el) => {
  el.innerHTML = `<div class="authwrap">
    <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
    <img class="logo" src="img/logo.png" alt="">
    <h1>Welcome back</h1><p class="lead">Sign in to continue.</p>
    <form id="f" novalidate>
      ${field("id", "Email or username", { autocomplete: "username", inputmode: "email" })}
      ${field("pw", "Password", { type: "password", autocomplete: "current-password", enter: "go" })}
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="go" type="submit">Sign in</button>
    </form>
    <div class="row between mt-s"><a class="linkbtn" href="#/forgot">Forgot password?</a><a class="linkbtn" href="#/signup">Create account</a></div>
  </div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault();
    const id = val("id").trim(), pw = val("pw"); const fe = $("#ferr"); fe.classList.add("hidden");
    setFieldError("id", id ? "" : "Enter your email or username."); setFieldError("pw", pw ? "" : "Enter your password.");
    if (!id || !pw) return;
    const btn = $("#go"); busy(btn, true, "Signing in…");
    try { setSession(await api("POST", "/api/auth/login", { identifier: id, password: pw })); RF.subs = null; go("/home"); }
    catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
  };
});

route("/signup", (el) => {
  el.innerHTML = `<div class="authwrap">
    <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
    <img class="logo" src="img/logo.png" alt="">
    <h1>Create account</h1><p class="lead">Join Rhythm &amp; Flow.</p>
    <form id="f" novalidate>
      ${field("name", "Full name", { autocomplete: "name", cap: "words" })}
      ${field("user", "Username", { autocomplete: "username" })}
      ${field("email", "Email", { type: "email", autocomplete: "email", inputmode: "email" })}
      ${field("pw", "Password (8 or more characters)", { type: "password", autocomplete: "new-password", enter: "go" })}
      <p class="legal">By creating an account you agree to the <a href="${RF.legal.terms}" target="_blank" rel="noopener">Terms of Use</a> and <a href="${RF.legal.privacy}" target="_blank" rel="noopener">Privacy Policy</a>.</p>
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="go" type="submit">Create account</button>
    </form>
    <div class="center"><a class="linkbtn" href="#/signin">I already have an account</a></div>
  </div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault();
    const v = { fullName: val("name").trim(), username: val("user").trim(), email: val("email").trim(), password: val("pw") };
    const fe = $("#ferr"); fe.classList.add("hidden");
    setFieldError("name", v.fullName.length >= 2 ? "" : "Enter your full name.");
    setFieldError("user", v.username.length >= 3 ? "" : "At least 3 characters.");
    setFieldError("email", /^\S+@\S+\.\S+$/.test(v.email) ? "" : "Enter a valid email address.");
    setFieldError("pw", v.password.length >= 8 ? "" : "At least 8 characters.");
    if ($$(".field.err").length) return;
    const btn = $("#go"); busy(btn, true, "Creating…");
    try { setSession(await api("POST", "/api/auth/register", v)); RF.subs = null; go("/home"); }
    catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
  };
});

route("/forgot", (el) => {
  let email = "";
  const stepOne = () => {
    el.innerHTML = `<div class="authwrap">
      <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
      <h1>Forgot password</h1><p class="lead">We'll email you a 6-digit code.</p>
      <form id="f" novalidate>${field("email", "Email", { type: "email", autocomplete: "email", inputmode: "email", enter: "go", value: email })}
      <div class="formerr hidden" id="ferr" role="alert"></div><button class="btn accent" id="go" type="submit">Send code</button></form></div>`;
    $("#f").onsubmit = async (e) => {
      e.preventDefault(); email = val("email").trim(); const fe = $("#ferr"); fe.classList.add("hidden");
      if (!/^\S+@\S+\.\S+$/.test(email)) return setFieldError("email", "Enter a valid email address."); setFieldError("email", "");
      const btn = $("#go"); busy(btn, true, "Sending…");
      try { await api("POST", "/api/auth/forgot-password", { email }); stepTwo(); }
      catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
    };
  };
  const stepTwo = () => {
    el.innerHTML = `<div class="authwrap">
      <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
      <h1>Check your email</h1><p class="lead">If ${esc(email)} has an account, a code is on its way. It lasts 15 minutes.</p>
      <form id="f" novalidate>${field("code", "6-digit code", { inputmode: "numeric", autocomplete: "one-time-code", max: 10 })}
      ${field("pw", "New password (8 or more characters)", { type: "password", autocomplete: "new-password", enter: "go" })}
      <div class="formerr hidden" id="ferr" role="alert"></div><button class="btn accent" id="go" type="submit">Reset password</button></form></div>`;
    $("#f").onsubmit = async (e) => {
      e.preventDefault(); const code = val("code").trim(), pw = val("pw"); const fe = $("#ferr"); fe.classList.add("hidden");
      setFieldError("code", code ? "" : "Enter the code from your email."); setFieldError("pw", pw.length >= 8 ? "" : "At least 8 characters.");
      if ($$(".field.err").length) return;
      const btn = $("#go"); busy(btn, true, "Resetting…");
      try { await api("POST", "/api/auth/reset-password", { email, code, newPassword: pw }); toast("Password changed. Please sign in."); go("/signin"); }
      catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
    };
  };
  stepOne();
});
