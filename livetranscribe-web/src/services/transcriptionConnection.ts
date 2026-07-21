import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { env } from "../config/env";

/**
 * Uma conexão SignalR só, compartilhada entre sessões, com reconexão
 * automática. Quem quiser acompanhar uma sessão dá join no grupo dela
 * (ver TranscriptionHub.JoinSession no backend); ao reconectar, o chamador
 * é responsável por reentrar no grupo e rebuscar o que perdeu via REST.
 */
export function createTranscriptionConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(env.hubUrl)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    // Critical, não Warning: o próprio hook já reflete falha de conexão na UI
    // via connectionState — sobra só ruído interno da lib (ex.: abort de
    // negociação no StrictMode do React em dev) que não precisa virar log.
    .configureLogging(LogLevel.Critical)
    .build();
}
