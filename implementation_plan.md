# Audio Processing & Transcription Implementation Plan

Akan dilakukan tiga tahap pengembangan untuk memenuhi seluruh skenario yang Anda minta.


## Phase 1: VAD Detection (Tanpa NAudio)
Anda **tidak perlu** menginstall `NAudio`. File `wav` sudah berhasil dibaca sebagai `byte[]` (PCM 16-bit Mono 16000Hz). Kita dapat mengonversi byte array ini ke `float[][]` secara manual di C#.

### `Src/AudioTranscription.cs`
- Tambahkan logika untuk memproses `byte[]` PCM. Setiap 2 byte (16-bit `short`) dikonversi ke `float` dengan membaginya dengan `32768.0f`.
- Potong data float menjadi chunk berukuran `512` (karena Sample Rate 16000Hz membutuhkan 512 sampel per proses untuk Silero VAD).
- Panggil `vadModel.Call(chunk, 16000)` untuk setiap potongan, dan tampilkan indikator suara (probabilitas > 0.5 berarti ada suara).
- Lakukan test run dengan perintah `dotnet run -- -i testing/episode2.mkv`.

## Phase 2: Pemilihan Bahasa (Track Selection)
Jika Phase 1 berhasil, kita akan menambahkan opsi pemilihan bahasa.

### `Src/AudioTranscription.cs` atau `Program.cs`
- Sebelum ekstrak audio dengan FFmpeg, jalankan `FFProbe.Analyse(videoPath)` untuk mendeteksi `AudioStreams`.
- Ambil metadata bahasa (seperti `spa` atau `eng`).
- Tampilkan daftar bahasa yang tersedia ke console agar user bisa menginput angka pilihan mereka.
- Ubah argumen `.WithCustomArgument("-map 0:a:0")` pada FFmpeg menjadi `-map 0:a:{Pilihan_User}`.

## Phase 3: Voice to Text (Whisper) & Output .srt
- gunakan whisper jika pun ada file letakan di folder models dan tambahkan resync.csproj
- Setelah filter VAD dan track selesai, kita akan konversi percakapan ke `.srt`.

### Penambahan Package
- Install `Whisper.net` dan `Whisper.net.Runtime` (atau package Whisper yang sesuai untuk C#) untuk menjalankan Whisper secara efisien tanpa butuh banyak memori, dibanding memanggil program eksternal.

### `Src/AudioTranscription.cs`
- Inisialisasi prosesor Whisper.
- Lewatkan data audio (hanya di bagian yang terdeteksi manusia oleh VAD, atau seluruhnya jika Whisper VAD sudah cukup baik).
- Dapatkan `SegmentData` dari Whisper yang sudah memiliki rentang waktu (`Start` dan `End`).
- Format teks dan rentang waktu ke format SubRip (`.srt`) seperti `00:00:00,000 --> 00:00:00,000`.
- Simpan `.srt` ke direktori output (atau sama dengan lokasi video).

## Verification Plan
1. **Automated / Manual Execution**: Jalankan `dotnet run -- -i testing/episode2.mkv`.
2. Pastikan VAD berhasil mendeteksi dan menampilkan persentase kemungkinan suara.
3. Pastikan CLI memberikan opsi pemilihan bahasa sebelum ekstraksi FFmpeg.
4. Pastikan output akhir berupa file `.srt` dengan format timestamp yang benar.
