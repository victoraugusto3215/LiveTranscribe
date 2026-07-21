using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LiveTranscribe.Api.Services.RateLimiting;

/// <summary>
/// Protege o endpoint de upload (e, por consequência, a chave da Groq) de
/// abuso: N envios por IP dentro da janela configurada. Sem fila — quem
/// estourar o limite recebe 429 na hora, não fica esperando.
/// </summary>
public static class RateLimitingSetup
{
    public const string UploadPolicy = "upload";

    public static IServiceCollection AddUploadRateLimiting(this IServiceCollection services, int permitLimit, TimeSpan window)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(UploadPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";

                string retrySuffix = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? $" Tente novamente em {FormatDuration(retryAfter)}."
                    : "";

                string unidade = permitLimit == 1 ? "áudio" : "áudios";
                await context.HttpContext.Response.WriteAsync(
                    $"Limite de envio atingido — só {(permitLimit == 1 ? "é permitido" : "são permitidos")} {permitLimit} {unidade} a cada {FormatDuration(window)} por conexão.{retrySuffix}",
                    cancellationToken);
            };
        });

        return services;
    }

    private static string FormatDuration(TimeSpan span)
    {
        int hours = (int)span.TotalHours;
        int minutes = span.Minutes;

        if (hours <= 0)
        {
            return $"{Math.Max(1, minutes)} min";
        }

        return minutes > 0 ? $"{hours}h{minutes:D2}" : $"{hours}h";
    }
}
