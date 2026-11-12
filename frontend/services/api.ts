import { api } from "@/lib/axios";
import {
  type CreateCryptoCurrencyRequest,
  type CreateExchangeRequest,
  type CreateNoteRequest,
  type CreatePortfolioEntryRequest,
  type CreatePortfolioRequest,
  type CreatePortfolioResponse,
  type CryptoCurrencyResponse,
  type ExchangeResponse,
  type LoginUserRequest,
  type LoginUserResponse,
  type NoteResponse,
  type PaginatorResponse,
  type PendingActionResult,
  type PortfolioAllocationGroupBy,
  type PortfolioAllocationResponse,
  type PortfolioEntryResponse,
  type PortfolioHistoryResponse,
  type PortfolioHoldingsResponse,
  type PortfolioResponse,
  type PortfolioValueResponse,
  type RefreshPortfolioPricesResponse,
  type RegisterUserRequest,
  type RegisterUserResponse,
  type SendAgentMessageResponse,
  type SendMafAgentMessageRequest,
  type SendMafAgentMessageResponse,
  type UpdateCryptoCurrencyRequest,
  type UpdateExchangeRequest,
  type UpdateNoteRequest,
  type UpdatePortfolioEntryRequest,
  type UpdatePortfolioRequest,
  PortfolioAllocationGroupBy as GroupBy,
} from "@/types";

export interface ListParams {
  page?: number;
  limit?: number;
}

// ---------- Auth ----------

export const authService = {
  login: (data: LoginUserRequest) =>
    api.post<LoginUserResponse>("/Users/login", data).then((r) => r.data),

  register: (data: RegisterUserRequest) =>
    api.post<RegisterUserResponse>("/Users/register", data).then((r) => r.data),
};

// ---------- Portfolios ----------

export type PortfolioListParams = ListParams;

export const portfolioService = {
  list: (params: PortfolioListParams = {}) =>
    api.get<PaginatorResponse<PortfolioResponse>>("/Portfolio", { params }).then((r) => r.data),

  getById: (id: number) =>
    api.get<PortfolioResponse>(`/Portfolio/${id}`).then((r) => r.data),

  create: (data: CreatePortfolioRequest) =>
    api.post<CreatePortfolioResponse>("/Portfolio", data).then((r) => r.data),

  update: (id: number, data: UpdatePortfolioRequest) =>
    api.put<PortfolioResponse>(`/Portfolio/${id}`, data).then((r) => r.data),

  remove: (id: number) => api.delete<void>(`/Portfolio/${id}`).then((r) => r.data),

  // Analytics: the domain's own valuation logic, not raw entries the frontend recomputes.
  getValue: (id: number, date?: string) =>
    api
      .get<PortfolioValueResponse>(`/Portfolio/${id}/value`, { params: { date } })
      .then((r) => r.data),

  getHistory: (id: number) =>
    api.get<PortfolioHistoryResponse>(`/Portfolio/${id}/history`).then((r) => r.data),

  getHoldings: (id: number, date?: string) =>
    api
      .get<PortfolioHoldingsResponse>(`/Portfolio/${id}/holdings`, { params: { date } })
      .then((r) => r.data),

  getAllocation: (id: number, groupBy: PortfolioAllocationGroupBy, date?: string) =>
    api
      .get<PortfolioAllocationResponse>(`/Portfolio/${id}/allocation`, {
        // The endpoint binds the enum by name (e.g. "Asset"); GroupBy[...] reverse-maps
        // the numeric enum value back to that name.
        params: { groupBy: GroupBy[groupBy], date },
      })
      .then((r) => r.data),

  refreshPrices: (id: number) =>
    api
      .post<RefreshPortfolioPricesResponse>(`/Portfolio/${id}/refresh-prices`)
      .then((r) => r.data),
};

// ---------- Portfolio entries ----------

export interface PortfolioEntryListParams extends ListParams {
  portfolioId?: number;
  cryptoCurrencyId?: number;
  fromDate?: string;
  toDate?: string;
}

export const portfolioEntryService = {
  list: (params: PortfolioEntryListParams = {}) =>
    api
      .get<PaginatorResponse<PortfolioEntryResponse>>("/PortfolioEntry", { params })
      .then((r) => r.data),

  getById: (id: number) =>
    api.get<PortfolioEntryResponse>(`/PortfolioEntry/${id}`).then((r) => r.data),

  create: (data: CreatePortfolioEntryRequest) =>
    api.post<PortfolioEntryResponse>("/PortfolioEntry", data).then((r) => r.data),

  update: (id: number, data: UpdatePortfolioEntryRequest) =>
    api.put<PortfolioEntryResponse>(`/PortfolioEntry/${id}`, data).then((r) => r.data),

  remove: (id: number) => api.delete<void>(`/PortfolioEntry/${id}`).then((r) => r.data),
};

// ---------- Cryptocurrencies ----------

export const cryptoCurrencyService = {
  list: (params: ListParams = {}) =>
    api
      .get<PaginatorResponse<CryptoCurrencyResponse>>("/CryptoCurrency", { params })
      .then((r) => r.data),

  getById: (id: number) =>
    api.get<CryptoCurrencyResponse>(`/CryptoCurrency/${id}`).then((r) => r.data),

  create: (data: CreateCryptoCurrencyRequest) =>
    api.post<CryptoCurrencyResponse>("/CryptoCurrency", data).then((r) => r.data),

  update: (id: number, data: UpdateCryptoCurrencyRequest) =>
    api.put<CryptoCurrencyResponse>(`/CryptoCurrency/${id}`, data).then((r) => r.data),

  remove: (id: number) => api.delete<void>(`/CryptoCurrency/${id}`).then((r) => r.data),
};

// ---------- Exchanges ----------

export const exchangeService = {
  list: (params: ListParams = {}) =>
    api.get<PaginatorResponse<ExchangeResponse>>("/Exchange", { params }).then((r) => r.data),

  getById: (id: number) => api.get<ExchangeResponse>(`/Exchange/${id}`).then((r) => r.data),

  create: (data: CreateExchangeRequest) =>
    api.post<ExchangeResponse>("/Exchange", data).then((r) => r.data),

  update: (id: number, data: UpdateExchangeRequest) =>
    api.put<ExchangeResponse>(`/Exchange/${id}`, data).then((r) => r.data),

  remove: (id: number) => api.delete<void>(`/Exchange/${id}`).then((r) => r.data),
};

// ---------- Notes ----------

export interface NoteListParams extends ListParams {
  category?: string;
  isActive?: boolean;
}

export const noteService = {
  list: (params: NoteListParams = {}) =>
    api.get<PaginatorResponse<NoteResponse>>("/Note", { params }).then((r) => r.data),

  latest: (count = 10) =>
    api.get<NoteResponse[]>("/Note/latest", { params: { count } }).then((r) => r.data),

  getById: (id: string) => api.get<NoteResponse>(`/Note/${id}`).then((r) => r.data),

  create: (data: CreateNoteRequest) =>
    api.post<NoteResponse>("/Note", data).then((r) => r.data),

  update: (id: string, data: UpdateNoteRequest) =>
    api.put<NoteResponse>(`/Note/${id}`, data).then((r) => r.data),

  remove: (id: string) => api.delete<void>(`/Note/${id}`).then((r) => r.data),
};

// ---------- Agent ----------

export const agentService = {
  // The backend caps `message` at 2000 chars and carries no history between calls —
  // the caller is responsible for trimming any prior turns into this single string.
  chat: (message: string) =>
    api.post<SendAgentMessageResponse>("/Agent/chat", { message }).then((r) => r.data),
};

// ---------- MAF agent ----------

export const mafAgentService = {
  // The conversation lives on the server: send only the new message plus the id the
  // previous turn returned (null starts a new one). `message` is capped at 2000 chars.
  chat: (data: SendMafAgentMessageRequest) =>
    api.post<SendMafAgentMessageResponse>("/MafAgent/chat", data).then((r) => r.data),

  // Runs the proposed write. Once the action is claimed every outcome comes back as 200
  // with a status (Succeeded, Failed, Stale); 4xx means it could not be claimed.
  confirm: (id: string) =>
    api.post<PendingActionResult>(`/MafAgent/pending-actions/${id}/confirm`).then((r) => r.data),

  reject: (id: string) =>
    api.post<void>(`/MafAgent/pending-actions/${id}/reject`).then((r) => r.data),
};
