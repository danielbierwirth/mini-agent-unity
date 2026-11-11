#!/usr/bin/env bash
# Exports the dialect fine-tuned Whisper for Arabic speech to ONNX, for VoiceAgent ▸ Speech ▸ Install Whisper Small
# (Arabic dialects), which runs this script itself when the export is missing.
#
# Model: huggingface.co/oddadmix/whisper-small-arabic-dialectal-v2 (Apache 2.0), openai/whisper-small fine-tuned on
# 13 Arabic dialects (Gulf, Iraqi, Egyptian, Levantine, Maghrebi, …). It has no ONNX export on Hugging Face, so this
# makes one with Optimum in a private virtual environment: encoder_model.onnx and decoder_model.onnx (no KV cache), the
# same interface as the onnx-community/whisper-* exports the other sizes use.
#
# Usage: Tools/export_whisper_dialect.sh [output folder]   (default: Library/WhisperExport/small-dialect)
# Needs python3 (3.9+). First run downloads ~1 GB of Python packages and ~1 GB of model weights.
set -euo pipefail

MODEL="oddadmix/whisper-small-arabic-dialectal-v2"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/Library/WhisperExport/small-dialect}"
VENV="$ROOT/Library/WhisperExport/venv"
PYTHON="${PYTHON:-python3}"

if [ ! -x "$VENV/bin/optimum-cli" ]; then
  echo "Creating the export environment in ${VENV}…"
  "$PYTHON" -m venv "$VENV"
  "$VENV/bin/pip" install --quiet --upgrade pip
  "$VENV/bin/pip" install --quiet optimum-onnx onnxruntime
fi

echo "Exporting $MODEL to ${OUT}…"
rm -rf "$OUT"
"$VENV/bin/optimum-cli" export onnx --model "$MODEL" --task automatic-speech-recognition --opset 17 \
  --no-post-process "$OUT"
test -f "$OUT/encoder_model.onnx" && test -f "$OUT/decoder_model.onnx"
echo "Done: $OUT"
