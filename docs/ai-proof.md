# Offline AI proof (installed build)

Build: `BUILD 20261113-1945 / 2AD94E0A`
Installed app: `E:\StudentManagementSystem\site`
Endpoint: `http://localhost:8080`

## Installed assets

| Item | Value |
| --- | --- |
| Model | `AI\model\Ektome-Qwen1.5-0.5B-Chat-PristinelyUncensored.Q2_K.gguf` |
| Model size | `247,363,712` bytes (verified by status handler) |
| Runtime | `AI\runtime\llama-server.exe`, llama.cpp `0.9.0-dev` build 12283 |
| Port | `8137` |
| Backend | local llama.cpp, no network calls |

## Live status payload

From `GET /Handlers/AiStatus.ashx` on the installed app (`docs/ui-structure/ai-status.json`):

```json
{
  "ok": true,
  "enabled": true,
  "modelPresent": true,
  "runtimePresent": true,
  "ready": true,
  "state": "ready",
  "model": "Ektome-Qwen1.5-0.5B-Chat-PristinelyUncensored.Q2_K.gguf",
  "modelBytes": 247363712,
  "runtime": "llama.cpp (bundled)",
  "port": 8137,
  "error": ""
}
```

## UI answer

Prompt sent through `Chatbot/Index.aspx`:

> In one sentence, what can this system help me do?

Rendered reply (trimmed of trailing blank lines):

> The system can help me understand the user's current status, preferences, and goals in their personal and professional lives.

The status line read `Local model ready`; the message metadata read `7.7s · 320 tokens · local`. A short probe earlier answered in `0.6s / 21 tokens`.

Screenshot: `docs/ui-structure/ai-answer-1440.png`.

Note: the bundled model is a 0.5B Q2 quantisation, so answers are fast but can be vague. The UI, readiness, streaming and error states are what this proof covers; answer quality is model-limited.

## Reproduce

```powershell
node tools/check-ai.js
node tools/ui-structure-audit.js
```

`check-ai.js` prints the status JSON, the status label, the last two messages and writes `docs/ai-after-1440.png`.
`ui-structure-audit.js` asserts model/runtime presence, `ready` state and a rendered answer longer than 20 characters.

## Installer coverage

`Deploy/installer.iss` ships `..\build\stage\AI\*` to `{app}\AI`, and `Build-All.ps1` stages both `AI\model` and `AI\runtime`. A clean install therefore contains the same model and runtime as this test.