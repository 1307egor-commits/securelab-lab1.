# Карта архітектури — SecureLab, варіант 2-A (DEL-02)

Документ дає змогу знайти змінений маршрут без читання всього коду.

## 1. Компоненти

| Компонент | Що це | Де в репозиторії |
|---|---|---|
| Browser client | Одна HTML-сторінка + Vanilla JS, без фреймворків | `src/SecureLab.Api/Client/` |
| ASP.NET Core Web API | Minimal API, .NET 10, Kestrel на `http://localhost:5080` | `src/SecureLab.Api/` |
| EF Core | ORM, провайдер Npgsql; одна міграція `InitialCreate` | `src/SecureLab.Api/Data/` |
| PostgreSQL | Локальна БД `securelab` на `127.0.0.1:54329` | штатно `infra/compose.yaml`; на цьому стенді — портативні бінарники (див. звіт) |

Процес API і сервер PostgreSQL — **окремі** компоненти: успішний запуск API не
доводить доступність БД, тому є окремий `GET /health`.

## 2. Вибраний маршрут (реалізована точка розширення)

`GET /api/incidents/severity-summary` — підсумок кількості інцидентів за рівнем
критичності.

```
кнопка «Оновити підсумок» у Client/index.html
  → обробник click → loadSeveritySummary() у Client/app.js
  → GET /api/incidents/severity-summary        (fetch, без body)
  → IncidentEndpoints.GetSeveritySummaryAsync  (routing, перевірка ?status= за allowlist)
  → IncidentQueries.GetSeveritySummaryAsync    (AsNoTracking, GroupBy, Count, ToListAsync)
  → SecureLabDbContext.Incidents  →  PostgreSQL: таблиця incidents
  → доповнення відсутніх рівнів нулями + порядок Low/Medium/High/Critical
  → List<IncidentSeveritySummaryResponse>  →  JSON-масив
  → renderChild: li.textContent = `${severity}: ${count}`  →  DOM (<ul id="severity-summary-list">)
```

## 3. Ключові файли

| Рівень | Файл | Символ |
|---|---|---|
| Клієнт (розмітка) | `src/SecureLab.Api/Client/index.html` | `#load-severity-summary`, `#severity-summary-list` |
| Клієнт (логіка) | `src/SecureLab.Api/Client/app.js` | `loadSeveritySummary()`, `apiFetch()` |
| Endpoint | `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs` | `GetSeveritySummaryAsync`, `SummaryStatusAllowlist` |
| Application layer | `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs` | `GetSeveritySummaryAsync`, `SeverityOrder` |
| Response DTO | `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs` | `IncidentSeveritySummaryResponse` |
| DbContext | `src/SecureLab.Api/Data/SecureLabDbContext.cs` | `DbSet<Incident> Incidents`, `ToTable("incidents")`, `HasConversion<string>()` |
| Таблиця | PostgreSQL | `incidents` |
| Seed / reset | `src/SecureLab.Api/Data/DbSeeder.cs` | `SeedAsync`, `ResetAsync` |

Готовий сусідній приклад того ж класу — фільтр списку
`GET /api/incidents?status=...` (`IncidentEndpoints.GetListAsync` →
`IncidentQueries.GetListAsync`), і гілка відсутнього ресурсу
`GET /api/incidents/{id:guid}` → 404 Problem Details.

## 4. Межі довіри

| Межа / перехід | Дані, що її перетинають | Чого не можна припускати | Контроль у цьому маршруті |
|---|---|---|---|
| браузер → API | method, URL, `?status=` | «клієнт надішле лише значення зі `<select>`» | `?status=` перевіряється за явним allowlist в endpoint; невідоме → 400 Validation Problem Details |
| API → PostgreSQL | `id`, умова `Where`, ключ `GroupBy` | «збережений текст автоматично безпечний» | параметризація EF Core, `AsNoTracking()`, агрегат лише `severity` + `count` (не `Incident`) |
| API → браузер | JSON із полями підсумку / details | «право прочитати entity = право одержати всі поля» | окремі DTO: `IncidentSeveritySummaryResponse`, `IncidentDetailsResponse` без `OwnerUserId`, email, внутрішніх коментарів |
| дані response → DOM | текстові значення з JSON | «текст можна вставити як HTML» | лише `textContent` / `document.createTextNode`; тест `ClientScript_DoesNotUseDangerousInnerHtmlSink` забороняє `innerHTML` тощо |
| конфігурація → API | connection string | «локальна конфігурація придатна для іншого середовища» | базове значення у `appsettings.Development.json`; перевизначення лише через env `ConnectionStrings__SecureLab`; реальні значення не в Git |

Frontend лише допомагає сформувати коректний request; будь-який HTTP-клієнт може
повторити його без форми й кнопки.

## 5. Конфігураційні входи (без реальних значень)

| Вхід | Файл / місце | Призначення |
|---|---|---|
| Вибір .NET SDK | `global.json` | смуга SDK і політика `rollForward` |
| Логи, ліміти | `src/SecureLab.Api/appsettings.json` | базові налаштування |
| Connection string (локальний baseline) | `src/SecureLab.Api/appsettings.Development.json` → `ConnectionStrings:SecureLab` | відкриті навчальні credentials лише для `127.0.0.1` |
| Перевизначення connection string | env `ConnectionStrings__SecureLab` | реальне значення поза Git |
| PostgreSQL (порт, пароль) | `infra/compose.yaml`, `infra/.env.example` (`POSTGRES_PORT`) | локальний контейнер БД |
| Профіль запуску | `src/SecureLab.Api/Properties/launchSettings.json` | `applicationUrl=http://localhost:5080`, `ASPNETCORE_ENVIRONMENT=Development` |

## 6. Повернення до відомого seed-стану

```bash
dotnet run --no-build --project src/SecureLab.Api -- --reset-database
```

Працює лише в Development. Застосовує міграції, очищує відомі навчальні таблиці
(`incident_status_changes`, `incident_comments`, `incidents`, `users`) і повторно
заповнює їх фіксованими seed-значеннями (ті самі UUID, що у `DbSeeder`).
Той самий `ResetAsync` викликає `SecureLabApiFactory` перед інтеграційними тестами.
