# Dashboard Frontend

Dashboard moderno para gestionar tus criptomonedas con una interfaz intuitiva.

## 🚀 Stack Tecnológico

- **Framework**: Next.js 14+ con App Router
- **Lenguaje**: TypeScript
- **UI**: Tailwind CSS + Componentes Personalizados
- **Estado Global**: Zustand
- **Data Fetching**: @tanstack/react-query + Axios
- **Iconos**: Lucide React

## 📋 Requisitos Previos

- Node.js 18+
- npm o yarn
- Backend API (.NET) 

## 🏃 Ejecutar el Proyecto

### Desarrollo
```bash
npm run dev
```

La aplicación estará disponible en `http://localhost:3000`

### Producción
```bash
npm run build
npm start
```

## 📁 Estructura del Proyecto

```
frontend/
├── app/                          # App Router pages
│   ├── dashboard/
│   ├── portfolio/
│   ├── cryptocurrencies/
│   ├── exchanges/
│   ├── settings/
│   ├── layout.tsx               # Root layout
│   ├── page.tsx                 # Home page
│   └── globals.css
├── components/                   # React components
│   ├── layout/                  # Layout components (Header, Sidebar)
│   ├── ui/                      # UI components (Button, Input, Card)
│   ├── portfolio/               # Portfolio components
│   ├── cryptocurrency/          # Cryptocurrency components
│   └── exchange/                # Exchange components
├── hooks/                        # Custom React hooks
│   ├── usePortfolios.ts
│   ├── useCryptoCurrencies.ts
│   └── useExchanges.ts
├── services/                     # API service layer
│   └── api.ts
├── store/                        # Zustand stores
│   └── useAppStore.ts
├── types/                        # TypeScript types
│   └── index.ts
├── lib/                          # Utilities and helpers
│   ├── axios.ts                 # Axios instance
│   └── query-client.ts          # React Query client
└── package.json
```

## 🔌 Integración con Backend

El frontend se conecta con tu API .NET a través de Axios. Asegúrate de que:

1. Tu API esté ejecutándose en `https://localhost:7123`
2. Los endpoints sean accesibles desde el navegador
3. CORS esté configurado correctamente en tu backend

### Endpoints Utilizados

```
GET/POST    /portfolios
GET         /portfolios/{id}
PUT         /portfolios/{id}
DELETE      /portfolios/{id}

GET/POST    /cryptocurrencies
GET         /cryptocurrencies/{id}
PUT         /cryptocurrencies/{id}
DELETE      /cryptocurrencies/{id}

GET/POST    /exchanges
GET         /exchanges/{id}
PUT         /exchanges/{id}
DELETE      /exchanges/{id}
```

## 🎨 Componentes Disponibles

### UI Components
- `Button` - Botón con múltiples variantes
- `Input` - Input con validación
- `Card` - Contenedor de contenido

### Layout Components
- `Header` - Barra superior
- `Sidebar` - Navegación lateral

### Feature Components
- `PortfolioList` - Lista de portfolios
- `CryptoCurrencyList` - Tabla de criptomonedas
- `ExchangeList` - Lista de exchanges

## 🌳 Flujo de Datos

```
Component → Hook (useQuery/useMutation)
  ↓
React Query (caching + state management)
  ↓
Service Layer (api.ts)
  ↓
Axios Instance (lib/axios.ts)
  ↓
Backend API (.NET)
```

## 🚀 Próximos Pasos

- [ ] Agregar gráficos con Recharts
- [ ] Crear página de detalles de portfolio
- [ ] Agregar formularios de creación/edición
- [ ] Implementar autenticación
- [ ] Agregar historial de transacciones
- [ ] Implementar tema oscuro/claro

## 📝 Notas

- Los tokens de autenticación se guardan en `localStorage`
- Los datos se cachean durante 5 minutos por defecto
- El layout responde automáticamente al tamaño de pantalla

## 🆘 Troubleshooting

### Error de conexión con la API
```
Verifica que:
- La API esté ejecutándose en https://localhost:7123
- CORS esté habilitado en tu backend
- Las credenciales SSL sean válidas (desarrollo)
```

### Componentes no se renderizan
```
Asegúrate de usar 'use client' en componentes con hooks
```

## 📄 Licencia

Este proyecto está bajo la licencia MIT.
