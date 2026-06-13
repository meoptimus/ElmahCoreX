using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ElmahCore;
using ElmahCore.Mvc.Handlers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ElmahCore.Mvc.Tests;

public class ErrorApiHandlerTests
{
    private readonly MemoryErrorLog _errorLog;

    public ErrorApiHandlerTests()
    {
        _errorLog = new MemoryErrorLog(500);
        // Clear all entries since it uses a shared static list in memory
        _errorLog.DeleteAllErrorsAsync().Wait();
    }

    [Fact]
    public async Task ProcessRequest_Ping_ReturnsPong()
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/ping");

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Contain("application/json");

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var body = await reader.ReadToEndAsync();
        
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetString().Should().Be("pong");
    }

    [Fact]
    public async Task ProcessRequest_Config_ReturnsConfig()
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/config");

        context.Response.StatusCode.Should().Be(200);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var body = await reader.ReadToEndAsync();

        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("logName").GetString().Should().Be(_errorLog.Name);
    }

    [Fact]
    public async Task ProcessRequest_SetReviewed_UpdatesError()
    {
        var error = new Error(new Exception("Test error"));
        error.IsReviewed.Should().BeFalse();
        var id = _errorLog.Log(error);

        var context = new DefaultHttpContext();
        context.Request.Method = "PATCH";
        context.Request.QueryString = new QueryString($"?id={id}&isReviewed=true");

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/error/review");

        context.Response.StatusCode.Should().Be(200);

        var updatedEntry = await _errorLog.GetErrorAsync(id);
        updatedEntry.Should().NotBeNull();
        updatedEntry.Error.IsReviewed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRequest_BulkDelete_DeletesErrors()
    {
        var err1 = new Error(new Exception("Err1"));
        var err2 = new Error(new Exception("Err2"));
        var id1 = _errorLog.Log(err1);
        var id2 = _errorLog.Log(err2);

        var context = new DefaultHttpContext();
        context.Request.Method = "DELETE";
        var ids = new List<string> { id1, id2 };
        var json = JsonSerializer.Serialize(ids);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/errors/bulk");

        context.Response.StatusCode.Should().Be(200);

        (await _errorLog.GetErrorAsync(id1)).Should().BeNull();
        (await _errorLog.GetErrorAsync(id2)).Should().BeNull();
    }

    [Fact]
    public async Task ProcessRequest_DeleteAll_ClearsErrors()
    {
        var err1 = new Error(new Exception("Err1"));
        var id1 = _errorLog.Log(err1);

        var context = new DefaultHttpContext();
        context.Request.Method = "DELETE";

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/errors/all");

        context.Response.StatusCode.Should().Be(200);

        (await _errorLog.GetErrorAsync(id1)).Should().BeNull();
    }

    [Fact]
    public async Task ProcessRequest_GetCount_ReturnsCounts()
    {
        var err1 = new Error(new Exception("Err1")) { StatusCode = 500 };
        var err2 = new Error(new Exception("Err2")) { StatusCode = 404 };
        _errorLog.Log(err1);
        _errorLog.Log(err2);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?statusCode=404");

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/count");

        context.Response.StatusCode.Should().Be(200);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var body = await reader.ReadToEndAsync();

        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("total").GetInt32().Should().Be(2);
        data.GetProperty("filtered").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ProcessRequest_ExportCsv_ReturnsCsvData()
    {
        var err1 = new Error(new Exception("Err1")) { HostName = "localhost", StatusCode = 500 };
        _errorLog.Log(err1);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?format=csv");

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/export");

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Contain("text/csv");

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var csv = await reader.ReadToEndAsync();
        
        csv.Should().Contain("ErrorId,Time,Host,Type,Message,User,StatusCode,Source,IsReviewed");
        csv.Should().Contain("localhost");
        csv.Should().Contain("500");
    }

    [Fact]
    public async Task ProcessRequest_ExportJson_ReturnsJsonData()
    {
        var err1 = new Error(new Exception("Err1")) { HostName = "localhost", StatusCode = 500 };
        _errorLog.Log(err1);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?format=json");

        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await ErrorApiHandler.ProcessRequest(context, _errorLog, "api/export");

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Contain("application/json");

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
    }
}
