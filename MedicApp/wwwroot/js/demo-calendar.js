"use strict";
document.addEventListener("DOMContentLoaded", () => {
    const element = document.getElementById("demo-calendar");
    if (!element || !window.FullCalendar) return;
    new FullCalendar.Calendar(element, {
        locale: "es", timeZone: "UTC", initialView: "dayGridMonth", height: 650,
        buttonIcons: false, buttonText: { prev: "‹", next: "›" },
        headerToolbar: { left: "prev,next today", center: "title", right: "dayGridMonth,timeGridWeek,listWeek" },
        events: async (range, success, failure) => {
            try {
                const url = new URL(element.dataset.eventsUrl, location.href);
                url.searchParams.set("from", range.startStr.slice(0, 10));
                url.searchParams.set("to", range.endStr.slice(0, 10));
                const response = await fetch(url, { headers: { Accept: "application/json" } });
                if (!response.ok) throw new Error(element.dataset.loadError);
                success(await response.json());
            } catch (error) { failure(error); }
        }
    }).render();
});
