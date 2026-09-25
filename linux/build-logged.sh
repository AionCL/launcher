#!/usr/bin/env bash
# Run on the development VM; no remote deployment or GUI test.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
mkdir -p out/logs
log="out/logs/build-$(date +%Y%m%d-%H%M%S).log"
echo "Build and automated tests; log: $PWD/$log"
set +e
(
    set -e
    docker build -t aioncl-linux-build -f linux/Dockerfile linux
    docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -v "$PWD:/src" aioncl-linux-build
) >"$log" 2>&1
result=$?
set -e
printf 'BUILD_EXIT_CODE=%s\n' "$result" >>"$log"
echo "Finished: exit=$result; log=$PWD/$log"
exit "$result"
