using System.Net;

namespace BeatmapHarvest.Core.Osu;

public class OsuApiException : Exception
{
    public OsuApiException() { }
    public OsuApiException(string message) : base(message) { }
    public OsuApiException(string message, Exception innerException) : base(message, innerException) { }

    public OsuApiException(string message, HttpStatusCode? statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

/// <summary>Missing or rejected client id / secret.</summary>
public sealed class OsuAuthenticationException : OsuApiException
{
    public OsuAuthenticationException() { }
    public OsuAuthenticationException(string message) : base(message) { }
    public OsuAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}
