"use strict";
const labels = document.body.dataset;
document.querySelectorAll(".app-nav a").forEach(link => { if (link.pathname === window.location.pathname && !link.hash) { link.setAttribute("aria-current", "page"); } });
// Mobile navigation and session controls share one header; show one panel at a time.
(() => {
  const account = document.getElementById("account-menu");
  const menu = document.getElementById("main-nav");
  const closeNavigation = () => {
    if (window.matchMedia("(max-width: 1199.98px)").matches && menu?.classList.contains("show")) { bootstrap.Collapse.getOrCreateInstance(menu, { toggle: false }).hide(); }
  };
  menu?.addEventListener("shown.bs.collapse", () => { if (account?.getAttribute("aria-expanded") === "true") { closeNavigation(); } });
  account?.addEventListener("click", closeNavigation);
  account?.addEventListener("keydown", event => { if (event.key === "ArrowDown" || event.key === "ArrowUp") { closeNavigation(); } });
  document.querySelector('[data-bs-target="#main-nav"]')?.addEventListener("click", () => { if (account) { bootstrap.Dropdown.getInstance(account)?.hide(); } });
})();
if (window.jQuery && jQuery.fn.select2) {
  document.querySelectorAll("select[data-search-url]").forEach(element => {
    jQuery(element).select2({ width: "100%", language: "es", minimumInputLength: 2, ajax: { url: element.dataset.searchUrl, dataType: "json", delay: 300, data: params => ({ term: params.term }), processResults: data => data } });
  });
  document.querySelectorAll('select:not([data-search-url]):not([data-select2="off"])').forEach(element => {
    jQuery(element).select2({ width: "100%", language: "es", minimumResultsForSearch: 8 });
  });
  if (jQuery.validator) { jQuery.validator.setDefaults({ ignore: ":hidden:not(.select2-hidden-accessible)" }); }
}
if (window.DataTable) {
  document.querySelectorAll("table[data-local-table]").forEach(table => {
    const headings = [...table.tHead.rows[0].cells].map(cell => cell.textContent.trim());
    const footer = table.createTFoot().insertRow();
    headings.forEach((heading, index) => {
      const cell = document.createElement("th");
      const input = document.createElement("input");
      input.type = "search"; input.className = "form-control form-control-sm";
      input.setAttribute("aria-label", `${labels.filterColumn} ${heading}`);
      input.placeholder = heading; cell.append(input); footer.append(cell);
    });
    const data = new DataTable(table, { paging: false, searching: true, info: false, order: [], language: { search: labels.searchPage, zeroRecords: labels.noMatches, emptyTable: labels.noRecords } });
    [...footer.cells].forEach((cell, index) => cell.firstChild.addEventListener("input", event => data.column(index).search(event.target.value).draw()));
    const tools = document.createElement("div"); tools.className = "table-tools";
    const details = document.createElement("details");
    const summary = document.createElement("summary"); summary.textContent = labels.columns; details.append(summary);
    headings.forEach((heading, index) => {
      const label = document.createElement("label"); const checkbox = document.createElement("input");
      checkbox.type = "checkbox"; checkbox.checked = true;
      checkbox.addEventListener("change", () => data.column(index).visible(checkbox.checked));
      label.append(checkbox, document.createTextNode(` ${heading}`)); details.append(label);
    });
    const reset = document.createElement("button"); reset.type = "button"; reset.className = "btn btn-outline-secondary"; reset.textContent = labels.clearFilters;
    reset.addEventListener("click", () => { data.search(""); data.columns().search(""); footer.querySelectorAll("input").forEach(input => { input.value = ""; }); data.draw(); });
    tools.append(details, reset); table.closest(".table-responsive").before(tools);
  });
}
document.querySelectorAll("form[data-confirm]").forEach(form => form.addEventListener("submit", async event => {
  if (form.dataset.confirmed === "true") { return; }
  event.preventDefault();
  if (!form.reportValidity()) { return; }
  const submitter = event.submitter;
  const result = window.Swal ? await Swal.fire({ title: form.dataset.confirm, text: form.dataset.confirmText, icon: "question", showCancelButton: true, confirmButtonText: labels.confirm, cancelButtonText: labels.cancel }) : { isConfirmed: window.confirm(form.dataset.confirm) };
  if (result.isConfirmed) { form.dataset.confirmed = "true"; form.requestSubmit(submitter); }
}));


// Shared notices: server-rendered PRG feedback and browser actions use the same stack.
window.App = window.App || {};
(() => {
  const stack = document.getElementById("toast-stack");
  if (!stack) { return; }
  const styles = { success: ["circle-check", 5000], info: ["info", 7000], warning: ["alert", 9000], error: ["circle-x", 0] };
  let positionPending = false;
  const position = () => {
    positionPending = false;
    const header = document.querySelector(".app-header");
    let bottom = Math.max(0, header?.getBoundingClientRect().bottom || 0);
    document.querySelectorAll(".app-header .dropdown-menu.show, .app-header #main-nav.show, .app-header #main-nav.collapsing").forEach(menu => { bottom = Math.max(bottom, menu.getBoundingClientRect().bottom); });
    stack.style.setProperty("--toast-top", `${Math.ceil(bottom + 12)}px`);
  };
  const queuePosition = () => { if (!positionPending) { positionPending = true; requestAnimationFrame(position); } };
  window.addEventListener("resize", queuePosition);
  window.addEventListener("scroll", queuePosition, { passive: true });
  ["shown.bs.dropdown", "hidden.bs.dropdown", "shown.bs.collapse", "hidden.bs.collapse"].forEach(name => document.addEventListener(name, queuePosition));
  if (window.ResizeObserver) { const observer = new ResizeObserver(queuePosition); document.querySelectorAll(".app-header, #main-nav").forEach(element => observer.observe(element)); }
  position();
  function wire(toast) {
    let timer;
    let started = 0;
    let remaining = Number(toast.dataset.timeout) || 0;
    let hovered = toast.matches(":hover");
    let focused = toast.contains(document.activeElement);
    let closing = false;
    const pause = () => { if (timer) { clearTimeout(timer); timer = undefined; remaining = Math.max(0, remaining - (performance.now() - started)); } };
    const close = () => {
      if (closing) { return; } closing = true; pause();
      if (toast.contains(document.activeElement)) { const next = [...stack.children].find(item => item !== toast && !item.classList.contains("hiding")); next?.querySelector(".app-toast-close")?.focus(); }
      toast.classList.add("hiding");
      setTimeout(() => toast.remove(), window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 220);
    };
    const resume = () => { if (!closing && !hovered && !focused && !document.hidden && Number(toast.dataset.timeout) > 0 && !timer) { started = performance.now(); timer = setTimeout(close, remaining); } };
    const visibility = () => { if (document.hidden) { pause(); } else { resume(); } };
    toast.addEventListener("mouseenter", () => { hovered = true; pause(); });
    toast.addEventListener("mouseleave", () => { hovered = false; resume(); });
    toast.addEventListener("focusin", () => { focused = true; pause(); });
    toast.addEventListener("focusout", event => { focused = toast.contains(event.relatedTarget); if (!focused) { resume(); } });
    toast.querySelector(".app-toast-close")?.addEventListener("click", close);
    document.addEventListener("visibilitychange", visibility);
    const observer = new MutationObserver(() => { if (!toast.isConnected) { pause(); document.removeEventListener("visibilitychange", visibility); observer.disconnect(); } });
    observer.observe(stack, { childList: true });
    resume();
  }
  const icon = name => {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.classList.add("medic-icon"); svg.setAttribute("aria-hidden", "true"); svg.setAttribute("focusable", "false");
    const use = document.createElementNS(svg.namespaceURI, "use"); use.setAttribute("href", `${stack.dataset.iconsUrl}#${name}`); svg.append(use); return svg;
  };
  window.App.toast = (kind, text) => {
    if (typeof text !== "string" || !text.trim()) { return null; }
    text = text.trim(); if (!Object.hasOwn(styles, kind)) { kind = "info"; }
    const existing = [...stack.querySelectorAll(".app-toast:not(.hiding)")].find(item => item.dataset.kind === kind && item.querySelector(".app-toast-text")?.textContent === text);
    if (existing) { return existing; }
    const [name, timeout] = styles[kind];
    const toast = document.createElement("div"); toast.className = `app-toast toast-${kind}`; toast.dataset.kind = kind; toast.dataset.timeout = timeout; toast.tabIndex = 0;
    toast.setAttribute("role", kind === "error" || kind === "warning" ? "alert" : "status");
    const emblem = document.createElement("span"); emblem.className = "app-toast-icon"; emblem.append(icon(name));
    const body = document.createElement("div"); body.className = "app-toast-body";
    const title = document.createElement("div"); title.className = "app-toast-title"; title.textContent = stack.dataset[`${kind}Title`];
    const message = document.createElement("div"); message.className = "app-toast-text"; message.textContent = text;
    body.append(title, message);
    const button = document.createElement("button"); button.type = "button"; button.className = "app-toast-close"; button.setAttribute("aria-label", stack.dataset.closeLabel); button.append(icon("close"));
    toast.append(emblem, body, button);
    if (kind === "error") { stack.insertBefore(toast, stack.querySelector('.app-toast:not(.toast-error)')); } else { stack.append(toast); }
    wire(toast); queuePosition(); return toast;
  };
  stack.querySelectorAll(".app-toast").forEach(wire);
  // Unobtrusive validation cancels the POST, so report client-side invalid forms here too.
  document.addEventListener("invalid", () => window.App.toast("error", labels.invalidForm), true);
  if (window.jQuery) { jQuery("form").on("invalid-form.validate", () => window.App.toast("error", labels.invalidForm)); }
})();
