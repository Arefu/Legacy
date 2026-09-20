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

        public void Load(byte[]? bytes, string? path)
        {
            Rom = bytes == null ? null : ROM.FromBytes((byte[])bytes.Clone());
            Path = path;
            Loaded?.Invoke();
        }

        public void NotifyChanged(object? source = null) => Changed?.Invoke(source);

        public void Apply()
        {
            if (Rom != null) _commit(Rom.ToArray());
        }

        public void SaveAs(IWin32Window? owner)
        {
            if (Rom == null) return;
            using var dlg = new SaveFileDialog
            {
                Title = "Save edited ROM As",
                Filter = "GBA ROM (*.gba)|*.gba",
                InitialDirectory = Path != null ? System.IO.Path.GetDirectoryName(Path) : null,
                FileName = Path != null ? System.IO.Path.GetFileName(Path) : "",
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return;
            File.WriteAllBytes(dlg.FileName, Rom.ToArray());
        }
    }

    /// <summary>The "Apply to DBZKit" / "Save ROM As..." pair, shared by every editor tab.</summary>
    internal sealed class ApplyBar : FlowLayoutPanel
    {
        public ApplyBar(EditSession session)
        {
            Dock = DockStyle.Bottom;
            Height = 42;
            FlowDirection = FlowDirection.RightToLeft;
            Padding = new Padding(6);
            var use = new Button { Text = "Apply to DBZKit", AutoSize = true };
            use.Click += (_, _) => session.Apply();
            var save = new Button { Text = "Save ROM As...", AutoSize = true };
            save.Click += (_, _) => session.SaveAs(FindForm());
            Controls.AddRange([use, save]);
        }
    }
}
