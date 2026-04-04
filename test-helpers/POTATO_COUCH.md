# Couch Potato Test — Design & Implementation Plan

## What Is This?

The "Couch Potato Test" is an automated end-to-end test for dIKta.me's dictation pipeline. It:

1. Plays pre-recorded speech through the system speakers
2. The real microphone picks it up (acoustic coupling — no virtual audio)
3. Triggers the app to record, then stop, via IPC
4. Waits for the full pipeline: Recording → STT → LLM → Injection → Idle
5. Logs timing per phrase and compares transcription to expected text (from SRT subtitles)

This tests the **entire real pipeline** with real audio degradation — speaker → room → microphone. No mocking.

## Architecture

```
┌──────────────┐    Named Pipe IPC     ┌──────────────┐
│  Python      │◄─────────────────────►│  DiktaMe App │
│  Feeder      │  JSON commands/events │  (WinUI 3)   │
│              │                       │              │
│  Plays audio │───► Speakers ───► Mic─┤  Records mic │
│  via         │       (air)           │  via NAudio  │
│  simpleaudio │                       │              │
└──────────────┘                       └──────────────┘
```

## The IPC Protocol

**Pipe:** `\\.\pipe\DiktaMe.V2.Api`
**Format:** Newline-delimited JSON, one object per line.

### Server → Client Events

On connection, the server sends 3 events immediately (the "snapshot"):
```json
{"event":"state","state":"Idle"}
{"event":"settings","RawModeOverride":false,...}
{"event":"modes","modes":[...]}
```

During operation, state changes are broadcast to all clients:
```json
{"event":"state","state":"Recording"}
{"event":"state","state":"Transcribing"}
{"event":"state","state":"Processing"}
{"event":"state","state":"Injecting"}
{"event":"state","state":"Idle"}
```

### Client → Server Commands

```json
{"action":"trigger","pipeline":"dictate"}
{"action":"toggle","setting":"AutoCollapse"}
{"action":"query","target":"settings"}
```

The trigger is a **toggle**: first call starts recording, second call stops it.

### Known Working Client: test-ipc-pipe.ps1

Uses .NET `NamedPipeClientStream` + `StreamReader` + `StreamWriter`:
```powershell
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "DiktaMe.V2.Api", [System.IO.Pipes.PipeDirection]::InOut)
$pipe.Connect(5000)
$writer = New-Object System.IO.StreamWriter($pipe)
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($pipe)

# Read: $reader.ReadLine()
# Write: $writer.WriteLine($json)
```

This works perfectly. Connects, reads events, sends commands, sees state transitions.

## What Went Wrong With the Python Client

The current `audio_feeder.py` uses `win32file.CreateFile()` + `ReadFile(handle, 1)` (one byte at a time) with a background listener thread. This approach has multiple compounding bugs:

### Bug 1: Abandoned reader threads corrupt the pipe

The `connect()` method used `_readline_blocking()` which spawned a **new thread per read**, each calling `ReadFile(handle, 1)`. If any read timed out, the thread was abandoned but still blocking inside `ReadFile` on the same handle. When the listener thread later started, two threads were reading from the same pipe — bytes got split randomly between them, corrupting JSON lines.

**Status:** Fixed by removing `_readline_blocking` entirely. Listener thread now handles all reads.

### Bug 2: UTF-8 BOM corrupts first JSON line

The C# `StreamWriter` with `Encoding.UTF8` writes a 3-byte BOM (`EF BB BF`) before the first line. Python's `json.loads()` chokes on the BOM character (`\uFEFF`) prepended to the JSON. The first event (state) is silently dropped by the `except JSONDecodeError: pass` handler. The feeder never learns the initial state.

**Status:** Fixed by stripping `\uFEFF` from decoded lines.

### Bug 3: Byte-by-byte reading is fragile and slow

`ReadFile(handle, 1)` on a synchronous pipe handle reads one byte at a time. This works but is orders of magnitude slower than buffered line reading. More importantly, any exception in the read loop silently kills the listener thread (`except Exception: pass`), with no diagnostic output.

**Status:** Partially addressed with print statements. Root fragility remains.

### Bug 4: No audio ever played

When `send_command("START")` times out (because the listener never saw the Recording state event — Bug 1), the feeder's `smart_feed()` calls `continue`, skipping the audio playback entirely. The app is left recording silence. The next phrase calls `wait_for_idle()` which hangs because the app is stuck in Recording. The user has to manually stop it with a hotkey.

**Status:** Code flow is correct IF the listener receives events. This is a symptom, not a root cause.

## The New Plan: Rewrite the IPC Client

Instead of continuing to patch the `win32file.ReadFile` byte-by-byte approach, rewrite `DiktaMeIpcClient` to mirror what **actually works**: the PowerShell pattern using high-level stream I/O.

### Option A: Python `open()` on the pipe path (simplest)

```python
pipe = open(r"\\.\pipe\DiktaMe.V2.Api", "r+b", buffering=0)
# Read: line = pipe.readline()
# Write: pipe.write(json.dumps(cmd).encode() + b"\n"); pipe.flush()
```

**Risk:** Python's `open()` on named pipes on Windows is unreliable — it was the FIRST thing tried in the original V2 port and failed. May not support bidirectional I/O.

### Option B: pywin32 `CreateFile` + buffered `FileObject` wrapper (moderate)

```python
import win32file, msvcrt, os

handle = win32file.CreateFile(PIPE_NAME, ...)
fd = msvcrt.open_osfhandle(handle.Detach(), os.O_RDWR)
pipe = os.fdopen(fd, "r+b", buffering=0)
# Then use pipe.readline() / pipe.write() with a background thread
```

Converts the win32 HANDLE to a Python file descriptor. Gets buffered I/O without .NET. Listener thread uses `readline()` instead of byte-by-byte reads.

**Risk:** `msvcrt.open_osfhandle` on named pipe handles may not work. Needs testing.

### Option C: pywin32 `CreateFile` + larger `ReadFile` buffer (safe incremental fix)

Keep the current `win32file.CreateFile` connection but replace byte-by-byte reading with buffered reads:

```python
def _read_line(self) -> str:
    """Read one newline-terminated line from the pipe."""
    buf = b""
    while True:
        _, data = win32file.ReadFile(self._handle, 4096)
        buf += data
        if b"\n" in buf:
            line, self._remainder = buf.split(b"\n", 1)
            return line.decode("utf-8", errors="replace").lstrip("\ufeff")
    # Handle remainder buffer for next call
```

Reads 4KB chunks instead of single bytes. Keeps a remainder buffer for data after the newline. Much faster, still uses the proven `win32file` connection.

**Risk:** Lowest risk. Same connection pattern that we've verified works. Just changes the read strategy.

### Bug 5: Synchronous handle deadlocks read+write from different threads

**Discovered:** Attempt 1 of Option C implementation.

The pipe is opened with flag `0` (synchronous). The listener thread blocks in `ReadFile(handle, 4096)` waiting for data. When the main thread calls `WriteFile(handle, ...)` to send a command, it blocks too — Windows serializes all I/O on a synchronous handle. The write can't proceed until the read completes, but the read won't complete until the server sends data, and the server won't send data until it receives the command. **Deadlock.**

**Fix:** Open the pipe with `FILE_FLAG_OVERLAPPED` and use overlapped I/O for reads, or use `pywintypes.OVERLAPPED` objects. This allows concurrent read+write on the same handle.

## Failure Log

### Attempt 1 — Byte-by-byte reader with abandoned threads (original code)

**What:** `_readline_blocking()` spawned a new thread per read, each calling `ReadFile(handle, 1)`. Timed-out threads were abandoned but still held the pipe handle. Listener thread started afterward, two threads reading same handle = corrupted data.

**Symptom:** `send_command("START")` timed out waiting for "Recording" state. Feeder skipped audio playback. App recorded silence forever. User had to manually stop with hotkey.

**Fix applied:** Removed `_readline_blocking`, started listener thread first, waited for state event from queue.

### Attempt 2 — Listener receives events but script hangs after "OK"

**What:** After fixing Attempt 1, the listener thread successfully received the initial state event (`[LISTENER] state: Idle` printed). But the script hung immediately after printing "OK" — never reached preflight step 3 or 4.

**Root cause:** `disable_autocollapse()` calls `self.send()` → `win32file.WriteFile()` on the main thread. But the listener thread is blocked in `ReadFile(handle, 4096)` on the same handle. With a synchronous handle (flag `0`), Windows serializes all I/O — the write can't start until the read completes. The read won't complete until the server sends data. The server won't send data until it receives the command. **Deadlock.**

**Fix applied:** Changed pipe open flag from `0` to `FILE_FLAG_OVERLAPPED`. Rewrote listener to use overlapped reads (`pywintypes.OVERLAPPED` + `WaitForSingleObject`). Rewrote `send()` to use overlapped writes.

### Attempt 3 — Overlapped I/O produces garbage data + CloseHandle crash

**What:** Opened pipe with `FILE_FLAG_OVERLAPPED`, used `pywintypes.OVERLAPPED` for reads.

**Symptom 1 — Garbage data:** The overlapped `ReadFile` returns raw memory garbage instead of pipe data. Output shows binary noise (`6� %�6��8�Є�8�`) mixed with fragments of real JSON. The `GetOverlappedResult` call returns a byte count but the buffer (`chunk[:nbytes]`) contains corrupted data. Likely cause: the `ReadFile` with overlapped I/O on pywin32 doesn't populate the read buffer the same way as synchronous reads — the buffer object returned by `ReadFile` may be a pre-allocated buffer that `GetOverlappedResult` doesn't update in-place.

**Symptom 2 — CloseHandle crash:** `win32event.CloseHandle()` doesn't exist. The function is `win32api.CloseHandle()`. Script crashes on first `send()` call.

**Fix:** Abandon overlapped I/O approach entirely. Instead, use **two separate pipe connections** — one for reading (listener thread), one for writing (main thread). Each uses synchronous I/O. No overlapped complexity. The C# server already supports `MaxAllowedServerInstances` so multiple connections work.

### Attempt 4 — Two connections: second one gets ERROR_PIPE_BUSY (231)

**What:** Opened two separate synchronous pipe connections — one for reading (listener), one for writing (main thread). First connection succeeded. Second connection failed with `(231, 'CreateFile', 'All pipe instances are busy.')`.

**Root cause:** The C# `AcceptLoopAsync` creates ONE `NamedPipeServerStream` at a time, calls `WaitForConnectionAsync`, and only loops back to create the next instance after the first client connects and the handler task starts. The second `CreateFile` arrives during the window between accepting the first client and creating the next pipe instance. With the snapshot writes happening synchronously before the accept loop creates the next instance, there's a race window where no pipe instance is available.

**Fix:** Abandon two-connection approach. Go back to a single connection. Solve the read/write deadlock differently: use a **single pipe handle** but make the listener thread do ALL I/O — both reading events AND sending commands. The main thread puts commands into a thread-safe queue, the listener thread picks them up and writes them between reads.

### Attempt 5 — Single overlapped handle with IO thread: writes never execute

**What:** Single `FILE_FLAG_OVERLAPPED` handle. One IO thread handles both reads (overlapped `ReadFile` with 50ms poll) and writes (drain `_cmd_queue` between read polls).

**Symptom:** IO thread successfully reads initial snapshot events (`[IO] read 3B immediate`, `[IO] read 128B immediate`, `[IO] read 128B async`). Events reach Python. AutoCollapse check passes. But when `send_command("START")` puts a trigger on `_cmd_queue`, the IO thread never writes it — no `[IO] wrote` log appears. The 10s timeout expires. All 3 phrases fail.

**Likely cause:** pywin32's overlapped ReadFile with `AllocateReadBuffer` may not interleave correctly with overlapped WriteFile on the same handle. The write drain section runs but either the `WriteFile` call silently fails, the event never signals, or there's a subtle bug in the overlapped state machine. Without native Windows debugging tools, this is very hard to diagnose.

**Conclusion:** Five attempts using `win32file` APIs have all failed for different reasons. The pywin32 named pipe API is unreliable for bidirectional overlapped I/O. **Abandoning win32file entirely.**

### NEW APPROACH: PowerShell subprocess bridge

The PowerShell `test-ipc-pipe.ps1` has always worked. Instead of fighting pywin32, use a tiny PowerShell script as a bridge process:

- Python spawns PowerShell subprocess
- PowerShell connects to the named pipe using .NET `NamedPipeClientStream` + `StreamReader` + `StreamWriter` (proven working)
- Python writes JSON commands to PowerShell's stdin
- PowerShell forwards them to the pipe and streams pipe events back to Python via stdout
- Python reads events from PowerShell's stdout

This is ~20 lines of PowerShell. It completely sidesteps every pywin32 issue we've hit.

### Attempt 6 — PowerShell bridge: selectors crash on Windows pipes

**What:** Replaced all pywin32 with a PowerShell subprocess bridge. Python uses `selectors.DefaultSelector` to non-blocking-read bridge's stderr for connection status.

**Symptom:** `OSError: [WinError 10093] WSAStartup failed`. Python's `selectors` module on Windows uses `select.select()` which requires Winsock sockets. Pipe-backed file descriptors from `subprocess.Popen(stderr=PIPE)` are not sockets.

**Fix:** Replace `selectors` with a dedicated stderr drain thread. Thread reads stderr lines, appends to a shared list. Main thread polls the list for "connected"/"failed" keywords.

### Attempt 7 — Bridge reader thread exits immediately (no events)

**What:** Bridge connects successfully (`BRIDGE: connected`). Python starts reader thread on bridge stdout. Thread exits immediately — `[BRIDGE] reader thread exited` — no events read. `_connected` set to False. `send()` raises OSError.

**Root cause:** PowerShell's `System.Threading.Thread` with `ThreadStart` scriptblock cannot capture `$reader` from parent scope. The thread gets a null `$reader`, `ReadLine()` returns null immediately, loop exits.

**Fix 1:** Use `ParameterizedThreadStart` and pass `$script:pipeReader` as explicit argument to `$readerThread.Start($script:pipeReader)`.

**Fix 2:** Python `send()` checks `_proc.poll()` (is process alive?) instead of `_connected` flag, since the reader thread setting `_connected = False` is a race condition.

### Attempt 8 — ParameterizedThreadStart still can't read pipe

**What:** Changed `ThreadStart` to `ParameterizedThreadStart`, passed `$script:pipeReader` as explicit argument. Same result — reader thread exits immediately, no events read, bridge process dies.

**Root cause:** PowerShell threads (even with ParameterizedThreadStart) run in separate Runspaces. The StreamReader object either can't cross Runspace boundaries, or the named pipe's underlying handle isn't accessible from the child Runspace. This is a fundamental PowerShell limitation for cross-thread .NET stream access.

**Fix for Attempt 9:** Flip the architecture completely:
- **Main thread** reads from the pipe (ReadLineAsync + polling) — no cross-thread pipe sharing
- **Background Runspace** reads from stdin only — just `[Console]::In.ReadLine()` → `ConcurrentQueue<string>`. No pipe objects cross thread boundaries at all.
- Main thread drains the ConcurrentQueue and writes to the pipe.
- Uses `RunspaceFactory.CreateRunspace()` + `SessionStateProxy.SetVariable()` for safe variable sharing (the standard PowerShell threading pattern).

### Attempt 9 — Bridge connects, commands sent, but server never receives them

**What:** Rewrote bridge with Runspace-based stdin reader + ReadLineAsync polling. Bridge connects, reads initial state event (`[BRIDGE] state: Idle`), sends commands (`[BRIDGE] >> {"action": "trigger"...}`). But the app never reacts — no recording starts, no state changes.

**Root cause found in C# server logs:** `sending initial snapshot to client...` at 19:52:36 — but `initial snapshot sent` NEVER appears. The server is **stuck writing the snapshot**. The `StreamWriter` was created with `bufferSize: 1`, causing each character to be written as a separate kernel call. For a settings JSON of ~2KB, that's ~2000 individual pipe writes. The pipe's flow control + per-char kernel overhead made the snapshot write take so long that the server never entered the read loop to receive commands.

**Additionally:** `Encoding.UTF8` writes a BOM preamble on first write, which corrupted the first JSON line for clients.

**Fix (C# server):** Changed `LocalApiServer.HandleClientAsync`:
- `new UTF8Encoding(false)` — no BOM
- `bufferSize: 4096` — batch writes
- `AutoFlush = true` — flush after each WriteLine (safe since client is actively reading; the working `test-ipc-pipe.ps1` also uses `AutoFlush = true`)

This is the FIRST C# server-side fix. All previous attempts only changed the Python client.

### Attempt 10 — Server snapshot completes, but commands never arrive

**What:** C# StreamWriter fix worked — `initial snapshot sent, entering read loop` now appears in server logs. The bridge receives events (`[BRIDGE] state: Idle`), AutoCollapse check passes, Python sends commands (`[BRIDGE] >> {"action": "trigger"...}`). But server's `ReadLineAsync` never receives anything — no `received command` log.

**Root cause (investigating):** The bridge's stdin→cmdQueue→pipeWriter pipeline may be broken. Python writes to bridge's stdin, but the background Runspace that reads stdin and enqueues to `$cmdQueue` may not be receiving the data. The main thread's `$cmdQueue.TryDequeue()` → `$pipeWriter.WriteLine()` never fires because the queue is empty. Added diagnostic stderr prints to both the stdin reader and the pipe writer to identify which link in the chain is broken.

**Status:** Diagnostics added. Need one more run.

### Attempt 10b — AutoFlush deadlocks snapshot write for second client

See above.

### Attempt 10c — Direct pipe.Write for snapshot, bufferSize=1 for StreamWriter

**What:** Snapshot written as raw bytes via `pipe.Write()`. Snapshot completes (423 bytes, 35ms). Server receives settings query. But server never receives trigger commands even though bridge logs `writing to pipe:` for them.

**Root cause:** The bridge's `$pipeWriter` had `AutoFlush = $true`. `AutoFlush` calls `FlushFileBuffers()` on every `WriteLine()`. The settings query write succeeded because the server was actively in `ReadLineAsync`. But the trigger write blocked on `FlushFileBuffers()` — the server read the query response but didn't read the trigger fast enough. The bridge's main loop froze on the first trigger write, preventing all subsequent reads and writes.

**Fix attempted:** Changed bridge's StreamWriter to `bufferSize: 1` without AutoFlush (same pattern as C# server).

### Attempt 10d — Bridge bufferSize=1 without AutoFlush

**What:** Bridge StreamWriter changed to `bufferSize: 1` (no AutoFlush). `writing to pipe:` now appears for ALL triggers (not just the first). But server still never receives them — no `received command` log for any trigger. No state change events come back. All 3 phrases fail with TIMEOUT.

**Root cause:** `bufferSize: 1` on .NET StreamWriter means each character triggers `stream.Write(byte[], offset, 1)`. For a 50-char JSON command, that's 50 individual `NamedPipeClientStream.Write()` calls. Each is a separate kernel call. The data reaches the pipe kernel buffer char-by-char. The server's `ReadLineAsync()` reads from the kernel buffer. But the char-by-char writes may not include the newline character quickly enough — or the pipe's internal buffering may not coalesce the individual bytes into a readable line before the server's async read times out.

More likely: `bufferSize: 1` on a .NET NamedPipeClientStream may trigger the same `FlushFileBuffers()` behavior as `AutoFlush`, since each `stream.Write()` call goes directly to the kernel. The fundamental problem is that `StreamWriter.Flush()` → `NamedPipeStream.Flush()` → `FlushFileBuffers()` is unavoidable in .NET's pipe implementation.

## FINAL STATUS: ABANDONED

**12 attempts across 2 sessions. Zero successful end-to-end runs.**

### What works (verified):
- Python can connect to the named pipe via `win32file.CreateFile` (verified with standalone test)
- C# `LocalApiServer` receives and processes commands (verified: triggers start recording, settings queries responded to)
- `test-ipc-pipe.ps1` (PowerShell) works perfectly as an interactive client
- Audio loading, SRT parsing, subtitle merging all work
- `simpleaudio` is installed and detected

### What doesn't work:
- No approach successfully achieved bidirectional communication between the Python feeder and the C# pipe server during a test run
- Audio never plays because the START command always times out
- The app never starts/stops recording from the feeder

### Root problem:
Windows Named Pipes with bidirectional I/O from Python (or a Python-spawned subprocess) have fundamental issues:
1. **Synchronous handles deadlock** when read+write happen from different threads
2. **Overlapped I/O via pywin32** produces corrupt data or silently fails to write
3. **PowerShell subprocess bridge** deadlocks on `FlushFileBuffers()` called by both `AutoFlush` and `bufferSize=1` StreamWriter patterns
4. The only pattern that works (`test-ipc-pipe.ps1`) is single-threaded sequential read/write — incompatible with the feeder's need for concurrent event listening + command sending

### Options for future work:
1. **C# console bridge** — A 30-line C# console app using `NamedPipeClientStream` with proper async/await could solve all the .NET StreamWriter issues. `await writer.WriteLineAsync()` + `await writer.FlushAsync()` might not call `FlushFileBuffers()`.
2. **Switch to TCP** — Add a TCP listener to `LocalApiServer` alongside the named pipe. Python TCP sockets are trivial and proven (V1 used this).
3. **Switch to HTTP** — Embed a tiny HTTP server (Kestrel or HttpListener) for IPC. REST + SSE for events.
4. **Abandon automated testing** — Use manual hotkey-based testing with `test-ipc-pipe.ps1` for IPC verification only.

### Attempt 10b — AutoFlush deadlocks on second client

**What:** The `bufferSize: 4096` + `AutoFlush = true` fix worked for client #1 (Stream Deck, connected during startup), but client #2 (bridge, connected 13s later) hung on snapshot write. `AutoFlush` calls `FlushFileBuffers()` after every `WriteLine`. If the client hasn't started its `ReadLineAsync` yet (in the 20ms sleep between polls), `FlushFileBuffers` blocks indefinitely. This is the exact deadlock the original `bufferSize: 1` comment warned about.

**Additional finding:** `DiktaMe.StreamDeck.exe` (PID 34168) connects to the pipe as client #1, consuming an accept slot and receiving events. The bridge is always client #2.

**Fix:** Write the initial snapshot as raw bytes directly to `pipe.Write(snapshotBytes)`, bypassing StreamWriter entirely. One kernel call, no `FlushFileBuffers`, data goes straight to the pipe kernel buffer (default 64KB). For ongoing broadcasts, keep `bufferSize: 1` (pushes chars to kernel buffer without FlushFileBuffers). The snapshot is the only write that needs to be fast and non-blocking — broadcasts are small single-line events.

### Recommended: Option C

Option C is the safest because:
- The `win32file.CreateFile` connection is **verified working** (we connected successfully in testing)
- The C# trigger dispatch is **verified working** (app logs show Recording started)
- Only the reader loop changes — the connection, protocol, and command layer stay identical
- No new dependencies or unproven APIs

### Implementation Steps

1. Add a `_remainder` buffer field to `DiktaMeIpcClient.__init__`
2. Replace `_read_one_byte()` with `_read_line()` that reads 4KB chunks and splits on `\n`
3. Strip BOM (`\uFEFF`) from all decoded lines
4. Update `_listen_loop` to use `_read_line()` instead of byte-by-byte accumulation
5. Keep all existing: `send()`, `trigger_dictate()`, `wait_for_state()`, `send_command()`
6. Keep all existing: `smart_feed()`, `dumb_feed()`, audio playback, timing collection

### Verification

1. `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors
2. Restart DiktaMe app (to pick up `LocalApiServer.Start()` change)
3. Run: `.\test-helpers\Invoke-AudioFeeder.ps1 --last-download --count 3`
4. Expected output:
   - `[LISTENER] state: Idle` — listener receives initial state
   - `[CMD] START... -> Recording` — trigger works and feeder sees it
   - `[AUDIO] Playing X.Xs audio...` — audio plays through speakers
   - `[LISTENER] state: Transcribing` → `Processing` → `Injecting` → `Idle`
   - `[OK] Success: 3` in the summary

### Files

| File | Change |
|------|--------|
| `test-helpers/audio_feeder.py` | Rewrite `_read_one_byte` → `_read_line` with 4KB buffered reads + remainder. Strip BOM. |
| `src/DiktaMe.App/App.xaml.cs` | Already done: `LocalApiServer.Start()` uncommented |
| `src/DiktaMe.App/Services/LocalApiServer.cs` | Already done: trigger dispatch logging |
| `test-helpers/Invoke-AudioFeeder.ps1` | Already done: `PYTHONIOENCODING=utf-8` |
