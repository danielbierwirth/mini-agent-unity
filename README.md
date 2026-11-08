# mini-agent-unity

An **on-device smart-home agent** built in Unity. A tiger lives in an isometric 3D home (kitchen, living room, bedroom) and you drive the house with **natural-language commands** in Korean or English, **typed or spoken**. Every command is parsed **fully on-device** by a fine-tuned **FunctionGemma-270M** model running through **Unity Sentis**; speech is transcribed on-device too. When a request isn't a home action, the agent can fall back to an LLM for a conversational reply.

The project also contains **Nova**, the voice-agent demo from [voice-agent-unity](#-voice-agent-demo-scene-nova), as a second scene. A button switches between the two.

[![mini-agent-unity demo](https://img.youtube.com/vi/mLzsQLpqRhk/0.jpg)](https://www.youtube.com/watch?v=mLzsQLpqRhk)

## ✨ Highlights
* **On-device function calling:** a 270M model turns a sentence into a validated tool call locally.
* **Voice input:** tap **Mic** and speak; Silero VAD + SenseVoice transcribe on-device and the transcript runs the same turn as a typed command.
* **Bilingual & multi-intent:** Korean and English, including compound commands like `거실 불 켜고 거실 티비 켜줘`.
* **Embodied:** the tiger walks to the target device, *then* acts; replies appear in a speech bubble over its head.
* **Hybrid:** a schema gate validates every call; anything that isn't a valid home action defers to an optional LLM (local or cloud, via Ollama).
* **Two agents, one project:** switch to the Nova voice-agent demo scene and back with one button.

## 🗣 What you can say
16 model tools plus 4 local intents span home control and information retrieval:

| Category | Tools | Example |
|---|---|---|
| Lights | `turn_on_light` (room, brightness 0–100), `turn_off_light`, `set_light_color` (red/orange/yellow/green/blue/purple/pink/white/warm/cool) | `turn on the bedroom light at 30%`, `set the kitchen light to blue` |
| TV / Computer | `turn_on_tv` / `turn_off_tv`, `turn_on_computer` / `turn_off_computer` | `turn off the living room TV` |
| Music | `play_music` (room, volume, genre: rock/jazz/lofi), `stop_music`, `set_volume` (0–100), `get_volume`, `volume_up` / `volume_down`* (±10) | `play some jazz`, `set the volume to 60`, `louder` |
| Cleaning | `start_vacuum` (room), `stop_vacuum`* (returns to the dock) | `start the vacuum`, `stop the vacuum` |
| Info | `get_weather` (city), `get_time` (city), `get_location`, `web_search` (query) | `what's the weather in Seoul?`, `what time is it?` |
| About | `introduce_self`* | `who are you?`, `자기소개 해줘` |

\* **Local intents** (`LocalIntents.cs`): actions from the voice agent that FunctionGemma's trained tool list has no function for. A small rule matcher catches them **before** generation, and only for single, unambiguous requests (no compound commands, other devices or explicit numbers); everything else goes to the model unchanged. This matters most for `stop the vacuum`, which the model would otherwise map to `start_vacuum`.

Rooms: **kitchen**, **living room**, **bedroom** (room-less commands resolve to a sensible default). Room and device names accept synonyms in both languages.

## 🧩 How it works

```
  speech ─► [ VoiceInput ]  Silero VAD (CPU) + SenseVoice (GPU) · on-device
               │  final transcript
               ▼
  user text  ("Play jazz music")
     │
     ▼
  [ LocalIntents ]  stop_vacuum / volume_up / volume_down / introduce_self ──┐
     │ no match                                                              │
     ▼                                                                       │
  [ FunctionGemmaGenerator ]      on-device · Unity Sentis (GPUCompute) · offline
    Gemma3-270M, greedy decode    KV cache + prefix cache + GPU argmax head  │
     │                                                                       │
     │  raw text:  call:play_music{genre:<esc>jazz<esc>}   ◄─────────────────┘
     ▼
  [ FgCallParser ]  ──►  [ ToolCallValidation ]      gate: name? types? ranges? enums?
     │                          │
     │ valid call(s)            │ no valid call
     ▼                          ▼
  [ EXECUTE ]              [ LLM fallback ]  (optional, OpenAI-compatible endpoint)
     │                          └──►  conversational reply     e.g. "How are you feeling today?"
     │
     ├─ local device tools ───────────►  HomeDeviceController        (instant, offline)
     │    turn_on_light / turn_off_light / set_light_color
     │    turn_on_tv / turn_off_tv / turn_on_computer / turn_off_computer
     │    play_music / stop_music / set_volume / get_volume / volume_up / volume_down
     │    start_vacuum / stop_vacuum
     │
     └─ info tools (network) ──────────►  AgentInfoApis
          get_time / get_location / get_weather
          web_search ─► fetch results ─► LLM (grounded synthesis) ─► answer
```

Two paths reach the LLM: **(1)** when no valid tool call is produced, the whole turn falls back to it for a conversational reply; **(2)** `web_search` fetches results on-device and then asks the LLM to synthesize a grounded answer. Everything else (all device control) stays fully on-device and offline.

**0. Voice input: `VoiceCommandInput.cs` (optional second input)**
Tap **Mic** next to the command field. The microphone (16 kHz) feeds Silero VAD, which opens an utterance when you start talking and closes it after 0.7 s of silence; SenseVoice-Small transcribes it, with live partial transcripts shown in the command field. English and Korean are detected per utterance. The final transcript goes to `HomeAgentConnector.SubmitTranscript`, the same turn a typed command runs, so there is still **one agent**. A transcript that arrives while a turn is running waits for it to finish. The speech models load after the agent has warmed up. `InjectTranscript(string)` feeds a transcript through the same path without a microphone (for testing).

**1. On-device inference: `FunctionGemmaGenerator` (Unity package `com.sky-kim.functiongemma-home`)**
The model is FunctionGemma-270M-it (Gemma3 architecture: 18 layers, multi-query attention with a single KV head, head-dim 256, 262 144-token vocab, `gelu_pytorch_tanh`, 512-token sliding-attention window). It runs in Unity Sentis (`com.unity.ai.inference`) on the `GPUCompute` backend (measured ~2× faster than CPU Burst, token-identical). Because Sentis has no built-in KV cache, the cache is managed manually as plain graph I/O:

* **Manual KV cache:** past keys/values are fed back each step and kept **device-resident** (`CopyOutput`, GPU→GPU, no per-token CPU sync), double-buffered so a worker never reads and writes the same tensor.
* **Prefix-KV cache:** the fixed developer block (system instruction + all tool declarations) is prefilled **once** at warmup and its KV reused for every command, so each turn only tokenizes and feeds the user turn.
* **GPU ArgMax head:** a tiny `Functional` graph picks the next token on-GPU, so the 262k-wide logits never get read back to the CPU; greedy decoding, stopping on `<eos>` / `<end_of_turn>` / `<start_function_response>`.
* **Explicit masks:** both a full-causal and a sliding-window additive mask are supplied every step (the sliding layers require it once the prompt exceeds the 512-token window).
* **Non-blocking:** generation yields **one token per frame**, so movement/animation keep running while the model thinks.

The prompt is assembled to be byte-identical to the fine-tuning chat template; the model emits a flat call format `call:NAME{key:<escape>value<escape>}`.

**2. Parse & validate: `FgCallParser` + `ToolCallValidation.cs`**
`FgCallParser.ParseAll` extracts one or more calls from the raw text (handling the escape spans and multiple calls). Each parsed call passes through a schema gate that checks the tool exists and every argument is well-typed and in range (enums like light colors, numeric bounds like brightness/volume). Invalid calls are rejected and logged, never executed. Local-intent calls go through the same parser and gate.

**3. Execute: `HomeDeviceController.cs` + `TigerController.cs`**
Valid calls mutate device state (lights, TV, computer, speaker + volume/genre, vacuum). The tiger (a `NavMeshAgent`) navigates to the relevant device before the state changes, on a NavMesh baked at startup by `RuntimeNavMeshBake`. Responses render in a speech bubble above Tiger's head (`TigerSpeechBubble`). Info tools (`get_weather`, `get_time`, `get_location`, `web_search`) call live web APIs in `AgentInfoApis.cs`; `web_search` additionally grounds a synthesized answer through the LLM.

**4. LLM fallback: `AgentCloudClient.cs` (optional)**
When no valid tool call comes out (e.g. `오늘 기분 어때?`), the turn defers to an LLM over an **OpenAI-compatible** chat-completions endpoint, with a persona system prompt. Turn the toggle off to run purely on-device; home control still works.

The turn orchestration, status UI, and per-turn timing/harvest logging live in `HomeAgentConnector.cs`.

## 🧠 On-Device Models
The command model comes from the Unity package [`com.sky-kim.functiongemma-home`](https://huggingface.co/Sky-Kim/com.sky.sentis.functiongemma.home), declared in `Packages/manifest.json`. The package ships **`functiongemma_home_uint8.sentis`** (uint8 weights, about 436 MB), the tokenizer and the developer block in its `Models~` folder, so the Package Manager downloads everything once with the package. The Editor reads the files straight from the package cache; a player build gets a copy in `StreamingAssets/FunctionGemmaHome` for the build only, and Android copies it out of the APK on first launch.

The training, quantization, and export pipeline that produced this model is in the companion repo **[Sky-Kim/functiongemma-270m-finetune](https://huggingface.co/Sky-Kim/functiongemma-270m-finetune)**.

The speech and Nova models come the same way, as Hugging Face packages with their models in `Models~`:

| Package | Model | Used by |
|---|---|---|
| [`com.sky.sentis.silero-vad`](https://huggingface.co/Sky-Kim/com.sky.sentis.silero-vad) | Silero VAD (FP16, CPU) | voice input in both scenes |
| [`com.sky.sentis.sensevoice`](https://huggingface.co/Sky-Kim/com.sky.sentis.sensevoice) | SenseVoice-Small (FP16, GPU) | voice input in both scenes |
| [`com.sky.sentis.gemma3-270m-it`](https://huggingface.co/Sky-Kim/com.sky.sentis.gemma3-270m-it) | gemma-3-270m-it (FP16) | Nova: intent ranking + chat |
| [`com.sky.sentis.supertonic`](https://huggingface.co/Sky-Kim/com.sky.sentis.supertonic) | Supertonic 3 (FP16, CPU) | Nova: text to speech |

Before a player build, `ModelBuildStaging` copies these four into `StreamingAssets/Models` (about 1.4 GB) and removes the copy afterwards; Android copies them out of the APK on first launch.

## 🎙 Voice Agent demo scene (Nova)
`Assets/Scenes/VoiceAgentDemo.unity` is the demo from [voice-agent-unity](https://github.com/skykim/voice-agent-unity), copied in with its code (`Assets/VoiceAgent`) and intent head (`Assets/StreamingAssets/Intent`). The only change is in the shared `VoiceInput` (see below). Nova is a separate assistant with its own pipeline: Silero VAD + SenseVoice for speech, a frozen gemma-3-270m-it with a trained Decision AI head that ranks 20 commands live while you talk, Gemma3 generation for chat, and Supertonic 3 for spoken replies, all on-device.

**Switching scenes:** a `SceneToggle` button sits in each scene: **Voice Agent** (top-left) in MiniAgentScene, **Mini Agent** (top-center) in VoiceAgentDemo. Loading the other scene unloads the current one, so only one scene's models are in memory at a time. Target scene, label, position, size and font size are set in the Inspector; both scenes must be in the Build Profile's scene list (they are).

Both scenes share one copy of the speech-to-text code (`Assets/VoiceAgent/Runtime/Audio`); Tiger's Mic button uses it through `VoiceCommandInput`. Compared with voice-agent-unity, `VoiceInput` adds an `IsLoaded` flag, catches up on buffered mic audio each frame, and spreads speech recognition over frames on mobile so the 3D scene keeps its frame rate.

## ☁️ LLM Fallback (optional)
Configured on the `HomeAgentConnector` component:

* **Endpoint:** `http://localhost:11434/v1/chat/completions`: an [Ollama](https://ollama.com) server on the same machine.
* **Model:** `qwen3.5:9b`, a local model (`ollama pull qwen3.5:9b`; needs a recent Ollama).
* **Reasoning effort:** `none`. Thinking models spend the token budget before answering, and an all-thinking reply comes back empty. Use `none` for qwen3.5 (it ignores `low`), `low` for gpt-oss (it can't turn thinking off), and leave it empty for models without thinking (qwen2.5 rejects the field).
* **Toggle:** `Use Cloud Fallback` (on by default)

Ollama cloud models such as `gpt-oss:120b-cloud` also work through the same daemon (`ollama signin`), but need usage credits on the Ollama account; set the reasoning effort to `low` for gpt-oss.

**Phone builds:** `localhost` is the phone itself, so set the endpoint to your computer's LAN IP (e.g. `http://192.168.x.y:11434/v1/chat/completions`). Ollama listens on `127.0.0.1` only by default; to accept requests from the phone, run it on all interfaces (`launchctl setenv OLLAMA_HOST 0.0.0.0` on macOS, then restart Ollama) and allow incoming connections if the firewall asks. Players allow plain HTTP for this (`Allow downloads over HTTP` in Player Settings).

Any OpenAI-compatible endpoint works; point it wherever you like, or disable it entirely.

## 🗂 Project structure
```
Assets/
├─ Agent/
│  ├─ HomeAgentConnector.cs       # turn orchestration, UI, timing/harvest logging, LLM fallback
│  ├─ LocalIntents.cs             # rule matcher for stop_vacuum / volume_up / volume_down / introduce_self
│  ├─ CommandSuggestions.cs       # example command buttons above the input field + shuffle
│  ├─ ToolCallValidation.cs       # tool schemas + validating gate
│  ├─ AgentCloudClient.cs         # OpenAI-compatible chat-completions client
│  ├─ AgentInfoApis.cs            # weather / time / location / web_search backends
│  ├─ RuntimeNavMeshBake.cs       # bakes the NavMesh at startup
│  ├─ TigerSpeechBubble.cs        # speech bubble above Tiger's head
│  └─ MiniAgentTestRunner.cs      # scripted command list, started/stopped with a button
├─ Voice/
│  └─ VoiceCommandInput.cs        # Mic button: speech → transcript → HomeAgentConnector
├─ Scripts/
│  ├─ HomeDeviceController.cs     # device state (lights, TV, speaker, vacuum)
│  ├─ TigerController.cs          # WASD + NavMesh movement
│  ├─ RoomTransparencyManager.cs  # fades away walls of the rooms the tiger isn't in
│  └─ SceneToggle.cs              # button that switches MiniAgentScene ⇄ VoiceAgentDemo
├─ Scenes/
│  ├─ MiniAgentScene.unity        # Tiger (startup scene)
│  └─ VoiceAgentDemo.unity        # Nova
├─ VoiceAgent/                    # Nova from voice-agent-unity (Runtime, UI, Resources, Editor)
│  └─ Runtime/Audio/              # VoiceInput, MicrophoneStream, SpeechLanguage: shared by both scenes
├─ StreamingAssets/Intent/        # Nova's trained Decision AI head
├─ Materials/Mat_TransparentVariant.mat  # keeps the transparent URP Lit variant in player builds
└─ Resources/Music/              # jazz.wav / lofi.wav / rock.wav (synthesized loops)

com.sky-kim.functiongemma-home (git package from Hugging Face)
                                 # FunctionGemmaGenerator (Sentis inference: KV cache, prefix cache, GPU argmax, masks)
                                 # and Models~ (uint8 model, tokenizer, developer block)
com.sky.sentis.silero-vad / sensevoice / gemma3-270m-it / supertonic (git packages from Hugging Face)
```

## ▶️ Running
1. Install [git-lfs](https://git-lfs.com) (the model packages store their models with LFS).
2. Open the project in **Unity 6000.6.5f1** (URP).
3. Wait for the Package Manager to resolve the packages; the first time it downloads the FunctionGemma model (about 436 MB) and the speech, Gemma3 and Supertonic models (about 1.4 GB).
4. Open `Assets/Scenes/MiniAgentScene.unity` and press **Play**.
5. Type a command in the on-screen input box and send it, tap **Mic** and say it, or move the tiger with `W` `A` `S` `D`.
6. Press **Voice Agent** (top-left) to switch to Nova, and **Mini Agent** to come back.

The microphone needs permission on first use (macOS asks for the Editor or the player; iOS shows "Tiger listens to your voice commands.").

The **Demo** button (top-right) starts the `MiniAgentTestRunner`'s scripted command list and stops it mid-run. To start it automatically when the scene plays instead, tick `Auto Run` on the `Test` object (the list, start delay and interval are set in the Inspector too).

## 🛠 Environment
* **Unity:** 6000.6.5f1
* **Render Pipeline:** URP
* **Inference:** Unity Sentis (`com.unity.ai.inference` 2.6.1), `GPUCompute` backend, via the `com.sky-kim.functiongemma-home` package and the `com.sky.sentis.*` packages
* **Input:** Input System (`com.unity.inputsystem`)
* **Navigation:** Unity AI Navigation (`com.unity.ai.navigation`)

## 📦 Assets & Credits
Environment and character from Kenney.nl:
* [Furniture Kit](https://kenney.nl/assets/furniture-kit): environment and furniture
* [Cube Pets](https://kenney.nl/assets/cube-pets): tiger character

Voice agent:
* Nova and the speech pipeline from [voice-agent-unity](https://github.com/skykim/voice-agent-unity)
* Nova's icons: [Material Icons](https://github.com/google/material-design-icons) by Google, Apache License 2.0 (`Assets/VoiceAgent/Resources/Icons/LICENSE.txt`)
