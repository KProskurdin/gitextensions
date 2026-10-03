#!/usr/bin/env bash
# Reports upstream test classes that changed since the fork base and are mirrored by the fork:
#   - the XplatLinux*.cs counterparts (class name without the XplatLinux prefix, with or without a trailing "s");
#   - the classes named in the XplatPlatformSkips.cs entries.
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
TEST_DIR="$REPO/tests/xplat/GitCommands.Tests"
UPSTREAM_TESTS="tests/app/UnitTests/GitCommands.Tests"

if ! git -C "$REPO" rev-parse --verify --quiet "$UPSTREAM_REF^{commit}" > /dev/null; then
    echo "xplat-drift: upstream ref '$UPSTREAM_REF' not found; fetch the upstream remote first" >&2
    exit 2
fi

BASE="$(grep -E '^\| Fork base' "$SYNC_LOG" | grep -oE '`[0-9a-f]{7,40}`' | head -n 1 | tr -d '`' || true)"
if [[ -z "$BASE" ]] || ! git -C "$REPO" cat-file -e "$BASE^{commit}" 2> /dev/null; then
    echo "xplat-drift: fork base not found in $SYNC_LOG" >&2
    exit 2
fi

# Prints the upstream test file that declares "class $1", or nothing.
find_upstream_file() {
    git -C "$REPO" grep -l -E "class $1\b" "$UPSTREAM_REF" -- "$UPSTREAM_TESTS" 2> /dev/null \
        | head -n 1 | sed "s#^$UPSTREAM_REF:##" || true
}

# Maps each mirrored upstream class to the fork files that mirror it, from the "// Mirrors: <class>" marker.
declare -A mirrored=()
for file in "$TEST_DIR"/XplatLinux*.cs; do
    marker="$(grep -oE '^// Mirrors: .*' "$file" | head -n 1 | sed 's#^// Mirrors: ##' | tr -d '\r' || true)"
    if [[ -z "$marker" ]]; then
        echo "MARKER: $(basename "$file") has no '// Mirrors:' line"
        mirrored["(no marker: $(basename "$file"))"]="$(basename "$file")"
        continue
    fi
    if [[ "$marker" == none* ]]; then
        continue
    fi
    mirrored["$marker"]="$(basename "$file")"
done

while IFS= read -r entry; do
    entry="${entry//\"/}"
    class="${entry%.*}"
    class="${class##*.}"
    if [[ "${mirrored[$class]:-}" != *XplatPlatformSkips.cs* ]]; then
        mirrored["$class"]="${mirrored[$class]:+${mirrored[$class]}, }XplatPlatformSkips.cs"
    fi
done < <(grep -oE '"GitCommandsTests\.[^"]+"' "$TEST_DIR/XplatPlatformSkips.cs")

echo "xplat-drift: checking ${#mirrored[@]} mirrored classes against $UPSTREAM_REF since fork base $BASE"

needs_review=0
while IFS= read -r name; do
    source="${mirrored[$name]}"
    upstream_file="$(find_upstream_file "$name")"

    if [[ -z "$upstream_file" ]]; then
        needs_review=$((needs_review + 1))
        echo "MISSING: $name (from $source): no upstream class in $UPSTREAM_REF"
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
