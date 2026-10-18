#!/usr/bin/env python3
"""Publishes the GitHub Pages homepage for a release version.

Usage: publish-homepage.py <version>

Renders packaging/homepage/index.html.template with string.Template,
clones the gh-pages branch, and commits the homepage: the rendered
index.html plus every static asset from packaging/homepage and a
.nojekyll file. The feed, the icons, the public key and the Windows
bootstrapper stay untouched, so the feed publish job merges on top of
this commit.

Requires: python3, git. Expects GITHUB_TOKEN in the environment. The version
is the release version without the leading v, for example 1.2.0.
"""

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from string import Template

REPO_ROOT = Path(__file__).resolve().parent.parent
PAGE_DIR = REPO_ROOT / "packaging" / "homepage"
GH_OWNER = "timo-reymann"
GH_REPO = "MothaSync"
PAGES_URL = f"https://{GH_OWNER}.github.io/{GH_REPO}/"
BRANCH = "gh-pages"


def fail(message):
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


def run(command):
    subprocess.run(command, check=True)


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)
    version = sys.argv[1]

    token = os.environ.get("GITHUB_TOKEN")
    if not token:
        fail("GITHUB_TOKEN is not set")
    repo_url = f"https://x-access-token:{token}@github.com/{GH_OWNER}/{GH_REPO}.git"

    # Render the homepage. substitute fails when a placeholder is missing.
    template_path = PAGE_DIR / "index.html.template"
    html = Template(template_path.read_text(encoding="utf-8")).substitute(version=version)

    work = tempfile.mkdtemp(prefix="mothasync-homepage-")
    pages = Path(work) / "pages"
    try:
        # Check out the pages branch. The first run starts a new branch.
        cloned = subprocess.run(
            ["git", "clone", "--branch", BRANCH, repo_url, str(pages)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        ).returncode == 0
        if not cloned:
            print(f"No {BRANCH} branch found. Creating a new one.")
            run(["git", "init", "-q", "-b", BRANCH, str(pages)])
            run(["git", "-C", str(pages), "remote", "add", "origin", repo_url])

        # Write the rendered page and copy every static asset from the
        # homepage folder. Nothing else on the branch changes.
        (pages / "index.html").write_text(html, encoding="utf-8")
        (pages / ".nojekyll").touch()
        for asset in sorted(PAGE_DIR.iterdir()):
            if asset.is_file() and asset.suffix != ".template":
                shutil.copyfile(asset, pages / asset.name)

        run(["git", "-C", str(pages), "add", "-A"])

        # A quiet diff exits 0 with no staged changes, so a re-run is safe.
        diff = subprocess.run(["git", "-C", str(pages), "diff", "--cached", "--quiet"])
        if diff.returncode == 0:
            print("Homepage is current. Nothing to commit.")
            return
        if diff.returncode != 1:
            fail(f"git diff exited with code {diff.returncode}")

        run(["git", "-C", str(pages), "config", "user.name", "CircleCI"])
        run(["git", "-C", str(pages), "config", "user.email", "no-reply@circleci.com"])
        run(["git", "-C", str(pages), "commit", "-q", "-m", f"chore: Publish homepage for {version} [skip ci]"])
        run(["git", "-C", str(pages), "push", "-q", "origin", BRANCH])
        print(f"Published homepage: {PAGES_URL}")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
