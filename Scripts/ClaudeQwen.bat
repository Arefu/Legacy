@echo off
rem Launch Claude Code against the Qwen3.8 27B (abliterated) model on the remote Ollama box.
rem   ClaudeQwen.bat                    interactive Claude Code session on the remote model
rem   ClaudeQwen.bat <prompt file>      loop: one fresh session per run, each told to read that file and follow it
rem   ClaudeQwen.bat <prompt file> once a single run, no loop
rem The prompt file path may be absolute or relative to where you call this from.
rem The loop ends when the model's last message of a run contains ALL DONE (finished) or STOPPED: (gave up),
rem after 3 failed runs in a row, or on Ctrl+C. State between runs must live in files / the IDB, so say so in the prompt.
rem Settings (set before calling to override):
rem   QWEN_HOST   Ollama box                (default 192.168.68.66)
rem   QWEN_MODEL  model name on that box    (default qwen3.8-27b-abl-cc)
rem   QWEN_TURNS  step limit per run        (default 200)
rem Permissions: none asked. Every run is started with --dangerously-skip-permissions, so the model runs any shell
rem command, edit or IDA call it decides on without a prompt (user's choice, 2026-11-15). Keep the repo committed or
rem backed up before a long loop, since nothing stops a bad command.
rem The model must already exist on the box; this PC only runs Claude Code + IDA.
setlocal
rem Resolve the prompt path before any cd, and the script folder before any shift.
set "HERE=%~dp0"
set "PROMPT_FILE=%~f1"
set "MODE=%~2"
if "%~1"=="" set "PROMPT_FILE="
if not defined QWEN_HOST set "QWEN_HOST=192.168.68.66"
if not defined QWEN_MODEL set "QWEN_MODEL=qwen3.8-27b-abl-cc"
if not defined QWEN_TURNS set "QWEN_TURNS=200"
set "URL=http://%QWEN_HOST%:11434"
set "MODEL=%QWEN_MODEL%"
set "CTX=65536"

curl -s -m 5 -o nul %URL%/api/version || (echo Remote Ollama at %URL% is not answering. & exit /b 1)
curl -s -m 5 %URL%/api/tags | findstr /c:"%MODEL%:latest" >nul || (echo Model %MODEL% is not on %QWEN_HOST%. & exit /b 1)
echo Using %MODEL% on %QWEN_HOST%.

rem Point Claude Code at Ollama's Anthropic-compatible API.
set "ANTHROPIC_BASE_URL=%URL%"
set "ANTHROPIC_AUTH_TOKEN=ollama"
set "ANTHROPIC_API_KEY="
set "ANTHROPIC_MODEL=%MODEL%"
set "ANTHROPIC_DEFAULT_OPUS_MODEL=%MODEL%"
set "ANTHROPIC_DEFAULT_SONNET_MODEL=%MODEL%"
set "ANTHROPIC_DEFAULT_HAIKU_MODEL=%MODEL%"
set "CLAUDE_CODE_SUBAGENT_MODEL=%MODEL%"
set "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1"
set "CLAUDE_CODE_MAX_CONTEXT_TOKENS=%CTX%"
rem Claude Code reserves the max reply size out of the window; the default reserve eats half of 64k.
set "CLAUDE_CODE_MAX_OUTPUT_TOKENS=8192"
rem Slow model: don't let long generations time out.
set "API_TIMEOUT_MS=3000000"

rem Work from the repo root.
cd /d "%HERE%.."

if not defined PROMPT_FILE (
  claude --model %MODEL% --dangerously-skip-permissions
  exit /b
)
if not exist "%PROMPT_FILE%" (echo Prompt file not found: %PROMPT_FILE% & exit /b 1)

rem Give the model the real tool schemas up front (tool search hides them, so small models guess argument names) ...
set "ENABLE_TOOL_SEARCH=false"
rem ... and hide the IDA tools a decomp run doesn't need: fewer schemas in a 64k window, and no patching by accident.
set "I=mcp__plugin_ida-pro-mcp_idalib__"
set "DENY=%I%add_bookmark %I%analyze_batch %I%analyze_component %I%basic_blocks %I%callgraph %I%entity_query %I%export_funcs %I%find_regex %I%find_xref_signatures %I%force_recompile %I%func_profile %I%idb_close %I%idb_list %I%imports %I%imports_query %I%insn_query %I%int_convert %I%make_signature %I%make_signature_for_function %I%make_signature_for_range %I%patch %I%patch_asm %I%put_int %I%search_text %I%server_health %I%survey_binary %I%trace_data_flow %I%undefine"
rem QwenStream.ps1 finds this run's claude.exe by the prompt file path on its command line.
set "QWEN_RUN_TAG=%PROMPT_FILE%"
set "MSG=Read %PROMPT_FILE% and follow it exactly. Start now."
set /a RUN=0
set /a FAILS=0

:loop
set /a RUN+=1
echo [%date% %time%] run %RUN%: %PROMPT_FILE%
rem The prompt goes right after -p: --allowedTools takes a list and would swallow anything after it.
rem --disallowedTools here only hides tools to save context (unused IDA calls) or that can't work against Ollama
rem (WebSearch/WebFetch) -- it is not a permission limit; Bash/PowerShell are available.
claude -p "%MSG%" --model %MODEL% --max-turns %QWEN_TURNS% --dangerously-skip-permissions --disallowedTools %DENY% mcp__ida-pro-mcp WebFetch WebSearch Agent --output-format stream-json --verbose | powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%QwenStream.ps1"
rem QwenStream.ps1: 0 ok, 1 killed / Ctrl+C, 2 run failed (retry fresh), 3 ALL DONE, 4 STOPPED:.
set "RC=%errorlevel%"
if "%RC%"=="1" (echo Run was stopped, stopping the loop. & exit /b 1)
if "%RC%"=="3" (echo ALL DONE. & exit /b 0)
if "%RC%"=="4" (echo The model stopped on a problem - see its last message above. & exit /b 1)
if "%RC%"=="2" goto :failed
set /a FAILS=0
if /i "%MODE%"=="once" exit /b 0
timeout /t 10 /nobreak >nul
goto :loop

:failed
set /a FAILS+=1
if /i "%MODE%"=="once" exit /b 1
if %FAILS% GEQ 3 (echo 3 failed runs in a row, stopping. & exit /b 1)
echo Run failed (%FAILS% in a row) - starting a fresh one.
timeout /t 10 /nobreak >nul
goto :loop
