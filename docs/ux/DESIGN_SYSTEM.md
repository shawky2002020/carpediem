# Carpediem design system foundation

**Status:** design foundation proposed; typography, primary UI palette, and photo rights require owner decisions before production registration. No Angular workspace exists yet.

## Read in order

1. [DESIGN_DISCOVERY.md](DESIGN_DISCOVERY.md) — existing repo, architecture boundaries, gaps.
2. [BRAND_DIRECTION.md](BRAND_DIRECTION.md) — visual thesis and logo evidence.
3. [TYPOGRAPHY.md](TYPOGRAPHY.md) and [COLOR_SYSTEM.md](COLOR_SYSTEM.md) — owner review of high-impact choices.
4. [DESIGN_TOKENS.md](DESIGN_TOKENS.md) — Sass primitives and CSS semantic aliases.
5. [MOBILE_FIRST_RULES.md](MOBILE_FIRST_RULES.md), [COMPONENT_FOUNDATIONS.md](COMPONENT_FOUNDATIONS.md), [MOTION_SYSTEM.md](MOTION_SYSTEM.md), [ACCESSIBILITY.md](ACCESSIBILITY.md) — implementation contracts.
6. [ASSET_AUDIT.md](ASSET_AUDIT.md) and [ASSET_REGISTRY.md](ASSET_REGISTRY.md) — reviewed imagery and rights gate.
7. [DESIGN_QA.md](DESIGN_QA.md) — build, image, and rendered specimen evidence.

## Review the direction

- [Design direction specimen](specimens/direction.html) — the proposed “field journal for operations” identity, responsive composition, and component language.
- [Typography in use](specimens/typography-study.html) — Bricolage Grotesque and Hanken Grotesk across display, data, and form content.

Both specimens are static development artifacts. They use realistic example content to make the foundation reviewable without inventing a product feature or Angular workflow.

## Code foundation

`design-system/styles/index.scss` imports semantic tokens, minimal global foundations, and motion. It is intentionally disconnected from an app. `tools/optimize_design_assets.py` generates content-reviewed, rights-unknown image candidates in `design-assets/review/` from the separately held Drive originals. No app navigation, feature form, domain rule, or Angular component was introduced.

## Performance budget for the first application slice

- Proposed initial fonts: Hanken Grotesk variable Latin WOFF2 at **34,704 B** plus Bricolage Grotesque 600 Latin WOFF2 at **40,016 B**, for **74,720 B** total. Preload only Hanken by default, use `font-display: swap`, and measure fallback layout shift after approval.
- Initial decorative/location image: **≤ 200 KB** on an ordinary phone when it is actually needed. Keep image assets out of operational form screens unless they add useful context.
- Defer below-fold images with lazy loading; provide dimensions and intentional mobile/desktop crops. Never deliver 21–22 MB source PNGs.
- Avoid continuous animation, blur-heavy panels, blanket shadows, and an animation library for simple state transitions.
- Measure LCP, CLS, and responsiveness on a representative mid-range phone once the Angular app exists. These are budgets, not measured application results.

## Iconography

Choose one outline icon family when the app is scaffolded. Use a 20/24 px visual grid with consistent optical alignment and about 2 px stroke. Keep status icons paired with words. A filled form can indicate selected state. Do not create dozens of custom camping icons or mix libraries without a visible reason.

## Next integration gate

When FOUNDATION-01 creates the Angular workspace, review its actual paths and import `index.scss` once. After owner approval, self-host the proposed font files at the real application asset boundary and register only rights-cleared optimized imagery. Translate the static component board into the first small set (Button, FormField/TextInput, StatusBadge, InlineMessage), then verify 320/360/390/430 px, tablet, desktop, focus, 200% zoom, reduced motion, and accessibility in rendered browsers before promoting the system.
