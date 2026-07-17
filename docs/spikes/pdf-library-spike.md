# PDF library spike

Completed: 2026-07-16

## Decision

- Rendering/metadata: `PDFiumCore`.
- Composition/export: `PDFsharp`.

## Findings

- `PDFiumCore` 152.0.7947 targets .NET Standard 2.1 / .NET 5 and NuGet computes compatibility through `net10.0-windows`. It includes runtime native binaries for Win-x86 and Win-x64, so it fits the Windows x64 deployment requirement.
- `PDFiumCore` is Apache-2.0 on NuGet.
- `PDFsharp` supports `net10.0` and NuGet lists the Windows-specific companion packages for `net10.0-windows`; the core package is enough to start composition/export work.
- `PDFsharp` is MIT on NuGet.
- Avoid iText for MVP unless a later task proves PDFsharp cannot preserve fidelity, because iText licensing can introduce AGPL/commercial obligations.

## Risks to verify in implementation

- Validate password handling and typed encrypted-file errors with real fixtures.
- Confirm page import preserves MediaBox, rotation, fonts, vector content and images for mixed documents.
- Confirm native PDFium DLL probing works in `win-x64` publish output before enabling single-file publish.
- Keep rendering behind `IPdfRenderService` and export behind `IPdfExportService` so either adapter can be replaced if fidelity tests fail.

## Sources

- NuGet `PDFiumCore`: https://www.nuget.org/packages/PDFiumCore/
- NuGet `PDFsharp`: https://www.nuget.org/packages/PdfSharp/
