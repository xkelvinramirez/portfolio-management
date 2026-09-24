---
version: 1
slug: "frontend-app-dashboard"
primary_target: "frontend/app/(app)/dashboard"
related_targets: []
---

## Scope and visitor mode

Operate. Route: `frontend/app/(app)/dashboard`. First surface of a new visual world (no prior DESIGN.md or implementation).

## Audience, job, action, proof

A single authenticated individual reviewing their own crypto (and later index-fund) holdings, manually logged across multiple exchanges. Primary task: see total value, its history, allocation, and audit individual entries. Proof/content is entirely the user's own `PortfolioEntry` data (quantity, price, exchange, cryptocurrency, recorded date) — no external feed, no invented data.

## Direction contract

THESIS: The dashboard is an open accounting ledger, not a trading terminal wearing a calm skin — it refuses the crypto-dashboard default of tickers, candlesticks, and flashing green/red.

OWN-WORLD: Warm ledger-paper cream ground, near-black ink for text and rules, one committed accent — a deep ledger-red used exclusively for negative movement and corrections (classic accounting red, never decorative). Hairline horizontal rules instead of shadowed cards. One workhorse sans for all UI text (headings, labels, nav, buttons); a true monospace with tabular figures for every number in the interface (amounts, percentages, dates), so figures align the way a ledger's columns do.

STORY: The visitor opens on their holdings ledger, not a hero chart — the table is the protagonist. Smaller summary panels (line history, allocation circle) sit above it, read as the ledger's own summary page, not the main event.

FIRST VIEWPORT: A holdings table spanning full width, ruled like an open ledger page, above the fold. Above it, two compact summary panels side by side: a line chart of portfolio value over time (range selector), and an allocation circle with an asset/exchange toggle. An operational alerts strip sits above both, quiet and dismissible, never a modal.

FORM: Columnar Ledger — rank 1 of 7 in the derived world list (ship's log, strip-chart recorder, columnar ledger, coin album, herbarium sheet, barometer panel, card catalog), chosen by the user over the assigned roll (rank 6, Barometer/Instrument Panel). Seed key: 3c25da38. Raises adopted from declined challengers: tabular monospace numerals throughout (from the phosphor-terminal challenger); single committed accent discipline (from the Kraftwerk man-machine challenger).

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance.

## Constraints

- Code-led build: no image generation available this session: no comp exists or is expected; ambition lives in this contract's FIRST VIEWPORT and signature interaction, audited in behavior at finish.
- UI vocabulary is asset-type-agnostic ("Activo", not "Cripto") even though only `CryptoCurrency` data exists today; do not build a second asset-type UI until the backend models one.
- Backend endpoints (portfolio CRUD, cryptocurrency CRUD, exchange CRUD, plus `/Portfolio/{id}/value|history|holdings|allocation`) are complete — the sequencing prerequisite that blocked wiring real data is resolved.
- Signature interaction: dragging the line chart's time axis acts like turning a dial — no literal dial glyph, just the scrub-and-live-update behavior, adapted from the variable-font-specimen challenger.

## Unresolved decisions

- Exact threshold for an operational alert (e.g. "30 days without a new entry") — not yet chosen by the user.
- Whether a news section ships at all; recommended deferred to v2 (no data source, would break the "no external feed" product principle) unless the user overrides.
