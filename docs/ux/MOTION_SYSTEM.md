# Motion system

Motion clarifies state changes. Ordinary controls use CSS; the proposed system adds no animation package. Durations: instant 0 ms, fast 120 ms, normal 180 ms, slow 240 ms. Standard easing is `cubic-bezier(0.2, 0, 0, 1)`; enter and exit use the semantic values in `_semantic-tokens.scss`.

| Pattern | Default | Reduced motion |
| --- | --- | --- |
| Button press | Brief color/opacity change within 120 ms; no bounce. | Instant state change. |
| Popover/dropdown | Opacity plus 4–8 px translate, 120–180 ms. | Instant appearance with focus still moved correctly. |
| Dialog | Backdrop opacity and slight panel translate, 180 ms. | Instant panel, still traps focus and restores it on close. |
| Bottom sheet | Transform from bottom, at most 240 ms; do not animate height. | Instant position change. |
| Inline message/toast | Opacity 180 ms; urgent messages can appear immediately. | Instant visibility; announcements remain accessible. |
| List insert/remove | Small opacity/transform only after actual data change. | Instant replacement; preserve reading and focus order. |
| Loading → content | Reserve space with aspect ratio or skeleton, then gentle opacity transition. | Instant swap with no movement. |
| Expand/collapse | Prefer native disclosure behavior; animate only when measured and useful. | Instant open/close. |
| Route/content continuity | Preserve shell; avoid full-screen page sweeps. | No route movement. |
| Success feedback | State label/icon update; optional short opacity reveal. | State label/icon update only. |

Animate `transform` and `opacity` where movement is needed. Avoid animating width, height, top, left, blur, or large shadows in repeated interactions. The `prefers-reduced-motion: reduce` media query sets timing tokens to 0 and removes the supplied entrance keyframe, while preserving visible final states. Do not use motion as the only signal of a change.
