Project Goal
Fork ElmahCoreEx/ElmahCoreEx, replace the embedded legacy UI with a modern Vue 3 + Vite single-page app served via the existing middleware, and add the missing features that the original and the fork never shipped.

Phase 0 — Repo Setup

 verify the solution builds (dotnet build)
 Read the existing embedded resource structure — locate where HTML/JS/CSS are embedded in ElmahCore.Mvc or the main project
 Identify the middleware handler that serves the dashboard (ElmahHandlerFactory, ElmahModule, or equivalent)
 Document every existing API endpoint the UI calls (error list, error detail, delete, RSS, etc.)
 Create a src/ui/ folder at repo root for the Vue app; add it to .gitignore for dist/ output except the one embedded path
 Set up a CONTRIBUTING.md and CHANGELOG.md baseline


Phase 1 — Backend API Cleanup (C#)
1.1 Audit existing endpoints

 List all HTTP handlers: GET /elmah, GET /elmah/detail, GET /elmah/xml, GET /elmah/json, DELETE, etc.
 Confirm JSON response contracts — document them as OpenAPI or plain markdown
 Check pagination contract (currently pageIndex / pageSize query params)

1.2 Missing / broken backend features to fix

 Bulk delete — add DELETE /elmah/bulk accepting a JSON array of error GUIDs
 Delete all — add DELETE /elmah/all with optional filter params
 Search / filter endpoint — GET /elmah?type=&message=&from=&to=&host=&user=&statusCode= — most of these params exist in XML storage but are not exposed via HTTP
 Error count endpoint — GET /elmah/count returning { total, filtered } for badge display
 Mark as reviewed — add a reviewed flag column/field (SQL log only) and PATCH /elmah/{id}/review
 Export endpoint — GET /elmah/export?format=csv|json streaming response
 Health/ping endpoint — GET /elmah/ping returns 200 OK so the UI can show backend connectivity
 Fix CORS headers — add Access-Control-Allow-Origin option for dev mode (Vue dev server proxy)
 Return ETag / Last-Modified on list endpoint for client-side caching
 Consistent error envelope: { success, data, error } across all JSON responses

1.3 SQL Error Log improvements

 Add IsReviewed BIT DEFAULT 0 column to ELMAH_Error table (migration script with IF NOT EXISTS guard)
 Add ApplicationName NVARCHAR(100) column for multi-app support
 Index on TimeUtc DESC, Application, StatusCode for fast filtered queries
 Stored procedure for bulk delete by GUID list using OPENJSON
 Stored procedure for filtered count


Phase 2 — Vue 3 UI Project Setup

 Scaffold with npm create vite@latest ui -- --template vue inside src/ui/
 Install dependencies: vue-router@4, pinia, axios, @vueuse/core, dayjs
 Install UI framework: PrimeVue 4 or Naive UI (pick one — both are lightweight and tree-shakeable)
 Install dev deps: @vitejs/plugin-vue, vite-plugin-html, sass
 Configure vite.config.js — base: '/elmah/', proxy /elmah/api to https://localhost:5000 for dev
 Configure vite build output to ../ElmahCore.Mvc/wwwroot/elmah/ (or embedded resource path)
 Set up ESLint + Prettier
 Set up pinia store structure: useErrorStore, useFilterStore, useSettingsStore
 Set up vue-router with routes: /, /detail/:id, /settings
 Create src/api/elmahApi.js — single Axios instance wrapping all backend calls


Phase 3 — UI Feature Implementation
3.1 Error List Page (main dashboard)
Current state: Basic table, no filtering, no bulk actions, no real-time update.

 Responsive data table with columns: Severity icon, Type, Message, Source, User, Host, Time, Status Code, Actions
 Column visibility toggle (persist in localStorage)
 Sortable columns (client-side for current page; server-side sort param for API)
 Pagination — page size selector (25 / 50 / 100 / 500), page jump input
 Filter bar (collapsible):

 Free-text search (message / type)
 Exception type dropdown (populated from distinct values)
 HTTP status code filter (multi-select: 4xx, 5xx, specific codes)
 Date range picker (from / to)
 Host filter
 User filter
 Application filter (for multi-app SQL logs)


 Active filter chips shown below the bar with individual × dismiss
 Bulk actions toolbar (appears when rows selected):

 Select all on page / select all matching
 Bulk delete selected
 Bulk mark as reviewed
 Export selected as CSV / JSON


 Row-level quick actions: View, Delete, Mark reviewed, Copy GUID
 Error count badge in page title
 Auto-refresh toggle — poll every N seconds (configurable: off / 10s / 30s / 60s)
 New-error indicator: highlight newly appeared rows since last load
 Empty state illustration when no errors
 Loading skeleton rows (not spinner) while fetching
 Toast notifications for delete/review actions

3.2 Error Detail Page
Current state: Raw XML dump with basic stack trace. No navigation between errors.

 Breadcrumb: Dashboard → Error Detail
 Prev / Next navigation between errors in the filtered list
 Tabbed layout:

 Overview — Type, Message, Time, Host, User, URL, HTTP Method, Status Code, Source
 Stack Trace — syntax-highlighted, collapsible frames, link to source file (uses existing SourcePaths feature)
 Request — Headers table, Query String table, Form Data table, Cookies table, Server Variables table
 Request Body — raw body (JSON prettified if applicable)
 Method Parameters — if logged via LogParams()
 Raw XML — collapsible, copy button


 Copy error GUID button
 "Report as Issue" button (configurable GitHub/Jira URL pattern with {id} placeholder)
 Delete error button with confirmation dialog
 Mark as reviewed toggle
 Share link button (copies URL with error ID)

3.3 Dashboard / Stats Page
New feature — does not exist in original.

 Error rate chart: errors per hour/day (line chart using Chart.js or recharts-equivalent)
 Top 10 exception types (bar chart or donut)
 Top 10 URLs with errors
 Top 10 users with errors
 Error count by HTTP status code (grouped bar)
 Heatmap: errors by hour-of-day × day-of-week
 Date range selector for all charts
 Stats are derived from the existing error list API (client-side aggregation) — no new backend endpoint needed initially

3.4 Settings Page
New feature.

 Display current configuration (read-only): log type, path, max errors, source paths
 UI preferences (stored in localStorage): theme, default page size, default sort, auto-refresh interval
 "Test connection" button → calls /elmah/ping
 Link to NuGet package page and GitHub repo

3.5 Global UI Shell

 Sidebar navigation (collapsible): Dashboard, Errors, Stats, Settings
 Top bar: app name/logo, environment badge (Development / Staging / Production based on host), dark/light mode toggle, refresh button
 Dark mode (CSS variables based, persisted)
 Keyboard shortcuts: R refresh, F focus filter, Escape clear filter, ? show shortcut help modal
 Error boundary component: catches Vue render errors, shows fallback UI
 Offline/disconnected banner when /elmah/ping fails


Phase 4 — Embedding the Built UI

 Add MSBuild Target in the .csproj to run npm run build before publish (optional — can also commit built assets)
 Register built index.html and assets as embedded resources or as static files under wwwroot
 Update the middleware to serve index.html for all non-API routes under /elmah/* (SPA fallback)
 Ensure base path in Vue router matches the configured Elmah path (pass it via a <script>window.__ELMAH_BASE__</script> injection in the middleware)
 Verify the UI works when Elmah is mounted at a custom path (not /elmah)


Phase 5 — Auth & Security

 Respect existing OnPermissionCheck — all API endpoints must go through the same check
 Add CSP headers to embedded UI responses
 Sanitize all error message / stack trace HTML before rendering (use DOMPurify in Vue)
 Add X-Frame-Options: DENY to UI responses
 Rate-limit the delete endpoints (optional middleware)


Phase 6 — Testing
Backend

 Unit tests for new bulk delete stored procedure
 Unit tests for filter query building
 Integration tests for new API endpoints using WebApplicationFactory

Frontend

 Unit tests with Vitest for store actions and API wrapper
 Component tests with @vue/test-utils for the filter bar, error table, error detail tabs
 E2E tests with Playwright against the demo .NET app


Phase 7 — Documentation & Release

 Update README.md with new UI screenshots
 Document all new configuration options
 Document API contract (endpoints, request/response shapes)
 Write migration guide: "upgrading from ElmahCore / ElmahCoreEx"
 Tag v3.0.0, publish updated NuGet packages: ElmahCoreEx.Common, ElmahCoreEx.Sql, etc.
 Add GitHub Actions CI: dotnet build + dotnet test + npm run build + npm test


Known Gaps in Current ElmahCoreEx to Address
GapWhere to fixNo search/filter in UIPhase 1.2 + Phase 3.1No bulk deletePhase 1.2 + Phase 3.1No dark modePhase 3.5No stats / chartsPhase 3.3Stack trace is unstyled raw textPhase 3.2Request body not shown in UI (only logged)Phase 3.2Method parameters not shown in UIPhase 3.2No auto-refreshPhase 3.1No exportPhase 1.2 + Phase 3.1No multi-app filteringPhase 1.3 + Phase 3.1No keyboard shortcutsPhase 3.5No error "review/acknowledge" workflowPhase 1.2 + Phase 3.1

Suggested Tech Decisions Summary
ConcernChoiceReasonFrontend frameworkVue 3 (Composition API)Already in useBuild toolViteFast, simple embeddingUI component libPrimeVue 4 or Naive UITree-shakeable, dark mode built-inState managementPiniaVue 3 standardHTTP clientAxiosInterceptors for auth/error handlingChartsChart.js via vue-chartjsLightweightDate handlingDay.jsSmall, BS/AD friendly with pluginsTesting (FE)Vitest + @vue/test-utils + PlaywrightTesting (BE)xUnit + WebApplicationFactoryAlready in project