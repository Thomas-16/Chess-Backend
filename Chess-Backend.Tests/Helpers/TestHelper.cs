using System.Net;
using Chess_Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Chess_Backend.Tests.Helpers;

public static class TestHelper
{
    public static ChessDbContext CreateInMemoryContext(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ChessDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ChessDbContext(options);
    }

    public static ILogger<T> CreateMockLogger<T>()
    {
        return Mock.Of<ILogger<T>>();
    }

    public static IConfiguration CreateFakeConfiguration(
        string? openAiKey = "fake-openai-key",
        string? groqKey = "fake-groq-key",
        string? elevenLabsKey = "fake-elevenlabs-key")
    {
        var configData = new Dictionary<string, string?>();
        if (openAiKey != null) configData["OPENAI_API_KEY"] = openAiKey;
        if (groqKey != null) configData["GROQ_API_KEY"] = groqKey;
        if (elevenLabsKey != null) configData["ELEVENLABS_API_KEY"] = elevenLabsKey;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    public static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseContent);
        return new HttpClient(handler);
    }

    public static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, byte[] responseBytes)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseBytes);
        return new HttpClient(handler);
    }

    public static HttpClient CreateThrowingHttpClient(Exception exception)
    {
        var handler = new ThrowingHttpMessageHandler(exception);
        return new HttpClient(handler);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly byte[] _responseBytes;
    private readonly bool _isBinary;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestContent { get; private set; }

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
    {
        _statusCode = statusCode;
        _responseBytes = System.Text.Encoding.UTF8.GetBytes(responseContent);
        _isBinary = false;
    }

    public MockHttpMessageHandler(HttpStatusCode statusCode, byte[] responseBytes)
    {
        _statusCode = statusCode;
        _responseBytes = responseBytes;
        _isBinary = true;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content != null)
        {
            LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        var response = new HttpResponseMessage(_statusCode);
        if (_isBinary)
        {
            response.Content = new ByteArrayContent(_responseBytes);
        }
        else
        {
            response.Content = new StringContent(System.Text.Encoding.UTF8.GetString(_responseBytes));
        }
        return response;
    }
}

public class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
