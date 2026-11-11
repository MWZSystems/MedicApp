"use strict";
// Los textos traducidos vienen de un elemento de la página (la estructura común es dueña del <body>).
const labels = document.getElementById("medic-labels")?.dataset ?? {};
document.querySelectorAll(".app-nav a").forEach(link => { if (link.pathname === window.location.pathname && !link.hash) { link.setAttribute("aria-current", "page"); } });
if (window.jQuery && jQuery.fn.select2) {
  document.querySelectorAll("select[data-search-url]").forEach(element => {
    jQuery(element).select2({ width: "100%", language: "es", minimumInputLength: 2, ajax: { url: element.dataset.searchUrl, dataType: "json", delay: 300, data: params => ({ term: params.term }), processResults: data => data } });
  });
  document.querySelectorAll('select:not([data-search-url]):not([data-select2="off"])').forEach(element => {
    jQuery(element).select2({ width: "100%", language: "es", minimumResultsForSearch: 8 });
  });
  if (jQuery.validator) { jQuery.validator.setDefaults({ ignore: ":hidden:not(.select2-hidden-accessible)" }); }
}
// Paging and searching run on the server; only marked controls auto-submit.
document.addEventListener("change", event => {
  const control = event.target;
  if (control instanceof HTMLElement && control.matches("select[data-autosubmit], input[data-autosubmit]")) { control.closest("form")?.requestSubmit(); }
});
document.querySelectorAll("form[data-confirm]").forEach(form => form.addEventListener("submit", async event => {
  if (form.dataset.confirmed === "true") { return; }
  event.preventDefault();
  if (!form.reportValidity()) { return; }
  const submitter = event.submitter;
  // La ventana de confirmación es la de MWZSystem.Ui.
  const question = form.dataset.confirmText ? form.dataset.confirm + "\n\n" + form.dataset.confirmText : form.dataset.confirm;
  if (await mwz.confirmar(question, { aceptar: labels.confirm })) { form.dataset.confirmed = "true"; form.requestSubmit(submitter); }
}));


// Avisos emergentes: los dibuja MWZSystem.Ui (mwz-ui.js). App.toast(tipo, texto) se conserva para el código de MedicApp.
window.App = window.App || {};
(() => {
  const mostrar = { success: mwz.aviso, info: mwz.info, warning: mwz.advertencia, error: mwz.error };
  window.App.toast = (kind, text) => { if (typeof text === "string" && text.trim()) { (mostrar[kind] || mwz.info)(text.trim()); } };
  // Unobtrusive validation cancels the POST, so report client-side invalid forms here too.
  document.addEventListener("invalid", () => window.App.toast("error", labels.invalidForm), true);
  if (window.jQuery) { jQuery("form").on("invalid-form.validate", () => window.App.toast("error", labels.invalidForm)); }
})();
