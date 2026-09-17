# Carpediem asset registry

## Status and delivery gate

The six photographs below passed **visual-content review**. Their rights are **RIGHTS UNKNOWN**. Optimized derivatives are held in `design-assets/review/`, outside any application runtime path. Do not move them into `public/`, `src/assets/`, or a deployment bundle until the owner confirms usage rights. The official `logo.pdf` remains the primary brand source and was not converted into a new logo mark.

| Canonical source / original filename / Drive ID | Usage category and possible context | Alt-text guidance | Mobile focal point | Desktop focal point | Processing / formats | Rights |
| --- | --- | --- | --- | --- | --- | --- |
| [Hut 1.jpg](https://drive.google.com/file/d/12638wgHErxPtnY42zvoZ3kMqEWqBNU2w/view) · `12638wgHErxPtnY42zvoZ3kMqEWqBNU2w` | Hut exterior; location storytelling | “Painted mango hut beneath tree canopy” when informative; empty alt if decorative. | Door near center | Door near center | 1200/1600 px 16:9; AVIF, WebP, JPEG review outputs | RIGHTS UNKNOWN |
| [Hut 2.jpg](https://drive.google.com/file/d/1NEKqckjwRiSbpX6ByiZpMlu8xn5GOsLP/view) · `1NEKqckjwRiSbpX6ByiZpMlu8xn5GOsLP` | Alternate hut exterior; location context | “Flower-painted campsite hut under mango trees” when informative. | Door near center | Facade centered | 1200 px 16:9; AVIF, WebP, JPEG review outputs | RIGHTS UNKNOWN |
| [Huts.png](https://drive.google.com/file/d/1OtqMhZ0UQKAABZ-afPzwAIswj3wSz5yq/view) · `1OtqMhZ0UQKAABZ-afPzwAIswj3wSz5yq` | Portrait hut art; mobile entry/location visual | “Mango-painted hut with a pink door” when informative. | Door and painted wall at center | Not selected for desktop | 480/768 px 4:5; AVIF, WebP, JPEG review outputs | RIGHTS UNKNOWN |
| [IMG_4016.png](https://drive.google.com/file/d/1he9tQ2Sk_b86jpqq4y2I2sO8p5euiAHy/view) · `1he9tQ2Sk_b86jpqq4y2I2sO8p5euiAHy` | Mango grove; optional entry atmosphere | “Mango trees along a sandy path” when informative. | Canopy opening in upper center | Not selected for desktop | 480/768 px 4:5; AVIF, WebP, JPEG review outputs | RIGHTS UNKNOWN |
| [Hut Inside 1.jpg](https://drive.google.com/file/d/19QjxaMstU7HqkCdYVyh6TreSAq9wTXId/view) · `19QjxaMstU7HqkCdYVyh6TreSAq9wTXId` | Interior detail; location context | “Wooden bunk beds inside a hut, with the grove visible through the door.” | Not selected for mobile | Bunks and doorway | 1200 px 16:9; AVIF, WebP, JPEG review outputs | RIGHTS UNKNOWN |
| [P1655568.jpg](https://drive.google.com/file/d/1LIISk7ZhptgcaBipbTamPe-_ZrxZPFNY/view) · `1LIISk7ZhptgcaBipbTamPe-_ZrxZPFNY` | Hall/environment detail | “Colorful low seating in the campsite hall.” | Not selected for mobile | Symmetric hall | 1200 px 16:9; AVIF, WebP, JPEG review outputs | RIGHTS UNKNOWN |

## Curated style reference set (7 items)

1. Original `logo.pdf` at repository root — canonical mark, no runtime export yet.
2. `mango-hut-01-portrait-480.webp` — painted hut, phone composition.
3. `mango-hut-01-landscape-1200.webp` — canopy and hut, wide composition.
4. `flower-hut-02-landscape-1200.webp` — alternate painted hut.
5. `mango-grove-01-portrait-480.webp` — nature atmosphere.
6. `hut-interior-01-landscape-1200.webp` — practical interior.
7. `community-hall-01-landscape-1200.webp` — another Carpediem environment.

These are style grounding sources, not an instruction to put photos on every screen. The review outputs have stable normalized filenames; `manifest.json` records dimensions, bytes, hashes, source IDs, and rights state. The `tools/optimize_design_assets.py` script deterministically produces intended crops from separately held originals. No AI-generated asset was created because the reviewed photography supplies enough visual character and there is no defined UI slot for a pattern or illustration.

## Runtime handoff after rights clearance

Select only needed variants for a feature, place them under its actual Angular asset boundary, use `<picture>` with AVIF/WebP and JPEG fallback, declare width and height or `aspect-ratio`, and lazy-load noncritical images. Keep original files outside runtime delivery. The first production entry image should target about 100–200 KB for a phone variant and no more than roughly 350 KB for a necessary desktop image, then validate visual quality on real devices.
