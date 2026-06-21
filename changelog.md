# Release Notes

## 2.2.8

### Summary

UI enhancements and dashboard layout refinements.

### Changes

- Nested action buttons and error messages into a clean two-row stacked layout in the detail panel.
- Increased list pane width to 380px and removed JavaScript-level message truncation to maximize visible information.
- Updated list pane typography to use high contrast dark black colors.
- Integrated official Tux vector SVG for Linux in system badges.

## 2.2.7

### Summary

UI redesign and code cleanup.

### Changes

- Redesigned the error monitoring dashboard with a minimalist interface.
- Removed dark mode and unnecessary comments.
- Fixed compiler warnings (CS0618 and ASP0019).
- Cleaned up git tracking for Vue compiled assets.

## 2.1.5

### Summary

Migrate all projects to .NET 10, update dependencies and adjust solution structure

### Changes

- Migrate to net10
- Update dependencies
- Add `LogAllXml` option to disable full XML logging in database error logs
- Add optional `statusCode` parameter to `RaiseError()` methods for custom HTTP status codes

## 2.1.4-beta 1

## Summary

### Breaking changes

- Upgrade to net8 dependencies

### Maintenance

- Bump packages to current releases

### Fixes

- Fix minor concurrency bug.


## 2.1.3 -- Changes from ElmahCore 2.1.2

### Summary

Drop in replacement for existing ElmahCore with collection of outstanding PR's merged in.

### Changes

- Fix to improve memory usage with large Serialization payload (#4)

#### 2.1.3-alpha.2

- refactor: Refactoring to statics, parser.
- refactor: Drop Dictionary cache (No LRU)
- perf: Set regex to compiled.
- chore: Bump HtmlAgility to 1.11.48, system.Text.Json 6.0.8
- fix: UserAgent < 3 characters will not throw exception. [#60](https://github.com/ElmahCore/ElmahCore/issues/168)


#### 2.1.3-alpha.1

- Dropped support for net core 3.1, 5.0 (unsupported from Microsoft)
- Tests run under .NET6 [#162](https://github.com/ElmahCore/ElmahCore/pull/162)
- Update other package dependencies

### Fixes

- Increase size of MySQL data length for Message to TEXT and AllXML [#1]
- Updated ELMAHCore to use `Microsoft.Data.SqlClient` [#157](https://github.com/ElmahCore/ElmahCore/pull/163)
- Fix a stack trace high CPU usage bug [#158](https://github.com/ElmahCore/ElmahCore/pull/164)
- Make ErrorTextFormatter public [#165](https://github.com/ElmahCore/ElmahCore/pull/165)