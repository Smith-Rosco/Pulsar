## ADDED Requirements

### Requirement: System SHALL capture and cache window thumbnails for sub-menu slots
The system SHALL capture a static screenshot of each target window upon sub-menu entry and cache it keyed by window handle (hWnd), displaying the thumbnail on the corresponding sub-menu slot instead of the generic application icon.

#### Scenario: Thumbnail captured successfully on sub-menu entry
- **WHEN** a sub-menu is opened for a multi-window process group
- **THEN** the system SHALL invoke `CaptureWindowAsync(hWnd)` for each window in the sub-menu
- **AND** the captured bitmap SHALL be resized to 48×48 pixels (preserving aspect ratio, letterboxed to square)
- **AND** the thumbnail SHALL be stored in an in-memory cache keyed by hWnd
- **AND** the thumbnail SHALL be bound to the slot's display in place of the app icon

#### Scenario: Thumbnail capture fails for a window
- **WHEN** `CaptureWindowAsync` fails (window minimized, cloaked, or capture error)
- **THEN** the slot SHALL fall back to displaying the application's extracted icon (`AppIcon`)
- **AND** the slot SHALL additionally apply color coding (see `submenu-window-color-coding` spec)
- **AND** no error SHALL be surfaced to the user

#### Scenario: Thumbnail already cached for a window
- **WHEN** a sub-menu entry requests a thumbnail for an hWnd that already has a valid cached entry
- **THEN** the system SHALL use the cached thumbnail immediately without re-capturing
- **AND** the cached thumbnail's `CapturedAt` timestamp SHALL NOT be updated

### Requirement: Thumbnail cache SHALL invalidate on window state change
The thumbnail cache SHALL detect stale entries and evict them when the associated window's title changes or the window handle becomes invalid.

#### Scenario: Window title changes after capture
- **WHEN** the system detects that a cached thumbnail's `WindowTitle` no longer matches the current window title for that hWnd
- **THEN** the cached entry SHALL be evicted
- **AND** the next sub-menu entry for that window SHALL trigger a fresh capture

#### Scenario: Window handle becomes invalid
- **WHEN** `IsWindow(hWnd)` returns false for a cached thumbnail's window handle
- **THEN** the cached entry SHALL be evicted immediately

#### Scenario: Cache eviction under memory pressure
- **WHEN** the cache size exceeds the maximum of 50 entries
- **THEN** the least recently accessed entry SHALL be evicted (LRU policy)
- **AND** the eviction SHALL be non-blocking

### Requirement: Thumbnail display SHALL integrate with existing slot icon priority
The thumbnail display SHALL fit into the existing three-tier `SlotOrb` icon resolution: Thumbnail takes highest priority, falling through to icon key glyph, then to label text.

#### Scenario: Slot has valid thumbnail
- **WHEN** a sub-menu slot has a valid cached thumbnail
- **THEN** the `SlotOrb` SHALL render the thumbnail bitmap in its `<Image>` element (priority 1)
- **AND** the thumbnail SHALL be rendered using `HighQuality` bitmap scaling mode

#### Scenario: Slot has no thumbnail but has app icon
- **WHEN** a sub-menu slot has no cached thumbnail but has an `IconImage` from `ProcessWindowInfo.AppIcon`
- **THEN** the `SlotOrb` SHALL render the app icon in its `<Image>` element (priority 2)

#### Scenario: Slot has neither thumbnail nor icon
- **WHEN** a sub-menu slot has no thumbnail and no app icon
- **THEN** the `SlotOrb` SHALL fall through to glyph/text rendering (priority 3)

### Requirement: Thumbnail cache SHALL be thread-safe
The thumbnail cache SHALL support concurrent access from the UI thread (reads) and background capture tasks (writes).

#### Scenario: Concurrent read and write
- **WHEN** the UI thread reads a cached thumbnail while a background task writes a new capture
- **THEN** the read SHALL return either the old value or the new value, never a corrupted state
- **AND** the cache implementation SHALL use `ConcurrentDictionary<IntPtr, CachedThumbnail>`
