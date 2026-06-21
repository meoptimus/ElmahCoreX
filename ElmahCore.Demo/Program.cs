using ElmahCore;
using ElmahCore.Demo;
using ElmahCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddElmah<XmlFileErrorLog>(options =>
{
    options.LogPath = "~/log";
    options.Notifiers.Add(new MyNotifier());
    options.Filters.Add(new CmsErrorLogFilter());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseElmahExceptionPage();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseElmah();

app.MapRazorPages();

app.MapGet("/seed-errors", async (HttpContext context, ErrorLog errorLog) =>
{
    await SeedSampleErrors(errorLog);
    return Results.Ok("Successfully seeded 20 sample errors. Refresh your ElmahCore dashboard to view them!");
});

app.Run();

async Task SeedSampleErrors(ErrorLog log)
{
    var exceptionTypes = new (string typeName, string message, string source, string method, string path, int statusCode, string user)[]
    {
        ("System.NullReferenceException", "Object reference not set to an instance of an object.", "ElmahCore.Demo", "UserService.GetUserProfile", "/api/users/profile", 500, "SUMAN-PC\\Suman"),
        ("System.ArgumentNullException", "Value cannot be null. (Parameter 'orderId')", "ElmahCore.Demo", "OrderController.Checkout", "/checkout/process", 400, "SUMAN-PC\\Suman"),
        ("Microsoft.Data.SqlClient.SqlException", "Execution Timeout Expired. The timeout period elapsed prior to completion of the operation or the server is not responding.", "System.Data", "ProductRepository.ListProducts", "/products", 500, ""),
        ("System.Net.Http.HttpRequestException", "Response status code does not indicate success: 502 (Bad Gateway).", "System.Net.Http", "PaymentGateway.ProcessPayment", "/api/payments/charge", 502, "guest-user"),
        ("System.InvalidOperationException", "Sequence contains no elements", "System.Core", "SessionManager.GetActiveSession", "/api/auth/session", 500, "SUMAN-PC\\Suman"),
        ("System.IndexOutOfRangeException", "Index was outside the bounds of the array.", "ElmahCore.Demo", "DataParser.ParseArray", "/upload/parse", 500, ""),
        ("System.Collections.Generic.KeyNotFoundException", "The given key 'cache_config_key' was not present in the dictionary.", "System.Collections", "CacheProvider.GetCachedItem", "/api/config", 500, "admin"),
        ("System.UnauthorizedAccessException", "Access to the path 'D:\\inetpub\\wwwroot\\admin\\delete' is denied.", "mscorlib", "AdminController.DeleteUser", "/admin/users/delete", 401, "SUMAN-PC\\Suman"),
        ("System.IO.FileNotFoundException", "Could not load file or assembly 'System.Text.Json, Version=9.0.0.0' or one of its dependencies. The file cannot be found.", "mscorlib", "FileStorage.LoadAvatar", "/users/avatar", 404, ""),
        ("System.TimeoutException", "The operation has timed out.", "System", "ExternalService.FetchWeatherData", "/weather/today", 504, ""),
        ("System.DivideByZeroException", "Attempted to divide by zero.", "ElmahCore.Demo", "Calculator.CalculateMetrics", "/stats/metrics", 500, "SUMAN-PC\\Suman"),
        ("System.FormatException", "Input string was not in a correct format.", "mscorlib", "ConfigReader.ParsePort", "/settings/save", 400, "admin"),
        ("System.NotSupportedException", "Specified method is not supported.", "ElmahCore.Demo", "ExportService.ExportToPdf", "/report/download", 501, "guest-user"),
        ("System.OutOfMemoryException", "Exception of type 'System.OutOfMemoryException' was thrown.", "mscorlib", "ImageProcessor.ResizeBatch", "/gallery/upload", 500, ""),
        ("System.Net.Sockets.SocketException", "No connection could be made because the target machine actively refused it 127.0.0.1:25", "System.Net.Sockets", "SmtpClient.SendEmail", "/contact/send", 500, "guest-user"),
        ("System.Text.Json.JsonException", "Expected depth of at most 64. Path: $.Settings.Theme | LineNumber: 12.", "System.Text.Json", "SettingsDeserializer.LoadSettings", "/api/settings", 400, "admin"),
        ("System.Security.SecurityException", "Request for the permission of type 'System.Security.Permissions.FileIOPermission' failed.", "mscorlib", "TokenValidator.VerifySignature", "/api/auth/token", 403, ""),
        ("Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException", "Database operation expected to affect 1 row(s) but actually affected 0 row(s). Data may have been modified.", "Microsoft.EntityFrameworkCore", "InventoryService.UpdateStock", "/api/inventory/stock", 500, "SUMAN-PC\\Suman"),
        ("Microsoft.AspNetCore.Http.BadHttpRequestException", "Unexpected end of request content.", "Microsoft.AspNetCore.Server.Kestrel.Core", "ApiController.UploadFile", "/api/files/upload", 400, ""),
        ("System.Threading.Tasks.TaskCanceledException", "A task was canceled.", "mscorlib", "BackgroundJob.ExecuteAsync", "/api/jobs/run", 408, "admin")
    };

    var baseTime = DateTime.Now.AddHours(-48);

    for (int i = 0; i < exceptionTypes.Length; i++)
    {
        var item = exceptionTypes[i];
        var ex = new Exception(item.message);

        var error = new Error(ex)
        {
            Time = baseTime.AddMinutes(i * 137),
            StatusCode = item.statusCode,
            User = item.user,
            Source = item.source,
            ApplicationName = "ElmahCore.Demo",
            Type = item.typeName
        };

        // Populate realistic stacktrace
        error.Detail = $"{item.typeName}: {item.message}" + Environment.NewLine +
                       $"   at {item.method}(String param) in D:\\Projects\\ElmahCoreX\\ElmahCore.Demo\\{item.source.Replace(".", "\\")}\\{item.method.Split('.')[0]}.cs:line {42 + i * 7}" + Environment.NewLine +
                       $"   at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task)" + Environment.NewLine +
                       $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.TaskOfIActionResultExecutor.Execute(IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)" + Environment.NewLine +
                       $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Awaited|12_0(ControllerActionInvoker invoker, ValueTask`1 actionResultValueTask)" + Environment.NewLine +
                       $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, State state, Scope scope, Object state2, Boolean isCompleted)" + Environment.NewLine +
                       $"   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeFunctionAsync()";

        // Add method & path to ServerVariables
        error.ServerVariables.Add("REQUEST_METHOD", i % 3 == 0 ? "POST" : "GET");
        error.ServerVariables.Add("PathBase", "");
        error.ServerVariables.Add("Path", item.path);
        error.ServerVariables.Add("HTTP_HOST", "localhost:5001");
        error.ServerVariables.Add("HTTP_USER_AGENT", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        // Add headers
        error.ServerVariables.Add("Header_Accept", "application/json");
        error.ServerVariables.Add("Header_Host", "localhost:5001");
        error.ServerVariables.Add("Header_User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        // Add SQL log for database errors
        if (item.typeName.Contains("Sql") || item.typeName.Contains("Db"))
        {
            error.SqlLog.Add(new ElmahLogSqlEntry
            {
                TimeStamp = error.Time.AddMilliseconds(-120),
                SqlText = $"SELECT * FROM Products WHERE CategoryId = {10 + i} AND IsActive = 1",
                DurationMs = 2450 + i * 15,
                CommandType = "Text"
            });
        }

        // Add params
        error.Params.Add(new ElmahLogParamEntry(
            error.Time,
            new[] { new KeyValuePair<string, string>("id", (i + 1).ToString()), new KeyValuePair<string, string>("category", "electronics") },
            "ElmahCore.Demo.Controllers",
            item.method.Split('.')[1],
            $"{item.method.Split('.')[0]}.cs",
            120 + i
        ));

        await log.LogAsync(error);
    }
}
