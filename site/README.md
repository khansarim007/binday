# The website

The landing page for BinDay. Plain HTML, CSS and one small script - no
framework, no bundler, no `npm install`. It is also what GitHub Pages
publishes from `.github/workflows/pages.yml`.

```
site/
  index.html          the whole page
  assets/styles.css   every colour, font and layout rule
  assets/main.js      a small nudge towards the download button
  assets/icon.svg     the app icon
  assets/screenshot.png  a screenshot of the real window
  assets/og.png       the link preview image
  vercel.json         used when the Vercel root directory is "site"
```

## Run it locally

Open `site/index.html` in a browser, or serve the folder:

```bash
python3 -m http.server 8000 --directory site
```

## Deploy it to Vercel

The repository is set up for a one-click import:

1. **Add New → Project** in Vercel and import `khansarim007/binday`.
2. **Framework Preset:** `Other`.
3. **Root Directory:** leave it on the repository root. The `vercel.json` at
   the root of the repository points Vercel at `site/` and skips the build
   step, because there is nothing to build.
4. **Deploy.**

If you would rather point Vercel straight at the folder, set **Root
Directory** to `site` instead - `site/vercel.json` covers that case.

The download button points at the latest GitHub release, so a new release
updates the site with no change to the page.

## Deploy it to GitHub Pages

Push to `main` and the `pages.yml` workflow publishes `site/` to
`https://khansarim007.github.io/binday/`.

## Changing the links

The page links to:

| What | Where |
|---|---|
| Download | `https://github.com/khansarim007/binday/releases/latest/download/BinDay-windows.zip` |
| Source | `https://github.com/khansarim007/binday` |
| Canonical URL | the `og:url` and `canonical` tags at the top of `index.html` |

Change the canonical URL and the `og:image` if you host the site on a
different domain.
