#!/usr/bin/env bash
# Publishes BinDay to GitHub: creates the repo if it is missing, pushes the
# project, turns on GitHub Pages and cuts a release with the Windows zip.
#
#   ./publish.sh              # publish to khansarim007/binday
#   BINDAY_REPO=you/binday ./publish.sh
#
# The branch that holds the finished project is "public-main"; it is pushed
# as "main" so the public repo has the project at its root.

set -euo pipefail

cd "$(dirname "$0")"

REPO="${BINDAY_REPO:-khansarim007/binday}"
BRANCH="public-main"
VERSION="${BINDAY_VERSION:-v1.0.0}"

command -v gh >/dev/null 2>&1 || { echo "The GitHub CLI (gh) is required."; exit 1; }
command -v zip >/dev/null 2>&1 || { echo "zip is required."; exit 1; }

echo "== building =="
./build.sh

echo
echo "== checking $REPO =="
if gh api "repos/$REPO" >/dev/null 2>&1; then
    echo "  the repository already exists"
else
    echo "  creating $REPO (public)"
    if ! gh api -X POST user/repos \
        -f name="$(basename "$REPO")" \
        -f description="Free up space on your Windows PC. Native, lightweight, and it never deletes anything without asking." \
        -F private=false -F has_issues=true -F has_wiki=false >/dev/null 2>&1; then
        cat <<'TXT'

  Could not create the repository with this token.

  Either create it by hand and run this script again:

    https://github.com/new
      Repository name: binday
      Visibility:      Public
      Do NOT add a README, .gitignore or a licence

  or give the token permission to create repositories
  (Settings -> Developer settings -> Personal access tokens -> your token
  -> Repository permissions -> Administration: Read and write), then run it again.

TXT
        exit 1
    fi
fi

echo
echo "== pushing =="
git push -u "https://github.com/$REPO.git" "$BRANCH:main"

echo
echo "== enabling GitHub Pages =="
if gh api "repos/$REPO/pages" >/dev/null 2>&1; then
    echo "  Pages is already on"
else
    gh api -X POST "repos/$REPO/pages" \
        -f source[branch]=main -f source[path]=/site >/dev/null 2>&1 \
        && echo "  Pages enabled" \
        || echo "  Turn Pages on by hand: Settings -> Pages -> Source: GitHub Actions"
fi

echo
echo "== releasing $VERSION =="
if gh release view "$VERSION" --repo "$REPO" >/dev/null 2>&1; then
    gh release upload "$VERSION" BinDay-windows.zip --repo "$REPO" --clobber
else
    gh release create "$VERSION" BinDay-windows.zip --repo "$REPO" \
        --title "BinDay $VERSION" \
        --notes "BinDay $VERSION - free up space on your Windows PC.

Download BinDay-windows.zip, unzip it anywhere, and double click Install.bat."
fi

cat <<TXT

  Done.

  Site:    https://$(echo "$REPO" | cut -d/ -f1).github.io/$(basename "$REPO")/
  Repo:    https://github.com/$REPO
  Release: https://github.com/$REPO/releases/latest
  Download: https://github.com/$REPO/releases/latest/download/BinDay-windows.zip

TXT
