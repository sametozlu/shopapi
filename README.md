# ShopAPI

ASP.NET Core 8 tabanlı, katmanlı mimari ile oluşturulmuş e-ticaret backend + React vitrin.

## Özellikler

- JWT + refresh token, rate limiting, Redis/in-memory cache
- EF Core migrations (SQLite / PostgreSQL / SQL Server)
- Sipariş akışı, mock + Stripe Checkout ödeme
- Admin API + admin panel (sipariş / ürün / kupon)
- Audit log, outbox events, health check, Prometheus metrics
- xUnit integration test + Playwright E2E
- Docker, GitHub Actions CI/CD, GHCR image publish

## Yerel çalıştırma

```powershell
dotnet run --project ShopAPI.API
cd ShopAPI.Frontend; npm run dev
```

| Endpoint | URL |
|----------|-----|
| Swagger | http://localhost:5095/swagger |
| Frontend | http://localhost:5173 |
| Health | http://localhost:5095/health |
| Metrics | http://localhost:5095/metrics |

Demo: `admin@admin.local` / `Admin123!`

## Testler

```powershell
dotnet test ShopAPI.Tests/ShopAPI.Tests.csproj -c Release
cd ShopAPI.Frontend
npm run test:e2e
```

E2E testler API + frontend'i otomatik başlatır (`playwright.config.js`).

## Yapılandırma

Geliştirme: `appsettings.Development.json`  
Üretim: ortam değişkenleri (bkz. `.env.example`)

```powershell
$env:Jwt__Key="your-secret-min-32-chars"
$env:Stripe__SecretKey="sk_test_..."
dotnet run --project ShopAPI.API
```

## CI/CD

- `ci.yml` — backend test, frontend build, docker build, Playwright E2E
- `deploy.yml` — `main` push'ta Docker image → `ghcr.io/<owner>/shopapi`

## Proje yapısı

| Klasör | Açıklama |
|--------|----------|
| `ShopAPI.API` | Controllers, middleware, observability |
| `ShopAPI.Application` | DTO, contracts, validators |
| `ShopAPI.Infrastructure/Services/` | İş mantığı servisleri |
| `ShopAPI.Frontend/src/pages/` | Login, Store, Admin sayfaları |
| `ShopAPI.Frontend/e2e/` | Playwright testleri |

_Last updated: 2026-06-05_
