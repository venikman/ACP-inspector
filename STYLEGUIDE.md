# F# Style Guide

This repo follows a single, shared F# style guide for naming, layout, and idioms.

- Source of truth: https://github.com/fsharp/fslang-design#style-guide
- Formatting: enforced with Fantomas
  - Format: `dotnet tool restore && dotnet fantomas src tests apps`
  - Check only: `dotnet tool restore && dotnet fantomas src tests apps --check`
