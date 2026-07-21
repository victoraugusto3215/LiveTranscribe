namespace LiveTranscribe.Api.Services.Transcription;

/// <summary>
/// Falha que não adianta tentar de novo no próximo chunk — chave ausente/
/// inválida, por exemplo. Diferente de um 429/5xx passageiro, isso
/// justifica encerrar a sessão inteira em vez de só pular o trecho.
/// </summary>
public sealed class TranscriptionUnavailableException(string message) : Exception(message);
