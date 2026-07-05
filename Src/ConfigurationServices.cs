using System.Text.Json;
using System.Text.Json.Serialization;

namespace Resync;


public class AppConfig
{
    public string? FFmpegBinaryFolder { get; set; }
}

[JsonSerializable(typeof(AppConfig))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public class ConfigurationServices
{
    private static readonly string ConfigFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Resync");

    private static readonly string ConfigFilePath = Path.Combine(ConfigFolder, "config.json");
    
    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return new AppConfig(); 
            
            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.AppConfig);
            
            return config ?? new AppConfig();
        }
        catch
        {
            return new AppConfig(); 
        }
    }
    
    public static void Save(AppConfig configData)
    {
        try
        {
            Directory.CreateDirectory(ConfigFolder);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var context = new SourceGenerationContext(options);
            var json = JsonSerializer.Serialize(configData, context.AppConfig);
            
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Warning] Could not save configuration: {e.Message}");
        }
    }
}