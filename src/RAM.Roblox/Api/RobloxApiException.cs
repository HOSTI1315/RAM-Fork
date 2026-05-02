using System.Net;

namespace RAM.Roblox.Api;

public sealed class RobloxApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? Endpoint { get; }

    public RobloxApiException(string message, HttpStatusCode? statusCode = null, string? endpoint = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
    }
}
