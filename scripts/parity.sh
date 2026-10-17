#!/usr/bin/env bash
# Proves the C# sim (dotnet/) matches the TypeScript reference sim byte for byte: for each seed it
# runs both CLIs and diffs their Ledgers, once with the default output and once with everything
# (--all --stats --chronicles and more people). Exits non-zero on the first difference.
#
# Usage: scripts/parity.sh [days] [seeds...]     (defaults: 200 days, seeds 1 to 8)
set -euo pipefail

cd "$(dirname "$0")/.."
days="${1:-200}"
shift || true
seeds=("$@")
if [ ${#seeds[@]} -eq 0 ]; then seeds=(1 2 3 4 5 6 7 8); fi

dotnet build dotnet/src/CrownAndCompass.Cli -c Release --nologo -v quiet >/dev/null
cli=dotnet/src/CrownAndCompass.Cli/bin/Release/net8.0/CrownAndCompass.Cli.dll

out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

failed=0
for seed in "${seeds[@]}"; do
  for mode in default full; do
    flags=(--days "$days" --seed "$seed")
    if [ "$mode" = full ]; then flags+=(--all --stats --chronicles --people 8); fi
    node src/cli.ts "${flags[@]}" >"$out/ts.txt"
    dotnet "$cli" "${flags[@]}" >"$out/cs.txt"
    if cmp -s "$out/ts.txt" "$out/cs.txt"; then
      echo "seed $seed, $days days, $mode: identical ($(wc -l <"$out/ts.txt") lines)"
    else
      echo "seed $seed, $days days, $mode: DIFFERENT"
      diff "$out/ts.txt" "$out/cs.txt" | head -10
      failed=1
    fi
  done
done
exit "$failed"
