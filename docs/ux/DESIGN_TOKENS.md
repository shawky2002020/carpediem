# Design token architecture

## Source and layers

1. **Primitive source:** private Sass values in `design-system/styles/_primitives.scss` for sampled brand color, neutral color, spacing, and radii. Future font and motion primitives should stay small and named.
2. **Semantic runtime aliases:** CSS custom properties in `_semantic-tokens.scss` for canvas, surface, text, borders, action, focus, status, typography, layout, and motion. Components consume aliases only.
3. **Component aliases:** add only when a real component needs a stable mapping across variants; never create a token for a one-off exception.

| Category | Small v1 scale or contract |
| --- | --- |
| Color | Brand and neutral Sass primitives; semantic CSS variables such as `--cd-bg-canvas`, `--cd-text-primary`, `--cd-action-primary`, `--cd-status-danger-fg`. |
| Font family | One provisional system stack; swap to owner-approved, self-hosted variable UI face later. |
| Font size | 12, 14, 16, 20, 24, 32 px. Body remains 16 px; 12 px is restricted to noncritical supporting text. |
| Weight | 400, 500, 600, 700. |
| Line height | 1.25 headings, 1.5 body/controls, 1.6 longer reading. |
| Letter spacing | Normal body; optional 0.04em for short uppercase labels only. No tracked long-form text. |
| Spacing | 0, 4, 8, 12, 16, 20, 24, 32, 40, 48 px. Use 8/16/24 rhythm most often. |
| Size | 48 px default field minimum, 44 px target minimum, 672 px reading maximum, 1152 px content maximum. |
| Radius | 8 px control, 12 px panel, full pill badge. |
| Border | 1 px default; 3 px keyboard focus ring. |
| Elevation | No shadow by default; one restrained overlay shadow. |
| Opacity | Disabled 0.45 only with native `disabled` semantics; loading is not represented through opacity alone. |
| Layer | Base 0, sticky 10, popover 20, dialog 30; overlay backdrop slots below active panel. |
| Breakpoint | Layout shifts around 48rem and 75rem only when content benefits. Component container queries may be added with evidence. |
| Motion | 0, 120, 180, 240 ms; standard/enter/exit easing. Reduced motion resolves movement to none. |

## Naming and consumption

- Public CSS variables use `--cd-<role>-<detail>` and logical dimensions (`inline`, `block`) wherever possible.
- Component CSS must use semantic color variables. Never bind brand orange to `warning` or logo red to `danger` by name coincidence.
- The provisional palette and system font stack are a safe integration skeleton, not a brand lock. Future Angular global styles can import `design-system/styles/index.scss` once the application exists.
- Runtime theming remains possible because semantic values are CSS properties. There is no dark theme or theme switch in v1.

## Governance

Add a token only when at least two reusable patterns need it or a clear accessibility/brand role warrants it. Review token additions with a mobile specimen, contrast pairing, and naming rationale. Deprecate aliases before removal when application code has adopted them.
