# Стан виконання ЛР 1 — SecureLab — ЗАВЕРШЕНО

## Підсумок

- Проєкт `SecureLab.Api` відтворено з нуля за методичкою (стартовий репозиторій викладача недоступний).
- Етап 3 реалізовано: `GET /api/incidents/severity-summary` 501 → 200, окремий DTO,
  політика повного переліку рівнів, порядок Low/Medium/High/Critical, `?status=` з allowlist → 400,
  структуроване журналювання, кнопка в клієнті з безпечним `textContent` і станами.
- `dotnet build` — чисто; `dotnet test` — **8/8** зелених.
- Git: `main` (merge `a2898d7`), тег **`v0.1.0` → a2898d7**, тег `starter-v0.1.0` → `d3ddeb5`.
  Усе запушено в https://github.com/1307egor-commits/securelab-lab1
- Звіт: `docs/SecureLab_LR1_report.docx` (6 сторінок), `docs/architecture.md`,
  докази в `docs/evidence/`, скриншоти в `docs/screenshots/`.

## Стенд (запущено локально під час виконання)

- PostgreSQL 17.6 портативний: `C:\Users\Admin\pgsql-portable\`
  - запуск: `C:\Users\Admin\pgsql-portable\pgsql\bin\pg_ctl.exe -D C:\Users\Admin\pgsql-portable\data -l C:\Users\Admin\pgsql-portable\pg.log -o "-p 54329 -c listen_addresses=127.0.0.1" start`
  - стоп: `C:\Users\Admin\pgsql-portable\pgsql\bin\pg_ctl.exe -D C:\Users\Admin\pgsql-portable\data stop`
  - БД `securelab`, користувач `securelab`, пароль `local-study-password`, порт 54329
- API: `dotnet run --project src/SecureLab.Api` (Development) на http://localhost:5080
- reset seed: `dotnet run --no-build --project src/SecureLab.Api -- --reset-database`
- тести: `dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj -c Release`

## Docker

Не використано: Docker Desktop на стенді не піднімає рушій (див. розділ «Скриншоти»/пояснення у звіті).
`infra/compose.yaml` збережено для середовища з робочим WSL2/Hyper-V.
