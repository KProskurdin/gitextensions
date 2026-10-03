#!/usr/bin/env bash
# Builds and runs the shadow test projects.
#  - Linux/macOS: build output goes to $XPLAT_ARTIFACTS (default ~/xplat-artifacts), so Linux runs never overwrite the
#    Windows artifacts/ folder. Upstream tests that assert Windows-only behavior are reported as Ignored with a reason
#    (see GitCommands.Tests/XplatPlatformSkips.cs), not filtered out.
#  - Windows: the whole suite runs in the default artifacts/ folder. dotnet is started through cmd.exe from the repo
#    directory, because from Git Bash the test host inherits a working directory that is not the repo, and the
#    RunBatchCommand tests run `git` in that directory.
# Usage: tests/xplat/run-tests.sh [project-dir]   (default: tests/xplat/GitCommands.Tests)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
PROJECT_DIR="${1:-$HERE/GitCommands.Tests}"
PROJECT=$(ls "$PROJECT_DIR"/*.csproj | head -1)

cd "$REPO"

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
        WIN_REPO="$(cygpath -w "$REPO")"
        WIN_PROJECT="$(cygpath -w "$PROJECT")"
        # No quotes: cmd.exe rejects the quoted cd form here, and the repo and project paths contain no spaces.
        dotnet_cmd() { cmd.exe //c "cd /d $WIN_REPO && dotnet $*"; }
        dotnet_cmd build "$WIN_PROJECT" -nologo -clp:NoSummary -v:m
        dotnet_cmd test "$WIN_PROJECT" --no-build -nologo -v:m
        ;;
    *)
        ARTIFACTS="${XPLAT_ARTIFACTS:-$HOME/xplat-artifacts}/"
        dotnet build "$PROJECT" -p:ArtifactsDir="$ARTIFACTS" -nologo -clp:NoSummary -v:m
        dotnet test "$PROJECT" --no-build -p:ArtifactsDir="$ARTIFACTS" -nologo -v:m
        ;;
esac
