![Build Status](https://github.com/meoptimus/ElmahCoreX/actions/workflows/build.yml/badge.svg)
![Latest Version](https://img.shields.io/nuget/v/ElmahCoreX?style=flat-square)
![Version Pre-release](https://img.shields.io/nuget/vpre/ElmahCoreX?style=flat-square)
![Last Commit](https://img.shields.io/github/last-commit/meoptimus/ElmahCoreX?style=flat-square)


![License](https://img.shields.io/github/license/meoptimus/ElmahCoreX)
![Contributors](https://img.shields.io/github/contributors/meoptimus/ElmahCoreX?style=flat-square)

# About

This is a fork of the [ElmahCore](https://github.com/ElmahCore/ElmahCore) project, with changes for PR's that were sitting idle.
It should be an almost slot in replacement for ElmahCore v2.1.2.

See [changelog.md](changelog.md) for any further changes from v2.1.2

The interfaces and namespaces have been kept the same.

# License

This project is licensed under the terms of the Apache License 2.0.

# Frontend UI

The front end has been completely rewritten in Vue 3 with a modern, minimalist design. The source code for the SPA is fully available in the `src/ui` folder. It can be built via `npm install` and `npm run build` which will output the compiled assets to the `ElmahCore.Mvc/wwwroot` folder as embedded resources.

# Using ElmahCore

ELMAH for Net Core 10 

Add NuGet package [ElmahCoreX](https://www.nuget.org/packages?q=ElmahCoreX)

## Simple usage

```csharp
// Startup.cs
services.AddElmah() //in ConfigureServices
// ...
app.UseElmah(); // in Configure, must be positioned after initializing other exception handlers
                // such as UseExceptionHandler and UseDeveloperExceptionPage
```

Default ELMAH endpoint path `~/elmah`.

## Change URL path

```csharp
services.AddElmah(options => options.Path = "you_path_here")
```

## Restrict access to the ELMAH URL

```csharp
services.AddElmah(options =>
{
    options.OnPermissionCheck = context => context.User.Identity.IsAuthenticated;
});
```

```csharp
// startup.cs
app.UseAuthentication();
app.UseAuthorization();
//...
app.UseElmah(); // needs to be positioned after `UseAuthentication` and `UseAuthorization`
```
or the user will be redirected to the sign-in screen even if they are authenticated.

## Change Error Log type

You can implement a custom error log adapter to write logs to alternate locations.

```csharp
public class MyErrorLog: ErrorLog {
    // Implement ErrorLog
}    
```

The ErrorLog adapters available:

- **MemoryErrorLog** – store errors in memory (by default)
- **XmlFileErrorLog** – store errors in XML files.
- **SqlErrorLog** - store errors in MS SQL (add reference to [ElmahCoreEx.Sql](https://www.nuget.org/packages/ElmahCoreEx.Sql))
- **MysqlErrorLog** - store errors in MySQL (add reference to [ElmahCoreEx.MySql](https://www.nuget.org/packages/ElmahCoreEx.MySql))
- **PgsqlErrorLog** - store errors in PostgreSQL (add reference to [ElmahCoreEx.Postgresql](https://www.nuget.org/packages/ElmahCoreEx.Postgresql))

Example to configure for XML:

```csharp
services.AddElmah<XmlFileErrorLog>(options =>
{
  options.LogPath = "~/log"; // OR options.LogPath = "с:\errors";
});
```

Example to configure For MSSQL

```csharp
services.AddElmah<SqlErrorLog>(options =>
{
  options.ConnectionString = "connection_string";
  options.SqlServerDatabaseSchemaName = "Errors"; // Defaults to dbo if not set
  options.SqlServerDatabaseTableName = "ElmahError"; // Defaults to ELMAH_Error if not set
});
```

## Disable Full XML Logging

For high-volume applications, you can disable storing the full XML representation of errors in the database to reduce storage requirements. When disabled, a minimal placeholder XML is stored instead. The basic error fields (type, message, source, user, time, statusCode) are still stored in dedicated database columns.

```csharp
services.AddElmah<SqlErrorLog>(options =>
{
  options.ConnectionString = "connection_string";
  options.LogAllXml = false; // Disable full XML storage
});
```

This option works with `SqlErrorLog`, `MySqlErrorLog`, and `PgsqlErrorLog`. Default is `true` for backward compatibility.

## Raise exception

To raise a custom exception to log:

```csharp
public IActionResult RaiseCustomExceptionExample()
{
    HttpContext.RaiseError(new InvalidOperationException("Test"));
}
```

## Microsoft.Extensions.Logging support

ElmahCoreEx support `Microsoft.Extensions.Logging`

## Source Preview

ElmahCoreEx support source preview.
Add paths to source files example:

```csharp
services.AddElmah(options =>
{
   options.SourcePaths = new []
   {
      @"D:\src\ElmahCore.DemoCore6",
      @"D:\src\ElmahCore.Mvc",
      @"D:\src\ElmahCore"
   };
});
```

## Logging request body

V2.0.5+ ElmahCoreEx can log the request body.

## Logging SQL request body

v2.0.6+ ElmahCoreEx can log the SQL request body.

## Logging method parameters

V2.0.6+ ElmahCoreEx can log method parameters.

```csharp
using ElmahCore;
...

public void TestMethod(string p1, int p2)
{
    // Logging method parameters
    this.LogParams((nameof(p1), p1), (nameof(p2), p2));
    ...
}
```

## Using UseElmahExceptionPage

You can replace `UseDeveloperExceptionPage` to `UseElmahExceptionPage`

```csharp
if (env.IsDevelopment())
{
   // app.UseDeveloperExceptionPage();
   app.UseElmahExceptionPage();
}
```

## Using Notifiers

You can create custom notifiers by implementing `IErrorNotifier` or `IErrorNotifierWithId` interface and add notifier to Elmah options:

```csharp
services.AddElmah<XmlFileErrorLog>(options =>
{
    options.Path = @"errors";
    options.LogPath = "~/logs";
    options.Notifiers.Add(new ErrorMailNotifier("Email",emailOptions));
});
```
Each notifier must have a unique name.

## Using Filters

You can use Elmah XML filter configuration in a separate file, create and add custom filters:

```csharp
services.AddElmah<XmlFileErrorLog>(options =>
{
    options.FiltersConfig = "elmah.xml";
    options.Filters.Add(new MyFilter());
})
```
Custom filter must implement `IErrorFilter`.
An XML filter config example:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<elmah>
 <errorFilter>
  <notifiers>
   <notifier name="Email"/>
  </notifiers>
  <test>
   <and>
    <greater binding="HttpStatusCode" value="399" type="Int32" />
    <lesser  binding="HttpStatusCode" value="500" type="Int32" />
   </and>
  </test>
 </errorFilter>
</elmah>
```

see more [here](https://elmah.github.io/a/error-filtering/examples/)

JavaScript filters have not been implemented

Attach notifiers to the errorFilter node if you wish to avoid sending notifications.
Errors that are filtered will be recorded but not dispatched.
