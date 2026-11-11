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
3. Arabic uses the size in `ModelRoots.WhisperSize` (default `"small"` since the vocabulary prompt, see below).

## Arabic with a vocabulary prompt (2026-11-11)

Whisper now gets `SpeechLanguage.ArabicPrompt` (the command words, spelled as `ArabicCommands` expects) as its "previous text" (`<|startofprev|>`). Same five phrases, plus six new phrasings and a chat line that should stay chat. A phrase passes when `ArabicCommands.ToEnglish` gives the expected command. Supertonic doesn't produce the same audio twice, so the table shows the range over three runs.

| Model | Prompt | Original 5 | New 6 | Avg. time |
|---|---|---|---|---|
| whisper-base | off | 1–3/5 | 3/6 | ~255 ms |
| whisper-base | on | 4–5/5 | 3–4/6 | ~375 ms |
| whisper-small | off | 3–4/5 | 4/6 | ~700 ms |
| whisper-small | on | **5/5** | 4–5/6 | ~1100–1300 ms |

* With the prompt, the transcripts mostly match the source text word for word (شغّل التلفاز, أوقف المكنسة, أطفئ نور غرفة النوم). Both models wrote قلل الصوت as "قل الصوت" and أوقف الروبوت as "الربوت"; `ArabicCommands` now reads both (only the last run, small 5/6, includes that fix). Whisper-small with the prompt is now the default.
* **Prompt hallucination:** on silence or noise a prompted Whisper repeats prompt clauses ("شغّل الموسيقى، شغّل الموسيقى، …"). Once, base also spliced prompt clauses into real speech. `SpeechLanguage.IsHallucination` drops a transcript when a clause repeats, when it copies two or more prompt clauses verbatim, or when it holds more text than the audio's length allows. One case still gets through: base produced a single "شغّل الموسيقى." from 2 s of faint white noise. In the app, Silero VAD only passes speech to Whisper, so this test (raw noise sent straight to Whisper) is harsher than live use.

## Dialect fine-tune vs. stock whisper-small (2026-11-11)

`small-dialect` is [`oddadmix/whisper-small-arabic-dialectal-v2`](https://huggingface.co/oddadmix/whisper-small-arabic-dialectal-v2), whisper-small fine-tuned on 13 Arabic dialects (install: **VoiceAgent ▸ Speech ▸ Install Whisper Small (Arabic dialects)**). There are 16 phrases: the 8 MSA commands from above, 7 dialect phrases (طفي النور لو سمحت، ولّع الضو بالمطبخ، شغل التلفزيون، علّي الصوت شوي، وين لبنان؟، فين الأهرامات؟، شو أخبار الطقس اليوم؟) and one chat line. Each phrase is spoken by Supertonic (F2), transcribed, and routed with `ArabicCommands`. Two more clips are scored separately: the real recording `where_is_lebanon.mp3` and 2 s of faint white noise. CER is the character error rate after `ArabicText.Normalize`, letters only.

| Model | Prompt | Routed right | CER | Avg. time | Real "أين يقع لبنان؟" | 2 s noise |
|---|---|---|---|---|---|---|
| whisper-small | off | 9/16 | 9.8% | 674 ms | ✅ exact | "سنقوم بعمل" |
| whisper-small | on | 12/16 | 6.7% | 1007 ms | ✅ exact | repeats "أوقف المكنسة" 15× (dropped by the guard) |
| small-dialect | off | 10/16 | 7.7% | 709 ms | ✅ exact | "هههه" |
| small-dialect | on | 12/16 | **4.1%** | 1028 ms | ✅ exact | **nothing** |

* **The dialect model with the prompt transcribes closest to what was said.** It got شو أخبار الطقس اليوم، فين الأهرامات، أوقف المكنسة and شغل التلفزيون letter for letter, where stock small wrote "الأحرامات" and "أخبر". It is as fast as stock small and the same size on disk.
* **It doesn't hallucinate the prompt on noise.** Stock small with the prompt filled 2 s of noise with a repeated command, which only `SpeechLanguage.IsHallucination` stopped. The dialect model returned nothing.
* **Most remaining misses are routing, not transcription.** `ArabicCommands` doesn't know the dialect verbs ولّع (turn on) and علّي (raise), so a correct "على الصوت شوي" still routes to chat. Both models misheard وين لبنان as "ويلبنان" / "ويا لبنان" and أوقف الروبوت as "أوف روبوت". (شو أخبار الطقس routing to the weather counts as a miss in the table, but it is a reasonable answer.)
* **The test voice is synthetic and speaks dialect text with an MSA accent**, so these numbers understate what a dialect fine-tune should gain on real dialect speakers. A recording from a native speaker is the test that counts.

