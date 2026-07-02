using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Resync;

class Program
{
    private static int Main(string[] args)
    {
        var inputOption = new Option<FileInfo>("--input", "-i")
        {
            Required = true,
            Description = "Input .srt file path"
        };
        var outputOption = new Option<FileInfo>("--output", "-o")
        {
            Required = true, 
            Description = "Output result file path"
        };
        
        var startOption = new Option<string?>("--start", "-s")
        {
            Description = "New start time (example: 00:00:25,644)"
        };
        
        var secondsOption = new Option<double?>("--seconds", "-t")
        {
            Description = "Shift time in seconds (example: 2.5 to advance, -1.5 to delay)"
        };

        RootCommand rootCommand = new("Resync SRT - Re-index and synchronize subtitle timing");
        rootCommand.Add(inputOption);
        rootCommand.Add(outputOption);
        rootCommand.Add(startOption);
        rootCommand.Add(secondsOption);
        
        rootCommand.SetAction(parseResult =>
        {
            var inputFile = parseResult.GetValue(inputOption)!;
            var outputFile = parseResult.GetValue(outputOption)!;
            var targetStartTimeStr = parseResult.GetValue(startOption);
            var  targetSeconds = parseResult.GetValue(secondsOption);
            if (!inputFile.Exists)
            {
                Console.WriteLine("Error: Input file not found.");
                return;
            }

            var lines = File.ReadAllLines(inputFile.FullName , System.Text.Encoding.UTF8);
            var outputLines = new List<string>();
            var newIndex = 1;
            
            TimeSpan? timeOffset = null;
            
            if (targetSeconds.HasValue)
            {
                timeOffset = TimeSpan.FromSeconds(targetSeconds.Value);
            }
            
            const  string timePattern = @"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})";

            for (var i = 0; i < lines.Length; i++)
            {
                var currentLine = lines[i].Trim();
                
           
                var isIndexLine = Regex.IsMatch(currentLine, @"^\d+$") && 
                                   (i + 1 < lines.Length && lines[i + 1].Contains("-->"));

                if (isIndexLine)
                {
                    outputLines.Add(newIndex.ToString());
                    newIndex++;
                    continue;
                }
                
                var timeMatch = Regex.Match(currentLine, timePattern);
                if (timeMatch.Success)
                {
                    var originalStart = TimeSpan.ParseExact(timeMatch.Groups[1].Value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
                    var originalEnd = TimeSpan.ParseExact(timeMatch.Groups[2].Value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);

                    if (timeOffset == null && !string.IsNullOrEmpty(targetStartTimeStr))
                    {
                        if (TimeSpan.TryParseExact(targetStartTimeStr, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var targetStart))
                        {
                            timeOffset = targetStart - originalStart; 
                        }
                        else
                        {
                            Console.WriteLine("Error: Invalid --start time format. Use HH:mm:ss,fff (example: 00:00:25,644)");
                            return;
                        }
                    }
                    
                    if (timeOffset.HasValue)
                    {
                        var newStart = originalStart + timeOffset.Value;
                        var newEnd = originalEnd + timeOffset.Value;

                      
                        if (newStart < TimeSpan.Zero) newStart = TimeSpan.Zero;
                        if (newEnd < TimeSpan.Zero) newEnd = TimeSpan.Zero;

                        var newLine = $"{newStart.ToString(@"hh\:mm\:ss\,fff")} --> {newEnd.ToString(@"hh\:mm\:ss\,fff")}";
                        outputLines.Add(newLine);
                    }
                    else
                    {
                        outputLines.Add(currentLine); 
                    }
                    continue;
                }
                outputLines.Add(lines[i]);
            }

            File.WriteAllLines(outputFile.FullName, outputLines, System.Text.Encoding.UTF8);
            Console.WriteLine($"Success! File saved at: {outputFile.FullName}");
            Console.WriteLine($"Total subtitles processed: {newIndex - 1}");
            
            if (timeOffset.HasValue)
            {
                Console.WriteLine($"Time successfully shifted by: {timeOffset.Value.TotalSeconds} seconds");
            }
        });

        return rootCommand.Parse(args).Invoke();
    }
}
