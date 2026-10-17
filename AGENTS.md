# Agent instructions for RMMonitor

Before making changes to this repository, inspect the relevant code and follow the applicable project skill:

- For C#, WPF, XAML, MVVM, bindings, commands, threading, resources, lifecycle, or Windows desktop behavior, read `.github/skills/wpf-expert/SKILL.md`.
- For layout, dashboard composition, sensor cards, charts, themes, typography, spacing, visual states, or accessibility, read `.github/skills/wpf-visual-design/SKILL.md`. For visual work, use both skills.

Project workflow:
- Do not commit directly to `main`. Create a focused working branch and propose changes through a pull request.
- Preserve the native C# WPF/.NET 10 stack unless the user explicitly approves a technology change.
- Inspect the diff and run relevant tests/build checks when available. Clearly report checks that could not be performed.
- Keep changes scoped to the request and preserve existing localization, theme behavior, and hardware compatibility.
