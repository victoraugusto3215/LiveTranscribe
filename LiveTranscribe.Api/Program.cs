using LiveTranscribe.Api.Data;
using LiveTranscribe.Api.Hubs;
using LiveTranscribe.Api.Services.Audio;
using LiveTranscribe.Api.Services.Processing;
using LiveTranscribe.Api.Services.RateLimiting;
using LiveTranscribe.Api.Services.Transcription;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR(options =>
{
    // padrão é 32KB — pequeno demais pra um chunk de áudio ao vivo de
    // alguns segundos em base64 (o modo de upload não usa o Hub pra
    // mandar bytes, só o modo ao vivo manda áudio por aqui)
    options.MaximumReceiveMessageSize = 5 * 1024 * 1024; // 5 MB
});

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=App_Data/livetranscribe.db"));

builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
builder.Services.Configure<AudioChunkStoreOptions>(builder.Configuration.GetSection(AudioChunkStoreOptions.SectionName));
builder.Services.Configure<SessionCleanupOptions>(builder.Configuration.GetSection(SessionCleanupOptions.SectionName));

builder.Services.AddHttpClient<ITranscriptionProvider, GroqWhisperProvider>();

builder.Services.AddSingleton<AudioChunkStore>();
builder.Services.AddSingleton<AudioNormalizer>();
builder.Services.AddSingleton<SessionProcessingQueue>();
builder.Services.AddScoped<SessionProcessor>();
builder.Services.AddHostedService<SessionProcessingWorker>();
builder.Services.AddHostedService<SessionCleanupWorker>();

builder.Services.AddSingleton<LiveSessionRegistry>();
builder.Services.AddScoped<LiveSessionProcessor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        string[] origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddUploadRateLimiting(permitLimit: 5, window: TimeSpan.FromHours(2));

// confia em X-Forwarded-For pra pegar o IP real do cliente atrás de um
// proxy reverso (Fly.io) — sem isso o rate limit trataria todo mundo como
// o mesmo IP (o do proxy). Limpar KnownNetworks/KnownProxies assume que só
// há um hop confiável na frente (o edge do Fly.io); não usar assim se a
// API algum dia for exposta direto na internet sem proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// baixa o binário do ffmpeg na primeira execução (fica em App_Data/ffmpeg,
// já coberto pelo .gitignore) — sem isso só WAV seria aceito
await AudioNormalizer.EnsureFFmpegAvailableAsync(
    Path.Combine(app.Environment.ContentRootPath, "App_Data", "ffmpeg"),
    CancellationToken.None);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TranscriptionHub>("/hubs/transcription");

app.Run();

public partial class Program;
