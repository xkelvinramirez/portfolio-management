import { api } from "@/lib/axios";
import {
  type CreateCryptoCurrencyRequest,
  type CreateExchangeRequest,
  type CreatePortfolioEntryRequest,
  type CreatePortfolioRequest,
  type CreatePortfolioResponse,
  type CryptoCurrencyResponse,
  type ExchangeResponse,
  type LoginUserRequest,
  type LoginUserResponse,
  type PaginatorResponse,
  type PortfolioAllocationGroupBy,
  type PortfolioAllocationResponse,
  type PortfolioEntryResponse,
  type PortfolioHistoryResponse,
  type PortfolioHoldingsResponse,
  type PortfolioResponse,
  type PortfolioValueResponse,
  type RegisterUserRequest,
  type RegisterUserResponse,
  type UpdateCryptoCurrencyRequest,
  type UpdateExchangeRequest,
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

export interface PortfolioListParams extends ListParams {
  userId?: number;
}

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
};

// ---------- Portfolio entries ----------

export interface PortfolioEntryListParams extends ListParams {
  portfolioId?: number;
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
