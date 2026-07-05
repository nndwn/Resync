using System.Runtime.InteropServices;
using FFMpegCore;
using FFMpegCore.Enums;

namespace Resync;

public class AudioTranscription
{
   private static IEnumerable<byte[]> GetAudioBuffers(string filePath, int bufferSize )
   {
       using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
       var buffer = new byte[bufferSize];
       int bytesRead ;
       while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
       {
           var chunk = new byte[bytesRead];
           Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
           yield return chunk;
       }
       
   }
    
    public static bool ExtractAndProcessAudioSafely(string videoPath)
    {
        var tempAudioPath = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid()}.wav");
        try
        {
            Console.WriteLine("[Multimedia] Extracting audio to temp disk ...");
            FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(tempAudioPath, overwrite: true, options => options
                    .WithAudioCodec("pcm_s16le")
                    .WithAudioSamplingRate(16000)
                    .WithCustomArgument("-ac 1")       
                    .WithCustomArgument("-vn"))
                .ProcessSynchronously();
            Console.WriteLine("[AI] Initializing Silero VAD Engine...");
            using var vad = new VoiceActivityDetector("models/silero_vad.onnx");

            const int sampleBufferSize = 1024;
            var speechCount = 0;
            var silenceCount = 0;
            foreach (var chunk in  GetAudioBuffers(tempAudioPath, sampleBufferSize))
            {
                if (chunk.Length < 1024) continue;
                var isSpeaking = vad.IsHumanSpeech(chunk, sampleRate: 16000, threshold: 0.5f);
                if (isSpeaking)
                    speechCount++;
                else
                    silenceCount++;
            }
            Console.WriteLine($"\n[VAD Analytics] Done processing. Detected {speechCount} active speech blocks and {silenceCount} silence blocks.");
            return true;
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
                Console.WriteLine("[Cleanup] Temporary audio file has been safely removed.");
            }
        }
    }
}