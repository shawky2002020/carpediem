# Design discovery — 18 September 2026

## Existing foundation

- The repository is documentation-led. It has no `package.json`, `angular.json`, `src/`, Angular components, SCSS, design tokens, runtime image directory, or existing design-system file.
- The locked architecture at commit `f3559a6` specifies Angular 22, Angular CDK, typed Reactive Forms, custom SCSS, mobile-first delivery, and a browser client with no domain authority. The engineering constitution requires 44 × 44 px touch targets and WCAG 2.2 AA.
- The current baseline commit when this track branched was `eb62069`; it adds roadmap reconciliation after the lock. Existing untracked `specs/` work belongs to the parallel feature track and was not edited.
- `logo.pdf` is the official brand source at repository root. It was untracked at discovery and remains untouched. The one-page Illustrator PDF was visually inspected at 144 dpi; its badge shows trees, tent, landscape, green ring, orange field, red landform, and white lettering.
- The supplied Drive folder is accessible. Its root contains `Camspite`, `Activities`, and a 396.7 MB Mango Tree Campsite presentation. The campsite folder includes huts, interiors, and nature imagery. See [ASSET_AUDIT.md](ASSET_AUDIT.md).
- Figma account access is connected, but no Carpediem Figma file key or URL was supplied, stored in Product Design context, or present in the repository. A library cannot be safely inspected or updated without identifying a target file.

## Gaps and conflicts

| Finding | Decision for this track |
| --- | --- |
| No Angular app or route tree | Publish framework-ready SCSS and component contracts. Defer Angular components, showcase route, build, and rendered app QA to FOUNDATION-01. |
| Official logo is only a PDF | Preserve the PDF unchanged. Do not invent mono or SVG variants; obtain approved exports from the owner or source designer. |
| Logo colors are expressive but not all accessible as UI text | Separate logo primitives from semantic application colors. Orange is accent only. |
| Drive imagery has no verified rights or subject consent metadata | Curate and optimize into non-runtime `design-assets/review/`; require rights clearance before production delivery. |
| Angular skill recommends Signal Forms for new Angular 22 apps while locked architecture specifies typed Reactive Forms | Follow the locked architecture; this design stream does not change form strategy. |
| Brand typography and primary palette require human choice | Keep candidate styling provisional and show comparison evidence before treating it as locked. |

## Implementation risks

1. CSS/font specimens cannot establish cross-browser quality until an Angular app and representative content exist.
2. Native date/time control behavior varies by browser; component APIs must be grounded in feature specs before implementation.
3. RTL remains a structural readiness goal; Arabic is not in the currently approved product scope found in the repository.
4. The source PDF has no declared web-asset export. Runtime logo packaging waits on a faithful, approved export.

## Scope boundary

This track defines visual foundations only. Event, staffing, attendance, notification, cancellation, feedback, incident, and retention behavior remain governed by their existing product specifications. No feature page or business rule is created here.
