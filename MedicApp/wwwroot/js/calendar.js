"use strict";
(() => {
  const page = document.getElementById("medical-calendar-page");
  if (!page || !window.FullCalendar) { return; }
  const labels = page.dataset;
  const dayInput = document.getElementById("booking-date");
  const timeInput = document.getElementById("booking-time");
  const reserveButton = document.getElementById("reserve-button");
  const status = document.getElementById("slots-status");
  const message = document.getElementById("calendar-message");
  const panel = document.getElementById("booking-panel");
  let slotRequest;
  let booking = false;
  let loading = false;
  let selectionSequence = 0;
  const signalMessage = (text, kind = "success") => {
    message.className = `alert alert-${kind}`;
    message.textContent = text;
  };
  const jsonRequest = async (url, settings = {}) => {
    const response = await fetch(url, { credentials: "same-origin", ...settings, headers: { Accept: "application/json", ...(settings.headers || {}) } });
    if (response.redirected || !response.headers.get("content-type")?.includes("json")) { throw new Error(labels.requestError); }
    const body = await response.json();
    if (!response.ok) { throw new Error(body.title || labels.requestError); }
    return body;
  };
  const updateButton = () => { reserveButton.disabled = booking || loading || !timeInput.value; };
  const loadSlots = async (date, preferredTime) => {
    const sequence = ++selectionSequence;
    slotRequest?.abort();
    slotRequest = new AbortController();
    timeInput.replaceChildren(new Option(labels.selectTime, ""));
    timeInput.disabled = true;
    loading = true; status.textContent = labels.loading; updateButton();
    try {
      const url = new URL(labels.slotsUrl, location.origin); url.searchParams.set("date", date);
      const slots = await jsonRequest(url, { signal: slotRequest.signal });
      if (sequence !== selectionSequence) { return; }
      slots.forEach(slot => timeInput.add(new Option(slot.time, slot.time)));
      if (preferredTime && slots.some(slot => slot.time === preferredTime)) { timeInput.value = preferredTime; }
      status.textContent = slots.length ? labels.autoDoctor : labels.noSlots;
    } catch (error) {
      if (error.name !== "AbortError" && sequence === selectionSequence) { status.textContent = error.message; }
    } finally {
      if (sequence === selectionSequence) { loading = false; timeInput.disabled = false; updateButton(); }
    }
  };
  const chooseDay = (date, time, focus = true) => {
    if (date < dayInput.min || date > dayInput.max) { signalMessage(labels.noSlots, "info"); return; }
    dayInput.value = date;
    loadSlots(date, time);
    if (focus) { panel.focus({ preventScroll: true }); if (window.innerWidth < 1000) { panel.scrollIntoView({ behavior: "smooth", block: "start" }); } }
  };
  // The calendar carries business-local wall times without a browser timezone conversion.
  // API ranges are business dates; persisted appointments remain UTC on the server.
  const calendar = new FullCalendar.Calendar(document.getElementById("appointments-calendar"), {
    locale: "es", timeZone: "UTC", initialDate: labels.initialDate,
    initialView: window.innerWidth < 600 ? "listWeek" : "dayGridMonth",
    headerToolbar: { left: "prev,next today", center: "title", right: "dayGridMonth,timeGridWeek,timeGridDay,listWeek" },
    buttonIcons: false,
    buttonText: { prev: "‹", next: "›", today: labels.todayLabel, month: labels.monthLabel, week: labels.weekLabel, day: labels.dayLabel, list: labels.listLabel },
    height: 650, expandRows: true, firstDay: 1, navLinks: true, now: `${labels.today}T00:00:00`, nowIndicator: false,
    editable: false, dayMaxEvents: 3, eventDisplay: "block", eventTimeFormat: { hour: "2-digit", minute: "2-digit", hour12: false },
    slotLabelFormat: { hour: "2-digit", minute: "2-digit", hour12: false }, slotDuration: "00:15:00", scrollTime: "07:00:00",
    events: (info, success, failure) => {
      const url = new URL(labels.eventsUrl, location.origin);
      url.searchParams.set("from", info.startStr.slice(0, 10)); url.searchParams.set("to", info.endStr.slice(0, 10));
      jsonRequest(url).then(success).catch(error => { signalMessage(error.message, "danger"); failure(error); });
    },
    dateClick: info => chooseDay(info.dateStr.slice(0, 10), info.allDay ? null : info.dateStr.slice(11, 16)),
    eventClick: info => {
      const detail = info.event.extendedProps;
      if (detail.kind === "availability") { chooseDay(detail.date); return; }
      showDetail(info.event);
    },
    eventDidMount: info => { info.el.setAttribute("title", info.event.title); },
    datesSet: () => { message.classList.add("d-none"); }
  });
  calendar.render();
  dayInput.addEventListener("change", () => {
    if (!dayInput.checkValidity()) { timeInput.replaceChildren(new Option(labels.selectTime, "")); updateButton(); return; }
    calendar.gotoDate(dayInput.value); loadSlots(dayInput.value);
  });
  timeInput.addEventListener("change", updateButton);
  document.getElementById("focus-booking").addEventListener("click", event => { event.preventDefault(); panel.focus(); dayInput.focus(); if (window.innerWidth < 1000) { panel.scrollIntoView({ behavior: "smooth" }); } });
  document.getElementById("quick-booking-form").addEventListener("submit", async event => {
    event.preventDefault(); const form = event.currentTarget;
    if (booking || loading || !form.reportValidity()) { return; }
    booking = true; updateButton();
    try {
      const result = await jsonRequest(form.action, { method: "POST", body: new FormData(form) });
      signalMessage(result.message); form.elements.RequestKey.value = crypto.randomUUID();
      calendar.refetchEvents(); await loadSlots(dayInput.value);
    } catch (error) { signalMessage(error.message, "danger"); calendar.refetchEvents(); await loadSlots(dayInput.value); }
    finally { booking = false; updateButton(); }
  });
  const detailForm = document.getElementById("calendar-change-form");
  const action = document.getElementById("detail-action");
  const reason = document.getElementById("detail-reason");
  const modal = new bootstrap.Modal(document.getElementById("appointment-detail"));
  const updateReason = () => { reason.required = action.value === "3"; document.getElementById("detail-reason-group").hidden = !reason.required; };
  const showDetail = event => {
    const detail = event.extendedProps;
    document.getElementById("detail-employee").textContent = detail.employee;
    document.getElementById("detail-doctor").textContent = detail.doctor;
    document.getElementById("detail-status").textContent = detail.status;
    const format = value => new Intl.DateTimeFormat("es-MX", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" }).format(value);
    document.getElementById("detail-time").textContent = `${format(event.start)} – ${format(event.end)}`;
    detailForm.elements.Id.value = detail.appointmentId; detailForm.elements.Version.value = detail.version;
    reason.value = ""; document.getElementById("detail-error").textContent = "";
    action.replaceChildren();
    if (detail.canCancel) { action.add(new Option(labels.cancel, "3")); }
    if (detail.canAttend) { action.add(new Option(labels.attended, "2")); action.add(new Option(labels.noShow, "4")); }
    detailForm.hidden = !action.options.length; updateReason(); modal.show();
  };
  action.addEventListener("change", updateReason);
  detailForm.addEventListener("submit", async event => {
    event.preventDefault(); if (!detailForm.reportValidity()) { return; }
    const button = detailForm.querySelector("button[type=submit],button:not([type])"); button.disabled = true;
    try { const result = await jsonRequest(detailForm.action, { method: "POST", body: new FormData(detailForm) }); modal.hide(); signalMessage(result.message); calendar.refetchEvents(); await loadSlots(dayInput.value); }
    catch (error) { document.getElementById("detail-error").textContent = error.message; }
    finally { button.disabled = false; }
  });
  updateButton();
  if (labels.openBooking === "true") { dayInput.focus(); }
})();
