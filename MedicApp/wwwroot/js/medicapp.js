"use strict";
const labels = document.body.dataset;
document.querySelectorAll(".app-nav a").forEach(link => { if (link.pathname === window.location.pathname) { link.setAttribute("aria-current", "page"); } });
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
