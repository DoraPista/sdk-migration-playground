# 14-02 The Gallery Freezes and Eats Memory – Interviewer Notes

**Type:** performance (WPF) · **Time:** 30 min · **Solution code:** `code/src/`

## Hints

1. "There are two separate problems in two files: one about how much memory each image costs, one about how many tiles exist."
2. "At what size does a BitmapImage decode by default? And what available height does a ScrollViewer give its child?"
3. "DecodePixelWidth with CacheOption.OnLoad and Freeze, decoding off the UI thread with bounded parallelism; and replace the ScrollViewer/ItemsControl/WrapPanel with a virtualizing ListBox."

## The causes

| # | Cause | Effect for 2,000 photos |
|---|---|---|
| 1 | `BitmapImage` decoded at full size (no `DecodePixelWidth`) | A 1,600 × 1,200 photo is 7.7 MB of pixels; 2,000 of them is ~15 GB of decoding for 160 px tiles |
| 2 | Bitmaps are not `Freeze()`d | They keep thread affinity and change notification; a bitmap made on a worker thread **cannot be shown** by the UI thread at all |
| 3 | `LoadAsync` is `async` in name only — it decodes on the calling thread and returns `Task.FromResult` | Called from the UI thread, the window is frozen for the whole load |
| 4 | The token is ignored | Closing the gallery does not stop the work |
| 5 | `ItemsControl` in a `ScrollViewer`, on a `WrapPanel` | Two separate reasons virtualization is off: the `ScrollViewer` measures its child with **infinite** height, and `WrapPanel` is not a virtualizing panel. All 2,000 tiles are built |

Candidates who find 1 and 5 have the core. 2 and 3 go together (freezing is what makes off-thread loading possible).

## The fixes

```csharp
image.DecodePixelWidth = ThumbnailWidth;   // decode small; never allocate the full-size pixels
image.CacheOption = BitmapCacheOption.OnLoad;  // read during EndInit, don't hold the file open
image.Freeze();                            // no thread affinity: safe to hand to the UI thread
```

plus `Parallel.ForAsync` with a small degree of parallelism (decoding is CPU-bound; unbounded `Task.WhenAll`
over 2,000 files would swamp the machine, see 03-01) and a `CancellationToken` that is honoured.

XAML: drop the outer `ScrollViewer`, use a `ListBox` (which brings a virtualizing `ScrollViewer`) with
`VirtualizingPanel.IsVirtualizing`, `VirtualizationMode="Recycling"` and `ScrollUnit="Pixel"`.

Setting only `DecodePixelWidth` **or** only the panel is a partial answer: memory and start-up time need both.

## How they would confirm it in a real app

Look for: the WPF Performance Suite / Visual Studio's *Application Timeline* and *Memory Usage* tools,
`dotnet-counters` (allocation rate, Gen 2 size), a memory snapshot showing `BitmapImage` and container counts,
`VisualTreeHelper`/Snoop to see how many containers exist while scrolling, and a stopwatch around the load.
A good answer also says *what* to measure: time to first paint, working set at rest, frames dropped while scrolling.

## Common mistakes

- `DecodePixelWidth` **and** `DecodePixelHeight` both set → the image is squashed. The aspect-ratio test catches it.
- Loading on a background thread without `Freeze()` → `InvalidOperationException: The calling thread cannot access this object…`. The cross-thread test catches it.
- `Task.Run(() => LoadAsync(...))` at the call site, leaving the service thread-hostile: it moves the freeze problem, it does not solve it.
- Adding `IsVirtualizing="True"` while leaving the outer `ScrollViewer` in place — no effect, and it *looks* like a fix.
- Replacing the images with a `WriteableBitmap` cache or a custom panel: far more work than the problem needs.
- Keeping `BitmapCacheOption.OnDemand`/`Default` and wondering why the files stay locked and the migration cannot delete them.

## Follow-up questions

- **"The designer insists on a wrapping grid."** Nothing in WPF virtualizes a `WrapPanel`. Options: group the items into rows in the view model and virtualize the rows; write a `VirtualizingPanel` subclass; or take a third-party virtualizing wrap panel. This is the best senior discriminator in the exercise.
- "The photos are on a network share." Loading is then IO-bound: higher parallelism, a `MemoryStream` copy, and a disk cache of thumbnails.
- "The user scrolls to the bottom immediately." Recycling containers plus loading thumbnails on demand per visible item (and cancelling ones scrolled past).
- "How big should the thumbnail cache be?" Bounded by count or bytes, with an LRU; 2,000 × 160 px thumbnails is only ~50 MB, so a full cache is fine here.

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds `Task.Run` and calls it fixed; does not know `DecodePixelWidth` or why the tiles are all built |
| Solid mid-level | Finds the decode size and the virtualization problem; freezes the bitmaps; honours cancellation |
| Strong | Explains *why* the outer `ScrollViewer` disables virtualization, bounds the decoding parallelism, and knows what `Freeze()` buys |
| Senior | Discusses measurement first, the wrapping-grid trade-off, on-demand loading and cache bounds |
