# Стан виконання ЛР 1 — SecureLab (для відновлення сесії)

> Цей файл веде Claude, щоб після перезавантаження ПК / нової сесії продовжити
> роботу точно з того місця, де зупинилися. Після кожного етапу — оновлювати.

## Контекст

- Завдання: «Методичні рекомендації ЛР 01» (файл був у `E:\Загрузки\Методичні_рекомендації_ЛР_01.docx`).
- Стартовий приватний репозиторій викладача **недоступний**, тому проєкт
  `SecureLab.Api` **відтворено з нуля** за детальним описом у методичці.
- Репозиторій: https://github.com/1307egor-commits/securelab-lab1
- Локальна папка: `C:\Users\Admin\source\repos\securelab-lab1`

## Обрані рішення (узгоджено за замовчуванням, підтвердити з користувачем за потреби)

- **Рівень**: «добрий» (окремий response DTO; необов'язковий `?status=` з allowlist і 400
  на некоректне значення; ≥4 HTTP-сценарії; структуроване журналювання; стани
  завантаження / порожньо / помилка в клієнті).
- **Політика нульових груп** для `severity-summary`: **повний перелік рівнів**
  (відсутні severity доповнюються елементами з `count: 0`).
  На baseline seed: Low=1, Medium=1, High=1, Critical=0.
- **Сталий порядок**: явний порядок критичності `Low, Medium, High, Critical`
  (після матеріалізації агрегату), задокументований у контракті.
- **SDK**: встановлено лише .NET 10.0.400 (смуга 10.0.4xx). `global.json`
  припнято до 10.0.400 `rollForward: latestMinor`. Методичка вимагає 10.0.302 —
  відхилення зафіксувати у звіті.
- **PostgreSQL**: Docker Desktop НЕ працює — на ПК вимкнено апаратну
  віртуалізацію (AMD SVM у BIOS) + вимкнено компоненти Windows
  (VirtualMachinePlatform / WSL2 / Hyper-V). Варіанти: (1) увімкнути SVM у BIOS +
  компоненти Windows і використати Docker Compose як у методичці; (2) портативний
  PostgreSQL без Docker; (3) `winget install PostgreSQL`. **Рішення користувача ще
  очікується.**

## Зроблено

- [x] Структура рішення: `SecureLab.slnx`, `global.json`, `.gitignore`, `.config/dotnet-tools.json` (dotnet-ef 10.0.0)
- [x] `src/SecureLab.Api` — усі файли baseline:
  - Data: `Incident`, `User`, `IncidentComment`, `IncidentStatusChange`, `IncidentSeverity`, `IncidentStatus`, `SecureLabDbContext` (таблиця `incidents`, enum→text), `DbSeeder` (seed + `--reset-database`)
  - Presentation: `Contracts/IncidentResponses.cs`, `Endpoints/IncidentEndpoints.cs`
  - Application: `Incidents/IncidentQueries.cs`
  - `Program.cs` (Npgsql, `/health`, OpenAPI+Scalar, статика клієнта, fallback на index.html)
  - `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (порт 5080)
  - `Client/index.html`, `Client/app.js` (лише `textContent`/`createTextNode`), `Client/styles.css`
  - `Data/Migrations/*InitialCreate*` — згенеровано
- [x] `tests/SecureLab.Api.Tests` — `SecureLabApiFactory` + `IncidentEndpointTests` (4 baseline-тести)
- [x] `tests/http/incidents.http`
- [x] `infra/compose.yaml` (postgres:17-alpine, порт 54329, `securelab`/`local-study-password`), `infra/.env.example`
- [x] `scripts/test.sh`
- [x] `dotnet build` — **успішно, 0 помилок, 0 попереджень**
- [x] Git-коміт baseline на `main`, тег `starter-v0.1.0`, гілка `lab/1-system`, push

## Далі (у цьому порядку)

1. **Підняти PostgreSQL** (за рішенням користувача).
2. `dotnet run --project src/SecureLab.Api` → перевірити `/health`, `/`, `/scalar/v1`,
   `GET /api/incidents`, фільтри, `/{id}`, 404, 400, і `severity-summary` → **501** (доказ «до»).
   Зберегти скриншоти/записи HTTP-обміну.
3. **Етап 3** на гілці `lab/1-system`:
   - `Presentation/Contracts/IncidentResponses.cs`: додати `public sealed record IncidentSeveritySummaryResponse(string Severity, int Count);`
   - `Application/Incidents/IncidentQueries.cs`: `GetSeveritySummaryAsync(IncidentStatus? status, CancellationToken)` —
     `dbContext.Incidents.AsNoTracking()` → (опц. `Where` за status) → `GroupBy(i => i.Severity)` →
     `Select(g => new { g.Key, Count = g.Count() })` → `ToListAsync(ct)` →
     доповнити відсутні severity нулями → упорядкувати `Low, Medium, High, Critical` →
     спроєктувати в `IncidentSeveritySummaryResponse`. Структурований `LogInformation` з кількістю груп.
   - `Presentation/Endpoints/IncidentEndpoints.cs`: замінити baseline-заготовку `GetSeveritySummaryAsync`
     на робочий метод (DI `IncidentQueries`, `CancellationToken`, `?status=` через `Enum.TryParse`+`Enum.IsDefined`
     → 400 Validation Problem Details для некоректного). Прибрати `.ProducesProblem(501)`,
     додати `.Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()` та `.ProducesValidationProblem()`.
   - `Client/index.html`: кнопка `#load-severity-summary`, `#severity-summary-status`, `<ul id="severity-summary-list">`.
   - `Client/app.js`: `loadSeveritySummary()` — стан «Завантаження…», `apiFetch("/api/incidents/severity-summary")`,
     стан «Даних немає» для `[]`, рядки через `textContent` (`li.textContent = summary.severity + ": " + summary.count`),
     `catch` — коротке фіксоване повідомлення; підписати на `click` кнопки.
   - `tests/SecureLab.Api.Tests/IncidentEndpointTests.cs`: додати тест(и) severity-summary
     (200, поля, порядок, повний перелік з Critical=0; 400 для некоректного `?status=`).
   - `tests/http/incidents.http`: коментар 501→200 вже підготовлено, перевірити.
4. `docs/architecture.md` (DEL-02), `docs/report-template.md` заповнити, `README.md`.
5. `dotnet test` — усі зелені (4 baseline + нові).
6. Скриншоти «після»: severity-summary 200, кнопка в клієнті, DevTools Network, `<script>` як текст.
7. Merge `lab/1-system` → `main` (`--no-ff`), тег `v0.1.0`, push (гілка + main + тег).
8. Зібрати **звіт `.docx`** зі скриншотами і фрагментами коду; віддати користувачеві.

## Команди для відновлення стану стенда

```bash
# PostgreSQL (варіант Docker):
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
# API:
dotnet run --project src/SecureLab.Api
# reset seed:
dotnet run --no-build --project src/SecureLab.Api -- --reset-database
# тести:
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
```
