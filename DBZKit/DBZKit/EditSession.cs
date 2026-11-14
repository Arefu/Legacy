using DrGero.IO;

namespace DBZKit
{
    /// <summary>
    /// The private, editable copy of the loaded ROM that the Engine tools, Sprite editor and Sound tabs all work on. "Apply to DBZKit" hands the
    /// edited bytes back to the main window (so the viewers show them); "Save ROM As..." writes them to disk, defaulting to the opened ROM's
    /// own folder and file name.
    /// </summary>
    internal sealed class EditSession(Action<byte[]> commit)
    {
        private readonly Action<byte[]> _commit = commit;

        public ROM? Rom { get; private set; }
        public string? Path { get; private set; }

        /// <summary>Raised after a ROM is opened (or cleared); panels reload from <see cref="Rom"/>.</summary>
        public event Action? Loaded;
        /// <summary>Raised after any panel edits <see cref="Rom"/>, so other panels can refresh what they show.</summary>
        public event Action<object?>? Changed;

        /// <summary>The file the last Save As wrote (suggested next time). The ROM that was opened is never written.</summary>
        public string? SavePath { get; private set; }

        /// <summary>True when the ROM has edits that aren't in a saved file yet.</summary>
        public bool Dirty { get; private set; }

        public event Action? DirtyChanged;

        public void Load(byte[]? bytes, string? path)
        {
            Rom = bytes == null ? null : ROM.FromBytes((byte[])bytes.Clone());
            Path = path;
            SavePath = null;
            SetDirty(false);
            Loaded?.Invoke();
        }

        /// <summary>
        /// An editor changed <see cref="Rom"/>: the main window's copy follows straight away (the boot-screen pages read it), the ROM
        /// counts as unsaved, and every other page refreshes.
        /// </summary>
        public void NotifyChanged(object? source = null)
        {
            Apply();
            SetDirty(true);
            Changed?.Invoke(source);
        }

        public void Apply()
        {
            if (Rom != null) _commit(Rom.ToArray());
        }

        private void SetDirty(bool dirty)
        {
            if (Dirty == dirty) return;
            Dirty = dirty;
            DirtyChanged?.Invoke();
        }

        /// <summary>Writes the edited ROM to a file the user picks. Picking the ROM that was opened is refused, so it stays untouched.</summary>
        public void SaveAs(IWin32Window? owner)
        {
            if (Rom == null) return;
            string? suggest = SavePath ?? Path;
            using var dlg = new SaveFileDialog
            {
                Title = "Save ROM As",
                Filter = "GBA ROM (*.gba)|*.gba",
                InitialDirectory = suggest != null ? System.IO.Path.GetDirectoryName(suggest) : null,
                FileName = suggest != null ? System.IO.Path.GetFileNameWithoutExtension(suggest) + (SavePath == null ? " (edited)" : "") : "",
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return;
            if (Path != null && string.Equals(System.IO.Path.GetFullPath(dlg.FileName), System.IO.Path.GetFullPath(Path), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(owner, "That's the ROM you opened. Pick another name so the original stays untouched.", "Save As",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                SaveAs(owner);
                return;
            }
            File.WriteAllBytes(dlg.FileName, Rom.ToArray());
            SavePath = dlg.FileName;
            SetDirty(false);
        }
    }
}
