using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Resync;
public class SileroVadOnnxModel : IDisposable
{
    private readonly InferenceSession _session;
    private float[][][] _state;
    private int _lastSr;
    private int _lastBatchSize;

    private float[][] _context;

    private static readonly List<int> SampleRates = [8000, 16000];

    public SileroVadOnnxModel(string modelPath)
    {
        var sessionOptions = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1,
            EnableCpuMemArena = true
        };
        _session = new InferenceSession(modelPath, sessionOptions);
        ResetStates();

    }

    public void ResetStates()
    {
        _state = new float[2][][];
        _state[0] = new float[1][];
        _state[1] = new float[1][];
        _state[0][0] = new float[128];
        _state[1][0] = new float[128];
        _context = [];
        _lastSr = 0;
        _lastBatchSize = 0;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public class ValidationResult(float[][] x, int sr)
    {
        public float[][] X { get; } = x;
        public int Sr { get; } = sr;
    }

    private static ValidationResult ValidateInput(float[][] x, int sr)
    {
        if (x.Length == 1)
        {
            x = [x[0]];
        }

        if (x.Length > 2)
        {
            throw new ArgumentException($"Incorrect audio data dimension : {x[0].Length}");
        }

        if (sr != 16000 && (sr % 16000 == 0))
        {
            var step = sr / 16000;
            var reducedX = new float[x.Length][];
            for (var i = 0; i < x.Length; i++)
            {
                var current = x[i];
                var newArr = new float[(current.Length + step - 1) / step];
                var index = 0;

                for (var j = 0; j < current.Length; j += step)
                {
                    newArr[index] = current[j];

                    index++;
                }

                reducedX[i] = newArr;
            }

            x = reducedX;
            sr = 16000;
        }

        if (!SampleRates.Contains(sr))
        {
            throw new ArgumentException(
                $"Only supports sample rates {string.Join(", ", SampleRates)} (or multiples of 16000)");
        }

        if (((float)sr) / x[0].Length > 31.25)
        {
            throw new ArgumentException("Input audio is too short");
        }

        return new ValidationResult(x, sr);
    }

    private static float[][] Concatenate(float[][] a, float[][] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("The number of rows in both arrays must be the same.");
        }

        var rows = a.Length;
        var colsA = a[0].Length;
        var colsB = b[0].Length;
        var result = new float[rows][];

        for (var i = 0; i < rows; i++)
        {
            result[i] = new float[colsA + colsB];
            Array.Copy(a[i], 0, result[i], 0, colsA);
            Array.Copy(b[i], 0, result[i], colsA, colsB);
        }

        return result;
    }

    private static float[][] GetLastColumns(float[][] array, int contextSize)
    {
        var rows = array.Length;
        var cols = array[0].Length;

        if (contextSize > cols)
        {
            throw new ArgumentException("contextSize cannot be greater than the number of columns in the array.");
        }

        var result = new float[rows][];

        for (var i = 0; i < rows; i++)
        {
            result[i] = new float[contextSize];
            Array.Copy(array[i], cols - contextSize, result[i], 0, contextSize);
        }

        return result;
    }

  public float[] Call(float[][] x, int sr)
    {
        var result = ValidateInput(x, sr);
        x = result.X;
        sr = result.Sr;
        var numberSamples = sr == 16000 ? 512 : 256;

        if (x[0].Length != numberSamples)
        {
            throw new ArgumentException($"Provided number of samples is {x[0].Length} (Supported values: 256 for 8000 sample rate, 512 for 16000)");
        }

        var batchSize = x.Length;
        var contextSize = sr == 16000 ? 64 : 32;

        if (_lastBatchSize == 0)
        {
            ResetStates();
        }
        if (_lastSr != 0 && _lastSr != sr)
        {
            ResetStates();
        }
        if (_lastBatchSize != 0 && _lastBatchSize != batchSize)
        {
            ResetStates();
        }

        if (_context.Length == 0)
        {
            _context = new float[batchSize][];
            for (var i = 0; i < batchSize; i++)
            {
                _context[i] = new float[contextSize];
            }
        }

        x = Concatenate(_context, x);

        var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(x.SelectMany(a => a).ToArray(), [x.Length, x[0].Length])),
                NamedOnnxValue.CreateFromTensor("sr", new DenseTensor<long>(new[] { (long)sr }, [1])),
                NamedOnnxValue.CreateFromTensor("state", 
                    new DenseTensor<float>(_state.SelectMany(a => a.SelectMany(b => b)).ToArray(), 
                        [_state.Length, _state[0].Length, _state[0][0].Length]))
            };

        using var outputs = _session.Run(inputs);
        var output = outputs.First(o => o.Name == "output").AsTensor<float>();
        var newState = outputs.First(o => o.Name == "stateN").AsTensor<float>();

        _context = GetLastColumns(x, contextSize);
        _lastSr = sr;
        _lastBatchSize = batchSize;

        _state = new float[newState.Dimensions[0]][][];
        for (var i = 0; i < newState.Dimensions[0]; i++)
        {
            _state[i] = new float[newState.Dimensions[1]][];
            for (var j = 0; j < newState.Dimensions[1]; j++)
            {
                _state[i][j] = new float[newState.Dimensions[2]];
                for (var k = 0; k < newState.Dimensions[2]; k++)
                {
                    _state[i][j][k] = newState[i, j, k];
                }
            }
        }

        return [.. output];
    }

}  