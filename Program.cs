using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FFMpegCore;

namespace Resync;

 class Program
 {

    private static int Main(string[] args)
    {
        var inputOption = new Option<FileInfo>("--input", "-i")
        {
            Required = true,
            Description = "Input file path (.srt for resync, .mp4/.mkv for auto-transcription)"
        };
        var outputOption = new Option<FileInfo>("--output", "-o")
        {
            Required = false, 
            Description = "Output result file path"
        };
        
        var startOption = new Option<string?>("--start", "-s") 
            { Description = "New start time (example: 00:00:25,644)" };
        var secondsOption = new Option<double?>("--seconds", "-t") 
            { Description = "Shift time in seconds (example: 2.5 to advance, -1.5 to delay)" };
        var editIndexOption = new Option<int?>("--edit-index", "-e") 
            { Description = "Target index to start shifting" };
        var fromTimeOption = new Option<string>("--from-time", "-f")
            { Description = "Target start time where desync begins (HH:mm:ss,fff)" };
        var newTimeOption = new Option<string?>("--new-time", "-n") 
            { Description = "New start time for targeted index (HH:mm:ss,fff)" };

        RootCommand rootCommand = new("Resync SRT - Re-index and synchronize subtitle timing")
        {
            inputOption,outputOption, startOption, secondsOption, editIndexOption, fromTimeOption, newTimeOption
        };
        
        var appFolder = AppDomain.CurrentDomain.BaseDirectory;
        var localFFmpegFolder = Path.Combine(appFolder, "ffmpeg");
        
        GlobalFFOptions.Configure(options => options.BinaryFolder = localFFmpegFolder);


        rootCommand.SetAction(parseResult =>
        {
            var inputFile = parseResult.GetValue(inputOption)!;
            if (!inputFile.Exists)
            {
                Console.WriteLine("Error: Input file not found.");
                return;
            }

            var extension = inputFile.Extension.ToLowerInvariant();
            string[] videoExtensions = [".mp4", ".mkv", ".avi", ".mov"];
            
            if (extension == ".srt")
            {
                var resync = new ResyncTimeSpan();
                resync.Run(parseResult, inputFile, outputOption, secondsOption, startOption, editIndexOption, fromTimeOption, newTimeOption);
            }
            else if (videoExtensions.Contains(extension))
            {
                Console.WriteLine($"[Mode] Auto-Transcription detected for video file: {inputFile.Name}");

                var isFFmpegReady = EnsureFFmpeg();
                if (!isFFmpegReady) return;
       
                AudioTranscription.ExtractAndProcessAudioSafely(inputFile.FullName);
            }
            else
            {
                Console.WriteLine($"Error: Unsupported file format '{extension}'. Please provide a .srt or video file.");
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static bool IsFFmpegGlobalPath(string binaryName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return false;
        
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var paths = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path.Trim(), binaryName);
                if (File.Exists(fullPath))
                {
                    return true;
                }
            }
            catch { }
        }
        return false;
    }
    private static bool EnsureFFmpeg()
    {
        
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        var  localFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");
        
        if (File.Exists(Path.Combine(localFolder, binaryName)))
        {
            GlobalFFOptions.Configure(options=> options.BinaryFolder = localFolder);
            return true;
        }

        var currentConfig = ConfigurationServices.Load();
        var savedPath = currentConfig.FFmpegBinaryFolder;
        if (!string.IsNullOrEmpty(savedPath) && File.Exists(Path.Combine(savedPath, binaryName)))
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = savedPath);
            return true;
        }

        if (IsFFmpegGlobalPath(binaryName))
        {
            return true;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[Dependency Check] FFmpeg binary was not found in the local folder");
        Console.WriteLine("FFmpeg is required to extract audio from video containers.");
        Console.ResetColor();
        
        Console.WriteLine("\nPlease enter the Folder Path where FFmpeg is installed");
        Console.Write("(or press Enter if it is already installed in your Global Path)\nFolder Path:");
        
        var userPath = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(userPath))
        {
            Console.WriteLine("[Info] Assuming FFmpeg is available in Global PATH. Proceeding...");
            return true;
        }
        userPath = userPath.Trim('"');
        if (!Directory.Exists(userPath) || !File.Exists(Path.Combine(userPath, binaryName)))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: '{binaryName}' could not be found inside '{userPath}'");
            Console.ResetColor();
            return false;
        }

        var  configToSave = ConfigurationServices.Load();
        configToSave.FFmpegBinaryFolder = userPath;
        ConfigurationServices.Save(configToSave);
        
        GlobalFFOptions.Configure(options => options.BinaryFolder = userPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[Success] FFmpeg path linked successfully!\n");
        Console.ResetColor();
        return true;
    }
    
  
}
