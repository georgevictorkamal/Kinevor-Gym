#!/bin/sh
# Start whichever DemoApi assembly the module build produced.
set -eu

assembly="$(find /app -maxdepth 1 -type f -name '*DemoApi.dll' -print -quit)"
if [ -z "$assembly" ]; then
  echo "No *DemoApi.dll found in /app. Check the Dockerfile.api publish step." >&2
  exit 1
fi

exec dotnet "$assembly"
