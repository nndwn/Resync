# Resync SRT CLI

A simple, fast, and reliable Command-Line Interface (CLI) tool built with .NET 9 to re-index and synchronize `.srt` subtitle files.

## Key Features

* **Auto Re-index:** Automatically fixes and sequentializes messy subtitle numbering (starting from 1).
* **Sync by Exact Time:** Shifts all subtitle timings by matching the first subtitle's start time to a specific target time (Format: `HH:mm:ss,fff`).
* **Sync by Seconds:** Easily advance or delay subtitle timings using seconds (supports decimal values).
* **High Precision:** Maintains millisecond accuracy using C#'s standard `TimeSpan` calculations.

##  Prerequisites
* [.NET 9.0 SDK](https://dotnet.microsoft.com/) (Required if you want to run via `dotnet run` or build the project from source).

## Installation & Build
You can run the program directly from the source code or build it into a standalone `.exe` file for portability.

**Build as a Single Executable (Windows x64):**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The compiled resync.exe file will be generated in the bin/Release/net9.0/win-x64/publish/ folder and can be executed on any Windows machine without requiring the .NET SDK.

## Usage
Run the program via your terminal or Command Prompt.

Run the program via your terminal or Command Prompt.

### Available Parameters

| Command | Alias | Description | Required |
| :--- | :--- | :--- | :---: |
| `--input` | `-in` | The original `.srt` file path to process. | ✅ Yes |
| `--output` | `-out` | The destination path to save the output `.srt` file. | ✅ Yes |
| `--start` | `-s` | Adjusts the start time of the first subtitle (Example: `00:00:25,644`). | ❌ No |
| `--seconds` | `-sec` | Shifts subtitle timing by a specific amount of seconds. | ❌ No |

---

### Execution Examples

**1. Re-index Only (No time shift)**
Ideal when the subtitle timings are perfectly synced, but the index numbering is broken or inconsistent.
```bash
resync --input "input.srt" --output "output.srt"
```

**2. Shift Time by Seconds (`--seconds`)**
Use this parameter if the subtitles appear slightly too early or too late throughout the video.
* *Advance subtitles by 2.5 seconds (shows up later):*
  ```bash
  resync -in "input.srt" -out "output.srt" -sec 2.5
  ```
* *Delay subtitles by 1.5 seconds (shows up earlier):*
  ```bash
  resync -in "input.srt" -out "output.srt" -sec -1.5
  ```

**3. Sync to a Specific Time (`--start`)**
If you know exactly when the first dialog should appear in the video (e.g., at `00:01:15,500`), the program will automatically calculate the offset and adjust the rest of the file accordingly.
```bash
resync -in "input.srt" -out "output.srt" -s "00:01:15,500"
```