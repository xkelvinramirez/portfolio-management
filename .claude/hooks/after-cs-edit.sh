#!/bin/bash
# .claude/hooks/after-cs-edit.sh
# Run after any .cs file is written or edited

INPUT=$(cat)
FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // .tool_input.path // empty')

if [ -z "$FILE" ] || [[ "$FILE" != *.cs ]]; then
  exit 0
fi

# Find the nearest .csproj
DIR=$(dirname "$FILE")
PROJ=$(find "$DIR" -maxdepth 4 -name "*.csproj" -print -quit 2>/dev/null)

if [ -z "$PROJ" ]; then
  exit 0
fi

dotnet format "$PROJ" --include "$FILE" --no-restore
dotnet build "$PROJ" --no-restore -v minimal