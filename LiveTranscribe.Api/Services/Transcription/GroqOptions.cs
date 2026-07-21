namespace LiveTranscribe.Api.Services.Transcription;

public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "whisper-large-v3-turbo";
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
}
