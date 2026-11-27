# TTSAPI — audio.cpp (OpenAI-compatible) TTS for ChilloutVR

Adds a TTS engine to ChilloutVR that speaks your text-comms messages through
[audio.cpp](https://github.com/0xShug0/audio.cpp) or any other server exposing an
OpenAI-style `POST /v1/audio/speech`.

It plugs into CVR's own TTS pipeline: the game plays the audio and transmits it as
your voice, so players nearby hear it with normal spatial falloff. The mod never
auto-selects itself and never touches your settings — you choose it as the TTS
module in game, and it only runs while CVR's TTS option for text comms is enabled.

## Requirements

- ChilloutVR 2026r182-hf4 (Mono) with MelonLoader 0.7.x.
- A speech server with a `/v1/audio/speech` endpoint that returns WAV. The setup
  below uses audio.cpp with a local model.
- Text comms must be enabled in game, including the TTS option.

## Server setup (audio.cpp)

1. Download an audio.cpp release and a TTS model package (Kokoro is small and
   CPU-friendly; any TTS family works). For a voice-clone model like Zonos3-Mini,
   start from [`docs/server-config-zonos3-example.json`](docs/server-config-zonos3-example.json)
   instead — see [Custom voice presets](#custom-voice-presets).
2. Save a config next to the server binary, based on
   [`docs/server-config-example.json`](docs/server-config-example.json), and point
   `path` at your downloaded GGUF. `models[].id` is the name the mod sends as
   `model` — this example uses `tts`.
3. Start it:

   ```powershell
   .\audiocpp_server.exe --config server.json --log --log-file server.log
   ```

4. Smoke-test it before touching the game:

   ```powershell
   curl.exe --fail-with-body http://127.0.0.1:8080/health

   # PowerShell mangles inline JSON passed to native curl.exe, so use a request file.
   '{"model":"tts","input":"Welcome to ChilloutVR."}' | Set-Content -Path request.json -Encoding ascii
   curl.exe --fail-with-body http://127.0.0.1:8080/v1/audio/speech `
     -H "Content-Type: application/json" `
     --data-binary "@request.json" -o speech.wav
   ```

   `speech.wav` should play. The first request after server start can be slow
   because the model loads on demand.

## Mod install

1. Build (or copy) `TTSAPI.dll` into `ChilloutVR\Mods\`. A Release build already
   deploys there.
2. Start the game once so `MelonPreferences.cfg` is created, then quit.
3. Edit `ChilloutVR\UserData\MelonPreferences.cfg`, section `[TTSAPI]`:

   ```ini
   [TTSAPI]
   BaseUrl = "http://127.0.0.1:8080"
   ApiKey = ""
   Model = "tts"
   Voice = "af_heart"
   Voices = "af_heart, am_michael"
   TimeoutSeconds = 180
   Speed = 0
   Volume = 1
   Instructions = ""
   ExtraHeaders = ""
   QueueMessages = true
   DelayChatBoxUntilSpeech = true
   ```

   `Model` must equal the server config's `models[].id`. `Voices` is the list CVR's
   voice selector shows; each entry is sent verbatim as the request's `voice`
   (Kokoro built-in ids such as `af_heart`, a configured audio.cpp
   `voice_presets` name, or a `voice_dir` WAV basename).

4. Launch the game and open the MelonLoader log; it should contain:

   ```
   [TTSAPI] TTS-API ready. Server: http://127.0.0.1:8080 | model: tts
   [TTSAPI] Registered TTS module 'AUDIOCPP'.
   ```

## Custom voice presets

Models without built-in speakers (Zonos3-Mini, XTTS, F5-TTS, …) take a reference
WAV per request. Define it once on the server as a preset, then select that preset
by name from CVR's voice list:

```json
"voice_presets": {
  "my_voice": {
    "voice_ref": "C:/audio-cpp/voices/my_voice.wav",
    "options": { "speaker_mode": "reference", "guidance_scale": 1.6 }
  }
},
"default_voice_preset": "my_voice"
```

Then set `Voice = "my_voice"` and `Voices = "my_voice"` in the mod
config. For several voices, add one preset per voice under `voice_presets` and list
every preset name in `Voices` (for example
`Voices = "my_voice, another_voice"`), then restart the server and the game.
CVR's TTS voice selector switches the preset for the next message. Each preset can
carry its own `options`, so voices can differ in sampling, guidance and
`speaker_mode`. The mod only sends `voice`; the server applies `voice_ref` and the preset
options, so no mod change is needed per voice. Zonos3 notes: the reference must be
WAV (mix-down and 44.1 kHz resample are automatic, first 60 s used);
`speaker_mode: "reference"` needs the raw-reference model package, while
`"embedding"` clones from a speaker embedding with a much faster first request.
Use a distinct preset name per voice and list them all in `Voices` to switch
between them in game.

## In game

1. In CVR's settings, enable text comms / ChatBox and its **TTS** option.
2. Open the text-comms keyboard. Because more than one engine is present (the
   built-in SYSTEM one, plus any other TTS mod), CVR shows a module selector —
   pick **audio.cpp (OpenAI-compatible)**. Pick a voice if you configured more
   than one.
3. Send a message. You hear it locally; other players hear it as your voice.

The module choice is stored by CVR, so later sessions keep using it until you
change it. If you also run another TTS mod (for example BetterDECtalk), the
selector simply lists both engines.

## Configuration reference

| Key | Default | Meaning |
|---|---|---|
| `BaseUrl` | `http://127.0.0.1:8080` | Server root; `/v1/audio/speech` is appended. |
| `ApiKey` | empty | Sent as `Authorization: Bearer <key>` when set. |
| `Model` | empty | Server model id. Required by audio.cpp (`missing required json key: model` otherwise); leave empty only for servers that accept an omitted model. |
| `Voice` | empty | Fallback voice when CVR has no voice selected. |
| `Voices` | empty | Comma/semicolon separated voice ids shown in CVR. Never displayed empty: falls back to `Voice`, then a `default` placeholder that is treated as "omit the voice" so the server's `default_voice_preset` applies. |
| `TimeoutSeconds` | `180` | Per-request timeout; long enough for a cold model load. |
| `Speed` | `0` | `0` omits the field. Servers/models without speed support reject non-1 values. |
| `Volume` | `1` | Playback gain for generated speech: `0` mute, `1` as generated, `2` max (clamped). Also scales the voice other players hear. CVR's own **TTS Volume** slider only affects your local playback. |
| `Instructions` | empty | OpenAI-style `instructions` field for models that accept it. |
| `ExtraHeaders` | empty | `Name: Value; Name2: Value2` added to every request. |
| `QueueMessages` | `true` | Speak messages in order: while one is generating or playing, later messages wait. Off = CVR default (a new message interrupts the current audio). |
| `DelayChatBoxUntilSpeech` | `true` | Hold the chatbox text until its speech starts, so text and audio appear together. Requires `QueueMessages`. |

## Message queue

Server-side TTS takes seconds per sentence, while CVR normally aborts the current
TTS job and stops its audio as soon as the next message is sent. With
`QueueMessages = true` (default), `TTSAPI` intercepts
`Comms_TTSHandler.ProcessMessage` with a Harmony prefix, holds each message, and
replays it once the previous one finished generating and playing. Order is kept,
only one request is in flight, and the current sentence is no longer cut off.

The queue holds up to 32 messages and drops the oldest when full. Sending an empty
message (CVR's stop action) clears the queue. Identical text arriving within one
second is treated as a single message, because some chatbox mods submit the same
message twice. Turning `QueueMessages` off returns to CVR's default behaviour
immediately. If the patch cannot be installed, the mod logs
`Could not install message-queue patch` and still works without queuing.

With `DelayChatBoxUntilSpeech = true` (default, and only while `QueueMessages` is
on), the chatbox text of a typed message is held back as well. It is sent to the
chatbox at the moment its speech starts playing, so other players see the text and
hear the voice together instead of reading it seconds before the audio is ready.
If synthesis fails, the text is released on its own; a message whose audio never
starts is released after 30 seconds.

## Troubleshooting

| Symptom | Check |
|---|---|
| No `Registered TTS module` line | MelonLoader log for `Could not register TTS module`; the mod must load at startup, before CVR initialises comms. |
| Nothing spoken in game, no errors | CVR's TTS option for text comms is off, or another module is selected in CVR's voice/module selector. |
| `TTS request failed: Cannot reach the speech server` | Server not running, wrong `BaseUrl`, firewall, or a proxy. |
| `TTS request failed: Speech request failed (400 BadRequest) ...` | `Model` does not match a configured `models[].id`, or the server rejected a field. |
| `... missing required json key: model` | `Model` is empty in the mod config; audio.cpp requires it. |
| `TTS request failed: Speech request failed (503 ...)` | The model is busy with another request; wait and resend. |
| `TTS request timed out after Ns` | Raise `TimeoutSeconds` for a cold model load, or check the server log. |
| `TTS response was not usable audio: not a RIFF/WAVE file (body starts with: ...)` | The endpoint returned HTML/JSON/MP3 instead of WAV — wrong URL, proxy error page, or a server whose default format is MP3 (e.g. real OpenAI). audio.cpp returns WAV by default. |
| `curl` gets `500` and the server log says `failed to parse json at byte 1` | PowerShell mangled inline JSON before `curl.exe` saw it. Put the body in a file and use `--data-binary "@request.json"`. |
| First message takes a long time | Cold model load; the server loads on the first request. Later messages are faster. |
| Voice list shows only `default` | `Voices`/`Voice` are empty; `default` is the placeholder and the server's default preset is used. |
| Other players don't hear it | They must receive your voice normally; check that the game's voice indicator moves while the message plays and that your mic isn't muted in a way that blocks TTS playback. |

## How it works

`AudioCppTtsModule` derives from CVR's `Comms_TTSModule`. CVR calls
`Initialize()` once and then `Process(text)` on a worker thread whenever a TTS
message is sent; the module converts the response to **48 kHz mono 16-bit PCM**
itself (CVR's own resampler only handles integer ratios, so 44.1 kHz would play
fast), applies a 15 ms fade-out and a 200 ms silent tail (with a 1-LSB marker so
CVR's positive-sample trim keeps it), and returns it to CVR, or `null` on failure.
The tail removes the abrupt end some TTS models produce and gives CVR's capture
thread time to send the last voice packet.

`TtsSynthesisService` talks to an `ITtsProvider`. Only
`OpenAiSpeechProvider` exists today (any OpenAI-compatible server that returns
WAV: audio.cpp, LocalAI, Kokoro-FastAPI, openedai-speech, …); adding ElevenLabs,
Azure, Piper or edge-tts means implementing `ITtsProvider` and selecting it in
the service factory — the CVR side stays unchanged.

## Development

You need the .NET SDK and a ChilloutVR install. `CVRPATH` defaults to the Steam
install path; override it for any other location:

```powershell
dotnet build .\TTSAPI.csproj -c Release -p:CVRPATH="D:\Games\ChilloutVR"
dotnet run --project .\tests\TTSAPI.Tests\TTSAPI.Tests.csproj
```

A Release build writes `TTSAPI.dll` straight into `$(CVRPATH)\Mods`. The tests
exercise the WAV parser, request builder, HTTP error mapping and synthesis
service without loading the game.

## License

MIT — see [LICENSE](LICENSE).
