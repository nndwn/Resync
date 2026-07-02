using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Resync;

partial class Program
{
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IndexRegex();
    
    [GeneratedRegex(@"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})")]
    private static partial Regex TimePatternRegex();
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
        
        var startOption = new Option<string?>("--start", "-s") { Description = "New start time (example: 00:00:25,644)" };
        var secondsOption = new Option<double?>("--seconds", "-t") { Description = "Shift time in seconds (example: 2.5 to advance, -1.5 to delay)" };
        var editIndexOption = new Option<int?>("--edit-index", "-e") { Description = "Target index to start shifting" };
        var newTimeOption = new Option<string?>("--new-time", "-n") { Description = "New start time for targeted index (HH:mm:ss,fff)" };

        RootCommand rootCommand = new("Resync SRT - Re-index and synchronize subtitle timing");
        rootCommand.Add(inputOption);
        rootCommand.Add(outputOption);
        rootCommand.Add(startOption);
        rootCommand.Add(secondsOption);
        rootCommand.Add(editIndexOption);
        rootCommand.Add(newTimeOption);
        
        rootCommand.SetAction(parseResult =>
        {
            var inputFile = parseResult.GetValue(inputOption)!;
            var outputFile = parseResult.GetValue(outputOption)!;
            var startStr = parseResult.GetValue(startOption);
            var seconds = parseResult.GetValue(secondsOption);
            var editIndex = parseResult.GetValue(editIndexOption);
            var newTimeStr = parseResult.GetValue(newTimeOption);
            
            if (!inputFile.Exists)
            {
                Console.WriteLine("Error: Input file not found.");
                return;
            }
            
            var blocks = ParseSrt(inputFile.FullName).OrderBy(b => b.StartTime).ToList();
            if (blocks.Count == 0)
            {
                Console.WriteLine("Error: No valid subtitles found.");
                return;
            }
            
            var globalDiff = TimeSpan.Zero;
            
            if (seconds.HasValue)
            {
                globalDiff = TimeSpan.FromSeconds(seconds.Value);
            }
            
            else if (!string.IsNullOrEmpty(startStr) && TimeSpan.TryParseExact(startStr, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var targetStart))
            {
                globalDiff = targetStart - blocks[0].StartTime;
            }
            if (globalDiff != TimeSpan.Zero)
            {
                foreach (var block in blocks)
                {
                    block.StartTime += globalDiff;
                    block.EndTime += globalDiff;
                }
                Console.WriteLine($"[Global Sync] Shifted all subtitles by {globalDiff.TotalSeconds} seconds.");
            }

            if (editIndex.HasValue )
            {
                
                if (string.IsNullOrEmpty(newTimeStr))
                {
                    Console.WriteLine("Error: --new-time parameter is required when using --edit-index.");
                    return;
                }
                
                if (editIndex.Value > 0 && editIndex.Value <= blocks.Count)
                {
                    if (TimeSpan.TryParseExact(newTimeStr, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture,
                            out var targetNewTime))
                    {
                        var targetArrayIndex = editIndex.Value - 1;
                        var targetedDiff = targetNewTime - blocks[targetArrayIndex].StartTime;
                        for (var i = targetArrayIndex; i < blocks.Count; i++)
                        {
                            blocks[i].StartTime += targetedDiff;
                            blocks[i].EndTime += targetedDiff;
                        }
                        
                        Console.WriteLine($"[Targeted Sync] Shifted from index {editIndex.Value} onwards by {targetedDiff.TotalSeconds} seconds.");
                    }
                }
                else
                {
                    Console.WriteLine("Error: Invalid --new-time format. Use HH:mm:ss,fff");
                    return;
                }
            }
            else
            {
                Console.WriteLine($"Error: Index {editIndex.Value} out of range (Total subtitles: {blocks.Count}).");
                return;
            }
            
            var output = new List<string>();
            for (var i = 0; i < blocks.Count; i++)
            {
              
                if (blocks[i].StartTime < TimeSpan.Zero) blocks[i].StartTime = TimeSpan.Zero;
                if (blocks[i].EndTime < TimeSpan.Zero) blocks[i].EndTime = TimeSpan.Zero;

                output.Add((i + 1).ToString());
                output.Add($"{blocks[i].StartTime:hh\\:mm\\:ss\\,fff} --> {blocks[i].EndTime:hh\\:mm\\:ss\\,fff}");
                output.AddRange(blocks[i].Lines);
                output.Add(""); 
            }
            
            File.WriteAllLines(outputFile.FullName, output, System.Text.Encoding.UTF8);
            Console.WriteLine($"Done! Saved to {outputFile.FullName}. Total subtitles: {blocks.Count}");
            
           
        });

        return rootCommand.Parse(args).Invoke();
    }
    
    private static List<SubtitleBlock> ParseSrt(string filePath)
    {
        var blocks = new List<SubtitleBlock>();
        var lines = File.ReadAllLines(filePath);
        SubtitleBlock? currentBlock = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IndexRegex().IsMatch(line)) continue;

            var match = TimePatternRegex().Match(line);
            if (match.Success)
            {
                currentBlock = new SubtitleBlock {
                    StartTime = TimeSpan.ParseExact(match.Groups[1].Value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture),
                    EndTime = TimeSpan.ParseExact(match.Groups[2].Value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture)
                };
                blocks.Add(currentBlock);
            }
            else if (currentBlock != null)
            {
                currentBlock.Lines.Add(line);
            }
        }
        return blocks;
    }
}
