---
name: wpf-expert
description: "Build, debug, and modernize native C# WPF applications on .NET. Use for XAML, MVVM, data binding, commands, threading, resource dictionaries, styles, controls, accessibility, and Windows desktop behavior."
---

# WPF Engineering Skill

Use this skill for native WPF work in RMMonitor. It is tailored from the [ManagedCode dotnet-skills WPF skill](https://github.com/managedcode/dotnet-skills/tree/main/catalog/Frameworks/WPF) and its references, with project-specific constraints. Consult upstream for broader patterns; repository code and tests remain authoritative for this application.

## Trigger conditions

Read and apply this skill when changing WPF views, XAML, bindings, commands, view-model boundaries, styles/templates, Dispatcher behavior, window lifecycle, Windows integration, or desktop modernization. For visual hierarchy, dashboards, charts, typography, spacing, and design consistency, also read `.github/skills/wpf-visual-design/SKILL.md`.

## Project constraints

- RMMonitor is native **C# + WPF** targeting **.NET 10**. Keep implementation in the existing stack.
- WPF is Windows-only. Do not introduce web UI frameworks, Electron, WinUI, Avalonia, or third-party UI frameworks unless the user explicitly approves the change.
- Preserve the existing architecture, naming, localization, theme support, package conventions, and patterns. Inspect neighboring files before proposing a new abstraction.
- Do not add MVVM Toolkit, a DI container, or another framework just because an upstream example uses it. Follow existing project patterns unless a migration is explicitly requested.
- Keep hardware access, USB HID I/O, polling, and blocking operations off the UI thread. Respect cancellation, ordering, device lifetime, and safe shutdown.
- Never edit `main` directly. Use a focused branch and propose integration through a pull request.

## Required workflow

1. **Map the real implementation first.** Read the target XAML and code-behind, related models/services, shared resources, localization entries, and tests. Trace the actual data flow and lifecycle.
2. **Make the smallest coherent change.** Avoid unrelated refactors and broad restyling during a functional fix.
3. **Keep UI and hardware responsibilities separated.** Prefer the existing service/model boundaries. Do not perform HID reads, disk/network I/O, waits, or expensive polling on the Dispatcher thread.
4. **Use async and Dispatcher deliberately.** Avoid `.Wait()`, `.Result`, and synchronous waits on UI paths. Use cancellation-aware async work where possible. Marshal only UI-bound operations to the Dispatcher; avoid unnecessary synchronous `Dispatcher.Invoke`.
5. **Make bindings explicit and diagnosable.** Verify binding paths, source/DataContext, mode, and `UpdateSourceTrigger` as needed. Avoid swallowing binding errors; use debug output or diagnostics when available.
6. **Use the project's command and notification patterns.** Prefer existing `ICommand` implementations and `INotifyPropertyChanged`/observable patterns. Keep business logic testable; do not move substantial logic into converters or event handlers.
7. **Respect resource and theme semantics.** Reuse shared resource dictionaries, brushes, styles, and templates. Use `DynamicResource` for values that must update at runtime (such as theme resources) and `StaticResource` where runtime replacement is not required. Avoid duplicate keys and accidental scope changes.
8. **Treat lifecycle as part of correctness.** Review timers, background tasks, event subscriptions, tray/window behavior, cancellation, HID handles, and disposal. Ensure close/reopen and repeated enable/disable operations do not leak or duplicate work.
9. **Preserve accessibility and localization.** Maintain keyboard access, visible focus, logical tab order, automation names, readable contrast, and localized user-facing strings.
10. **Validate both build-time and runtime-sensitive behavior.** XAML may compile while a resource lookup, binding, or control template fails at runtime. Run tests/build and inspect UI or Windows behavior when an environment is available. Clearly state what was not validated.

## RMMonitor telemetry and device rules

- Represent unavailable or invalid sensor values distinctly from real zero readings. Validate nullable values, NaN/infinity, units, and plausible sensor ranges before formatting.
- Keep acquisition, normalization, history storage, and rendering responsibilities distinct. Avoid duplicate polling, redundant sorting, unbounded histories, and repeated formatting work.
- A chart must preserve sample chronology and units, handle missing samples without implying false continuity, resize safely, and remain legible in both themes.
- Do not invent warning thresholds; use existing product logic or documented requirements.
- USB HID/display protocols are device-specific. Verify supported capabilities for the connected Rise Mode model; do not assume all devices share a protocol or control surface.
- Check errors and reconnects, duplicate device discovery, cancellation, handle disposal, and application exit when touching hardware code.
- For startup/tray work, account for UAC cancellation, idempotent enable/disable, missing scheduled tasks, quoted paths, duplicate legacy startup entries, and launch arguments.

## XAML and performance guidance

- Prefer readable, reusable styles/templates over deeply nested one-off markup.
- Avoid fixed dimensions when they cause clipping under resizing, Windows scaling/DPI, or long localized strings.
- Use virtualization for large item collections when compatible with the existing view.
- Avoid creating new brushes, geometries, timers, or event subscriptions on every telemetry refresh without a clear reason; freeze reusable Freezable resources when appropriate.
- Do not adopt APIs or XAML features without checking compatibility with the project's target framework and actual WPF runtime.
- Do not assume `x:CompileBindings` or other framework-specific features are available in WPF merely because they exist in another XAML stack.

## Testing and review

- Add/update tests for pure logic that can be tested without physical hardware (formatting, normalization, history bounds, command construction, state transitions).
- Keep hardware-dependent validation explicit; unit tests do not prove a physical HID device works.
- Run the repository's relevant test and Windows build workflows when available.
- Review the complete diff for regressions, localization omissions, theme regressions, thread-safety problems, and unrelated changes.

## Documentation references

- [ManagedCode WPF skill](https://github.com/managedcode/dotnet-skills/tree/main/catalog/Frameworks/WPF)
- [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [WPF data binding](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/)
- [WPF styles and templates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview)
- [WPF migration guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/migration/)
- [ManagedCode WPF patterns](https://github.com/managedcode/dotnet-skills/blob/main/catalog/Frameworks/WPF/skills/wpf/references/patterns.md)
- [ManagedCode WPF anti-patterns](https://github.com/managedcode/dotnet-skills/blob/main/catalog/Frameworks/WPF/skills/wpf/references/anti-patterns.md)
