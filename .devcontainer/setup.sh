#!/usr/bin/env bash
# Prepares a Kinevor Codespace. Safe to run more than once.
set -euo pipefail

FLUTTER_VERSION="${KINEVOR_FLUTTER_VERSION:-3.47.8}"
FLUTTER_HOME="${HOME}/flutter"

install_flutter() {
  if [ -x "${FLUTTER_HOME}/bin/flutter" ]; then
    echo "Flutter ${FLUTTER_VERSION} is already installed."
    return 0
  fi

  echo "Installing Flutter ${FLUTTER_VERSION} into ${FLUTTER_HOME}..."
  local archive
  archive="$(mktemp --suffix=.tar.xz)"
  curl -fsSL --retry 3 --retry-delay 2 \
    "https://storage.googleapis.com/flutter_infra_release/releases/stable/linux/flutter_linux_${FLUTTER_VERSION}-stable.tar.xz" \
    -o "${archive}"
  tar -xJf "${archive}" -C "${HOME}"
  rm -f "${archive}"
}

add_flutter_to_path() {
  if ! grep -q 'kinevor_flutter_path' "${HOME}/.bashrc" 2>/dev/null; then
    {
      echo
      echo "# kinevor_flutter_path"
      echo "export PATH=\"${FLUTTER_HOME}/bin:\$PATH\""
    } >> "${HOME}/.bashrc"
  fi

  # Login shells and VS Code tasks that do not source .bashrc.
  echo "export PATH=\"${FLUTTER_HOME}/bin:\$PATH\"" \
    | sudo tee /etc/profile.d/kinevor-flutter.sh >/dev/null
}

# The module backends consume the platform as NuGet packages from a local
# feed, so it must exist before C# Dev Kit restores the solution.
echo "Packing the shared platform packages..."
dotnet pack kinevor-platform/backend/Kinevor.Platform.slnx -c Release --nologo \
  -o kinevor-platform/backend/artifacts/packages

if [ "${KINEVOR_SKIP_FLUTTER:-0}" = "1" ]; then
  echo "Skipping Flutter (KINEVOR_SKIP_FLUTTER=1)."
else
  if install_flutter; then
    add_flutter_to_path
    export PATH="${FLUTTER_HOME}/bin:${PATH}"
  else
    echo "warning: Flutter installation failed. Rerun 'bash .devcontainer/setup.sh' to retry." >&2
  fi
fi

echo "Installing root Node tooling..."
npm ci

echo
echo "Kinevor devcontainer is ready."
dotnet --version
node --version
npm --version
docker --version
docker compose version
if [ -x "${FLUTTER_HOME}/bin/flutter" ]; then
  "${FLUTTER_HOME}/bin/flutter" --version
else
  echo "Flutter: not installed (set KINEVOR_SKIP_FLUTTER=0 and rerun the setup script)"
fi

echo
echo "C# projects restore from kinevor-platform/backend/artifacts/packages."
echo "If C# Dev Kit restored before the feed existed, reload the window to"
echo "trigger a fresh solution restore (Command Palette: 'Reload Window')."
echo "Start the showcase with: npm run demo:up:showcase"
echo "Or the full stack with:  npm run demo:up"
