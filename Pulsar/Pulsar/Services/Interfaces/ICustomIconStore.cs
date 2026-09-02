using System.Collections.Generic;
using System.Windows.Media;
using Pulsar.Helpers;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// One imported custom icon: the store key (filename) plus a preview image
    /// source. The preview is resolved lazily at list time and may be null when the
    /// backing file is missing or corrupt (the entry is then skipped by callers).
    /// </summary>
    public record CustomIconEntry(string Key, ImageSource? Preview);

    /// <summary>
    /// User-level store for custom icons imported for use as slot/profile icons.
    /// Icons are persisted as files under
    /// <c>%AppData%\Pulsar\CustomIcons\</c>; the filename is the stable key that is
    /// stored as the <c>IconKey</c> string. No metadata index is maintained, so the
    /// store survives application restarts and tolerates manual file edits.
    /// </summary>
    public interface ICustomIconStore
    {
        /// <summary>
        /// Copies the icon file at <paramref name="sourcePath"/> into the store and
        /// returns the new store key (filename), or null when the source is missing
        /// or the copy fails.
        /// </summary>
        string? Import(string sourcePath);

        /// <summary>
        /// Resolves the icon for a store key to a WPF image source. Returns null when
        /// the file is missing, corrupt, or the key is invalid.
        /// </summary>
        ImageSource? GetIcon(string key);

        /// <summary>
        /// Enumerates all persisted imported icons. Corrupt/unloadable files are
        /// skipped (their preview is not resolvable). The result is ordered by key.
        /// </summary>
        IReadOnlyList<CustomIconEntry> List();

        /// <summary>
        /// Removes the icon file for <paramref name="key"/>. Returns true when the
        /// file was removed, false when the key did not exist (or deletion failed).
        /// </summary>
        bool Delete(string key);
    }
}
