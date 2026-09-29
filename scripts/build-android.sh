#!/bin/bash
set -euo pipefail
project_dir="$(cd -P -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
unity_editor="${AR_UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.1.5f1/Unity.app/Contents/MacOS/Unity}"
if [ ! -x "$unity_editor" ]; then
  printf 'Set AR_UNITY_EDITOR to your Unity 6000.1.5f1 executable.\n' >&2
  exit 1
fi
mkdir -p "$project_dir/Logs"
build_method="EnvironmentBuild.BuildAir"
if [ "${1:-}" = "--environment" ]; then
  build_method="EnvironmentBuild.Build"
fi
exec "$unity_editor" -batchmode -nographics -quit -buildTarget Android \
  -projectPath "$project_dir" -executeMethod "$build_method" \
  -logFile "$project_dir/Logs/android-build.log"
