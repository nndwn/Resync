using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Resync;
public partial class ResyncTimeSpan
{
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IndexRegex();
    
    [GeneratedRegex(@"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})")]
    private static partial Regex TimePatternRegex();
    private  readonly string[] _timeFormats = [@"hh\:mm\:ss\,fff", @"hh\:mm\:ss\.fff"];
    
   public void Run(
        ParseResult parseResult, FileInfo inputFile, Option<FileInfo> outputOpt, Option<double?> secOpt, 
        Option<string?> startOpt, Option<int?> editOpt, Option<string> fromOpt, Option<string?> newOpt
        )
    {
        var outputFile = parseResult.GetValue(outputOpt);
        if (outputFile == null)
        {
            Console.WriteLine("Error: --output (-o) parameter is required when syncing .srt files.");
            return;
        }
        Console.WriteLine($"[Mode] Subtitle Synchronization detected for file: {inputFile.Name}");
        var blocks = ParseSrt(inputFile.FullName).OrderBy(b => b.StartTime).ToList();
        
        if (blocks.Count == 0)
        {
            Console.WriteLine("Error: No valid subtitles found.");
            return;
        }

        ApplyGlobalSync(blocks, parseResult.GetValue(secOpt), parseResult.GetValue(startOpt));
        
        var targetSuccess = ApplyTargetedSync(
            blocks,
            parseResult.GetValue(editOpt),
            parseResult.GetValue(fromOpt),
            parseResult.GetValue(newOpt));

        if (!targetSuccess) return;

        SaveSrt(outputFile.FullName, blocks);
    }
    
    private List<SubtitleBlock> ParseSrt(string filePath)
    {
        var blocks = new List<SubtitleBlock>();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        SubtitleBlock? currentBlock = null;
        foreach (var line in lines )
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IndexRegex().IsMatch(line)) continue;

            var match = TimePatternRegex().Match(line);
            if (match.Success)
            {
                currentBlock = new SubtitleBlock
                {
                    StartTime = TimeSpan.ParseExact(match.Groups[1].Value, _timeFormats, CultureInfo.InvariantCulture),
                    EndTime = TimeSpan.ParseExact(match.Groups[2].Value, _timeFormats, CultureInfo.InvariantCulture)
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
    
    private void ApplyGlobalSync(List<SubtitleBlock> blocks, double? seconds, string? startStr)
    {
        var globalDiff = TimeSpan.Zero;
        if (seconds.HasValue)
            globalDiff = TimeSpan.FromSeconds(seconds.Value);
        else if (!string.IsNullOrEmpty(startStr) &&
                 TimeSpan.TryParseExact(startStr, _timeFormats, CultureInfo.InvariantCulture, out var targetStart))
            globalDiff = targetStart - blocks[0].StartTime;
        if (globalDiff != TimeSpan.Zero)
        {
            foreach (var block in blocks)
            {
                block.StartTime += globalDiff;
                block.EndTime += globalDiff;
            }
            Console.WriteLine($"[Global Sync] Shifted all subtitles by {globalDiff.TotalSeconds} seconds");
        }
    }
    
    private bool ApplyTargetedSync(List<SubtitleBlock> blocks, int? editIndex, string? fromTimeStr,
        string? newTimeStr)
    {
        var syncMode = (
            HasIndex: editIndex.HasValue,
            HasTime: !string.IsNullOrEmpty(fromTimeStr),
            HasNewTime: !string.IsNullOrEmpty(newTimeStr));
        if (!syncMode.HasIndex && !syncMode.HasTime) return true;

        if (!syncMode.HasNewTime)
        {
            Console.WriteLine("Error: --new-time (-n) parameter is required for targeted sync.");
            return false;
        }

        if (syncMode.HasIndex && syncMode.HasTime)
        {
            Console.WriteLine("Error: Please use either --edit-index (-e) OR --from-time (-f), do not use both");
            return false;
        }

        if (!TimeSpan.TryParseExact(newTimeStr, _timeFormats, CultureInfo.InvariantCulture, out var targetNewTime))
        {
            Console.WriteLine("Error: Invalid --new-time format. Use HH:mm:ss,fff");
            return false;
        }

        var targetBlock = syncMode switch
        {
            (true, false, true) when editIndex!.Value > 0 && editIndex.Value <= blocks.Count => blocks[
                editIndex.Value - 1],
            (false, true, true) when TimeSpan.TryParseExact(fromTimeStr, _timeFormats, CultureInfo.InvariantCulture ,out var fromTime) => blocks.FirstOrDefault(b => b.StartTime >= fromTime),
            _ => null
        };

        if (syncMode.HasIndex && targetBlock is null)
        {
            Console.WriteLine($"Error: Index {editIndex} out of range (Total subtitles : {blocks.Count})");
            return false;
        }

        if (syncMode.HasTime && targetBlock is null)
        {
            Console.WriteLine($"Error: No subtitle found appearing at or after {fromTimeStr}");
            return false;
        }

        if (targetBlock is not null)
        {
            var targetedDiff = targetNewTime - targetBlock.StartTime;
            var startIndex = blocks.IndexOf(targetBlock);

            for (var i = startIndex; i < blocks.Count; i++)
            {
                blocks[i].StartTime += targetedDiff;
                blocks[i].EndTime += targetedDiff;
            }
            
            Console.WriteLine($"[Targeted Sync] Found target at index {startIndex + 1} ({targetBlock.StartTime:hh\\:mm\\:ss\\,fff}). Shifted onwards by {targetedDiff.TotalSeconds} seconds.");
            
        }

        return true;
    }
    
    private static void SaveSrt(string outputPath, List<SubtitleBlock> blocks)
    {
        var output = new List<string>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].StartTime < TimeSpan.Zero) blocks[i].StartTime = TimeSpan.Zero;
            if (blocks[i].EndTime < TimeSpan.Zero) blocks[i].EndTime = TimeSpan.Zero;
            
            output.Add((i+ 1).ToString());
            output.Add($"{blocks[i].StartTime:hh\\:mm\\:ss\\,fff} --> {blocks[i].EndTime:hh\\:mm\\:ss\\,fff}");
            output.AddRange(blocks[i].Lines);
            output.Add("");
        }
        File.WriteAllLines(outputPath, output, Encoding.UTF8);
        Console.WriteLine($"Done! Saved to {outputPath}. Total subtitles : {blocks.Count}");
    }

}
