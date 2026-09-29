#!/usr/bin/env bash
# Test production runtime scripts against fixtures; no Wine, network or GPU.
set -euo pipefail
base=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
work=$(mktemp -d)
trap 'rm -rf -- "$work"' EXIT
mkdir -p "$work/bin" "$work/helpers" "$work/prefix/drive_c/windows/system32" "$work/client/bin64" "$work/cache/aioncl/directx"
export PATH="$work/bin:$PATH" XDG_CACHE_HOME="$work/cache" AIONCL_TEST_LOG="$work/wine.log"
export AIONCL_WINE="$work/bin/custom-wine"
cp "$base/prepare-linux-runtime.sh" "$base/install-d3dx9.sh" "$work/helpers/"
cat > "$work/bin/custom-wine" <<'EOF'
#!/bin/sh
printf '%s\n' "$*" >> "$AIONCL_TEST_LOG"
EOF
cat > "$work/bin/curl" <<'EOF'
#!/bin/sh
exit 99
EOF
cat > "$work/bin/pgrep" <<'EOF'
#!/bin/sh
exit 1
EOF
cat > "$work/bin/sha256sum" <<'EOF'
#!/bin/sh
# The archive itself is a fixture; integrity pins remain in production scripts.
cat >/dev/null
exit 0
EOF
cat > "$work/bin/cabextract" <<'EOF'
#!/bin/bash
while (($#)); do
 case "$1" in
  -d) destination=$2;shift 2;;
  -F) file=$2;shift 2;;
  *) shift;;
 esac
done
printf 'microsoft-dll-fixture' > "$destination/$file"
EOF
for installer in install-dxvk.sh install-d3dcompiler.sh; do
 printf '#!/bin/sh\nexit 0\n' > "$work/helpers/$installer"
done
chmod +x "$work/bin/"* "$work/helpers/"*
printf archive-fixture > "$work/cache/aioncl/directx/directx_Jun2010_redist.exe"
printf wine-dll-fixture > "$work/prefix/drive_c/windows/system32/d3dx9_38.dll"
mkdir -p "$work/client/Shaders/Cache"
printf stale > "$work/client/Shaders/Cache/stale"
bash "$work/helpers/prepare-linux-runtime.sh" "$work/prefix" "$work/client" native > "$work/first.log"
[[ $(cat "$work/prefix/drive_c/windows/system32/d3dx9_38.dll") == microsoft-dll-fixture ]]
[[ $(cat "$work/prefix/aioncl-runtime-backup/d3dx9_38.dll") == wine-dll-fixture ]]
[[ ! -d $work/client/Shaders/Cache ]]
mkdir -p "$work/client/Shaders/Cache"
printf fresh > "$work/client/Shaders/Cache/fresh"
bash "$work/helpers/prepare-linux-runtime.sh" "$work/prefix" "$work/client" native > "$work/second.log"
[[ -f $work/client/Shaders/Cache/fresh ]]
grep -q D3DX9_38_ALREADY_READY "$work/second.log"
bash "$work/helpers/prepare-linux-runtime.sh" "$work/prefix" "$work/client" wine > "$work/wine-mode.log"
[[ ! -d $work/client/Shaders/Cache ]]
grep -q '/v d3dx9_38 /t REG_SZ /d builtin /f' "$work/wine.log"
bash "$work/helpers/prepare-linux-runtime.sh" "$work/prefix" "$work/client" native > "$work/native-mode.log"
[[ $(cat "$work/client/.aioncl/d3dx9-mode") == native ]]
if bash "$work/helpers/prepare-linux-runtime.sh" "$work/prefix" "$work/client" invalid >/dev/null 2>&1; then exit 1;fi
printf 'RUNTIME_SMOKE_PASS native install, custom runner, idempotence, Wine fallback, cache transitions\n'
