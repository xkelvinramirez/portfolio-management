# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Backend: .NET 10 Minimal API, Clean Architecture (CQRS via MediatR-style handlers), PostgreSQL + EF Core (Npgsql), JWT bearer auth.
Frontend: Next.js (App Router) + TypeScript, Tailwind CSS, Zustand (global state), TanStack React Query + Axios (data fetching), Lucide React (icons). Decided by the user before this session, recorded in `frontend/README.md`.

## Users

A single individual tracking their own cryptocurrency holdings. Each portfolio belongs to exactly one authenticated user (`User` 1→N `Portfolio`, JWT-scoped); there is no shared/team ownership model.

## Product Purpose

A personal tool to record and value cryptocurrency holdings held across multiple exchanges over time. The user manually logs each holding (asset, exchange, quantity, price, timestamp) rather than relying on an external market-data feed. Success means an accurate, browsable record of what the user owns, where, and how its value has changed over time.

## Positioning

Not built to compete with market-tracking products (CoinStats, Delta, etc.). Its distinguishing mechanism is a manually-curated, point-in-time ledger: `PortfolioEntry` ties a quantity + price to one cryptocurrency, one exchange, and a recorded timestamp, which lets the domain compute valuation as of any date (`Portfolio.GetPortfolioValueByDate`) and per-asset history (`Portfolio.GetCryptoCurrencyHistory`) without depending on a live price API.

## Operating Context

- Backend and frontend are separate apps in the same repo (`src/WebApi.MinimalAPI` and `frontend/`).
- Backend CRUD is now complete for all four entities (Portfolio, PortfolioEntry, CryptoCurrency, Exchange), plus analytics endpoints under `/Portfolio/{id}/value|history|holdings|allocation` that surface the domain's point-in-time valuation logic instead of making the frontend recompute it.
- Auth is JWT bearer (`DependencyInjection.cs` `AddJwtAuthentication`), secrets via `dotnet user-secrets`, not appsettings. `LoginUserResponse` returns only a token + expiry, no user profile — the frontend has no `/users/me` to call yet.

## Capabilities and Constraints

- Quantity and price per holding are entered manually by the user. No live market-data/price-feed integration is planned currently.
- Multi-exchange and multi-cryptocurrency support is a first-class part of the data model, not a stretch feature: `PortfolioEntry` references both `CryptoCurrencyId` and `ExchangeId` plus a `RecordedAt` timestamp.
- `Exchange` has an `ApiKey` field in the model but nothing consumes it yet — no live exchange-API sync exists; treat it as reserved/unused, not a working feature.
- Point-in-time valuation and per-asset history are implemented as domain methods on `Portfolio`, exposed via the `/Portfolio/{id}/value|history|holdings|allocation` endpoints — the frontend should consume these, not recompute valuation from raw `PortfolioEntry` rows.

## Brand Commitments

None established. "Dashboard Frontend" in `frontend/README.md` is a working description, not a product name.

## Evidence on Hand

None. No real holdings, screenshots, or demo data exist yet. Future design and copy work must not invent sample users, balances, or market prices as if real — use clearly-labeled placeholder data only.

## Product Principles

1. Manual entry is the source of truth — design around what the user records, never assume a live feed will fill gaps.
2. Multi-exchange, multi-asset visibility is core, not an afterthought — the dashboard should make "what do I hold, where" trivial to answer.
3. Every holding is a point-in-time record — valuation over time and per-asset history are primary views, not secondary reports.
4. Build for personal-scale correctness and clarity over enterprise hardening or multi-tenant scale.
