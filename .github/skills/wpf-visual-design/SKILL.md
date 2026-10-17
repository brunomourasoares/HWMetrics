---
name: wpf-visual-design
description: Design and refine the visual system of RMMonitor's native WPF hardware dashboard, including information hierarchy, sensor cards, charts, themes, typography, spacing, states, and accessibility.
---

# Native WPF Visual Design Skill

Use this skill when creating or refining RMMonitor screens, dashboards, controls, charts, visual states, and design consistency. This is a **native desktop UI** skill: implement designs with WPF XAML, styles, templates, resource dictionaries, and existing project components—not web technologies.

For WPF implementation details, also follow `.github/skills/wpf-expert/SKILL.md`. The visual approach complements, rather than replaces, the [ManagedCode WPF skill](https://github.com/managedcode/dotnet-skills/tree/main/catalog/Frameworks/WPF) and Microsoft WPF documentation.

## Design objectives

- Make live hardware status scannable at a glance.
- Prioritize actionable state and important telemetry over decoration.
- Keep a coherent visual language across windows, panels, sensor cards, charts, device controls, and tray-related surfaces.
- Support the application's existing dark/light themes and localization.
- Favor legibility, predictable interaction, and restrained motion over visual effects that compete with telemetry.

## Required discovery before design changes

1. Inspect the current screen and neighboring views, shared resource dictionaries, theme implementation, charting controls, and localization resources.
2. Identify existing colors, typography, spacing, corner radii, icons, animations, and component styles before introducing new ones.
3. Determine whether the requested change is a local refinement or a cross-app design-system change. Prefer existing tokens and shared components.
4. If screenshots are available, use them as evidence of the current UI; do not infer unseen layout details.
5. Check actual WPF capabilities and the project's target framework; do not transfer examples from WinUI, MAUI, or web design systems without adapting and validating them.

## Visual system

### Hierarchy and layout
- Establish one clear primary focus per view. Use size, weight, spacing, alignment, and contrast consistently to distinguish page title, section heading, metric label, primary value, and supporting detail.
- Group related readings together. Use consistent card padding, alignment, value baselines, and gaps; avoid arbitrary per-card variation.
- Use responsive WPF layout primitives such as Grid, DockPanel, StackPanel, and shared size definitions appropriately. Avoid fixed dimensions when they cause clipping at different window sizes, DPI scales, or localized text lengths.
- Preserve reasonable whitespace without wasting space needed for useful telemetry. Prefer a stable grid and deliberate alignment over decorative separators everywhere.
- Keep charts and history views readable: label axes and units, distinguish series clearly, and avoid color-only communication. Handle missing samples explicitly rather than drawing misleading continuous trends.

### Color and status
- Reuse the project's existing theme resources as the source of truth. Do not scatter literal colors through view XAML.
- Check both dark and light themes for contrast and semantic consistency.
- Reserve strong status colors for meaningful states such as warning, critical, unavailable, or active. Do not communicate state through color alone; include text, iconography, or another cue where appropriate.
- Keep temperature, frequency, utilization, fan speed, and storage metrics visually distinct only when the distinction improves interpretation—not merely to add color.

### Typography and data density
- Make the primary metric easy to scan; subordinate units, labels, timestamps, and secondary context.
- Use consistent number formatting, units, precision, and alignment for comparable readings.
- Avoid excessive font weights, all-caps labels, tiny low-contrast text, and unnecessary ornament.
- Distinguish unavailable data from a real value (for example, use the project's established placeholder/state instead of presenting a false zero).

### Interaction and states
- Design normal, hover, pressed, focused, disabled, loading, warning, critical, unavailable, and error states where applicable.
- Keep focus indicators and keyboard navigation visible and functional.
- Make interactive controls visually identifiable and use hit targets that are practical for mouse and touch where relevant.
- Animations should be short, purposeful, and non-blocking. Respect reduced-motion expectations where feasible and avoid animating high-frequency telemetry in ways that cause distraction.
- Tooltips supplement labels; they must not be the only way to discover essential actions or status.

## Theme, localization, and accessibility
- Verify the change in dark and light themes and with long translated strings; the application supports multiple languages.
- Use dynamic resources for runtime theme-switchable values when consistent with the project's implementation.
- Maintain adequate text and non-text contrast, keyboard access, logical tab order, and meaningful automation properties.
- Avoid relying solely on hue, position, or animation to convey critical hardware state.

## RMMonitor dashboard guidance
- Primary sensor value first; label and unit next; historical/trend information as supporting context.
- Keep CPU, GPU, storage, and other device groups consistent while allowing each metric type to expose relevant details.
- Use warning thresholds only when grounded in existing product logic or documented requirements; do not invent thresholds as a design shortcut.
- Preserve the distinction between measured values, calculated summaries, and unavailable readings.
- For Rise Mode device displays and controls, make device compatibility/capability explicit and avoid presenting controls unsupported by the connected model.
- Optimize for stable reading during live updates: prevent layout jitter when digits, labels, or availability change.
- Avoid redesigning the whole app when a focused component-level change solves the request.

## Design review checklist
- [ ] Existing design tokens/styles are reused where appropriate.
- [ ] Dark and light themes remain coherent.
- [ ] Text, units, and values are legible at realistic window sizes and DPI.
- [ ] Long localized strings do not clip or overlap.
- [ ] Loading, empty, unavailable, warning, and error states are represented.
- [ ] Keyboard focus and accessible labels remain usable.
- [ ] Live updates do not cause avoidable layout jitter or UI-thread work.
- [ ] Chart chronology, scale, units, and missing data are represented honestly.
- [ ] The diff contains no unrelated restyling or new dependency without justification.

## Deliverable expectations
When asked to redesign a view, state the key hierarchy and component decisions briefly, implement them using the repository's native WPF conventions, and validate with available build/tests or screenshots. Do not claim visual validation unless the UI was actually rendered and inspected.
