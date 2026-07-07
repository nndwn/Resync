using System.Runtime.InteropServices;
using FFMpegCore;
using FFMpegCore.Enums;

namespace Resync;

public class AudioTranscription
{

   public static byte[]? ReadWavPcm16Mono(string path, out int sampleRate)
   {
       sampleRate = 0;
       using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
       using var br = new BinaryReader(fs);

       var riff = new string(br.ReadChars(4));
       if (riff != "RIFF")
       {
           Console.WriteLine("error Riff");
           return null;
       }

      
       br.ReadInt32();
       var wave = new string(br.ReadChars(4));
       if (wave != "WAVE")
       {
           Console.WriteLine("error WAVE");
           return null;
       }

  
       var audioFormat = 0;
       var numChannels = 0;
       var bitsPerSample = 0;
       var dataSize = 0;
       long dataStartPos = -1;

       while (fs.Position < fs.Length)
       {
           var chunkId = new string(br.ReadChars(4));
           var chunkSize = br.ReadInt32();
           if (chunkId == "fmt ")
           {
               audioFormat = br.ReadInt16();
               numChannels = br.ReadInt16();
               sampleRate = br.ReadInt32();
               br.ReadInt32();
               br.ReadInt16();
               bitsPerSample = br.ReadInt16();

               var fmtExtra = chunkSize - 16;
               if (fmtExtra > 0) br.ReadBytes(fmtExtra);
           } 
           else if (chunkId == "data")
           {
               dataSize = chunkSize;
               dataStartPos = fs.Position;
               break;
           }
           else
           {
               br.ReadBytes(chunkSize);
           }
       }
       
       
       if (audioFormat != 1 || numChannels != 1 || bitsPerSample != 16 || sampleRate != 16000 || dataStartPos < 0) return null;
       
       fs.Position = dataStartPos;
       var pcm = br.ReadBytes(dataSize);
       return pcm;
   }
    
    public static async Task<bool> ExtractAndProcessAudioSafely(string videoPath, string modelPath)
    {
        var tempAudioPath = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid()}.wav");
        try
        {
            var mediaInfo = FFProbe.Analyse(videoPath);
            var audioStreams = mediaInfo.AudioStreams;
            int selectedTrackIndex = 0;

            if (audioStreams.Count > 1)
            {
                Console.WriteLine("\n[Multimedia] Multiple audio tracks detected:");
                for (int i = 0; i < audioStreams.Count; i++)
                {
                    var stream = audioStreams[i];
                    var language = stream.Tags?.TryGetValue("language", out var lang) == true ? lang : "Unknown";
                    var title = stream.Tags?.TryGetValue("title", out var titl) == true ? titl : "";
                    Console.WriteLine($"  [{i}] Language: {language} {(string.IsNullOrEmpty(title) ? "" : $"- {title}")} (Codec: {stream.CodecName})");
                }

                Console.Write("\nPlease select an audio track by number: ");
                var input = Console.ReadLine();
                if (int.TryParse(input, out int result) && result >= 0 && result < audioStreams.Count)
                {
                    selectedTrackIndex = result;
                    Console.WriteLine($"Selected Track: {selectedTrackIndex}");
                }
                else
                {
                    Console.WriteLine("Invalid selection, defaulting to Track 0");
                }
            }

            Console.WriteLine("\n[Multimedia] Extracting audio to temp disk ...");
            FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(tempAudioPath, overwrite: true, options => options
                    .WithAudioCodec("pcm_s16le")
                    .WithAudioSamplingRate(16000)
                    .WithCustomArgument("-ac 1")       
                    .WithCustomArgument("-vn")
                    .WithCustomArgument($"-map 0:a:{selectedTrackIndex}")
                )
                .ProcessSynchronously();
            Console.WriteLine($"{tempAudioPath}");
            Console.WriteLine("[AI] Initializing Silero VAD Engine...");
            var pcmBytes = ReadWavPcm16Mono(tempAudioPath, out int sampleRate);
            if (pcmBytes == null)
            {
                Console.WriteLine("Failed to read audio file.");
                return false;
            }

            using var vad = new SileroVadOnnxModel(modelPath);
            int chunkSize = 512;
            int numSamples = pcmBytes.Length / 2;
            int numChunks = numSamples / chunkSize;

            int speechChunks = 0;

            for (int i = 0; i < numChunks; i++)
            {
                float[][] chunk = [new float[chunkSize]];
                for (int j = 0; j < chunkSize; j++)
                {
                    int byteIndex = (i * chunkSize + j) * 2;
                    short sample = BitConverter.ToInt16(pcmBytes, byteIndex);
                    chunk[0][j] = sample / 32768.0f;
                }

                var result = vad.Call(chunk, sampleRate);
                if (result.Length > 0 && result[0] > 0.5f)
                {
                    speechChunks++;
                }
            }

            if (speechChunks > 0)
            {
                Console.WriteLine($"[VAD] Speech detected in {speechChunks} chunks.");
                
                Console.WriteLine("[AI] Initializing Whisper for transcription...");
                var modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                if (!Directory.Exists(modelsDir))
                {
                    Directory.CreateDirectory(modelsDir);
                }
                var whisperModelPath = Path.Combine(modelsDir, "ggml-tiny.bin");
                
                if (!File.Exists(whisperModelPath))
                {
                    Console.WriteLine("[AI] Downloading Whisper tiny model (low memory footprint)...");
                    using var httpClient = new HttpClient();
                    using var modelStream = await httpClient.GetStreamAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin");
                    using var fileWriter = File.OpenWrite(whisperModelPath);
                    await modelStream.CopyToAsync(fileWriter);
                }

                using var whisperFactory = Whisper.net.WhisperFactory.FromPath(whisperModelPath);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                var srtPath = Path.ChangeExtension(videoPath, ".srt");
                using var srtStream = new StreamWriter(srtPath);
                int srtIndex = 1;
                
                using var fileStream = File.OpenRead(tempAudioPath);
                Console.WriteLine("[AI] Processing speech to text...");
                
                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    Console.WriteLine($"[Whisper] {result.Start:hh\\:mm\\:ss\\.fff}->{result.End:hh\\:mm\\:ss\\.fff}: {result.Text}");
                    
                    var startStr = result.Start.ToString(@"hh\:mm\:ss\,fff");
                    var endStr = result.End.ToString(@"hh\:mm\:ss\,fff");
                    
                    await srtStream.WriteLineAsync(srtIndex.ToString());
                    await srtStream.WriteLineAsync($"{startStr} --> {endStr}");
                    await srtStream.WriteLineAsync(result.Text.Trim());
                    await srtStream.WriteLineAsync();
                    srtIndex++;
                }

                Console.WriteLine($"[Success] Subtitles saved to: {srtPath}");
                return true;
            }
            else
            {
                Console.WriteLine("[VAD] No speech detected.");
                return false;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error during processing: {e.Message}");
            return false;
        }
        finally
        {
            if (File.Exists(tempAudioPath))
            {
                File.Delete(tempAudioPath);
            }
        }
   
    }
}