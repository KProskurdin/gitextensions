#!/usr/bin/env bash
# Reports upstream test classes that changed since the fork base and are mirrored by the fork, in every shadow test
# project (each fork test directory is paired with the upstream one it compiles):
#   - the XplatLinux*.cs counterparts (from each file's "// Mirrors: <class>" line);
#   - the classes named in the XplatPlatformSkips.cs entries (a method entry names its class; a class entry is the class).
# A mirrored upstream file that changed since the fork base, or that no longer exists, needs its mirror reviewed.
# Exit codes:
#   0  no mirrored upstream file needs review
#   1  at least one mirrored upstream file changed or is missing
#   2  the upstream ref, the fork base, or the SYNC-LOG entry is missing
# Usage: tests/xplat/xplat-drift.sh   (UPSTREAM_REF defaults to upstream/master)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
UPSTREAM_REF="${UPSTREAM_REF:-upstream/master}"
SYNC_LOG="$REPO/docs/xplat/SYNC-LOG.md"

# Fork test directory (relative to tests/xplat) and the upstream test directory it compiles.
PROJECTS=(
    "GitCommands.Tests|tests/app/UnitTests/GitCommands.Tests"
    "GitExtensions.Extensibility.Tests|tests/app/UnitTests/GitExtensions.Extensibility.Tests"
    "GitExtUtils.Tests|tests/app/UnitTests/GitExtUtils.Tests"
    "ResourceManager.Tests|tests/app/UnitTests/ResourceManager.Tests"
    "plugins/DeleteUnusedBranches.Tests|tests/plugins/UnitTests/DeleteUnusedBranches.Tests"
    "plugins/GitUIPluginInterfaces.Tests|tests/plugins/UnitTests/GitUIPluginInterfaces.Tests"
    "plugins/ReleaseNotesGenerator.Tests|tests/plugins/UnitTests/ReleaseNotesGenerator.Tests"
    "plugins/BuildServerIntegration/AppVeyorIntegration.Tests|tests/plugins/UnitTests/BuildServerIntegration/AppVeyorIntegration.Tests"
    "plugins/BuildServerIntegration/AzureDevOpsIntegration.Tests|tests/plugins/UnitTests/BuildServerIntegration/AzureDevOpsIntegration.Tests"
    "plugins/BuildServerIntegration/GitHubActionsIntegration.Tests|tests/plugins/UnitTests/BuildServerIntegration/GitHubActionsIntegration.Tests"
    "plugins/BuildServerIntegration/GitlabIntegration.Tests|tests/plugins/UnitTests/BuildServerIntegration/GitlabIntegration.Tests"
    "plugins/BuildServerIntegration/JenkinsIntegration.Tests|tests/plugins/UnitTests/BuildServerIntegration/JenkinsIntegration.Tests"
)

if ! git -C "$REPO" rev-parse --verify --quiet "$UPSTREAM_REF^{commit}" > /dev/null; then
    echo "xplat-drift: upstream ref '$UPSTREAM_REF' not found; fetch the upstream remote first" >&2
    exit 2
fi

BASE="$(grep -E '^\| Fork base' "$SYNC_LOG" | grep -oE '`[0-9a-f]{7,40}`' | head -n 1 | tr -d '`' || true)"
if [[ -z "$BASE" ]] || ! git -C "$REPO" cat-file -e "$BASE^{commit}" 2> /dev/null; then
    echo "xplat-drift: fork base not found in $SYNC_LOG" >&2
    exit 2
fi

# Prints the upstream test file under $2 that declares "class $1", or nothing.
find_upstream_file() {
    git -C "$REPO" grep -l -E "class $1\b" "$UPSTREAM_REF" -- "$2" 2> /dev/null \
        | head -n 1 | sed "s#^$UPSTREAM_REF:##" || true
}

# Maps "<upstream test dir>|<mirrored class>" to the fork files that mirror it.
declare -A mirrored=()

add_mirror() {
    local key="$1|$2" source="$3"
    if [[ "${mirrored[$key]:-}" != *"$source"* ]]; then
        mirrored["$key"]="${mirrored[$key]:+${mirrored[$key]}, }$source"
    fi
}

for project in "${PROJECTS[@]}"; do
    test_dir="$HERE/${project%%|*}"
    upstream_tests="${project#*|}"
    [[ -d "$test_dir" ]] || continue

    for file in "$test_dir"/XplatLinux*.cs; do
        [[ -e "$file" ]] || continue
        source="${project%%|*}/$(basename "$file")"
        marker="$(grep -oE '^// Mirrors: .*' "$file" | head -n 1 | sed 's#^// Mirrors: ##' | tr -d '\r' || true)"
        if [[ -z "$marker" ]]; then
            echo "MARKER: $source has no '// Mirrors:' line"
            mirrored["$upstream_tests|(no marker: $source)"]="$source"
            continue
        fi
        if [[ "$marker" == none* ]]; then
            continue
        fi
        add_mirror "$upstream_tests" "$marker" "$source"
    done

    skips="$test_dir/XplatPlatformSkips.cs"
    [[ -f "$skips" ]] || continue
    source="${project%%|*}/XplatPlatformSkips.cs"
    # Entries are tuples whose first item is a dotted test name: ("Namespace.Class.Method", Reason).
    while IFS= read -r entry; do
        entry="${entry#(\"}"
        entry="${entry%\"}"
        last="${entry##*.}"
        # A class entry ends with the class name; a method entry ends with the method, after its class.
        if [[ -n "$(find_upstream_file "$last" "$upstream_tests")" ]]; then
            class="$last"
        else
            class="${entry%.*}"
            class="${class##*.}"
        fi
        add_mirror "$upstream_tests" "$class" "$source"
    done < <(grep -oE '\("[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+"' "$skips")
done

echo "xplat-drift: checking ${#mirrored[@]} mirrored classes against $UPSTREAM_REF since fork base $BASE"

needs_review=0
while IFS= read -r key; do
    upstream_tests="${key%%|*}"
    name="${key#*|}"
    source="${mirrored[$key]}"
    upstream_file="$(find_upstream_file "$name" "$upstream_tests")"

    if [[ -z "$upstream_file" ]]; then
        needs_review=$((needs_review + 1))
        echo "MISSING: $name (from $source): no upstream class in $UPSTREAM_REF:$upstream_tests"
        continue
    fi

    commits="$(git -C "$REPO" log --format='  %h %s' "$BASE..$UPSTREAM_REF" -- "$upstream_file")"
    if [[ -n "$commits" ]]; then
        needs_review=$((needs_review + 1))
        echo "CHANGED: $name (from $source) -> $upstream_file"
        echo "$commits"
    fi
done < <(printf '%s\n' "${!mirrored[@]}" | sort)

echo "xplat-drift: $needs_review mirrored classes need review (missing or changed)"

if [[ "$needs_review" -gt 0 ]]; then
    exit 1
fi
