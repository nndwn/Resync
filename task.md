# Tasks

- `[x]` Phase 1: VAD Detection
  - `[x]` Modify `Src/AudioTranscription.cs` to convert `byte[]` to `float[][]`.
  - `[x]` Integrate `SileroVadOnnxModel.Call` in `ExtractAndProcessAudioSafely`.
  - `[x]` Test VAD with `dotnet run -- -i testing/episode2.mkv`.
- `[x]` Phase 2: Track Selection
  - `[x]` Read `AudioStreams` using `FFProbe.Analyse`.
  - `[x]` Prompt user to select audio track.
  - `[x]` Update `FFMpegArguments` with `-map 0:a:{trackIndex}`.
- `[x]` Phase 3: Whisper Integration
  - `[x]` Install `Whisper.net` and `Whisper.net.Runtime`.
  - `[x]` Implement Whisper transcription logic.
  - `[x]` Save output to `.srt`.
