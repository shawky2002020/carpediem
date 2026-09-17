# Typography study — approval pending

## Realistic content used

The study uses an event/location title (`Mango Tree Campsite - East Activity Grove`), a date/time (`Sat, 24 Oct 2026  08:30-17:00`), a status (`Response needed`), and a form label (`Leader availability`). All widths below are offline font metrics at 16 px, regular weight from the official variable TTF files. They predict wrapping; they do not prove Windows, Android, iOS, or browser rendering.

At 320 px with 16 px page gutters, the content width is 288 px. At 360 px it is 328 px; at 390 px it is 358 px. Long location names must be allowed to wrap. Data columns must not require this full string on one line.

| Option | 16 px title width | 16 px date/time width | Normal variable TTF size | Character and tradeoff |
| --- | ---: | ---: | ---: | --- |
| A — DM Sans | 324 px | 229 px | 240,164 B | Friendly geometric structure with a useful optical-size axis. Title wraps at 320 px and fits at 360 px. Best brand warmth in this set. |
| B — Source Sans 3 | 266 px | 185 px | 646,340 B | Compact, very clear operational text; title fits at 320 px. Less distinctive. Raw TTF is largest; an actual WOFF2 subset budget must be checked. |
| C — Plus Jakarta Sans | 320 px | 233 px | 176,288 B | Contemporary and expressive; title wraps at 320 px. Wider status and metadata text can reduce roster density. |

All three are listed as OFL families with variable weight in the official [DM Sans metadata](https://github.com/google/fonts/blob/main/ofl/dmsans/METADATA.pb), [Source Sans 3 metadata](https://github.com/google/fonts/blob/main/ofl/sourcesans3/METADATA.pb), and [Plus Jakarta Sans metadata](https://github.com/google/fonts/blob/main/ofl/plusjakartasans/METADATA.pb). They are Latin-script choices; none is an Arabic solution. The logo lettering is artwork and is not a UI font.

## Recommendation

**Option A, DM Sans**, is the leading identity choice. Use one variable family for headings, body, forms, metadata, and controls; restrict weights to 400, 500, 600, and 700 in application CSS. Keep tabular numerals for roster counts and times if the selected font supports them in the shipped subset. Use the system font stack in the provisional SCSS until approval and font packaging.

Option B is the practical alternative if dense roster testing favors compact text. Option C is best kept as a visual comparison candidate. Do not combine families without a measured need because each additional face adds payload and fallback complexity.

## Browser specimen evidence

The [comparison specimen](specimens/typography-study.html) renders all three candidates against the proposed token CSS. In the Codex in-app browser on Windows, all three web fonts loaded. At 320 px, the 24 px location title occupied three lines in DM Sans, two in Source Sans 3, and three in Plus Jakarta Sans. At 360, 390, and 430 px, each occupied two lines; at a 768 px single-column layout, each fit one line. At 1280 px, the three-column comparison gave each title two lines. No horizontal document overflow was observed at the tested widths. The narrowest buttons were 44 px high and inputs 49 px high. Keyboard focus on the first input showed the 3 px forest outline and offset. This verifies the **specimen** only, not an Angular app.

## Required rendered acceptance pass

At 320, 360, 390, 430 px, tablet, and desktop, compare each option using: a long event title, the location string above, form labels and inputs, validation text, `08:30–17:00`, `11/12`, status labels, two-line buttons, and dense roster rows. Check wrapping at 200% zoom, numeral alignment, punctuation, Windows ClearType, Android Chrome, and iOS Safari. If Arabic enters approved scope, test Arabic font pairing, bidi/RTL, numerals, line height, and font metrics before adding production weight. This cross-browser pass is deferred until the Angular foundation exists.
