# ShopAPI

ASP.NET Core 8 tabanlı, katmanlı mimari ile oluşturulmuş e-ticaret backend projesi. React (Vite) vitrin arayüzü ayrı klasördedir.

## Teknolojiler

- ASP.NET Core Web API
- Entity Framework Core (SQLite varsayılan, PostgreSQL ve SQL Server desteği)
- JWT + refresh token
- BCrypt password hashing
- FluentValidation + AutoMapper
- Serilog (konsol + dosya)
- Redis veya bellek içi dağıtık önbellek
- Rate limiting
- Audit log (EF interceptor)
- Arka plan işi (düşük stok uyarısı, outbox dispatcher)
- Swagger/OpenAPI
- xUnit + FluentAssertions (validator + integration test)
- Docker + docker-compose
- GitHub Actions CI (backend, frontend, docker)

## Proje yapısı

| Proje | Açıklama |
|--------|----------|
| `ShopAPI.API` | Controller, middleware, Program.cs |
| `ShopAPI.Application` | DTO, contract, validator, AutoMapper |
| `ShopAPI.Domain` | Entity ve enumlar |
| `ShopAPI.Infrastructure` | EF Core, servisler (`Services/`), cache, audit, background job |
| `ShopAPI.Tests` | Validator + integration testleri |
| `ShopAPI.Frontend` | React vitrin (gerçek API; admin panel dahil) |

## Yerel çalıştırma

```powershell
dotnet restore ShopAPI.sln
dotnet build ShopAPI.sln -c Release
dotnet run --project ShopAPI.API
```

- Swagger: `http://localhost:5095/swagger`
- Health: `http://localhost:5095/health`
- Log dosyaları: `ShopAPI.API/logs/shopapi-*.log`

### Frontend

```powershell
cd ShopAPI.Frontend
npm install
npm run dev
```

`http://localhost:5173` — giriş, ürün listesi, sepet, checkout ve admin panel backend'e bağlıdır.

Demo giriş: `admin@admin.local` / `Admin123!`

## Yapılandırma

Geliştirme için `appsettings.Development.json` kullanılır. Üretimde secret'ları ortam değişkeni ile verin (örnek: `.env.example`).

| Bölüm | Açıklama |
|--------|----------|
| `ConnectionStrings:DefaultConnection` | SQLite veya PostgreSQL |
| `Jwt:Key` | JWT imza anahtarı (zorunlu) |
| `Payments:Provider` | `mock` veya `stripe` |
| `Stripe:SecretKey` | Stripe test/live secret |
| `Stripe:WebhookSecret` | Stripe webhook imza doğrulama |
| `Stripe:FrontendUrl` | Ödeme dönüş adresi |

```powershell
$env:Jwt__Key="your-secret-min-32-chars"
$env:Stripe__SecretKey="sk_test_..."
dotnet run --project ShopAPI.API
```

## Ödeme akışı

**Mock:** `POST /api/orders` → `POST /api/orders/{id}/pay` → anında `Paid`

**Stripe Checkout:** aynı akış → `checkoutUrl` ile Stripe sayfasına yönlendirme → dönüşte `POST /api/orders/{id}/confirm-payment`

**Stripe Webhook:** `POST /api/payments/stripe-webhook` (imza doğrulamalı)

## API özeti

- `api/auth`, `api/products`, `api/cart`, `api/orders`, `api/addresses`, `api/coupons`
- `GET /api/orders` — admin sipariş listesi
- `PATCH /api/orders/{id}/status` — admin durum güncelleme
- `GET /api/admin/audit-logs` — audit log
- `GET /health` — sağlık kontrolü

## Testler

```powershell
dotnet test ShopAPI.Tests/ShopAPI.Tests.csproj -c Release
```

Integration testler: sipariş oluştur → öde → iptal et (stok iadesi dahil).

## Docker

```powershell
docker compose up --build
```

- API: `http://localhost:8080`
- Redis: `6379`
- PostgreSQL: `5432`

## CI/CD

`.github/workflows/ci.yml` — backend test, frontend build, docker image build.

## Güvenlik notu

Üretimde `Jwt:Key`, Stripe secret'ları ve connection string'leri repoya yazmayın; ortam değişkeni veya secret store kullanın.

_Last updated: 2026-06-05_
