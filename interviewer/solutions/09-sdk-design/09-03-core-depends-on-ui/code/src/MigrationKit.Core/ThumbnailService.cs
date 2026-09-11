// Thumbnail rendering moved out of the core: it needs an imaging stack, and every host already has one.
// The core asks for IThumbnailRenderer (see Contracts.cs); the WPF host implements it in
// MigrationKit.WpfDemo/WpfThumbnailRenderer.cs, and the MAUI host will implement its own.
