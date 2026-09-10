"use strict";

// Мінімальний браузерний клієнт baseline 2-A.
// Правило показу даних: текстові значення від API виводяться лише через
// textContent або document.createTextNode. Дані з БД не є довіреним HTML.

const listStatusEl = document.querySelector("#list-status");
const incidentListEl = document.querySelector("#incident-list");
const filterForm = document.querySelector("#filter-form");
const statusFilterEl = document.querySelector("#status-filter");

const detailsSectionEl = document.querySelector("#incident-details");
const detailsStatusEl = document.querySelector("#details-status");
const detailsBodyEl = document.querySelector("#details-body");
const detailsCommentsEl = document.querySelector("#details-comments");

const severitySummaryButtonEl = document.querySelector("#load-severity-summary");
const severitySummaryStatusEl = document.querySelector("#severity-summary-status");
const severitySummaryListEl = document.querySelector("#severity-summary-list");

/**
 * Обгортка над fetch. Повертає розібраний JSON для 2xx або кидає помилку
 * з кодом стану. Читання відповіді та читання тіла — дві окремі дії.
 */
async function apiFetch(path) {
    const response = await fetch(path, {
        headers: { "Accept": "application/json" }
    });

    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.includes("json") ? await response.json() : null;

    if (!response.ok) {
        const message = payload && payload.title ? payload.title : `HTTP ${response.status}`;
        const error = new Error(message);
        error.status = response.status;
        throw error;
    }

    return payload;
}

/** GET /api/incidents[?status=...] і показ списку карток. */
async function loadIncidents(status) {
    listStatusEl.textContent = "Завантаження списку…";
    incidentListEl.replaceChildren();

    const path = status
        ? `/api/incidents?status=${encodeURIComponent(status)}`
        : "/api/incidents";

    try {
        const incidents = await apiFetch(path);
        renderIncidentList(incidents);
        listStatusEl.textContent = incidents.length === 0
            ? "За цим фільтром інцидентів немає."
            : `Показано інцидентів: ${incidents.length}.`;
    } catch (error) {
        listStatusEl.textContent = `Не вдалося завантажити список (${error.status || "мережа"}).`;
    }
}

function renderIncidentList(incidents) {
    const fragment = document.createDocumentFragment();

    for (const incident of incidents) {
        const item = document.createElement("li");
        item.dataset.id = incident.id;

        const title = document.createElement("div");
        title.className = "incident-card-title";
        title.textContent = incident.title;

        const meta = document.createElement("div");
        meta.className = "incident-card-meta";
        meta.textContent = `${incident.severity} · ${incident.status} · ${formatDate(incident.occurredAtUtc)}`;

        item.append(title, meta);
        item.addEventListener("click", () => loadIncidentDetails(incident.id));
        fragment.append(item);
    }

    incidentListEl.replaceChildren(fragment);
}

/** GET /api/incidents/{id} і показ деталей одного інциденту. */
async function loadIncidentDetails(id) {
    detailsSectionEl.hidden = false;
    detailsStatusEl.textContent = "Завантаження деталей…";
    detailsBodyEl.replaceChildren();
    detailsCommentsEl.replaceChildren();

    try {
        const details = await apiFetch(`/api/incidents/${encodeURIComponent(id)}`);
        renderIncidentDetails(details);
        detailsStatusEl.textContent = "";
    } catch (error) {
        detailsStatusEl.textContent = error.status === 404
            ? "Інцидент не знайдено (404)."
            : `Не вдалося завантажити деталі (${error.status || "мережа"}).`;
    }
}

function renderIncidentDetails(details) {
    const rows = [
        ["Назва", details.title],
        ["Опис", details.description],
        ["Критичність", details.severity],
        ["Статус", details.status],
        ["Виник", formatDate(details.occurredAtUtc)],
        ["Створено", formatDate(details.createdAtUtc)],
        ["Власник", details.ownerDisplayName]
    ];

    const fragment = document.createDocumentFragment();
    for (const [label, value] of rows) {
        const dt = document.createElement("dt");
        dt.textContent = label;
        const dd = document.createElement("dd");
        // Навіть якщо value містить символи <script>, вони залишаються текстом.
        dd.append(document.createTextNode(value ?? ""));
        fragment.append(dt, dd);
    }
    detailsBodyEl.replaceChildren(fragment);

    const commentsFragment = document.createDocumentFragment();
    for (const comment of details.comments || []) {
        const li = document.createElement("li");
        li.textContent = `${comment.authorDisplayName}: ${comment.body}`;
        commentsFragment.append(li);
    }
    detailsCommentsEl.replaceChildren(commentsFragment);
}

function formatDate(value) {
    if (!value) {
        return "—";
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? String(value) : date.toISOString().replace("T", " ").slice(0, 16);
}

/**
 * GET /api/incidents/severity-summary і безпечний показ результату.
 * Окремі стани: завантаження -> (порожньо | успіх) | безпечна помилка.
 */
async function loadSeveritySummary() {
    severitySummaryStatusEl.textContent = "Завантаження…";
    severitySummaryListEl.replaceChildren();

    try {
        const summary = await apiFetch("/api/incidents/severity-summary");

        if (!Array.isArray(summary) || summary.length === 0) {
            severitySummaryStatusEl.textContent = "Даних немає.";
            return;
        }

        const fragment = document.createDocumentFragment();
        for (const row of summary) {
            const item = document.createElement("li");
            // Лише текст: жодного перетворення даних API на розмітку.
            item.textContent = `${row.severity}: ${row.count}`;
            fragment.append(item);
        }
        severitySummaryListEl.replaceChildren(fragment);
        severitySummaryStatusEl.textContent = "";
    } catch (error) {
        // Коротке фіксоване повідомлення без stack trace, SQL чи внутрішніх деталей.
        severitySummaryStatusEl.textContent = "Не вдалося завантажити підсумок.";
    }
}

filterForm.addEventListener("submit", (event) => {
    event.preventDefault();
    loadIncidents(statusFilterEl.value);
});

severitySummaryButtonEl.addEventListener("click", () => loadSeveritySummary());

loadIncidents("");
loadSeveritySummary();
