# Field desk design system

## Palette and type

| Token | Value and use |
| --- | --- |
| Rail | `#152b24` for workspace chrome, the brand panel, and factual summary surfaces |
| Canvas | `#f8f7f2` for the working background |
| Surface | `#ffffff` for forms and controls |
| Primary ink | `#261f2e` for titles and content |
| Fern | `#225c46` for primary action and focus on light backgrounds |
| Mango | `#ffd488` for current navigation and focus on the dark rail |

The self-hosted Bricolage Grotesque display font anchors entries and page titles. Hanken Grotesk handles body text and controls. Tabular numerals keep counts and dates aligned. Status always has a label with the semantic success, warning, danger, or information treatment.

## Layout and reusable behavior

- Wide signed-in layouts use a 14 rem workspace rail. Below 64 rem, a disclosure shows all role destinations and the bottom bar provides the most-used routes. Short viewports move the bar into normal flow.
- Default gutters are 16, 24, and 32 px. Reading forms stay narrow. Queues use rows and dividers. Candidate comparison uses a split view where it fits, with the correct priority on mobile.
- Controls use the existing semantic SCSS button, field and focus contracts. Targets use a 44 px floor. Local summaries and lighter status surfaces serve supporting detail.
- Native buttons, radios, details disclosures and dialogs supply keyboard behavior. Route navigation moves focus to the main landmark. Confirmations retain or restore meaningful focus.
- Normal motion is short and attached to feedback. Reduced motion zeroes the common motion tokens. No route animation or decorative pulse is needed for day-to-day work.
- The supplied wordmark is a 448 × 239 transparent WebP on the dark entry surfaces. It has intrinsic dimensions. The compact official emblem is used in the masthead and signed-in home.

Implementation in [semantic tokens](../../../design-system/styles/_semantic-tokens.scss), [shared components](../../../design-system/styles/_components.scss), and the [Angular shell](../../../frontend/src/app/layout/app-shell.component.html).
