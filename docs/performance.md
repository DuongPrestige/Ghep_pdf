# Performance notes

Date: 2026-07-16

Environment:

- Windows, .NET 10 SDK.
- Test run from repository root with `dotnet test PDFPageComposer.slnx --no-restore`.

Implemented controls:

- Source file list and page list use WPF recycling virtualization.
- Thumbnail render starts from realized page cards, not during PDF import.
- Thumbnail render queue uses bounded concurrency and cancellation.
- Thumbnail cache uses an LRU memory budget of 64 MB by default.
- Large preview image is transient ViewModel state and is cleared when preview closes.

Verification:

- `MainViewModelTests.ImportPdfFilesAsync_imports_500_pages_without_eager_thumbnail_rendering` imports 10 synthetic PDFs with 500 total pages, asserts zero thumbnail render calls during import, and enforces a 2 second budget for the metadata/state update path.
- `ThumbnailCacheServiceTests.Set_evicts_least_recently_used_entries_when_budget_is_exceeded` verifies cache memory stays under budget with LRU eviction.
- `ThumbnailRenderQueueTests.RenderAsync_limits_concurrent_renders` verifies bounded concurrent thumbnail rendering.
- Current result: 79 tests passed in the latest run.

Follow-up measurement for real PDFs:

- Use 10 real PDFs totaling around 500 pages.
- Import them and scroll Source Workspace from top to bottom while watching process working set in Task Manager.
- Expected behavior: import should remain responsive before thumbnails finish; working set should stabilize because off-viewport render requests cancel and the thumbnail cache is bounded.
