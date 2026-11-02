#!/usr/bin/env bash
# Builds the stack bundle for a release from a checkout: everything the stack runs from outside the
# image, so an update can replace all of it. Run by publish-backend after the image is pushed, because a
# release with a bundle is one that can be installed.
#
#   ./scripts/bundle.sh <version> [output directory]
#
# Writes kvitteria-stack-<version>.tar.gz and .sha256 beside it. compose.override.yaml is left out on
# purpose: it is the development overlay, and on a server it would build from source.
set -euo pipefail

cd "$(dirname "$0")/.."

version="${1:-}"
out="${2:-.}"

printf '%s' "$version" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$' || {
	echo "usage: $0 <version> [output directory]   (a plain x.y.z)" >&2
	exit 1
}

mkdir -p "$out"
out="$(cd "$out" && pwd)"
name="kvitteria-stack-${version}.tar.gz"

# An install runs these directly, and a checkout made on Windows records no executable bit.
chmod +x scripts/*.sh

# Fixed owner and order, so two runs over one tag differ only in timestamps. Bundle tooling stays out of
# the bundle; the systemd units are inside scripts/.
tar -czf "${out}/${name}" \
	--owner=0 --group=0 --numeric-owner --sort=name \
	--exclude='scripts/bundle.sh' \
	.env.example \
	compose.yaml compose.public.yaml compose.gpu.yaml \
	caddy/Caddyfile \
	postgres/initdb \
	scripts

(cd "$out" && sha256sum "$name" > "${name%.tar.gz}.sha256")

echo "${out}/${name}"
