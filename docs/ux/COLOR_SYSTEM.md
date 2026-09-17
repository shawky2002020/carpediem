# Color system — proposed light theme

## Source and philosophy

The brand primitives below were sampled from flat regions of the official `logo.pdf` rendering at 144 dpi. They are starting points for a UI system; the PDF has not supplied named vector swatches. Photographed hut green and mango foliage support the overall direction without turning every photographic color into a token.

| Primitive | Value | UI role |
| --- | --- | --- |
| `brand.forest` | `#1D5041` | Proposed primary action and focus. |
| `brand.evergreen` | `#15281D` | Primary text. |
| `brand.orange` | `#F47025` | Occasional brand accent only. |
| `brand.red` | `#D13531` | Brand art; not the validation danger token. |
| `neutral.canvas` | `#F8F9F5` | Warm application canvas. |
| `neutral.white` | `#FFFFFF` | Surface and inverse text. |
| `neutral.secondary` | `#526157` | Secondary and muted text. |
| `neutral.border` | `#C8D2C9` | Decorative panel edge only; controls use the strong border. |

## Semantic aliases

| Alias | Value | Notes |
| --- | --- | --- |
| `bg.canvas` / `bg.surface` / `bg.subtle` | `#F8F9F5` / `#FFFFFF` / `#EAF2ED` | One light theme. |
| `text.primary` / `text.secondary` / `text.inverse` | `#15281D` / `#526157` / `#FFFFFF` | Muted text uses secondary contrast; no faint gray body copy. |
| `border.default` / `border.strong` | `#C8D2C9` / `#829486` | Strong border has 3.22:1 against white and is the field boundary. Decorative panel borders may be quieter. |
| `action.primary` / `hover` / `active` | `#1D5041` / `#174335` / `#102F25` | White label. |
| `focus.ring` | `#1D5041` | 3 px outline, 3 px offset. |
| `status.success` | `#246A45` on `#EEF7F0` | Icon and text accompany color. |
| `status.warning` | `#7A4C09` on `#FFF8EB` | Icon and text accompany color. |
| `status.danger` | `#B42318` on `#FFF2F0` | Separate from logo red. |
| `status.info` | `#195A7A` on `#ECF6FA` | Separate from brand palette. |

## Contrast checks

Calculated WCAG relative-luminance ratios: forest on white **9.23:1**, evergreen on canvas **14.68:1**, secondary on white **6.55:1**, secondary on canvas **6.19:1**, success on its tint **5.96:1**, warning on its tint **6.94:1**, and info on its tint **6.87:1**. Brand orange on white is only **2.92:1** and must not carry meaningful text or an essential focus indicator. These checks cover named pairings only; actual component states require rendered QA. WCAG 2.2 AA targets 4.5:1 for normal text and 3:1 for meaningful UI graphics and component boundaries.

The proposed colors are present in `design-system/styles/` as **provisional**, unconnected SCSS. Owner approval is required before application registration.
