using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace ElmahCore.Tests;

public class LogCookiesTests
{
    private const string CookieHeader = "session-id=secret-token; theme=dark";

    [Fact]
    public void ElmahOptions_LogCookies_DefaultsToTrue()
    {
        var options = new ElmahOptions();

        options.LogCookies.Should().BeTrue();
    }

    [Fact]
    public void ElmahOptions_LogCookies_CanBeSetToFalse()
    {
        var options = new ElmahOptions();

        options.LogCookies = false;

        options.LogCookies.Should().BeFalse();
    }

    [Fact]
    public void Error_WhenLogCookiesEnabled_CapturesCookieCollection()
    {
        var error = new Error(new Exception("boom"), CreateContext());

        error.Cookies.AllKeys.Should().BeEquivalentTo("session-id", "theme");
        error.Cookies["session-id"].Should().Be("secret-token");
    }

    [Fact]
    public void Error_WhenLogCookiesEnabled_CapturesCookieHeader()
    {
        var error = new Error(new Exception("boom"), CreateContext());

        error.ServerVariables["Header_Cookie"].Should().Be(CookieHeader);
    }

    [Fact]
    public void Error_WhenLogCookiesDisabled_DoesNotCaptureCookieCollection()
    {
        var error = new Error(new Exception("boom"), CreateContext(), logCookies: false);

        error.Cookies.Count.Should().Be(0);
    }

    [Fact]
    public void Error_WhenLogCookiesDisabled_DoesNotCaptureAnyCookieServerVariable()
    {
        var error = new Error(new Exception("boom"), CreateContext(), logCookies: false);

        error.ServerVariables.AllKeys
            .Where(k => k.Contains("Cookie", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty();
    }

    [Fact]
    public void Error_WhenLogCookiesDisabled_StillCapturesOtherHeaders()
    {
        var error = new Error(new Exception("boom"), CreateContext(), logCookies: false);

        error.ServerVariables["Header_User-Agent"].Should().Be("xunit");
    }

    private static HttpContext CreateContext()
    {
        var headers = new HeaderDictionary
        {
            ["Cookie"] = CookieHeader,
            ["User-Agent"] = "xunit",
            ["Host"] = "localhost"
        };

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            Method = "GET",
            Path = "/",
            Headers = headers
        });

        // The real (Kestrel) feature collection exposes the request headers through a
        // "RequestHeaders" property, which is what Error reflects over to build Header_* server
        // variables. FeatureCollection alone does not, so wrap it to reproduce that shape.
        return new DefaultHttpContext(new RequestHeaderAwareFeatures(features, headers));
    }

    /// <summary>
    ///     An <see cref="IFeatureCollection" /> that also exposes the request headers as a property,
    ///     mirroring the shape of the server's own feature collection.
    /// </summary>
    private sealed class RequestHeaderAwareFeatures : IFeatureCollection
    {
        private readonly IFeatureCollection _inner;

        public RequestHeaderAwareFeatures(IFeatureCollection inner, IHeaderDictionary requestHeaders)
        {
            _inner = inner;
            RequestHeaders = requestHeaders;
        }

        public IHeaderDictionary RequestHeaders { get; }

        public bool IsReadOnly => _inner.IsReadOnly;

        public int Revision => _inner.Revision;

        public object this[Type key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public TFeature Get<TFeature>() => _inner.Get<TFeature>();

        public void Set<TFeature>(TFeature instance) => _inner.Set(instance);

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
