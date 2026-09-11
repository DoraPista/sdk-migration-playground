# Exercise 14-02 – The Gallery Freezes and Eats Memory

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- WPF list virtualization
- image decoding and memory
- keeping work off the UI thread

## Scenario

After a migration, users open a gallery to check that the files arrived. For a project with a few dozen
photos it is fine. For the site-photo project (2,000 images, 3–8 MB each) the window is white for half a
minute, the app takes ~2 GB, and scrolling stutters.

`ThumbnailService` produces the thumbnails; `GalleryView.xaml` shows them.

## Your Task

1. Name the causes before you change anything — there is more than one, in more than one file.
2. Fix them. Thumbnails are displayed at 160 px wide.
3. Say how you would confirm the improvement in a real app.

## Constraints

- `ThumbnailService.LoadAsync` keeps its signature.
- The gallery still shows the file name under each thumbnail.
- No third-party packages.

## Acceptance Criteria

- Thumbnails are decoded at display size, not at full size.
- Thumbnails can be produced away from the UI thread and then shown on it.
- Loading can be cancelled.
- The gallery only builds the items that are on screen.

## How to Run

```bash
dotnet test exercises/14-performance/14-02-thumbnail-gallery-freezes/tests
```

The tests create their own sample images, so nothing needs to be downloaded.

## When You're Done

All tests pass, and you can explain each fix — including what `Freeze()` does and why the XAML change matters.
