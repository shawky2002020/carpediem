# Accessibility contract and QA status

Target: WCAG 2.2 AA plus the repository's 44 × 44 px mobile target rule. Accessibility is a token and component requirement, not a later pass.

## Foundation contracts

- Semantic HTML and native buttons/inputs first; use Angular CDK a11y and overlay facilities for dialogs, sheets, and focus management once the app exists.
- Every field has a persistent visible label. Link help and error text through `aria-describedby`; mark invalid fields with `aria-invalid`; focus the first invalid field or an error summary after submit. Required/optional meaning is conveyed in words.
- Keyboard focus uses a 3 px forest outline plus 3 px offset. Do not remove it. Sticky app bars and bottom actions must not fully obscure focused elements (WCAG 2.2 focus not obscured).
- Text pairings target at least 4.5:1 and meaningful non-text indicators at least 3:1. Orange on white fails normal text requirements and is decorative only.
- Interactive targets are at least 44 × 44 px in project UI. Place checkbox/radio labels within the hit area. No color-only status or error cue.
- Loading, success, and error changes use concise live-region text only when useful; avoid repetitive announcements. Dialogs need a name, focus trap, Escape behavior, and focus restoration.
- Reduced motion removes travel; 200% zoom and 320 px reflow must preserve task completion. Explicit image dimensions/aspect ratios prevent layout movement.
- Prepare for RTL through logical CSS properties and icon-direction review. Arabic-specific fonts and localization behavior wait for an approved scope.

## Verified now

- Named semantic text pairings were checked mathematically in [COLOR_SYSTEM.md](COLOR_SYSTEM.md).
- SCSS includes a visible `:focus-visible` rule, 44 px default button target, 48 px form control minimum, logical layout properties, and reduced-motion handling.

## Not yet verifiable

There is no Angular application, so keyboard traversal, screen-reader naming, CDK dialog focus, 200% zoom in a product flow, browser contrast in every state, target spacing, and RTL rendering remain **pending**. The future development showcase and first feature slice must run browser and assistive-technology checks. No WCAG conformance claim is made for an unbuilt application.
