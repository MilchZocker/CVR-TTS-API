using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace TTSAPI.Tts;

public sealed class OpenAiSpeechProvider : ITtsProvider
{
    public const string SpeechPath = "/v1/audio/speech";

    private static readonly HttpClient SharedHttp = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    private readonly TtsSettings _settings;
    private readonly HttpClient _http;

    public OpenAiSpeechProvider(TtsSettings settings, HttpClient httpClient = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _http = httpClient ?? SharedHttp;
    }

    public static string BuildRequestBody(TtsSettings settings, string text, string voice)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');

        bool first = true;
        void Add(string name, string jsonValue)
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append('"').Append(name).Append("\":").Append(jsonValue);
        }

        if (!string.IsNullOrWhiteSpace(settings.Model))
            Add("model", Quote(settings.Model.Trim()));

        Add("input", Quote(text ?? ""));

        if (!string.IsNullOrWhiteSpace(voice))
            Add("voice", Quote(voice.Trim()));

        if (settings.Speed > 0f && float.IsFinite(settings.Speed))
            Add("speed", settings.Speed.ToString("0.###", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(settings.Instructions))
            Add("instructions", Quote(settings.Instructions));

        sb.Append('}');
        return sb.ToString();
    }

    public byte[] Synthesize(string text, string voice, CancellationToken cancellationToken)
    {
        string url = (_settings.BaseUrl ?? "").TrimEnd('/') + SpeechPath;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            throw new TtsProviderException($"Invalid BaseUrl '{_settings.BaseUrl}'");

        int timeoutSeconds = Math.Clamp(_settings.TimeoutSeconds, 1, 3600);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(BuildRequestBody(_settings, text, voice), Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _settings.ApiKey.Trim());

        foreach (var header in _settings.ParseExtraHeaders())
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        try
        {
            using HttpResponseMessage response = _http.SendAsync(request, linked.Token).GetAwaiter().GetResult();

            // Default HttpCompletionOption buffers the body during SendAsync, so this does not block on the network.
            byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                string snippet = bytes.Length == 0
                    ? "(empty body)"
                    : Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 1000));
                throw new TtsProviderException(
                    $"Speech request failed ({(int)response.StatusCode} {response.StatusCode}) from {uri}: {snippet}");
            }

            if (bytes.Length == 0)
                throw new TtsProviderException($"Speech request returned an empty body from {uri}");

            return bytes;
        }
        catch (TtsProviderException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TtsProviderException($"Speech request was cancelled ({uri})");

            throw new TtsProviderException($"Speech request timed out after {timeoutSeconds}s ({uri})");
        }
        catch (HttpRequestException ex)
        {
            throw new TtsProviderException($"Cannot reach the speech server at {uri}: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new TtsProviderException($"Speech response could not be read from {uri}: {ex.Message}");
        }
    }

    private static string Quote(string value)
    {
        if (value == null)
            return "\"\"";

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');

        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
