#!/usr/bin/env bash
set -euo pipefail

# Generates the 0install feed for a release version and publishes it to the
# gh-pages branch.
#
# Usage: scripts/publish-0install-feed.sh <version>
# The version is the release version without the leading v, for example 1.2.0.
#
# Requires: 0template, gpg, python3, git. Expects the context variables
# GITHUB_TOKEN and GPG_KEY (armored private key without a passphrase).
# The run must start after the release archives are uploaded, because
# 0template downloads them to compute the manifest digests.
# The merge and signature come from scripts/merge-feed.py, because the
# Python 0publish cannot sign with current Python versions (0publish issue 5).

cd "$(dirname "$0")/.."

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <version>"
  echo "The version is the release version without the leading v, for example 1.2.0."
  exit 1
fi

version="$1"
released="$(date -u +%F)"
gh_owner=timo-reymann
gh_repo=MothaSync
feed_url="https://${gh_owner}.github.io/${gh_repo}/mothasync.xml"
repo_url="https://x-access-token:${GITHUB_TOKEN}@github.com/${gh_owner}/${gh_repo}.git"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Import the release key. The key must have no passphrase for unattended use.
echo "$GPG_KEY" | gpg --batch --import
key_id="$(gpg --list-secret-keys --with-colons | awk -F: '/^sec/ { print $5; exit }')"
if [[ -z "$key_id" ]]; then
  echo "error: no signing key found after GPG import" >&2
  exit 1
fi
echo "Signing feeds with key $key_id"

# Generate the feed for the new version. 0template downloads the five release
# archives from the GitHub release and fills in sizes, digests and IDs.
feed_dir="$work/feed"
mkdir "$feed_dir"
cp packaging/0install/mothasync.xml.template "$feed_dir/"
(cd "$feed_dir" && 0template mothasync.xml.template version="$version" released="$released")
generated="$feed_dir/mothasync-$version.xml"

# Check out the feed branch. The first run starts a new branch.
if git clone --branch gh-pages "$repo_url" "$work/pages" 2>/dev/null; then
  master="$work/pages/mothasync.xml"
else
  echo "No gh-pages branch found. Creating a new one."
  rm -rf "$work/pages"
  mkdir "$work/pages"
  git -C "$work/pages" init -q
  git -C "$work/pages" symbolic-ref HEAD refs/heads/gh-pages
  git -C "$work/pages" remote add origin "$repo_url"
  master=""
fi

if [[ -f "$master" ]]; then
  # Merge the new version into the master feed and re-sign it.
  python3 scripts/merge-feed.py "$master" "$generated" "$key_id"
else
  # The generated feed becomes the first version of the master feed.
  # merge-feed.py signs it and the public key is exported next to it below,
  # so 0install clients can fetch the key from the same directory.
  python3 scripts/merge-feed.py --init "$feed_url" "$work/pages/mothasync.xml" "$generated" "$key_id"
fi

# Publish the icons next to the feed. The icon gives the Windows bootstrapper
# and 0install launchers their icon.
cp src/MothaSync.Client/Assets/logo.png "$work/pages/mothasync.png"
cp src/MothaSync.Client/MothaSync.ico "$work/pages/mothasync.ico"
cp src/MothaSync.Client/MothaSync.icns "$work/pages/mothasync.icns"

# Store the public key next to the feed, so 0install clients can fetch the
# key from the same directory when they import the feed.
gpg --export "$key_id" > "$work/pages/$key_id.gpg"

git -C "$work/pages" config user.name "CircleCI"
git -C "$work/pages" config user.email "no-reply@circleci.com"
git -C "$work/pages" add -A
git -C "$work/pages" commit -q -m "chore: Publish feed for ${version} [skip ci]"
git -C "$work/pages" push -q origin gh-pages

echo "Published feed: ${feed_url}"
