// AudioWorkletProcessor roda numa thread de áudio separada — não pode
// importar nada do bundle principal, por isso é um arquivo solto em
// public/ carregado via audioContext.audioWorklet.addModule(url), não
// pelo Vite. Só repassa os blocos de PCM bruto (Float32) pra thread
// principal, que faz o VAD e o corte em chunks.
class PcmRecorderProcessor extends AudioWorkletProcessor {
  process(inputs) {
    const input = inputs[0];
    if (input && input.length > 0 && input[0].length > 0) {
      // .slice() copia o buffer — o original é reciclado pelo audio engine
      this.port.postMessage(input[0].slice());
    }
    return true;
  }
}

registerProcessor("pcm-recorder-processor", PcmRecorderProcessor);
