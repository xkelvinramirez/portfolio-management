📊 ARQUITECTURA DEL PROYECTO
═════════════════════════════════════

┌─────────────────────────────────────────────────────────────┐
│                   FRONTEND (REACT/NEXT.JS)                  │
│              http://localhost:3000                          │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────┐   │
│  │            Components (React)                       │   │
│  │  - Header, Sidebar                                 │   │
│  │  - PortfolioList, CryptoCurrencyList, ExchangeList│   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           Hooks (React Query)                       │   │
│  │  - usePortfolios, useCryptoCurrencies, useExchanges│   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │          Services (Axios Instance)                 │   │
│  │  - portfolioService, cryptoCurrencyService, etc.   │   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │          State Management (Zustand)                │   │
│  │  - useAppStore (UI, Portfolio, Crypto, Exchange)  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                         ↓
                    AXIOS / HTTP
                         ↓
┌─────────────────────────────────────────────────────────────┐
│                  BACKEND (.NET API)                         │
│          https://localhost:7123                            │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────┐   │
│  │           Web.Api (Endpoints)                       │   │
│  │  - POST/GET/PUT/DELETE /portfolios                 │   │
│  │  - POST/GET/PUT/DELETE /cryptocurrencies           │   │
│  │  - POST/GET/PUT/DELETE /exchanges                  │   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │        Application Layer (Commands/Queries)         │   │
│  │  - CreatePortfolioCommand, UpdatePortfolioCommand  │   │
│  │  - CreateCryptoCurrencyCommand, etc.               │   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │         Domain Layer (Business Logic)               │   │
│  │  - Portfolio, CryptoCurrency, Exchange (Entities)  │   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │         Infrastructure (Database)                   │   │
│  │  - PostgreSQL / Entity Framework Core               │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘


FLUJO DE DATOS
════════════════════════════════════════

Usuario interactúa con UI (React Component)
        ↓
Component dispara Hook (useQuery/useMutation)
        ↓
React Query ejecuta la función del Service
        ↓
Service llama a Axios con petición HTTP
        ↓
Backend procesa en Application Layer (Command/Query Handler)
        ↓
Domain Layer ejecuta la lógica de negocio
        ↓
Infrastructure persiste/recupera datos
        ↓
Respuesta viaja de vuelta al Frontend
        ↓
React Query cachea y actualiza el estado
        ↓
Component se re-renderiza con nuevos datos


STACK RESUMIDO
════════════════════════════════════════

FRONTEND:
├── Framework: Next.js 14+
├── Language: TypeScript
├── Styling: Tailwind CSS
├── State: Zustand
├── Data: React Query + Axios
├── Icons: Lucide React
└── Build: npm

BACKEND:
├── Framework: .NET 10
├── Architecture: Clean Architecture
├── Pattern: CQRS
├── Database: PostgreSQL
├── ORM: Entity Framework Core
└── Build: dotnet


RUTAS PRINCIPALES
════════════════════════════════════════

Frontend Pages:
- /                    → Home
- /dashboard          → Dashboard
- /portfolio          → Portfolio Management
- /cryptocurrencies   → Crypto Management
- /exchanges          → Exchange Management
- /settings           → Settings

Backend API Endpoints:
- POST   /portfolios          → Crear Portfolio
- GET    /portfolios          → Obtener todos
- GET    /portfolios/{id}     → Obtener uno
- PUT    /portfolios/{id}     → Actualizar
- DELETE /portfolios/{id}     → Eliminar

(Similar para /cryptocurrencies y /exchanges)


DIRECTORIOS PRINCIPALES
════════════════════════════════════════

📦 ManagementAssetsApi/
├── 📁 frontend/               ← NUEVO PROYECTO REACT
│   ├── 📁 app/               → Páginas Next.js
│   ├── 📁 components/        → Componentes React
│   ├── 📁 hooks/             → Custom Hooks
│   ├── 📁 services/          → Servicios de API
│   ├── 📁 store/             → Estado global Zustand
│   ├── 📁 types/             → TypeScript Types
│   ├── 📁 lib/               → Utilities
│   ├── 📄 package.json
│   ├── 📄 tsconfig.json
│   ├── 📄 tailwind.config.ts
│   └── 📄 next.config.js
│
├── 📁 src/                   → Proyecto Backend .NET
│   ├── 📁 WebApi.MinimalAPI/
│   ├── 📁 Application/
│   ├── 📁 Domain/
│   ├── 📁 Infrastructure/
│   └── 📁 SharedKernel/

═══════════════════════════════════════════════════════════════
