# Speech-to-text round trip: test phrases and results

Five equivalent home commands per language, used to compare the speech-to-text models without a microphone. Each phrase is spoken by Supertonic 3 (Nova's voice, F2), resampled from 44.1 kHz to 16 kHz, transcribed with the language set explicitly, and the transcript is run through Nova's intent pipeline (Arabic rewrite + Gemma3 / Decision AI head). A phrase **passes** when the transcript still selects the expected command.

## Phrases

| # | Expected command | English | Korean | Arabic |
|---|---|---|---|---|
| 1 | `turn_off_light` | Turn off the lights, please. | 불 좀 꺼줘. | أطفئ الأضواء من فضلك. |
| 2 | `turn_on_tv` | Turn on the TV. | 티비 켜줘. | شغّل التلفاز. |
| 3 | `volume_up` | Turn up the volume. | 볼륨 올려줘. | ارفع الصوت. |
| 4 | `play_music` | Play some music. | 음악 틀어줘. | شغّل الموسيقى. |
| 5 | `stop_vacuum` | Stop the vacuum. | 청소기 멈춰줘. | أوقف المكنسة. |

## Results (2026-11-08, Unity 6000.6.5f1, Sentis GPUCompute, macOS)

| Model | Size on disk (FP16) | English | Korean | Arabic | Avg. transcription time |
|---|---|---|---|---|---|
| whisper-tiny | 114 MB | 5/5 | 5/5 | 2/5 | 130 ms |
| whisper-base | 194 MB | 5/5 | 5/5 | 3/5 | 205 ms |
| whisper-small | 543 MB | 5/5 | 4/5 | 3/5 | 584 ms |
| sensevoice | (package) | 5/5 | 5/5 | n/a (no Arabic) | 76 ms |

Clips are 1.3–2.5 s long. Times are per utterance after warm-up; Whisper re-runs the decoder over the whole token prefix each step (no KV cache).

### English

| Phrase | whisper-tiny | whisper-base | whisper-small | sensevoice |
|---|---|---|---|---|
| Turn off the lights, please. | ✅ Turn off the lights, please. → `turn_off_light` | ✅ Turn off the lights, please. → `turn_off_light` | ✅ Turn off the lights, please. → `turn_off_light` | ✅ Turn off the lights, please. → `turn_off_light` |
| Turn on the TV. | ✅ Turn on the TV. → `turn_on_tv` | ✅ Turn on the TV. → `turn_on_tv` | ✅ Turn on the TV. → `turn_on_tv` | ✅ Turn on the TV. → `turn_on_tv` |
| Turn up the volume. | ✅ Turn up the volume. → `volume_up` | ✅ Turn up the volume. → `volume_up` | ✅ Turn up the volume. → `volume_up` | ✅ Turn up the volume. → `volume_up` |
| Play some music. | ✅ Play some music. → `play_music` | ✅ Play some music. → `play_music` | ✅ Play some music. → `play_music` | ✅ Play some music. → `play_music` |
| Stop the vacuum. | ✅ Stop the vacuum. → `stop_vacuum` | ✅ Stop the vacuum. → `stop_vacuum` | ✅ Stop the vacuum. → `stop_vacuum` | ✅ Stop the vacuum. → `stop_vacuum` |

### Korean

| Phrase | whisper-tiny | whisper-base | whisper-small | sensevoice |
|---|---|---|---|---|
| 불 좀 꺼줘. | ✅ 불 좀 꺼져 → `turn_off_light` | ✅ 불 좀 꺼져 → `turn_off_light` | ✅ 불 좀 꺼져. → `turn_off_light` | ✅ 불 좀 꺼져. → `turn_off_light` |
| 티비 켜줘. | ✅ TV 켜고 → `turn_on_tv` | ✅ TV 켜 → `turn_on_tv` | ❌ tbq → `web_search` | ✅ 티비켜. → `turn_on_tv` |
| 볼륨 올려줘. | ✅ 볼륨 올려줘 → `volume_up` | ✅ 볼륨 올려줘 → `volume_up` | ✅ 볼륨 올려줘. → `volume_up` | ✅ 볼륨 올려줘. → `volume_up` |
| 음악 틀어줘. | ✅ 음악트로죠 → `play_music` | ✅ 음악 틀어줘 → `play_music` | ✅ 음악 틀어줘. → `play_music` | ✅ 음악 틀 어 줘. → `play_music` |
| 청소기 멈춰줘. | ✅ 청소기 멈춰져 → `stop_vacuum` | ✅ 청소기 멈춰져 → `stop_vacuum` | ✅ 청소기 멈춰져. → `stop_vacuum` | ✅ 청소기 멈춰져. → `stop_vacuum` |

### Arabic

| Phrase | whisper-tiny | whisper-base | whisper-small |
|---|---|---|---|
| أطفئ الأضواء من فضلك. | ✅ أطفيل أضواء من فضلك → `turn_off_light` | ✅ أتفل أضواء من فضلك → `turn_off_light` | ✅ أطفل أضواء من فضلك → `turn_off_light` |
| شغّل التلفاز. | ❌ شغفة تفاز → `chat` | ❌ شكرا في تقفز → `chat` | ❌ شارفة تفاز → `chat` |
| ارفع الصوت. | ✅ أرفع الصوت → `volume_up` | ✅ أرفع الصوت → `volume_up` | ✅ أرفع الصوت → `volume_up` |
| شغّل الموسيقى. | ❌ شر للموسيقا → `chat` | ✅ شغل الموسيقى → `play_music` | ✅ شغل الموسيقى → `play_music` |
| أوقف المكنسة. | ❌ أوى في المكان الساعة → `chat` | ❌ أوى في المكان نساء → `chat` | ❌ أواف المكانسة → `chat` |

## Reading the results

* **The test voice is synthetic.** Some errors are shared by every model, which points at Supertonic's pronunciation rather than the recognizer: Korean 꺼줘 is heard as 꺼져 by all four models (still the right command), and Arabic شغّل التلفاز comes out as "…تفاز / تقفز" from tiny, base and small alike. Arabic is synthesized without diacritics. Treat these numbers as a relative comparison of the models; a real recorded voice is the test that counts.
* **English and Korean:** SenseVoice (used for EN / KR) and every Whisper size pass all five, except whisper-small turning 티비 켜줘 into "tbq".
* **Arabic:** tiny 2/5, base 3/5, small 3/5. base gains 1 phrase over tiny at about 1.6× the time; small transcribes closer to the text (أواف المكانسة, شغل الموسيقى) but recovers no more commands than base here, at about 4.5× tiny's time and 543 MB.
* "أوقف المكنسة" fails everywhere: the verb is misheard, and the Arabic rules deliberately don't guess a direction (a vacuum word with no recognizable verb would otherwise start it).

## Re-running

1. Install the models: **VoiceAgent ▸ Speech ▸ Download Whisper Tiny / Base / Small** (written to `Assets/StreamingAssets/Whisper/<size>`, not in git).
2. Open `VoiceAgentDemo`, enter Play mode, then synthesize each phrase with `SpeechOutput.SynthesizeClip`, transcribe it with `WhisperRecognizer` (`ModelRoots.WhisperFor(size)`) or SenseVoice, and score it with `AssistantController.ShowChips`. `VoiceAgentDemoScene.SpeakAndListen(text)` runs the same round trip for the selected language through the app's own voice path.
3. Arabic uses the size in `ModelRoots.WhisperSize` (default `"base"`, chosen from these results).
