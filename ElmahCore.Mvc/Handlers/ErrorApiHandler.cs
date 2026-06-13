using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElmahCore.Mvc.Notifiers;
using Microsoft.AspNetCore.Http;

namespace ElmahCore.Mvc.Handlers;

internal static class ErrorApiHandler
{
    public static async Task ProcessRequest(HttpContext context, ErrorLog errorLog, string path)
    {
        var method = context.Request.Method;

        try
        {
            switch (path)
            {
                case "api/error":
                    {
                        var errorId = context.Request.Query["id"].ToString();
                        if (string.IsNullOrEmpty(errorId))
                        {
                            await context.Response.WriteErrorAsync("Missing id parameter", 400);
                            break;
                        }
                        var error = await GetErrorAsync(errorLog, errorId);
                        if (error == null)
                        {
                            await context.Response.WriteErrorAsync("Error not found", 404);
                        }
                        else
                        {
                            await context.Response.WriteSuccessAsync(error);
                        }
                    }
                    break;

                case "api/error/review":
                    {
                        if (method != "PATCH")
                        {
                            context.Response.StatusCode = 405;
                            break;
                        }
                        var id = context.Request.Query["id"].ToString();
                        bool.TryParse(context.Request.Query["isReviewed"].ToString(), out var isReviewed);
                        if (string.IsNullOrEmpty(id))
                        {
                            await context.Response.WriteErrorAsync("Missing id parameter", 400);
                            break;
                        }
                        await errorLog.SetReviewedAsync(id, isReviewed);
                        await context.Response.WriteSuccessAsync<object>(null);
                    }
                    break;

                case "api/errors":
                    {
                        int.TryParse(context.Request.Query["i"].ToString(), out var errorIndex);
                        int.TryParse(context.Request.Query["s"].ToString(), out var pageSize);
                        var filter = ParseFilter(context);
                        var entities = await GetErrorsAsync(errorLog, errorIndex, pageSize, filter);
                        await context.Response.WriteSuccessAsync(entities);
                    }
                    break;

                case "api/errors/bulk":
                    {
                        if (method != "DELETE")
                        {
                            context.Response.StatusCode = 405;
                            break;
                        }
                        using var reader = new StreamReader(context.Request.Body);
                        var bodyStr = await reader.ReadToEndAsync();
                        var ids = JsonSerializer.Deserialize<List<string>>(bodyStr);
                        if (ids == null || ids.Count == 0)
                        {
                            await context.Response.WriteErrorAsync("Missing error IDs in request body", 400);
                            break;
                        }
                        await errorLog.DeleteErrorsAsync(ids);
                        await context.Response.WriteSuccessAsync<object>(null);
                    }
                    break;

                case "api/errors/all":
                    {
                        if (method != "DELETE")
                        {
                            context.Response.StatusCode = 405;
                            break;
                        }
                        var appName = context.Request.Query["application"].ToString();
                        await errorLog.DeleteAllErrorsAsync(string.IsNullOrEmpty(appName) ? null : appName);
                        await context.Response.WriteSuccessAsync<object>(null);
                    }
                    break;

                case "api/new-errors":
                    {
                        var id = context.Request.Query["id"].ToString();
                        var newEntities = await GetNewErrorsAsync(errorLog, id);
                        await context.Response.WriteSuccessAsync(newEntities);
                    }
                    break;

                case "api/count":
                    {
                        var filter = ParseFilter(context);
                        
                        // Total count (unfiltered)
                        var totalCount = await errorLog.GetErrorsAsync(0, 0, null, null);
                        
                        // Filtered count
                        var filteredCount = await errorLog.GetErrorsAsync(0, 0, null, filter);
                        
                        await context.Response.WriteSuccessAsync(new { total = totalCount, filtered = filteredCount });
                    }
                    break;

                case "api/export":
                    {
                        var filter = ParseFilter(context);
                        var format = context.Request.Query["format"].ToString().ToLower();
                        
                        var entries = new List<ErrorLogEntry>();
                        await errorLog.GetErrorsAsync(0, 10000, entries, filter);

                        var wrapped = entries.Select(i => new ErrorLogEntryWrapper(i)).ToList();

                        if (format == "csv")
                        {
                            context.Response.ContentType = "text/csv; charset=UTF-8";
                            context.Response.Headers.Add("Content-Disposition", "attachment; filename=elmah-errors.csv");
                            var csv = ToCsv(wrapped);
                            await context.Response.WriteAsync(csv);
                        }
                        else
                        {
                            context.Response.ContentType = "application/json; charset=UTF-8";
                            context.Response.Headers.Add("Content-Disposition", "attachment; filename=elmah-errors.json");
                            var json = JsonSerializer.Serialize(new { success = true, data = wrapped, error = (string)null }, JsonSerializerHelper.DefaultJsonSerializerOptions);
                            await context.Response.WriteAsync(json);
                        }
                    }
                    break;

                case "api/ping":
                    {
                        await context.Response.WriteSuccessAsync("pong");
                    }
                    break;

                case "api/config":
                    {
                        await context.Response.WriteSuccessAsync(new {
                            logName = errorLog.Name,
                            applicationName = errorLog.ApplicationName,
                            sourcePaths = errorLog.SourcePaths ?? Array.Empty<string>()
                        });
                    }
                    break;

                default:
                    context.Response.StatusCode = 404;
                    break;
            }
        }
        catch (Exception ex)
        {
            await context.Response.WriteErrorAsync(ex.Message, 500);
        }
    }

    private static async Task WriteSuccessAsync<T>(this HttpResponse response, T data, string contentType = null)
    {
        response.ContentType = contentType ?? "application/json; charset=UTF-8";
        var json = JsonSerializer.Serialize(new { success = true, data = data, error = (string)null }, JsonSerializerHelper.DefaultJsonSerializerOptions);
        await response.WriteAsync(json);
        await response.Body.FlushAsync();
    }

    private static async Task WriteErrorAsync(this HttpResponse response, string errorMessage, int statusCode)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=UTF-8";
        var json = JsonSerializer.Serialize(new { success = false, data = (object)null, error = errorMessage }, JsonSerializerHelper.DefaultJsonSerializerOptions);
        await response.WriteAsync(json);
        await response.Body.FlushAsync();
    }

    private static ErrorLogFilter ParseFilter(HttpContext context)
    {
        var query = context.Request.Query;
        var filter = new ErrorLogFilter();

        if (query.TryGetValue("type", out var type) && !string.IsNullOrEmpty(type))
            filter.Type = type;

        if (query.TryGetValue("message", out var msg) && !string.IsNullOrEmpty(msg))
            filter.Message = msg;

        if (query.TryGetValue("host", out var host) && !string.IsNullOrEmpty(host))
            filter.Host = host;

        if (query.TryGetValue("user", out var user) && !string.IsNullOrEmpty(user))
            filter.User = user;

        if (query.TryGetValue("statusCode", out var statusStr) && int.TryParse(statusStr, out var status))
            filter.StatusCode = status;

        if (query.TryGetValue("application", out var app) && !string.IsNullOrEmpty(app))
            filter.Application = app;

        if (query.TryGetValue("isReviewed", out var reviewedStr) && bool.TryParse(reviewedStr, out var reviewed))
            filter.IsReviewed = reviewed;

        if (query.TryGetValue("from", out var fromStr) && DateTime.TryParse(fromStr, out var from))
            filter.From = from;

        if (query.TryGetValue("to", out var toStr) && DateTime.TryParse(toStr, out var to))
            filter.To = to;

        if (filter.Type == null && filter.Message == null && filter.Host == null && filter.User == null &&
            !filter.StatusCode.HasValue && filter.Application == null && !filter.IsReviewed.HasValue &&
            !filter.From.HasValue && !filter.To.HasValue)
        {
            return null;
        }

        return filter;
    }

    private static string ToCsv(IEnumerable<ErrorLogEntryWrapper> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ErrorId,Time,Host,Type,Message,User,StatusCode,Source,IsReviewed");
        foreach (var entry in errors)
        {
            var err = entry.Error;
            var id = entry.Id;
            var time = err.Time.ToString("yyyy-MM-dd HH:mm:ss");
            var host = EscapeCsvField(err.HostName);
            var type = EscapeCsvField(err.Type);
            var msg = EscapeCsvField(err.Message);
            var user = EscapeCsvField(err.User);
            var status = err.StatusCode?.ToString() ?? "0";
            var source = EscapeCsvField(err.Source);
            var reviewed = err.IsReviewed ? "True" : "False";

            sb.AppendLine($"{id},{time},{host},{type},{msg},{user},{status},{source},{reviewed}");
        }
        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    private static async Task<ErrorLogEntryWrapper> GetErrorAsync(ErrorLog errorLog, string id)
    {
        var error = await errorLog.GetErrorAsync(id);
        return error == null ? null : new ErrorLogEntryWrapper(error);
    }

    private static async Task<ErrorsList> GetErrorsAsync(ErrorLog errorLog, int errorIndex, int pageSize, ErrorLogFilter filter)
    {
        if (errorIndex < 0) errorIndex = 0;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize > 500) pageSize = 500;

        var entries = new List<ErrorLogEntry>();
        var totalCount = await errorLog.GetErrorsAsync(errorIndex, pageSize, entries, filter);
        return new ErrorsList
        {
            Errors = entries.Select(i => new ErrorLogEntryWrapper(i)).ToList(),
            TotalCount = totalCount
        };
    }

    private static async Task<ErrorsList> GetNewErrorsAsync(ErrorLog errorLog, string id)
    {
        if (string.IsNullOrEmpty(id)) return await GetErrorsAsync(errorLog, 0, 50, null);
        var entries = new List<ErrorLogEntryWrapper>();
        await foreach (var item in errorLog.GetNewErrorsAsync(id))
        {
            entries.Add(new ErrorLogEntryWrapper(item));
        }

        return new ErrorsList
        {
            Errors = entries,
            TotalCount = entries.Count
        };
    }
}