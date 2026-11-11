# Portfolio asset value comparison

## Intent

Let the authenticated user ask the portfolio agent how each asset's value changed between two dates, with an explicit positive or negative difference.

## Goals and non-goals

- Add a `PortfolioAgentPlugin` function that compares assets in one owned portfolio at two dates.
- For each asset, return its value at the start date, its value at the end date, and the signed difference (end minus start).
- Treat each date as a snapshot using the latest portfolio entries recorded on or before that date. Sum holdings across exchanges for the same asset.
- Include assets present on either date; an asset absent from one snapshot has value zero there.
- Do not add a portfolio-total result, percentage change, market-price lookup, or frontend changes.

## Constraints

- Enforce portfolio ownership using the existing authenticated-user check.
- Reuse `Portfolio.GetHoldingsAsOf` and `IPortfolioEntryRepository`; keep the comparison in the plugin as requested.
- Date arguments use ISO `yyyy-MM-dd` so the agent can resolve locale-formatted user dates unambiguously (for example, Spanish `06/10/2026` means `2026-10-06`).
- The agent-facing tool description must clearly direct date-range per-asset questions to this function rather than current-value or peak/trough functions.

## Approaches

1. **Plugin comparison function (chosen):** Load the portfolio entries once, get each date's snapshot with the existing domain method, aggregate by symbol, then return the union of symbols with both values and their signed difference. This reuses the established snapshot rules and confines the change to the agent surface.
2. **Domain comparison operation:** Add a domain API for comparing two asset snapshots and call it from the plugin. This centralizes the operation but adds a new domain concept for a result that is currently needed only by the agent.

The plugin function is preferred because the domain already owns the snapshot calculation and the plugin only needs to map the two snapshots into an agent-friendly response.

## Implementation outline

- Add a response record for a symbol and its start value, end value, and signed difference.
- Add a kernel function taking a portfolio id and two ISO dates. It must reject invalid dates and a start date later than the end date, return `null` for a portfolio the user does not own, and otherwise compare the two snapshots.
- Update the function descriptions so the model selects this function for requests to compare multiple assets across dates.
- Add focused coverage for positive and negative changes, aggregation across exchanges, assets present on only one date, and date/ownership handling, following the repository's existing test conventions.