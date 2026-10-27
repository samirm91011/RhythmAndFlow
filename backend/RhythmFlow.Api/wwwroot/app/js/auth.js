/* Welcome, sign in, sign up, email confirmation and forgot password. */
"use strict";

route("/welcome", (el) => {
  el.innerHTML = `<section class="welcome">
    <img class="logo" src="img/logo.png" alt="Rhythm &amp; Flow" style="filter:brightness(0) invert(1)">
    <h1>A kinder,<br>stronger,<br>happier you</h1>
    <p class="eyebrow tagline">MOVE &nbsp;•&nbsp; NOURISH &nbsp;•&nbsp; REFLECT<br>RECONNECT &nbsp;•&nbsp; GROW</p>
    <div class="stack" style="width:100%;max-width:420px;margin-top:24px">
      <a class="btn white" href="#/signin">Get Started</a>
      <p class="small" style="color:rgba(255,255,255,.9)">Your personal Rhythm &amp; Flow space</p>
    </div>
    ${RF.demo ? `<p class="small" style="color:rgba(255,255,255,.8)">Demo data: nothing is saved.</p>` : ""}
  </section>`;
});
route("/signin", (el) => {
  el.innerHTML = `<div class="authwrap">
    <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
    <img class="logo" src="img/logo.png" alt="">
    <h1>Welcome Back!</h1><p class="lead">Log in to continue your wellness journey.</p>
    <form id="f" novalidate>
      ${field("id", "Username or email", { autocomplete: "username", inputmode: "email" })}
      ${field("pw", "Password", { type: "password", autocomplete: "current-password", enter: "go" })}
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="go" type="submit">Log In</button>
    </form>
    <p class="center mt-s"><a class="linkbtn" href="#/forgot">Forgot Password?</a></p>
    <p class="center">Don't have an account? <a class="linkbtn" href="#/signup">Sign up</a></p>
  </div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault();
    const id = val("id").trim(), pw = val("pw"); const fe = $("#ferr"); fe.classList.add("hidden");
    if (!id || !pw) { fe.textContent = "Enter your username or email and your password."; fe.classList.remove("hidden"); return; }
    const btn = $("#go"); busy(btn, true, "Signing in…");
    try { setSession(await api("POST", "/api/auth/login", { identifier: id, password: pw })); RF.subs = null; go(homePath()); }
    catch (err) {
      busy(btn, false);
      // The password was right but the email was never confirmed: the server has just sent a new code, so ask for it.
      if (err.code === "EMAIL_NOT_VERIFIED") { toast("Please confirm your email first. We've sent you a code."); return go("/verify/" + encodeURIComponent(err.data.email || id)); }
      fe.textContent = err.message; fe.classList.remove("hidden");
    }
  };
});

route("/signup", (el) => {
  el.innerHTML = `<div class="authwrap">
    <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
    <img class="logo" src="img/logo.png" alt="">
    <h1>Create Your Account</h1><p class="lead">Join Rhythm &amp; Flow today.</p>
    <form id="f" novalidate>
      ${field("name", "Full name", { autocomplete: "name", cap: "words" })}
      ${field("user", "Username", { autocomplete: "username" })}
      ${field("email", "Email", { type: "email", autocomplete: "email", inputmode: "email" })}
      ${field("pw", "Password", { type: "password", autocomplete: "new-password", enter: "go" })}
      <p class="legal">By creating an account you agree to the <a href="${RF.legal.terms}" target="_blank" rel="noopener">Terms of Use</a> and <a href="${RF.legal.privacy}" target="_blank" rel="noopener">Privacy Policy</a>.</p>
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="go" type="submit">Sign Up</button>
    </form>
    <p class="center mt-s">Already have an account? <a class="linkbtn" href="#/signin">Log in</a></p>
  </div>`;
  $("#f").onsubmit = async (e) => {
    e.preventDefault();
    const v = { fullName: val("name").trim(), username: val("user").trim(), email: val("email").trim(), password: val("pw") };
    const fe = $("#ferr"); fe.classList.add("hidden");
    setFieldError("name", v.fullName.length >= 2 ? "" : "Enter your full name.");
    setFieldError("user", v.username.length < 3 ? "At least 3 characters" : /\s/.test(v.username) ? "No spaces please" : "");
    setFieldError("email", /^\S+@\S+\.\S+$/.test(v.email) ? "" : "Enter a valid email address.");
    setFieldError("pw", v.password.length >= 8 ? "" : "At least 8 characters");
    if ($$(".field.err").length) return;
    const btn = $("#go"); busy(btn, true, "Creating…");
    try {
      // Signing up never signs anyone in. We email a code; once it is confirmed the person signs in themselves.
      const r = await api("POST", "/api/auth/register", v);
      if (r && r.verificationRequired === false) { toast("Your account is ready. Please sign in."); go("/signin"); }
      else go("/verify/" + encodeURIComponent(r && r.email ? r.email : v.email));
    } catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
  };
});

/* The step after signing up: type the 6-digit code that was emailed, then sign in. Also reached by signing in with an unconfirmed email. */
route("/verify/:email", (el, { email }) => {
  let wait = 30, timer = null;
  const confirmed = () => {
    clearInterval(timer);
    el.innerHTML = `<div class="authwrap center">
      <img class="logo" src="img/logo.png" alt="">
      <h1>Email confirmed</h1><p class="lead">Your account is ready.</p>
      <div class="big-check" aria-hidden="true">${ic("tick")}</div>
      <p class="mt">Thanks for confirming ${esc(email)}. Sign in to start.</p>
      <a class="btn accent mt" href="#/signin">Sign in</a></div>`;
  };
  el.innerHTML = `<div class="authwrap">
    <a class="iconbtn" href="#/signin" aria-label="Back">${ic("back")}</a>
    <img class="logo" src="img/logo.png" alt="">
    <h1>Check your email</h1><p class="lead">We sent a 6-digit code to ${esc(email)}.</p>
    <form id="f" novalidate>
      ${field("code", "6-digit code", { inputmode: "numeric", autocomplete: "one-time-code", max: 6, enter: "go" })}
      <p class="legal">The code expires in 15 minutes. Check your spam folder if you can't see it.</p>
      <div class="formerr hidden" id="ferr" role="alert"></div>
      <button class="btn accent" id="go" type="submit">Confirm email</button>
    </form>
    <div class="center mt-s"><button class="linkbtn" id="resend" type="button" disabled></button></div>
  </div>`;
  const resend = $("#resend");
  const paint = () => { resend.disabled = wait > 0; resend.textContent = wait > 0 ? `Send a new code in ${wait}s` : "Send me a new code"; };
  paint();
  timer = setInterval(() => { if (wait > 0) { wait--; paint(); } }, 1000);
  $("#code").addEventListener("input", (e) => { e.target.value = e.target.value.replace(/\D/g, "").slice(0, 6); $("#ferr").classList.add("hidden"); });
  $("#f").onsubmit = async (e) => {
    e.preventDefault(); const code = val("code").trim(); const fe = $("#ferr"); fe.classList.add("hidden");
    if (code.length !== 6) { fe.textContent = "Enter the 6-digit code from the email."; fe.classList.remove("hidden"); return; }
    const btn = $("#go"); busy(btn, true, "Checking…");
    try { await api("POST", "/api/auth/verify-email", { email, code }); confirmed(); }
    catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
  };
  resend.onclick = async () => {
    wait = 30; paint();
    try { await api("POST", "/api/auth/resend-verification", { email }); toast("We've sent a new code."); } catch (err) { toast(err.message); }
  };
  return () => clearInterval(timer);
});

route("/forgot", (el) => {
  let email = "";
  const stepOne = () => {
    el.innerHTML = `<div class="authwrap">
      <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
      <h1>Forgot your password?</h1><p class="lead">Enter your email and we'll send you a 6-digit code.</p>
      <form id="f" novalidate>${field("email", "Email", { type: "email", autocomplete: "email", inputmode: "email", enter: "go", value: email })}
      <div class="formerr hidden" id="ferr" role="alert"></div><button class="btn accent" id="go" type="submit">Send code</button></form></div>`;
    $("#f").onsubmit = async (e) => {
      e.preventDefault(); email = val("email").trim(); const fe = $("#ferr"); fe.classList.add("hidden");
      if (!/^\S+@\S+\.\S+$/.test(email)) { fe.textContent = "Enter the email address you signed up with."; fe.classList.remove("hidden"); return; }
      const btn = $("#go"); busy(btn, true, "Sending…");
      try { await api("POST", "/api/auth/forgot-password", { email }); stepTwo(); }
      catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
    };
  };
  const stepTwo = () => {
    el.innerHTML = `<div class="authwrap">
      <button class="iconbtn" data-back aria-label="Back">${ic("back")}</button>
      <h1>Check your email</h1><p class="lead">We sent a code to ${esc(email)}. It expires in 15 minutes.</p>
      <form id="f" novalidate>${field("code", "6-digit code", { inputmode: "numeric", autocomplete: "one-time-code", max: 10 })}
      ${field("pw", "New password", { type: "password", autocomplete: "new-password" })}${field("pw2", "Confirm new password", { type: "password", autocomplete: "new-password", enter: "go" })}
      <div class="formerr hidden" id="ferr" role="alert"></div><button class="btn accent" id="go" type="submit">Change password</button></form></div>`;
    $("#f").onsubmit = async (e) => {
      e.preventDefault(); const code = val("code").trim(), pw = val("pw"); const fe = $("#ferr"); fe.classList.add("hidden");
      setFieldError("code", code.length === 6 ? "" : "Enter the 6-digit code from the email"); setFieldError("pw", pw.length >= 8 ? "" : "At least 8 characters"); setFieldError("pw2", val("pw2") === pw ? "" : "The passwords don't match");
      if ($$(".field.err").length) return;
      const btn = $("#go"); busy(btn, true, "Changing…");
      try { await api("POST", "/api/auth/reset-password", { email, code, newPassword: pw }); toast("Your password has been changed. You can log in now."); go("/signin"); }
      catch (err) { fe.textContent = err.message; fe.classList.remove("hidden"); busy(btn, false); }
    };
  };
  stepOne();
});
