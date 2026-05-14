using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MediaButler.Mobile.Components.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// Provides platform-specific HTTP message handlers with secure certificate validation.
/// Implements IP-based trust strategy for local network NAS devices.
/// </summary>
public class HttpsClientHandlerService : IHttpsClientHandlerService
{
    private readonly ILogger<HttpsClientHandlerService> _logger;

    // Trusted local network IP ranges (RFC 1918 private networks)
    private static readonly string[] TrustedLocalRanges = new[]
    {
        "192.168.",  // Class C private network (typical home routers)
        "10.",       // Class A private network (large enterprise networks)
        "172.16.", "172.17.", "172.18.", "172.19.",  // Class B private network
        "172.20.", "172.21.", "172.22.", "172.23.",
        "172.24.", "172.25.", "172.26.", "172.27.",
        "172.28.", "172.29.", "172.30.", "172.31.",
        "127.0.0.1", // IPv4 loopback
        "localhost"  // Localhost DNS name
    };

    public HttpsClientHandlerService(ILogger<HttpsClientHandlerService> logger)
    {
        _logger = logger;
    }

    public HttpMessageHandler GetPlatformMessageHandler()
    {
#if ANDROID
        var handler = new Xamarin.Android.Net.AndroidMessageHandler();
#elif WINDOWS || MACCATALYST
        var handler = new HttpClientHandler();
#elif IOS
        var handler = new NSUrlSessionHandler();
#else
        var handler = new HttpClientHandler();
#endif

        // Apply custom certificate validation to all platforms
        handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;

        return handler;
    }

    /// <summary>
    /// Validates server SSL/TLS certificates with IP-based trust strategy.
    /// - Valid certificates: Always trusted (public HTTPS sites)
    /// - Local network hosts (192.168.x.x, 10.x.x.x): Trusted with warning logs
    /// - Remote hosts with invalid certs: REJECTED (protection against MITM attacks)
    /// </summary>
    private bool ValidateServerCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        var host = request.RequestUri?.Host;

        // ✅ Valid certificate - always trust (no SSL errors)
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            _logger.LogDebug("✅ Valid HTTPS certificate for {Host}", host);
            return true;
        }

        // ⚠️ Check if host is in trusted local network
        if (IsLocalNetworkHost(host))
        {
            _logger.LogWarning(
                "⚠️  Accepting self-signed certificate for local network host: {Host}. " +
                "SSL Errors: {Errors}. Certificate Subject: {Subject}. " +
                "This is expected for NAS devices with self-signed certificates.",
                host,
                sslPolicyErrors,
                certificate?.Subject ?? "N/A");
            return true; // Trust local NAS devices with self-signed certs
        }

        // ❌ Remote host with invalid certificate - REJECT for security
        _logger.LogError(
            "❌ REJECTING invalid certificate for remote host: {Host}. " +
            "SSL Errors: {Errors}. Certificate Subject: {Subject}. " +
            "This connection may be compromised (MITM attack).",
            host,
            sslPolicyErrors,
            certificate?.Subject ?? "N/A");
        return false;
    }

    /// <summary>
    /// Checks if the host is within trusted local network IP ranges (RFC 1918).
    /// </summary>
    private bool IsLocalNetworkHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return TrustedLocalRanges.Any(range =>
            host.StartsWith(range, StringComparison.OrdinalIgnoreCase));
    }
}
