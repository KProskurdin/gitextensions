#!/usr/bin/env bash
# Runs the app for a check that should end by itself. When it does not, reports what it is doing before ending it: the
# processes, the app's managed stacks (with dotnet-stack, if DOTNET_STACK names it) and, through ci-report.py, annotations
# that can be read without signing in. A runner's own step time limit reports nothing but the limit.
# Usage: tests/xplat/run-with-watchdog.sh <seconds> <command> [argument...]
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
LIMIT="$1"
shift

if python3 --version >/dev/null 2>&1; then PYTHON=python3; else PYTHON=python; fi

"$@" &
PID=$!

for ((elapsed = 0; elapsed < LIMIT; elapsed++)); do
    if ! kill -0 "$PID" 2>/dev/null; then
        wait "$PID"
        exit $?
    fi

    sleep 1
done

echo "run-with-watchdog: still running after $LIMIT seconds: $*" >&2

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) PROCESSES="$(tasklist //v //fo csv)"; WINDOWS=1 ;;
    *) PROCESSES="$(ps -ef)"; WINDOWS=0 ;;
esac

echo "$PROCESSES"
echo "$PROCESSES" | grep -iE 'git|dotnet|ssh|gpg|xvfb' \
    | "$PYTHON" "$HERE/ci-report.py" text "Processes after $LIMIT seconds"

# The process to examine is the app, not the "dotnet run" that started it. Its executable is named GitExtensions.
if [[ -n "${DOTNET_STACK:-}" ]]; then
    APP_PIDS="$(DOTNET_ROLL_FORWARD=Major "$DOTNET_STACK" ps | awk '/GitExtensions/ { print $1 }')"
    for APP_PID in $APP_PIDS; do
        STACKS="$(DOTNET_ROLL_FORWARD=Major "$DOTNET_STACK" report -p "$APP_PID" 2>&1)"
        echo "$STACKS"
        echo "$STACKS" | "$PYTHON" "$HERE/ci-report.py" stacks "App stacks after $LIMIT seconds"
        if [[ "$WINDOWS" == 1 ]]; then taskkill //F //T //PID "$APP_PID" >/dev/null 2>&1; else kill -9 "$APP_PID" 2>/dev/null; fi
    done
fi

kill "$PID" 2>/dev/null
exit 124
