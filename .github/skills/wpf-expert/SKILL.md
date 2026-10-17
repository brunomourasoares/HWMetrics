---
name: wpf-expert
description: Build, debug, and modernize native C# WPF applications on .NET. Use for XAML, MVVM, bindings, commands, threading, resource dictionaries, styles, controls, accessibility, and Windows desktop behavior.
---

# WPF Engineering Skill

Use this skill for native WPF work in RMMonitor. The upstream reference is the [ManagedCode dotnet-skills WPF skill](https://skills.managed-code.com/skills/wpf/); these project instructions adapt its scope to this repository.

## Project constraints

- The application is native **C# + WPF**, targeting **.NET 10**. Keep implementation in the existing stack.
- Do not introduce web UI frameworks, Electron, WinUI, Avalonia, or third-party UI frameworks unless the user explicitly approves the change.
- Preserve existing architecture, naming, localization, theme support, and patterns. Inspect neighboring files before proposing a new abstraction.
- Keep hardware access, USB HID I/O, polling, and other blocking work off the UI thread. Marshal UI-bound updates through WPF-safe mechanisms and respect cancellation/lifetime.
- Never perform unrelated edits or commit directly to `main`. Work on a dedicated branch and propose integration through a pull request.

## WPF implementation rules

1. **Inspect before editing.** Read the target view, its code-behind, ViewModel, models/services, resource dictionaries, and related tests. Identify the actual binding/data flow before changing it.
2. **Prefer MVVM where the project already uses it.** Keep presentation state in ViewModels, use `INotifyPropertyChanged`/existing observable patterns, and use `ICommand` or the repository's established command implementation. Do not add a new MVVM framework casually.
3. **Bindings must be diagnosable.** Use correct binding paths, explicit modes where useful, and appropriate `UpdateSourceTrigger`. Avoid silently swallowing binding errors. Check output/debug diagnostics when possible.
4. **Respect WPF resource semantics.** Reuse shared brushes, styles, templates, and resource dictionaries. Use `DynamicResource` for values that must respond to runtime theme changes; use `StaticResource` for immutable resources when appropriate. Avoid duplicate keys and accidental resource-scope regressions.
5. **Keep UI responsive.** Avoid synchronous waits, long-running work, and hardware polling on the Dispatcher thread. Use async APIs where appropriate, cancellation tokens, bounded polling, and safe shutdown/disposal.
6. **Handle lifecycle explicitly.** Consider window close/minimize/tray behavior, timers, event subscriptions, background tasks, HID handles, and disposal. Prevent duplicate startup entries, orphaned tasks, and stale event handlers.
7. **Maintain accessibility and localization.** Keep keyboard navigation, focus visibility, sensible automation names, readable contrast, and localized strings consistent with the existing language system. Do not hard-code user-facing text if the app has localization resources.
8. **Avoid fragile XAML.** Prefer clear, reusable templates and styles over deeply nested one-off markup. Use converters only when appropriate; do not hide substantial business logic in converters or code-behind.
9. **Preserve compatibility.** Check project target framework, nullable settings, package conventions, and Windows-specific assumptions before using APIs. Do not add dependencies for a problem that can be solved cleanly with existing .NET/WPF capabilities.

## RMMonitor-specific review checklist

For changes involving sensor cards, charts, device controls, or the tray:

- Verify that missing, unavailable, or temporarily invalid sensor values are represented safely and do not look like real zero readings.
- Keep telemetry collection independent from visual rendering and avoid redundant sorting, polling, or formatting.
- Ensure refreshes do not create duplicate timers/subscriptions or race with window closure.
- For USB HID or display controls, preserve device-specific protocol behavior and never assume all Rise Mode devices expose identical capabilities.
- Test theme changes, language changes, resizing, and empty/error states when the affected UI supports them.

## Workflow

- Read the relevant code and tests before editing.
- Make the smallest coherent change.
- Add or update tests for logic that can be tested without physical hardware.
- Run the relevant tests and build where available; report checks that could not be run.
- Review the final diff for regressions and unrelated changes.
