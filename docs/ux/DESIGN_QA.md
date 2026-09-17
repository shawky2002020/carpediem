# Foundation QA evidence — 18 September 2026

## Build and asset checks

- `npx --yes sass design-system/styles/index.scss docs/ux/specimens/foundation.css --no-source-map` completed with exit code 0.
- `tools/optimize_design_assets.py` generated 27 review files: AVIF, WebP, and JPEG for six people-free originals at intentional 4:5 or 16:9 crops. Total outputs: 3,736,356 bytes. Largest single output: 363,101 bytes. Original 21–22 MB PNGs remain outside runtime delivery.
- Review variants were opened and visually checked: the pink-door hut remains centered in portrait and landscape, mango-grove crop retains the canopy/path, and the hall crop retains the seating and arch.

## Rendered specimen checks

The local [typography specimen](specimens/typography-study.html) was opened in the Codex in-app browser with Google Fonts loaded. Screenshots were inspected at 320 × 800 and 1280 × 800; DOM geometry and wrapping were checked at all widths below.

| Viewport width | Document scroll width | Title lines A/B/C | Smallest button | Smallest input | Finding |
| ---: | ---: | --- | ---: | ---: | --- |
| 320 | 305 | 3 / 2 / 3 | 44 px | 49 px | No horizontal overflow; narrow text wraps cleanly. |
| 360 | 345 | 2 / 2 / 2 | 44 px | 49 px | No horizontal overflow. |
| 390 | 375 | 2 / 2 / 2 | 44 px | 49 px | No horizontal overflow. |
| 430 | 415 | 2 / 2 / 2 | 44 px | 49 px | No horizontal overflow. |
| 768 | 753 | 1 / 1 / 1 | 44 px | 49 px | Single-column specimens have generous width. |
| 1280 | 1280 | 2 / 2 / 2 | 44 px | 49 px | Three-column comparison fits without horizontal overflow. |

Keyboard Tab focused the first field in logical order and displayed a visible 3 px forest focus outline with offset. No interactive product workflow is represented by this specimen. Browser QA of dialogs, sheets, application navigation, forms, 200% zoom, reduced motion, and RTL awaits the Angular workspace and component implementation. No application accessibility conformance claim is made.
