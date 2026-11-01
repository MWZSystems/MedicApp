"use strict";
document.querySelectorAll("[data-password-toggle]").forEach(button => {
  const input = document.getElementById(button.dataset.passwordToggle);
  if (!input || input.type !== "password") { return; }
  const update = visible => {
    input.type = visible ? "text" : "password";
    const label = visible ? button.dataset.hideLabel : button.dataset.showLabel;
    button.setAttribute("aria-pressed", String(visible));
    button.setAttribute("aria-label", label);
    button.title = label;
    button.querySelector("[data-password-label]").textContent = label;
    button.querySelector("[data-password-show]").hidden = visible;
    button.querySelector("[data-password-hide]").hidden = !visible;
  };
  button.addEventListener("click", () => update(input.type === "password"));
  input.form?.addEventListener("submit", () => update(false));
  window.addEventListener("pagehide", () => update(false));
  button.hidden = false;
});
