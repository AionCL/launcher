#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
touch "$tmp/AionCL.Camera.exe"
cat >"$tmp/wine" <<'EOF'
#!/usr/bin/env bash
if [[ ${1:-} == tasklist ]]; then
    printf 'Image Name                     PID\n'
    printf 'aionclassic.bin                1234\n'
else
    process_stat=$(</proc/$$/stat)
    process_stat=${process_stat##*) }
    read -ra fields <<<"$process_stat"
    printf 'nice=%s args=' "${fields[16]}" >"$AIONCL_CAMERA_TEST_RESULT"
    printf '<%s>' "$@" >>"$AIONCL_CAMERA_TEST_RESULT"
fi
EOF
chmod +x "$tmp/wine"
AIONCL_WINE="$tmp/wine" AIONCL_CAMERA_START_DELAY=0 AIONCL_CAMERA_TEST_RESULT="$tmp/result" \
    linux/aioncl-camera "$tmp/AionCL.Camera.exe" 130 85
expected="nice=10 args=<$tmp/AionCL.Camera.exe><1234><130><85>"
grep -Fx -- "$expected" "$tmp/result" >/dev/null || {
    cat "$tmp/result" >&2
    exit 1
}
echo CAMERA_BRIDGE_SMOKE_PASS
