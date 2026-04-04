# dIKta.me V2 Audio Feeder — How to Use

The **Couch Potato test** feeds pre-recorded speech (a YouTube video) through your physical speakers, lets the real microphone pick it up, and drives DiktaMe automatically — start recording, play audio, stop recording, wait for injection, repeat. This exercises the full pipeline (microphone capture → STT → LLM → text injection) with real acoustic degradation, no mocking, no virtual audio devices.

---

## Prerequisites

| Tool | Install |
|------|---------|
| Python 3.10+ | https://python.org |
| pydub | `pip install pydub` |
| simpleaudio | `pip install simpleaudio` |
| pysrt | `pip install pysrt` |
| yt-dlp | `pip install yt-dlp` (for downloading fixtures) |
| ffmpeg | `winget install ffmpeg` or https://ffmpeg.org |
| DiktaMe V2 | Running, in **Idle** state |

**Check everything at once:**
```
pip install pydub simpleaudio pysrt yt-dlp
ffmpeg -version
python test-helpers\audio_feeder.py --help
```

---

## Quick Start (3 steps)

**1. Download a test video:**
```
python test-helpers\fetch_test_data.py https://www.youtube.com/watch?v=<video_id>
```
Output lands in `test-helpers\fixtures\`. Pick a TED Talk or clear-speech podcast (10–30 min).

**2. Make sure DiktaMe is running and idle. Then run:**
```
.\test-helpers\Invoke-AudioFeeder.ps1 --last-download --count 10
```

**3. Watch the output.** After 10 phrases you'll see a summary:
```
TEST SUMMARY
============================================================
Total Phrases:  10
[OK] Success:   9
[X]  Failed:    1
[-]  Skipped:   0
Duration:       4.2 minutes (253.4s)
Avg per phrase: 25.3s
Success rate:   90.0%

==================================================
  TIMING SUMMARY (successful phrases only)
==================================================
  Stage              avg      p50      p95
  --------------------------------------------
  recording        8400ms   8100ms   9200ms
  stt               380ms    350ms    620ms
  llm               290ms    260ms    480ms
  inject             42ms     38ms     71ms
  --------------------------------------------
  TOTAL             960ms    920ms   1350ms
```

---

## Smart Mode vs Dumb Mode

| Mode | When it runs | How it slices |
|------|-------------|---------------|
| **Smart** | `.srt` subtitle file available | Slices audio using subtitle timestamps, merges clips <8s into longer phrases |
| **Dumb** | No `.srt` file | Fixed-length chunks (default: 10s) |

Smart mode produces more realistic results because phrase boundaries match natural speech pauses.

---

## CLI Reference

```
python audio_feeder.py [source] [options]
```

### Source (pick one)

| Flag | Description |
|------|-------------|
| `--last-download` | Auto-select most recent `.wav` in `test-helpers/fixtures/` |
| `--file FILE` | Specify audio file path directly |
| `--subs FILE` | Specify `.srt` file (optional; triggers Smart mode) |

### Slice control

| Flag | Default | Description |
|------|---------|-------------|
| `--count N` | 0 (all) | Process only N phrases |
| `--start-at N` | 0 | Start at subtitle index N (0-indexed) — use to resume |
| `--loop` | off | Loop audio infinitely (Ctrl+C to stop cleanly) |
| `--chunk-size SEC` | 10 | Fixed-chunk length in seconds (Dumb mode only) |

### Playback

| Flag | Description |
|------|-------------|
| `--no-simpleaudio` | Skip simpleaudio, use PyAudio → pydub fallback |

### Optimization mode

| Flag | Description |
|------|-------------|
| `--csv-out FILE` | Write per-phrase timing CSV to FILE |

### Benchmark mode (scaffold)

| Flag | Default | Description |
|------|---------|-------------|
| `--benchmark` | off | Enable benchmark mode (scaffold — see below) |
| `--configs JSON` | `["Cloud","Local"]` | JSON array of engine config names to cycle |

---

## Optimization Mode — Tweaking the Pipeline

Use this to measure the effect of a setting change on end-to-end latency.

**Workflow:**

1. **Establish a baseline** before any changes:
   ```
   .\Invoke-AudioFeeder.ps1 --last-download --count 20 --csv-out test-helpers\results\baseline.csv
   ```

2. **Make your change** (e.g. switch STT provider, adjust LLM model, disable a feature).

3. **Run again** with a different output file:
   ```
   .\Invoke-AudioFeeder.ps1 --last-download --count 20 --csv-out test-helpers\results\after.csv
   ```

4. **Compare** — open both CSVs in Excel or diff them:
   ```
   # Quick Python comparison:
   python -c "
   import csv, statistics
   def avg(f, col):
       rows = list(csv.DictReader(open(f)))
       vals = [float(r[col]) for r in rows if r['success']=='1' and float(r[col])>0]
       return statistics.mean(vals) if vals else 0
   for col in ['stt_ms','llm_ms','inject_ms','total_ms']:
       print(f'{col:12} baseline={avg(\"results/baseline.csv\",col):.0f}ms  after={avg(\"results/after.csv\",col):.0f}ms')
   "
   ```

**CSV columns:**

| Column | Description |
|--------|-------------|
| `phrase` | Phrase number (1-indexed) |
| `expected_text` | First 80 chars of SRT text |
| `recording_ms` | Duration of recording (trigger→stop) |
| `stt_ms` | Time in Transcribing state |
| `llm_ms` | Time in Processing state |
| `inject_ms` | Time in Injecting state |
| `total_ms` | Full cycle from trigger to Idle |
| `success` | 1 = pipeline completed, 0 = timeout/failure |

**Timing stages explained:**

```
trigger → [Recording] → trigger → [Transcribing] → [Processing] → [Injecting] → [Idle]
           ↑ recording_ms ↑           ↑ stt_ms ↑     ↑ llm_ms ↑   ↑ inject_ms ↑
           ←──────────────────────── total_ms ───────────────────────────────────→
```

> Note: `total_ms` includes recording time. To compare STT+LLM+inject only: `stt_ms + llm_ms + inject_ms`.

---

## Benchmark Mode — Comparing Models

> **Status: Scaffold.** The config-cycling loop is not yet fully automated. Full implementation is planned for a future session.

**What works today (manual workaround):**

1. Open Notepad (or any text editor), click into it
2. Enable **+enter** injection mode in DiktaMe (Settings → Workflows → append newline after injection)
3. Run the feeder once per config you want to compare:
   ```
   # Run 1: Cloud engine
   .\Invoke-AudioFeeder.ps1 --last-download --count 20
   ```
4. Switch the engine in DiktaMe Settings → AI Engine → Active Profile → Local
5. Run again:
   ```
   # Run 2: Local engine
   .\Invoke-AudioFeeder.ps1 --last-download --count 20
   ```
6. After each run the results are injected as separate lines into your text file. The two blocks of 20 lines can be compared side by side.

**What `--benchmark` does today:**

Running `--benchmark` prints a clear scaffold message explaining the above manual process, then runs the pre-flight checks and exits. No crashing, no surprises.

```
.\Invoke-AudioFeeder.ps1 --last-download --count 5 --benchmark
```

---

## Auto-Collapse Behaviour

The feeder automatically verifies and disables the CP Auto-Collapse setting at startup via IPC. If Auto-Collapse is on, the control panel animates (300–500ms per phrase) which inflates timing measurements. The feeder fixes this for you:

```
  [IPC] AutoCollapse already off — no change needed
  # or, if it was on:
  [IPC] AutoCollapse disabled for feeder run
```

The setting is **not restored** after the run ends (it was off by default; if you turned it on intentionally, re-enable it in Settings → General after testing).

---

## Troubleshooting

### "DiktaMe NOT responding"
- DiktaMe must be running before you start the feeder
- Check the pipe: `powershell -File test-helpers\test-ipc-pipe.ps1`

### "No .wav files found in fixtures/"
- Download a video first: `python test-helpers\fetch_test_data.py <URL>`

### "simpleaudio failed"
- The feeder automatically falls back to PyAudio, then pydub.playback
- Install PyAudio for better fallback: `pip install pyaudio`
- Or use `--no-simpleaudio` to skip simpleaudio from the start

### "ffmpeg not found"
- Install: `winget install ffmpeg`
- Or download from https://ffmpeg.org and add to PATH
- yt-dlp and pydub both need ffmpeg for audio conversion

### Phrases timing out ("Pipeline timeout")
- The app may be stuck in a non-Idle state
- Check DiktaMe for any blocking dialogs or notifications
- Increase timeout by editing `wait_for_idle(timeout=60.0)` in `audio_feeder.py`

### Audio plays but nothing is transcribed
- Check speaker volume — the mic must be able to hear the speakers
- Try a quieter room or position speakers closer to mic
- Check DiktaMe shows "Recording" state during playback (visible in CP)

### No SRT subtitles downloaded
- Some YouTube videos block subtitle downloads; try a different video
- The feeder falls back to Dumb mode automatically (fixed 10s chunks)

---

## File Layout

```
test-helpers/
  audio_feeder.py         ← Main feeder script (run directly or via wrapper)
  fetch_test_data.py      ← Download YouTube fixtures
  Invoke-AudioFeeder.ps1  ← PowerShell launcher (passes args through)
  AUDIO_FEEDER.md         ← This file
  fixtures/
    .gitkeep              ← Directory tracked in git (audio files are not)
    hDpjMJw3flk.wav       ← Example fixture (not committed)
    hDpjMJw3flk.en.srt    ← Matching subtitles (not committed)
  results/                ← CSV timing outputs (not committed)
    baseline.csv
    after.csv
```
