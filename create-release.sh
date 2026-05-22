#!/bin/sh
set -eux

VERSION="0.5.0"

dotnet build --configuration Release

FILENAME="TransferWindowPlanner2_v${VERSION}.zip"
if [ -f "release/$FILENAME" ]; then
  echo "Zip file already exists!"
  exit 1
fi

zip -r "$FILENAME" \
  "GameData" \
  "LICENSE" \
  "LICENSE.ClickThroughBlocker" \
  "LICENSE.TransferWindowPlanner" \
  "README.md"
mv "$FILENAME" release

