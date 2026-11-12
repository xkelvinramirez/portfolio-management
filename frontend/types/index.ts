// Mirrors the backend's Contracts.* DTOs (src/Contracts). Keep in sync manually —
// the API has no OpenAPI client generation wired up yet.

// ---------- Common ----------

export interface PaginatorRequest {
  page?: number;
  limit?: number;
}

export interface PaginatorResponse<T> {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  data: T[];
}

// ---------- Auth / Users ----------

export interface LoginUserRequest {
  email: string;
  password: string;
}

export interface LoginUserResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export interface RegisterUserRequest {
  email: string;
  firstName: string;
  lastName?: string;
  password: string;
}

export interface RegisterUserResponse {
  id: number;
  email: string;
}

// ---------- CryptoCurrency ----------

export interface CryptoCurrencyResponse {
  id: number;
  symbol: string;
  name: string;
  coinGeckoId: string | null;
  image: string | null;
  createdAt: string;
}

export interface CreateCryptoCurrencyRequest {
  symbol: string;
  name: string;
  coinGeckoId?: string;
  image?: string;
}

export interface UpdateCryptoCurrencyRequest {
  symbol: string;
  name: string;
  coinGeckoId?: string;
  image?: string;
}

// ---------- Exchange ----------

export interface ExchangeResponse {
  id: number;
  name: string;
  apiKey: string;
  createdAt: string;
}

export interface CreateExchangeRequest {
  name: string;
  apiKey: string;
}

export interface UpdateExchangeRequest {
  name: string;
  apiKey: string;
}

// ---------- Portfolio ----------

export interface PortfolioResponse {
  id: number;
  userId: number;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreatePortfolioRequest {
  name: string;
  description: string;
}

export interface CreatePortfolioResponse {
  id: number;
  name: string;
}

export interface UpdatePortfolioRequest {
  name: string;
  description: string;
}

// ---------- PortfolioEntry ----------

export interface PortfolioEntryResponse {
  id: number;
  portfolioId: number;
  cryptoCurrencyId: number;
  exchangeId: number;
  quantity: number;
  pricePerUnit: number;
  recordedAt: string;
}

export interface CreatePortfolioEntryRequest {
  portfolioId: number;
  cryptoCurrencyId: number;
  exchangeId: number;
  quantity: number;
  pricePerUnit: number;
  recordedAt: string;
}

export interface UpdatePortfolioEntryRequest {
  cryptoCurrencyId: number;
  exchangeId: number;
  quantity: number;
  pricePerUnit: number;
  recordedAt: string;
}

// ---------- Portfolio analytics (value / history / holdings / allocation) ----------

export interface PortfolioValueResponse {
  portfolioId: number;
  date: string;
  value: number;
}

export interface PortfolioHistoryPointResponse {
  date: string;
  value: number;
}

export interface PortfolioAssetHistorySeriesResponse {
  cryptoCurrencyId: number;
  symbol: string;
  points: PortfolioHistoryPointResponse[];
}

export interface PortfolioHistoryResponse {
  portfolioId: number;
  points: PortfolioHistoryPointResponse[];
  byAsset: PortfolioAssetHistorySeriesResponse[];
}

export interface PortfolioHoldingItemResponse {
  cryptoCurrencyId: number;
  cryptoCurrencySymbol: string;
  cryptoCurrencyName: string;
  cryptoCurrencyImage: string | null;
  exchangeId: number;
  exchangeName: string;
  quantity: number;
  pricePerUnit: number;
  value: number;
  recordedAt: string;
}

export interface PortfolioHoldingsResponse {
  portfolioId: number;
  date: string;
  holdings: PortfolioHoldingItemResponse[];
}

// Wire value is numeric (System.Text.Json's default enum serialization: Asset = 0, Exchange = 1).
// The query param also accepts the name ("Asset"/"Exchange") — see services layer.
export enum PortfolioAllocationGroupBy {
  Asset = 0,
  Exchange = 1,
}

export interface PortfolioAllocationItemResponse {
  groupId: number;
  label: string;
  value: number;
  percentage: number;
}

export interface PortfolioAllocationResponse {
  portfolioId: number;
  date: string;
  groupBy: PortfolioAllocationGroupBy;
  items: PortfolioAllocationItemResponse[];
}

// ---------- Note ----------

export interface NoteResponse {
  id: string;
  category: string;
  title: string;
  content: string;
  createdAt: string;
  isActive: boolean;
}

export interface CreateNoteRequest {
  category: string;
  title: string;
  content: string;
  isActive: boolean;
  createdAt?: string;
}

export interface UpdateNoteRequest {
  category: string;
  title: string;
  content: string;
  isActive: boolean;
}

// ---------- Portfolio price refresh ----------

export interface RefreshedHoldingItemResponse {
  cryptoCurrencyId: number;
  symbol: string;
  exchangeId: number;
  quantity: number;
  previousPrice: number;
  newPrice: number;
}

export interface SkippedHoldingItemResponse {
  cryptoCurrencyId: number;
  symbol: string;
  reason: string;
}

export interface RefreshPortfolioPricesResponse {
  portfolioId: number;
  refreshedAt: string;
  updated: RefreshedHoldingItemResponse[];
  skipped: SkippedHoldingItemResponse[];
}

// ---------- Agent ----------

export interface SendAgentMessageRequest {
  message: string;
}

export interface SendAgentMessageResponse {
  reply: string;
}

// ---------- MAF agent ----------
// Mirrors src/Contracts/MafAgent/*.cs.

export type PendingActionOperation =
  | "CreatePortfolio"
  | "UpdatePortfolio"
  | "CreateEntry"
  | "UpdateEntry"
  | "CreateNote"
  | "UpdateNote"
  | "RefreshPrices"
  | "SendStatusReport";

// A write the agent proposed. Nothing runs until the user confirms `id`.
// `values` is the exact request that will be applied (dates as ISO-8601 UTC with Z);
// `before` is the current state for edits, null for creates and system actions.
export interface PendingAction {
  id: string;
  operation: PendingActionOperation;
  summary: string;
  values: Record<string, string | null>;
  before: Record<string, string | null> | null;
  expiresAt: string;
}

export interface SendMafAgentMessageRequest {
  // null starts a new conversation.
  conversationId: string | null;
  message: string;
  // The portfolio the user is looking at; the agent uses it unless the user names another.
  portfolioId: number | null;
}

export interface SendMafAgentMessageResponse {
  conversationId: string;
  reply: string;
  // Only the actions proposed during this turn.
  pendingActions: PendingAction[];
}

export type PendingActionResultStatus = "Succeeded" | "Failed" | "Stale";

export interface PendingActionResult {
  id: string;
  operation: PendingActionOperation;
  status: PendingActionResultStatus;
  result: unknown;
  error: { code: string; description: string } | null;
}
