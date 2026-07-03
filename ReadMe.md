# Resync SRT CLI

A simple, fast, and reliable Command-Line Interface (CLI) tool built with .NET 9 to re-index and synchronize `.srt` subtitle files.

## Key Features

* **Auto Re-index:** Automatically fixes and sequentializes messy subtitle numbering (starting from 1).
* **Sync by Exact Time:** Shifts all subtitle timings by matching the first subtitle's start time to a specific target time (Format: `HH:mm:ss,fff`).
* **Sync by Seconds:** Easily advance or delay subtitle timings using seconds (supports decimal values).
* **High Precision:** Maintains millisecond accuracy using C#'s standard `TimeSpan` calculations.
* **Hybrid Targeted Sync:** Fix desynced subtitles from a specific point forward without affecting earlier perfectly-timed lines. You can target the sync point using two flexible alternatives:
  * **Time-Anchor Target (`--from-time`):** Simply look at your video player, note the timestamp where the drift begins, and target that time directly. The tool will automatically hook onto the closest subtitle.
  * **Index Target (`--edit-index`):** Target a specific sequential line number directly if you prefer exact row precision.

##  Prerequisites
* [.NET 9.0 SDK](https://dotnet.microsoft.com/) (Required if you want to run via `dotnet run` or build the project from source).

## Installation & Build
You can run the program directly from the source code or build it into a standalone `.exe` file for portability.

**Build as a Single Executable (Windows x64):**
```bash
dotnet publish -c Release
```

The compiled resync.exe file will be generated in the bin/Release/net9.0/win-x64/publish/ folder and can be executed on any Windows machine without requiring the .NET SDK.

## Usage
Run the program via your terminal or Command Prompt.

### Available Parameters

| Command | Alias | Description                                                                                | Required |
| :--- | :--- |:-------------------------------------------------------------------------------------------| :---: |
| `--input` | `-i` | The original `.srt` file path to process.                                                  | ✅ Yes |
| `--output` | `-o` | The destination path to save the output `.srt` file.                                       | ✅ Yes |
| `--start` | `-s` | Adjusts the start time of the first subtitle (Example: `00:00:25,644`).                    | ❌ No |
| `--seconds` | `-t` | Shifts subtitle timing by a specific amount of seconds.                                    | ❌ No |
| `--edit-index` | `-e` | Target index number to start shifting. (Alternative to `-f`)                               | ❌ No |
| `--from-time` | `-f` | Target start time where desync begins. (Alternative to `-e`)                               | ❌ No |
| `--new-time` | `-n` | New start time for the targeted index and time (Requires `--edit-index` or `--from-time`). | ❌ No |

---

### Execution Examples

**1. Re-index Only (No time shift)**
Ideal when the subtitle timings are perfectly synced, but the index numbering is broken or inconsistent. The tool automatically sorts items chronologically and builds a clean 1-to-N sequential sequence.
```bash
resync -i "input.srt" -o "output.srt"
```

**2.Shift Time by Seconds (`--seconds` / `-t`)**

Use this parameter if the subtitles appear slightly too early or too late throughout the entire video.
  - Advance subtitles by 2.5 seconds (makes text appear later):
    ```bash
      resync -i "input.srt" -o "output.srt" -t 2.5
    ```
  - Delay subtitles by 1.5 seconds (makes text appear earlier):
  ```bash
    resync -i "input.srt" -o "output.srt" -t -1.5
  ```
**3.Global Sync to a Specific Time (`--start` / `-s`)**

If you know exactly when the very first dialog line should appear in the video, the program calculates the precise offset from the current first line and shifts the entire file accordingly.

```bash
  resync -i "input.srt" -o "output.srt" -s "00:01:15,500"
```
**4.Targeted Sync via Time-Anchor (`--from-time` / `-f` & `--new-time` / `-n` )**
Perfect when subtitles start drifting halfway through. Just note the timestamp in your video player where the lag starts. The tool dynamically finds the nearest subtitle and shifts it—along with everything after it—to the new correct timestamp

```bash
  resync -i "input.srt" -o "output.srt" -f "00:12:31,000" -n "00:01:00,533"
```
**5.Targeted Sync via Row Index (`--edit-index` / `-e` & `--new-time` / `-n` )**

An alternative precise targeting method. If you already know the exact line number where the desync begins (e.g., line 45), you can shift that row and all subsequent rows down the file, leaving rows 1 to 44 intact.

```bash
  resync -i "input.srt" -o "output.srt" -e 45 -n "00:25:30,150"
```

# Resync SRT CLI - Integration Test Suite

This document contains a comprehensive set of test cases to ensure all features run smoothly and safely before deploying the tool to a production environment.

## 🟢 1. Positive Test Cases (Success Scenarios)

Run the following commands in your terminal and verify that the output matches the expected results.
- [ ] **A. Global Sync via Target Time (`--start`)**
  - *Description:* Matches the first subtitle line's start time from `00:11:57,160` to exactly `00:00:25,644` (shifting back by ~691 seconds).
    ```bash
    dotnet run -- -i testing/episode1.srt -o testing/result_start.srt -s 00:00:25,644
    ```
  - *Expectation:* Terminal prints [Global Sync] Shifted all subtitles by -691.516 seconds. The file result_start.srt is successfully created with the perfectly adjusted first index.


- [ ] **B. Global Sync via Shifting Seconds (`--seconds`)**
  - *Description:* Advances all subtitle lines forward by `2.5` seconds using decimal values.
    ```bash
      dotnet run -- -i testing/episode1.srt -o testing/result_seconds.srt -t 2.5 
    ```
  - *Expectation:* Terminal prints [Global Sync] Shifted all subtitles by `2.5` seconds.

- [ ] **C. Targeted Sync via Subtitle Index Number (`--edit-index`)**
  - *Description:* Changes the 4th line (Index 193) and all subsequent lines to start at the 15-minute mark, leaving lines 1–3 untouched.
    ```bash
      dotnet run -- -i testing/episode1.srt -o testing/result_index.srt -e 4 -n 00:15:00,000
    ```
  - *Expectation:* Expectation: Terminal prints [Targeted Sync] Found target at index 4 (00:12:02,960). Shifted onwards...

- [ ] **D. Targeted Sync via Desync Timestamp (`--from-time`)**
  - *Description:* Automatically finds the closest subtitle appearing at or after `00:12:00,000` and shifts it to the new specified duration.
    ```bash
       dotnet run -- -i testing/episode1.srt -o testing/result_time.srt -f 00:12:00,000 -n 00:13:00,000
    ```
  - *Expectation:* Successfully detects the nearest line (at duration `00:12:00,760`) and shifts it and all following lines forward.


## 🔴 2. Negative Test Cases (Error Handling & Validation)

These scenarios ensure that the program handles bad user inputs gracefully without throwing native .NET runtime crashes (red walls of text).

- [ ] **A. Invalid / Missing Input File**
    ```bash
    dotnet run -- -i testing/ghost_file.srt -o testing/result.srt -s 00:00:25,644
    ```
  - *Expectation:* Standard output displays `Error: Input file not found.`

- [ ] **B. Missing New Time Parameter during Targeted Sync**
    ```bash
    dotnet run -- -i testing/episode1.srt -o testing/result.srt -e 3
    ```
  - *Expectation:* Standard output displays `Error: --new-time (-n) parameter is required for targeted sync.`

- [ ] **C. Providing Both Index and Time Parameters Simultaneously**
    ```bash
   dotnet run -- -i testing/episode1.srt -o testing/result.srt -e 3 -f 00:12:00,000 -n 00:13:00,000
    ```
  - *Expectation:* Standard output displays `Error: Please use either --edit-index (-e) OR --from-time (-f), do not use both.`

- [ ] **D. Target Index Out of Range**
    ```bash
   dotnet run -- -i testing/episode1.srt -o testing/result.srt -e 999 -n 00:15:00,000
    ```
  - *Expectation:* Standard output displays `Error: Index 999 out of range (Total subtitles : X).`

- [ ] **E. Malformed New Time Format**
    ```bash
   dotnet run -- -i testing/episode1.srt -o testing/result.srt -e 2 -n invalid_time_string
    ```
  - *Expectation:* Standard output displays `Error: Invalid --new-time format. Use HH:mm:ss,fff.`


## Sample File

```srt
190
00:11:57,160 --> 00:11:59,720
Gumball, kembalikan DVD hari ini

191
00:11:59,800 --> 00:12:00,640
atau didenda.

192
00:12:00,760 --> 00:12:02,840
Tak bisa kau saja? Kau punya mobil.

193
00:12:02,960 --> 00:12:06,040
Bukan aku yang menonton
<i>Alligators on a Train </i>72 kali.

194
00:12:06,800 --> 00:12:09,280
Secara teknis, kau penyewanya
dengan uangmu.

195
00:12:09,400 --> 00:12:11,960
Uang dari bekerja
untuk memberi makan kalian.

196
00:12:12,040 --> 00:12:14,480
Anak yang kau putuskan untuk lahirkan.
```