#!/usr/bin/env bash
# Lance PKHeX.Avalonia nativement sous Linux.
# Le SDK .NET 10 a été installé dans ~/.dotnet (sans root).
set -e
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$(dirname "$0")/.."
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release "$@"
