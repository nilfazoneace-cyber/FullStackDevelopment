# BugBoard

A small defect tracker. **React 19 + TypeScript SPA** (`frontend/`) calling an **ASP.NET Core 10 Web API** (`backend/`) that is secured with **JWT bearer tokens** and two roles: **Tester** and **Lead**.

| Layer | Technology |
|---|---|
| API | ASP.NET Core Web API (.NET 10), controllers, C# |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer`, HS256 tokens issued by the API, `PasswordHasher<User>` |
| Data | EF Core + SQLite, with an `InitialCreate` migration and seed data |
| API docs | Built-in OpenAPI + Scalar UI (with a bearer-token box) |
| Frontend | React 19, TypeScript, Vite, React Router |
| Tests | xUnit + `WebApplicationFactory` (backend), Vitest + React Testing Library (frontend), Playwright (end-to-end) |

---

## Seed credentials (development only)

| Role | Email | Password |
|---|---|---|
| Tester | `tester1@bugboard.local` | `Tester1Pass!` |
| Tester | `tester2@bugboard.local` | `Tester2Pass!` |
| Lead | `lead@bugboard.local` | `LeadPass123!` |

These are created automatically (and hashed) the first time the API starts in the **Development** environment. 12 sample bugs are seeded too, so filters and paging have something to show.

## Run it

You need the .NET 10 SDK and Node.js LTS.

### 1. Backend

```bash
cd backend/BugBoard.Api

# One-time: store the JWT signing key OUTSIDE the repo (needs to be 32+ characters)
dotnet user-secrets set "Jwt:Key" "put-a-long-random-string-of-at-least-32-characters-here"

dotnet run --launch-profile http
```

- API: <http://localhost:5082>
- Interactive docs: <http://localhost:5082/scalar/v1> (log in via `POST /api/auth/login`, copy `accessToken`, click **Authentication** and paste it)
- Raw OpenAPI document: <http://localhost:5082/openapi/v1.json>
- The SQLite file `bugboard.db` is created and migrated automatically on first start. Delete it to reset the data.

If the key is missing or too short the app refuses to start and tells you exactly what to do.

On another machine or in CI you can use an environment variable instead of user-secrets: `Jwt__Key=<secret>` (double underscore).

### 2. Frontend

```bash
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. The API address comes from `frontend/.env` (`VITE_API_URL=http://localhost:5082`). The port is fixed on purpose: the API's CORS policy only allows `http://localhost:5173`.

## Run the tests

```bash
# Backend: 41 integration tests (each starts the real API in memory with a throw-away SQLite file)
cd backend && dotnet test

# Frontend: 35 unit/component tests
cd frontend && npm test

# End to end: one Playwright test (starts the API and Vite itself, or reuses them if already running)
cd frontend && npx playwright install chromium   # first time only
npm run e2e
```

## Project layout

```
backend/
  BugBoard.Api/
    Program.cs          app wiring: services, security, middleware order
    Domain/             entities (User, Bug) and enums
    Data/               DbContext, seeder, (design-time factory for `dotnet ef`)
    Dtos/               request/response shapes (what crosses the wire)
    Services/           business rules (BugService), login (AuthService), JWT creation (TokenService)
    Controllers/        thin HTTP layer: routes, status codes
    Infrastructure/     error handling, validation response, OpenAPI bearer scheme, policy names
    Migrations/         EF Core migration (InitialCreate)
  BugBoard.Tests/       xUnit + WebApplicationFactory
frontend/
  src/api.ts            the ONLY place that calls fetch()
  src/types.ts          all API types in one place
  src/auth/             AuthProvider (token in memory), ProtectedRoute
  src/pages/            Login, BugList, BugForm (new + edit), NotFound
  src/validation.ts     client-side rules mirroring the API
  e2e/                  Playwright test
```

## API summary

| Method | Route | Who | Success | Errors |
|---|---|---|---|---|
| POST | `/api/auth/login` | anyone | 200 `{ accessToken, expiresAt, user }` | 400, 401 |
| GET | `/api/auth/me` | signed in | 200 | 401 |
| GET | `/api/users` | signed in | 200 | 401 |
| GET | `/api/bugs?status=&severity=&search=&page=&pageSize=` | signed in | 200 `{ items, total, page, pageSize }` | 400, 401 |
| GET | `/api/bugs/{id}` | signed in | 200 | 401, 404 |
| POST | `/api/bugs` | signed in | 201 + `Location` | 400, 401 |
| PUT | `/api/bugs/{id}` | reporter or Lead | 200 | 400, 401, 403, 404, 409 |
| DELETE | `/api/bugs/{id}` | Lead only | 204 | 401, 403, 404 |

## Hard constraints and where they are enforced

| Constraint | Where |
|---|---|
| Token in memory only | `frontend/src/auth/AuthProvider.tsx` (React state + a ref; a test asserts `localStorage`/`sessionStorage` stay empty) |
| No secrets in the repo | `Jwt:Key` from user-secrets / env var; `Program.cs` validates it on start |
| CORS locked down | `Program.cs` `AddCors` allows only `Cors:AllowedOrigins` (`http://localhost:5173`) |
| Server enforces the rules | `BugService` + `[Authorize(Policy = "LeadOnly")]`; the UI only mirrors them for convenience |
