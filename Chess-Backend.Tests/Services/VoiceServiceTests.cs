using System.Net;
using Chess_Backend.Services;
using Chess_Backend.Tests.Helpers;

namespace Chess_Backend.Tests.Services;

public class VoiceServiceTests
{
    private VoiceService CreateService(HttpClient httpClient)
    {
        var config = TestHelper.CreateFakeConfiguration();
        var logger = TestHelper.CreateMockLogger<VoiceService>();
        return new VoiceService(httpClient, config, logger);
    }

    [Fact]
    public async Task SuccessfulResponse_ReturnsAudioBytes()
    {
        byte[] fakeAudio = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00 };
        var httpClient = TestHelper.CreateMockHttpClient(HttpStatusCode.OK, fakeAudio);
        var service = CreateService(httpClient);

        var result = await service.TextToSpeechAsync("Hello world");

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ApiError_ReturnsEmptyArray()
    {
        var httpClient = TestHelper.CreateMockHttpClient(HttpStatusCode.BadRequest, "Bad request");
        var service = CreateService(httpClient);

        var result = await service.TextToSpeechAsync("Hello world");

        Assert.Empty(result);
    }

    [Fact]
    public async Task HttpException_ReturnsEmptyArray()
    {
        var httpClient = TestHelper.CreateThrowingHttpClient(new HttpRequestException("Network error"));
        var service = CreateService(httpClient);

        var result = await service.TextToSpeechAsync("Hello world");

        Assert.Empty(result);
    }

    [Fact]
    public async Task RequestUrl_ContainsElevenLabsVoiceId()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, new byte[] { 0x00 });
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.TextToSpeechAsync("Test");

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("elevenlabs.io", handler.LastRequest.RequestUri?.ToString());
        Assert.Contains("wyWA56cQNU2KqUW4eCsI", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task Request_ContainsApiKeyHeader()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, new byte[] { 0x00 });
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.TextToSpeechAsync("Test");

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("xi-api-key"));
        var values = handler.LastRequest.Headers.GetValues("xi-api-key").ToList();
        Assert.Contains("fake-elevenlabs-key", values);
    }

    [Fact]
    public async Task RequestBody_ContainsTextAndModelSettings()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, new byte[] { 0x00 });
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.TextToSpeechAsync("Check!");

        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("Check!", handler.LastRequestContent);
        Assert.Contains("eleven_monolingual_v1", handler.LastRequestContent);
        Assert.Contains("voice_settings", handler.LastRequestContent);
    }

    [Fact]
    public async Task EmptyText_StillMakesRequest()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, new byte[] { 0x00 });
        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.TextToSpeechAsync("");

        Assert.NotNull(handler.LastRequest);
    }
}
