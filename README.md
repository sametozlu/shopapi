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
- Arka plan işi (düşük stok uyarısı)
- Swagger/OpenAPI
- xUnit + FluentAssertions
- Docker + docker-compose
- GitHub Actions CI

## Proje yapısı

| Proje | Açıklama |
|--------|----------|
| `ShopAPI.API` | Controller, middleware, Program.cs |
| `ShopAPI.Application` | DTO, contract, validator, AutoMapper |
| `ShopAPI.Domain` | Entity ve enumlar |
| `ShopAPI.Infrastructure` | EF Core, servisler, cache, audit, background job |
| `ShopAPI.Tests` | Validator birim testleri |
| `ShopAPI.Frontend` | React vitrin (DummyJSON ürünleri; giriş ShopAPI’ye) |

## Yerel çalıştırma

```powershell
dotnet restore ShopAPI.sln
dotnet build ShopAPI.sln -c Release
dotnet run --project ShopAPI.API
```

- Swagger: `http://localhost:5095/swagger` (port `launchSettings.json`’a göre değişebilir)
- Log dosyaları: `ShopAPI.API/logs/shopapi-*.log`

### Frontend

```powershell
cd ShopAPI.Frontend
npm install
npm run dev
```

`http://localhost:5173` — giriş backend’e gider; ürün listesi DummyJSON’dan gelir.

Login sırasında backend `refreshToken` da döner ve frontend onu `localStorage`’a (`shopapi-refresh-token`) kaydeder.

## Yapılandırma (`ShopAPI.API/appsettings.json`)

| Bölüm | Açıklama |
|--------|----------|
| `ConnectionStrings:DefaultConnection` | SQLite: `Data Source=shopapi.db` veya PostgreSQL: `Host=...` |
| `Jwt` | Issuer, audience, key, access/refresh süreleri |
| `Redis:Enabled` | `true` → Redis; `false` → bellek içi cache |
| `Redis:ConnectionString` | Örn. `localhost:6379` |
| `RateLimiting` | Pencere ve istek limiti |
| `StockAlert` | Arka plan işi eşiği ve aralık |
| `Payments` | `Provider` (`mock` veya `stripe`) ve webhook secret |
| `Stripe` | `SecretKey`, `FrontendUrl` (ödeme dönüş adresi) |
| `Serilog` | Minimum seviye vb. |

Stripe Checkout (kart ekranı) için:

```json
"Payments": {
  "Provider": "stripe"
},
"Stripe": {
  "SecretKey": "sk_test_...",
  "FrontendUrl": "http://localhost:5173"
}
```

Akış: `POST /api/orders` → `POST /api/orders/{id}/pay` → Stripe Checkout sayfasına yönlendirme → dönüşte `POST /api/orders/{id}/confirm-payment?sessionId=...`

Mock modda (`Provider: mock`) ödeme ekranı açılmaz; sipariş anında tamamlanır.

## Seed veriler

İlk açılışta `EnsureCreated` + seed:

- Kategoriler: Electronics, Gaming, Home
- Örnek ürünler
- Admin: `admin@admin.local` / `Admin123!`

## API özeti

### Kimlik

| Metot | Yol | Açıklama |
|--------|-----|----------|
| POST | `/api/auth/register` | Kayıt |
| POST | `/api/auth/login` | JWT + refresh token |
| POST | `/api/auth/refresh` | Body: `{ "refreshToken": "..." }` |
| POST | `/api/auth/revoke` | Refresh token iptal (Authorize) |

Login/register yanıtı: `token`, `refreshToken`, `email`, `role`, `expiresAt`.

### Diğer

- `api/categories`, `api/products` (arama, filtre, sıralama, sayfalama)
- `api/cart`, `api/orders`
- `GET /api/admin/audit-logs` — yalnızca Admin rolü
- `POST /api/orders/{id}/pay` — mock: anında ödeme; stripe: `checkoutUrl` döner
- `POST /api/orders/{id}/confirm-payment` — Stripe dönüşünde ödemeyi doğrular
- `POST /api/orders/{id}/cancel` — kullanıcı sipariş iptali
- `api/addresses` — kullanıcı adres CRUD
- `api/coupons` — kupon doğrulama/yönetim
- `api/products/{id}/variants` — SKU/varyant yönetimi

### Profesyonel özellikler

- **Rate limiting:** Tüm API’ye sabit pencere limiti (`Program.cs`)
- **Cache:** Ürün listesi `ProductCacheService` ile önbelleklenir
- **Audit log:** `SaveChanges` öncesi değişiklikler `AuditLogs` tablosuna yazılır
- **Stok alarmı:** `StockAlertBackgroundService` eşik altı stokları Serilog ile yazar
- **Global hata:** Exception middleware
- **Sipariş akışı:** `Pending -> Paid -> Shipped / Cancelled`
- **Ödeme provider seçimi:** `MockPaymentGateway` veya `StripePaymentGateway`
- **Checkout:** adres + kargo metodu + kupon kodu ile sipariş oluşturma
- **DTO response modeli:** ürün, sepet ve sipariş yanıtları entity yerine DTO döner

## Migration komutları

```powershell
dotnet ef migrations add <Name> --project ShopAPI.Infrastructure --startup-project ShopAPI.API --output-dir Persistence/Migrations
dotnet ef database update --project ShopAPI.Infrastructure --startup-project ShopAPI.API
```

Uygulama başlangıcında otomatik olarak `Database.Migrate()` çalışır.

## Testler

```powershell
dotnet test ShopAPI.Tests/ShopAPI.Tests.csproj -c Release
```

## Docker

```powershell
docker compose up --build
```

- API: `http://localhost:8080`
- Redis: `6379`
- PostgreSQL: `5432`

## CI/CD

`.github/workflows/ci.yml` — `main`/`master` push ve PR’da restore, Release build, test.

## Güvenlik notu

Üretimde `Jwt:Key`, connection string ve Redis şifrelerini ortam değişkeni veya secret store ile verin; repodaki örnek değerleri kullanmayın.

_Last updated: 2026-06-02_
