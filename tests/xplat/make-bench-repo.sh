#!/usr/bin/env bash
# Creates a repository for the revision-graph benchmark (PLAN.md M3.5): a main line of COMMITS commits where every tenth
# commit merges a one-commit side branch that starts five commits back, so the graph has two to three lanes throughout.
# Usage: tests/xplat/make-bench-repo.sh <new-folder> [commits]   (default 5000; the repository then has 5500 commits)
set -euo pipefail

TARGET="$1"
COMMITS="${2:-5000}"

mkdir -p "$TARGET"
cd "$TARGET"
git init -q -b main .
awk -v n="$COMMITS" 'BEGIN {
  t = 1700000000; m = 0;
  for (i = 1; i <= n; i++) {
    if (i > 6 && i % 10 == 0) {
      m++; s = m + 1000000;
      printf "commit refs/heads/side\nmark :%d\ncommitter Bench <bench@example.com> %d +0000\ndata 6\nside%d\nfrom :%d\nM 644 inline s.txt\ndata %d\n%d\n\n", s, t + i, i % 10, i - 5, length(i "") + 1, i;
      printf "commit refs/heads/main\nmark :%d\ncommitter Bench <bench@example.com> %d +0000\ndata %d\nmerge %d\nfrom :%d\nmerge :%d\nM 644 inline f.txt\ndata %d\n%d\n\n", i, t + i, length("merge " i) + 1, i, i - 1, s, length(i "") + 1, i;
    } else {
      printf "commit refs/heads/main\nmark :%d\ncommitter Bench <bench@example.com> %d +0000\ndata %d\nc%d\n", i, t + i, length("c" i) + 1, i;
      if (i > 1) printf "from :%d\n", i - 1;
      printf "M 644 inline f.txt\ndata %d\n%d\n\n", length(i "") + 1, i;
    }
  }
}' | git fast-import --quiet
git checkout -q main
echo "$(git rev-list --count HEAD) commits, $(git rev-list --merges --count HEAD) merges in $TARGET"
