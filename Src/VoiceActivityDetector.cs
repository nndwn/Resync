using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Resync;

public class VoiceActivityDetector(string modelPath) : IDisposable
{


    private bool _disposed;
    private readonly InferenceSession _session = new (modelPath);
    private readonly float[] _state = new float[256];

    public bool IsHumanSpeech(byte[] byteChunk, int sampleRate = 16000, float threshold = 0.5f)
    {
        var floatSamples = ConvertByteToFloat(byteChunk);
        int[] audioShape = [1, floatSamples.Length];
        var audioTensor = new DenseTensor<float>(floatSamples, audioShape);
        var srTensor = new DenseTensor<long>(new  long[] { sampleRate }, [1]);
        
        var stateTensor = new DenseTensor<float>(_state, [2, 1, 128]);
        


        var inputs = new List<NamedOnnxValue>
        {  
            NamedOnnxValue.CreateFromTensor("input", audioTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
            NamedOnnxValue.CreateFromTensor("state", stateTensor),
        };

        using var outputs = _session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var speechProbability = outputTensor.First();
        
        var stateNextTensor = outputs.ElementAt(1).AsTensor<float>();
        
        Array.Copy(stateNextTensor.ToArray(), _state, 256);

        return speechProbability >= threshold;
    }

    private static float[] ConvertByteToFloat(byte[] bytes)
    {
        var sampleCount = bytes.Length / 2;
        var floats = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sample16 = BitConverter.ToInt16(bytes, i * 2);
            floats[i] = sample16 / 32768f;
        }
        return floats;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _session?.Dispose();
        }
        _disposed = true;
    }
}