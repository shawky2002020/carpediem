# Foundation QA evidence — 18 September 2026

## Build and asset checks

- `npx --yes sass design-system/styles/index.scss docs/ux/specimens/foundation.css --no-source-map` completed with exit code 0 after the token updates.
- `tools/optimize_design_assets.py` generated 27 review files: AVIF, WebP, and JPEG for six people-free originals at intentional 4:5 or 16:9 crops. Original 21–22 MB PNGs remain outside runtime delivery.
- The design direction requests the 48,954 B portrait AVIF on phones and the 149,564 B landscape AVIF above 767 px; WebP and JPEG fallbacks are present. Both crops preserve the pink hut door and canopy.
- The local font payload is 74,720 B: Hanken Grotesk Latin variable 34,704 B plus Bricolage Grotesque Latin 600 at 40,016 B. Both license files are committed beside the specimen fonts.
- The logo reference PNG is a faithful crop rendered from the unchanged repository `logo.pdf`, then reduced to a 128 × 128 review asset at 25,680 B. It is not a replacement logo source.

## Rendered direction checks

The local [design direction](specimens/direction.html) and [typography specimen](specimens/typography-study.html) were inspected in the Codex in-app browser on Windows. CSS geometry was read after each viewport settled.

| Viewport | Layout client width | Horizontal overflow | Smallest interactive height | Responsive image | Finding |
| ---: | ---: | --- | ---: | --- | --- |
| 320 × 780 | 305 px | None | 44 px | Portrait AVIF | Single-column hero and component board remain inside the page. Input and buttons are 48 px high. |
| 360 × 800 | 345 px | None | 44 px | Portrait AVIF | Content retains a 20 px authored gutter plus browser scrollbar. |
| 390 × 844 | 375 px | None | 44 px | Portrait AVIF | Display hierarchy remains clear and long data stays contained. |
| 430 × 900 | 415 px | None | 44 px | Portrait AVIF | Phone composition remains single column. |
| 768 × 900 | 753 px | None | 44 px | Landscape AVIF after settled media evaluation | Wider composition has room for navigation and two-column sections. |
| 1280 × 900 | 1265 px | None | 44 px | Landscape AVIF | Split hero, principles, component board, and system grid align without clipping. |

At 320 px, the mobile component board keeps the long location within a 48 px input using visual ellipsis; the underlying full value remains in the input. The warning specimen uses a dot and text, its label can wrap, and it does not rely on color alone. Both local fonts reported loaded through the Font Loading API.

## Local Lighthouse snapshot

A Chrome Lighthouse mobile run against the local static direction scored **99 performance, 100 accessibility, 100 best practices, and 100 SEO**. Recorded lab metrics were **1.2 s FCP, 1.8 s LCP, 0 CLS, and 0 ms TBT**. The 99 performance score reflects local lab conditions and does not predict a production Angular build or real network. Remaining diagnostic opportunities were cache lifetime, image delivery, dependency chain, and render-blocking CSS; the Python review server intentionally provides no production caching.

## Accessibility and interaction observations

- The pages use one `h1`, ordered `h2`/`h3` headings, landmarks, labeled fields, native buttons, a skip link, descriptive image alt text, and a distinct navigation label.
- Keyboard focus uses a 3 px forest outline with 4 px offset. Primary links meet a 44 px minimum block size; form fields and buttons are 48 px.
- Motion is limited to color and link-gap transitions. `prefers-reduced-motion: reduce` removes those transitions and smooth scrolling.
- Brand orange remains decorative. The warning specimen uses the approved warning foreground rather than orange body text.
- The responsive image reserves intrinsic width and height; CSS gives the visual a stable region before the image decodes.

## Remaining acceptance work

This verifies static review artifacts, not an Angular application. The production pass still needs 200% text zoom, Android Chrome, iOS Safari, keyboard behavior within real components, automated accessibility checks, screen-reader sampling, fallback-font CLS, Core Web Vitals, and RTL/bilingual typography if Arabic enters scope. Photo usage still needs rights clearance and the visual direction still needs owner approval.
