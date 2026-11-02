#!/usr/bin/env bash
# Builds and runs the shadow test projects (upstream tests compiled against the shadow projects).
#  - Linux/macOS: build output goes to $XPLAT_ARTIFACTS (default ~/xplat-artifacts), so Linux runs never overwrite the
#    Windows artifacts/ folder. Upstream tests that assert Windows-only behavior are reported as Ignored with a reason
#    (see each project's XplatPlatformSkips.cs), not filtered out.
#  - Windows: the whole suite runs in the default artifacts/ folder. dotnet is started through cmd.exe from the repo
#    directory, because from Git Bash the test host inherits a working directory that is not the repo, and the
#    RunBatchCommand tests run `git` in that directory.
# The fork's own tests (GitExtensions.Xplat.Core.Tests, GitExtensions.Xplat.App.Tests) are run separately.
# Usage: tests/xplat/run-tests.sh [project-dir...]   (default: every shadow test project below)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

if [[ $# -gt 0 ]]; then
    PROJECT_DIRS=("$@")
else
    PROJECT_DIRS=(
        "$HERE/GitCommands.Tests"
        "$HERE/GitExtensions.Extensibility.Tests"
        "$HERE/GitExtUtils.Tests"
        "$HERE/ResourceManager.Tests"
        "$HERE/plugins/DeleteUnusedBranches.Tests"
        "$HERE/plugins/GitUIPluginInterfaces.Tests"
        "$HERE/plugins/ReleaseNotesGenerator.Tests"
        "$HERE/plugins/BuildServerIntegration/AppVeyorIntegration.Tests"
        "$HERE/plugins/BuildServerIntegration/AzureDevOpsIntegration.Tests"
        "$HERE/plugins/BuildServerIntegration/GitHubActionsIntegration.Tests"
        "$HERE/plugins/BuildServerIntegration/GitlabIntegration.Tests"
        "$HERE/plugins/BuildServerIntegration/JenkinsIntegration.Tests"
    )
fi

cd "$REPO"

failed=0
for PROJECT_DIR in "${PROJECT_DIRS[@]}"; do
    PROJECT=$(ls "$PROJECT_DIR"/*.csproj | head -1)
    echo "=== $PROJECT"
    case "$(uname -s)" in
        MINGW*|MSYS*|CYGWIN*)
            WIN_REPO="$(cygpath -w "$REPO")"
            WIN_PROJECT="$(cygpath -w "$PROJECT")"
            # No quotes: cmd.exe rejects the quoted cd form here, and the repo and project paths contain no spaces.
            dotnet_cmd() { cmd.exe //c "cd /d $WIN_REPO && dotnet $*"; }
            dotnet_cmd build "$WIN_PROJECT" -nologo -clp:NoSummary -v:m \
                && dotnet_cmd test "$WIN_PROJECT" --no-build -nologo -v:m \
                || failed=$((failed + 1))
            ;;
        *)
            ARTIFACTS="${XPLAT_ARTIFACTS:-$HOME/xplat-artifacts}/"
            dotnet build "$PROJECT" -p:ArtifactsDir="$ARTIFACTS" -nologo -clp:NoSummary -v:m \
                && dotnet test "$PROJECT" --no-build -p:ArtifactsDir="$ARTIFACTS" -nologo -v:m \
                || failed=$((failed + 1))
            ;;
    esac
done

# Every project runs even when an earlier one fails, so one report shows all failures.
if [[ "$failed" -gt 0 ]]; then
    echo "run-tests: $failed of ${#PROJECT_DIRS[@]} test projects failed" >&2
    exit 1
fi
