"use strict";
(() => {
    const personForm = document.getElementById("leave-person-form");
    if (personForm && window.jQuery) {
        jQuery("#leave-person").on("select2:select", () => personForm.requestSubmit());
    }
    const page = document.getElementById("leave-calendar-page");
    if (!page || !window.FullCalendar) return;
    const labels = page.dataset;
    const status = document.getElementById("leave-calendar-status");
    const modal = new bootstrap.Modal(document.getElementById("leave-period-detail"));
    const formatTime = value => new Intl.DateTimeFormat("es-MX", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" }).format(new Date(value + "Z"));
    const formatDate = value => new Intl.DateTimeFormat("es-MX", { dateStyle: "long", timeZone: "UTC" }).format(new Date(value + "T12:00:00Z"));
    let pending;
    let sequence = 0;
    new FullCalendar.Calendar(document.getElementById("leave-calendar"), {
        locale: "es", timeZone: "UTC", initialDate: labels.initialDate, initialView: "dayGridMonth",
        height: 650, expandRows: true, firstDay: 1, editable: false, displayEventTime: false, dayMaxEvents: 3,
        buttonIcons: false, buttonText: { prev: "‹", next: "›", today: labels.today, month: labels.month, week: labels.week, list: labels.list },
        headerToolbar: { left: "prev,next today", center: "title", right: "dayGridMonth,dayGridWeek,listMonth" },
        events: async (range, success, failure) => {
            const request = ++sequence;
            pending?.abort(); pending = new AbortController();
            status.className = "alert d-none";
            try {
                const url = new URL(labels.eventsUrl, location.origin);
                url.searchParams.set("personId", labels.personId);
                url.searchParams.set("from", range.startStr.slice(0, 10));
                url.searchParams.set("to", range.endStr.slice(0, 10));
                const response = await fetch(url, { credentials: "same-origin", signal: pending.signal, headers: { Accept: "application/json" } });
                if (response.redirected || !response.headers.get("content-type")?.includes("json")) throw new Error(labels.error);
                const body = await response.json();
                if (!response.ok) throw new Error(body.title || labels.error);
                if (request !== sequence) return;
                success(body);
                status.textContent = body.length ? "" : labels.empty;
                status.className = body.length ? "alert d-none" : "alert alert-info";
            } catch (error) {
                if (error.name === "AbortError" || request !== sequence) return;
                status.textContent = error.message; status.className = "alert alert-danger"; failure(error);
            }
        },
        eventClick: info => {
            const detail = info.event.extendedProps;
            document.getElementById("leave-detail-start").textContent = formatTime(detail.startsAtLocal);
            document.getElementById("leave-detail-end").textContent = formatTime(detail.endsAtLocal);
            document.getElementById("leave-detail-through").textContent = formatDate(detail.coveredThrough);
            modal.show();
        },
        eventDidMount: info => { info.el.setAttribute("title", info.event.title); }
    }).render();
})();
