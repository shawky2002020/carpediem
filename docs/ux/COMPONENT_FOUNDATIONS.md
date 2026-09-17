# Universal component contracts

No Angular components are registered yet. Implement only after the Angular workspace exists, using typed Reactive Forms, Angular CDK for overlay/focus patterns, and semantic token CSS. Names and APIs below are neutral; feature behavior remains deferred.

| Primitive | Minimal anatomy / API | States and accessibility |
| --- | --- | --- |
| Button | Native `button`, label, optional leading/trailing icon, `type`, variant primary/secondary/quiet. | Default, hover, pressed, focus, disabled, loading. Preserve a clear label and 44 px target; loading prevents duplicate activation without losing the button name. |
| IconButton | Native button, icon and required accessible name. | Same as Button; 44 px target; tooltip is supplemental only. |
| FormField | Label, optional hint, control slot, optional error/status. | Required/optional text, error association, success description; never rely on placeholder as label. |
| TextInput / Textarea | Native control, `autocomplete` and input mode as appropriate. | Focus, disabled, error, read-only; 16 px text; preserve user-entered content. |
| Select foundation | Native select by default; custom listbox only when a validated task needs it. | Keyboard, active option, selected state, disabled/error, screen-reader name. |
| Date/time foundation | Native or CDK-supported control chosen per feature and browser evidence. | Clear locale/time-zone presentation and validation; business rules deferred. |
| Checkbox / Radio | Native input plus whole-label hit target. | Checked, indeterminate where meaningful, focus, disabled, error group description. |
| StatusBadge | Text plus optional status icon; no interaction unless specified. | Success/warning/danger/info/neutral; color never stands alone. |
| InlineMessage | Title optional, message, optional action. | Info/success/warning/error; `role=alert` only for urgent updates. |
| Dialog / BottomSheet | Named heading, content, actions, close affordance. | CDK focus trap, Escape, backdrop handling per task, focus restoration; avoid nested overlays. |
| Popover foundation | Trigger, anchored panel, optional actions. | Keyboard trigger and dismissal, focus management, no hover-only content. |
| Skeleton / Progress | Reserved shape or native progress semantics. | Announce loading once; skeleton decorative; avoid indefinite animation under reduced motion. |
| EmptyState / ErrorState | Plain title, explanation, optional recovery action. | Specific text; no decorative illustration requirement. |
| Avatar / Image | Explicit dimensions, safe crop, alt according to context. | Decorative `alt=""`; informative location description; never infer identity from image alone. |
| Divider | Native border or `hr` for thematic separation. | Decorative separators not announced unnecessarily. |

For every implemented component, document purpose, responsive anatomy, allowed variants, disabled/loading/error states, keyboard flow, accessible name, focus, and reduced-motion behavior. Resist adding variants until a feature supplies evidence.
