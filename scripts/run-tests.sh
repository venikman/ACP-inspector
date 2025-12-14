#!/usr/bin/env bash
set -euo pipefail

# Runs the test suite and emits a TRX report for stakeholders/CI.
# Output:
# - Console summary (normal verbosity)
# - tests/TestResults.trx (overwritten on each run)

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
cd "$repo_root/tests"

get_target_frameworks() {
  local proj="ACP.Tests.fsproj"

  if [[ ! -f "$proj" ]]; then
    echo "ERROR: Expected $proj in $(pwd)" >&2
    return 1
  fi

  local tfm tfms
  tfm="$(sed -n 's/.*<TargetFramework>\(.*\)<\/TargetFramework>.*/\1/p' "$proj" | head -n 1)"
  if [[ -n "$tfm" ]]; then
    echo "$tfm"
    return 0
  fi

  tfms="$(sed -n 's/.*<TargetFrameworks>\(.*\)<\/TargetFrameworks>.*/\1/p' "$proj" | head -n 1)"
  if [[ -n "$tfms" ]]; then
    echo "$tfms"
    return 0
  fi

  echo "ERROR: Could not detect TargetFramework(s) in $proj" >&2
  return 1
}

get_required_netcoreapp_majors() {
  local tfms="$1"
  local -a majors=()
  local tfm major

  IFS=';' read -r -a tfm_list <<<"$tfms"

  for tfm in "${tfm_list[@]}"; do
    if [[ "$tfm" =~ ^net([0-9]+)\. ]]; then
      major="${BASH_REMATCH[1]}"
      majors+=("$major")
    fi
  done

  if [[ ${#majors[@]} -eq 0 ]]; then
    echo ""
    return 0
  fi

  local -a unique=()
  local u
  for major in "${majors[@]}"; do
    local already=false
    for u in "${unique[@]}"; do
      if [[ "$u" == "$major" ]]; then
        already=true
        break
      fi
    done
    if [[ "$already" == false ]]; then
      unique+=("$major")
    fi
  done

  printf "%s\n" "${unique[@]}"
}

dotnet_has_required_runtimes() {
  local dotnet_bin="$1"
  shift
  local -a required_majors=("$@")

  # If we couldn't infer any required majors, assume dotnet is usable.
  if [[ ${#required_majors[@]} -eq 0 ]]; then
    return 0
  fi

  local runtimes
  if ! runtimes="$("$dotnet_bin" --list-runtimes 2>/dev/null)"; then
    return 1
  fi

  local major
  for major in "${required_majors[@]}"; do
    if ! grep -Eq "^Microsoft\\.NETCore\\.App ${major}\\." <<<"$runtimes"; then
      return 1
    fi
  done

  return 0
}

# Use the dotnet on PATH by default. Allow override via DOTNET_BIN, but validate it
# can actually run the test project's target framework(s).
DOTNET_BIN_OVERRIDE="${DOTNET_BIN:-}"

tfms="$(get_target_frameworks)"
required_netcoreapp_majors=()
while IFS= read -r major; do
  [[ -z "$major" ]] && continue
  required_netcoreapp_majors+=("$major")
done < <(get_required_netcoreapp_majors "$tfms")

DOTNET_BIN="dotnet"
if [[ -n "$DOTNET_BIN_OVERRIDE" ]]; then
  DOTNET_BIN="$DOTNET_BIN_OVERRIDE"
fi

# If an override is provided but can't run the required runtime(s), fall back to dotnet
# on PATH (common when an SDK 10 installation doesn't include net9 runtimes).
if ! dotnet_has_required_runtimes "$DOTNET_BIN" "${required_netcoreapp_majors[@]}"; then
  if [[ -n "$DOTNET_BIN_OVERRIDE" && "$DOTNET_BIN_OVERRIDE" != "dotnet" ]] && dotnet_has_required_runtimes "dotnet" "${required_netcoreapp_majors[@]}"; then
    echo "WARN: $DOTNET_BIN_OVERRIDE can't run target framework(s) '$tfms'; falling back to dotnet on PATH." >&2
    DOTNET_BIN="dotnet"
  else
    echo "ERROR: $DOTNET_BIN can't run target framework(s) '$tfms' (missing Microsoft.NETCore.App runtime(s): ${required_netcoreapp_majors[*]:-(unknown)})." >&2
    echo "       Try one of:" >&2
    echo "       - Use dotnet 9 on PATH: scripts/run-tests.sh" >&2
    echo "       - Install the missing runtime(s) for $tfms" >&2
    echo "       - Or set DOTNET_BIN to a dotnet that has them (e.g. DOTNET_BIN=\$(command -v dotnet))" >&2
    exit 1
  fi
fi

echo "Using dotnet: $DOTNET_BIN"

"$DOTNET_BIN" test --logger "console;verbosity=normal" --logger "trx;LogFileName=TestResults.trx"

echo
echo "TestResults.trx written to: $(pwd)/TestResults/TestResults.trx"
