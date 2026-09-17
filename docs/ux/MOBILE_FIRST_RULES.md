# Mobile-first layout rules

## Viewport contract

Begin at 320 CSS px and inspect 360, 375/390, 430, tablet, and desktop. Use the layout viewport, `viewport-fit=cover` only when a shell actually needs edge-to-edge treatment, and account for safe-area insets on fixed bottom actions. Content must reflow without primary-flow horizontal scrolling at 320 px or 400% zoom equivalent.

| Rule | Phone | Wide screen |
| --- | --- | --- |
| Page gutter | 16 px at 320–767 px; never crowd to 8 px. | 24 px from 768 px; 32 px from 1200 px. |
| Content width | Full available width with the gutter. | Max 1152 px, centered. Reading column max 672 px. |
| Forms | One column, controls at least 48 px high, 16 px input text to avoid mobile zoom behavior, visible labels above fields. | Form column max about 640–672 px; use two columns only for clearly related short fields. |
| Actions | Primary action has at least 44 × 44 px hit area and a clear label. Sticky actions only if they remain usable with the keyboard and safe area. | Actions can align with the form or panel, never far away from context. |
| Data | Group metadata into labeled rows; use responsive list or disclosure only when it preserves scanability. | Tabular alignment where cross-row comparison materially helps. |
| Dialogs | Prefer a compact dialog for short decisions and a bottom sheet for phone-heavy selection tasks. | Centered dialog or anchored popover when context supports it. |

## Navigation foundation

Define an app bar, optional bottom navigation, and a contextual action bar as patterns. Destinations and count are deferred to feature architecture. Never hide an essential action behind hover. The persistent bottom bar must leave room for the device safe area and browser chrome.

## Keyboard and zoom

Focus should scroll a field above the virtual keyboard without hiding its label or error. A sticky footer must release or reposition when it would obscure focus. Use `scroll-margin` on focusable targets. Test both portrait and landscape, 200% text zoom, long locations, and translated strings. Use logical properties (`margin-inline`, `inset-block`) and avoid direction-dependent icon mirroring except where meaning requires it.
