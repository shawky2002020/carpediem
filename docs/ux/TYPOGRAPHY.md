# Typography direction — owner approval pending

## Recommendation

Use a deliberate two-role system rather than a neutral all-purpose sans:

| Role | Family | Use | Shipped review subset |
| --- | --- | --- | ---: |
| Display | **Bricolage Grotesque 600** | Page titles, section statements, and rare brand-led moments above 24 px. Its irregular grotesque details connect to the hand-painted huts and the handmade logo without copying either. | 40,016 B WOFF2 |
| Interface | **Hanken Grotesk 400–700 variable** | Body, labels, controls, metadata, dates, times, counts, and dense working surfaces. Its forms stay calm beside the display face and its documented tabular figures support operational data. | 34,704 B WOFF2 |

The total Latin review payload is **74,720 bytes**. Hanken is preloaded because it sets the first body text. Bricolage is requested by the first display heading; do not preload both without LCP evidence. Both files use `font-display: swap`. The specimen licenses are stored beside the files.

The official logo lettering remains artwork and is never typeset with either family. These are Latin subsets. Arabic typography, bilingual metrics, bidi behavior, and numeral policy require a separate design and language pass before Arabic ships.

## Why this pair

- Bricolage Grotesque has enough character to make the system recognizably Carpediem, while a single 600 face prevents display typography from becoming theatrical or expensive.
- Hanken Grotesk was designed for interface, web, mobile, signage, and reading contexts. Use tabular figures for time, dates, capacity, and cross-row numeric comparison.
- The pair separates expression from work. Large statements can feel outdoor and human; controls and operational data remain predictable.
- The pairing avoids the generic geometric-sans look rejected during direction review.

Both projects are released under the SIL Open Font License 1.1. See the official [Bricolage Grotesque project](https://github.com/ateliertriay/bricolage), [Google Fonts Bricolage metadata](https://github.com/google/fonts/blob/main/ofl/bricolagegrotesque/METADATA.pb), [Hanken Grotesk project](https://github.com/marcologous/hanken-grotesk), and [Google Fonts Hanken metadata](https://github.com/google/fonts/blob/main/ofl/hankengrotesk/METADATA.pb).

## Typesetting contract

| Content | Rule |
| --- | --- |
| Display | Bricolage 600; fluid size with a defined cap; line height about 0.96–1.08; careful negative tracking only at large sizes; balance short statements. |
| UI body | Hanken 400; 16 px default; line height 1.5–1.6. Never reduce essential body copy to 12 px. |
| Labels and actions | Hanken 600–700; sentence case; no decorative all-caps eyebrow system. |
| Data | Hanken 500–600 with `font-variant-numeric: tabular-nums`; keep time and short counts together; let long locations wrap or truncate only where the full value remains available. |
| Long headings | Prefer natural lines. Do not force awkward `<br>` elements in production content; the specimen uses authored line breaks to demonstrate art direction. |
| Fallback | Display: Trebuchet MS, sans-serif. UI: Segoe UI, sans-serif. Measure fallback shift before production release. |

## Rendered evidence

The [design direction](specimens/direction.html) shows the pair across an editorial identity moment and a restrained component board. The [type-in-use specimen](specimens/typography-study.html) isolates display, working text, data, and a form field.

In the Codex in-app browser on Windows, both local WOFF2 files loaded. At 320 px the type specimen had no horizontal overflow, the field stayed 48 px high, and the long location remained within the content column. The design direction was also checked at 320, 360, 390, 430, 768, and 1280 px. Review still needs 200% text zoom, Android Chrome, iOS Safari, and real application data after the Angular workspace exists.

## Approval decision

Owner review should answer one question: **Does the Bricolage/Hanken pairing feel like Carpediem while remaining calm enough for daily operations?** Approval promotes the two family tokens and their licensed subsets into the application font boundary. Rejection returns to a new rendered study; it should not silently fall back to a generic UI identity.
