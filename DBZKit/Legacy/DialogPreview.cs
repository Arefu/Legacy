using DBZKit;
using DrGero.Engine;
using DrGero.IO;

namespace Legacy
{
    /// <summary>Vertical box position chosen by a message's leading '!' / '@' / '#' (Dialog_CreateTextBox, 0x800B1E2).</summary>
    internal enum BoxPosition { Auto, Top, Middle, Bottom }

    /// <summary>
    /// How a conversation line will look in-game: which portrait a speaker id shows, where the box sits and how wide it is.
    /// Every rule here comes from IDA's Dialog_CreateTextBox / Character_GetPortraitResource decompiles (2026-09-20):
    ///  - speaker id 0 = narrator: no portrait, 160 px box, default at the bottom (Y 120);
    ///  - any other id = 224 px box (160 + a 64 px portrait), default top (Y 40) or bottom depending on where the player stands;
    ///  - '!' forces Y 40, '@' Y 80, '#' Y 120, then '^' centres the text;
    ///  - id 1 = the ACTIVE character, ids 2-7 = party slots 0-5 (their portraits come from the live party), ids 8+ index Portrait_Table.
    /// The box HEIGHT and which side the portrait sits on are not verified, so the mock screen marks those as approximate.
    /// </summary>
    internal static class DialogPreview
    {
        private const uint PortraitTable = 0x083EC9D4;
        private const int PortraitEntries = 115;
        private const uint PalettePtr = 0x081DA6C8;

        public static string PositionPrefix(BoxPosition p) => p switch { BoxPosition.Top => "!", BoxPosition.Middle => "@", BoxPosition.Bottom => "#", _ => "" };

        /// <summary>Splits the leading position character and '^' off a message (the engine reads them in this order).</summary>
        public static (string Text, BoxPosition Position, bool Center) ExtractFormat(string raw)
        {
            var position = BoxPosition.Auto;
            if (raw.Length > 0)
            {
                switch (raw[0])
                {
                    case '!': position = BoxPosition.Top; raw = raw[1..]; break;
                    case '@': position = BoxPosition.Middle; raw = raw[1..]; break;
                    case '#': position = BoxPosition.Bottom; raw = raw[1..]; break;
                }
            }
            bool center = raw.Length > 0 && raw[0] == '^';
            if (center) raw = raw[1..];
            return (raw, position, center);
        }

        public static string ApplyFormat(string text, BoxPosition position, bool center) => PositionPrefix(position) + (center ? "^" : "") + text;

        public static int BoxWidth(byte speaker) => speaker == 0 ? 160 : 224;

        /// <summary>Y of the box's top edge in GBA pixels, or null for "Auto" (top or bottom depending on the player's position).</summary>
        public static int? BoxY(byte speaker, BoxPosition p) => p switch
        {
            BoxPosition.Top => 40,
            BoxPosition.Middle => 80,
            BoxPosition.Bottom => 120,
            _ => speaker == 0 ? 120 : null,
        };

        // Speaker ids that get the "style 3" box (Dialog_CreateTextBox's special-case list).
        private static readonly HashSet<byte> Style3 = [26, 28, 29, 30, 31, 32, 38, 39, 40, 49, 50, 53, 70, 84, 86, 96];
        public static int BoxStyle(byte speaker) => speaker == 0 ? 0 : Style3.Contains(speaker) ? 3 : speaker <= 7 ? 1 : 2;

        /// <summary>The portrait a speaker id shows, plus a one-line explanation of where it came from.</summary>
        public static Bitmap? PortraitFor(byte[] rom, byte speaker, out string description)
        {
            if (speaker == 0) { description = "Narrator: no portrait."; return null; }

            int index = speaker;
            if (speaker <= 7)
            {
                // Party speakers: the game asks the live party; the default party (new game) is shown here.
                int slot = speaker == 1 ? 0 : speaker - 2;
                try
                {
                    var view = ROM.FromBytes(rom);
                    var party = RosterTables.ReadDefaultParty(view);
                    var rows = RosterTables.ReadDisplayRows(view);
                    index = rows[party[slot].DisplayIndex].PortraitIndex;
                    description = speaker == 1
                        ? $"The ACTIVE character (whoever the player is controlling). Showing party slot 0's default portrait ({index})."
                        : $"Party slot {slot} (the character in that slot). Showing the new-game one: portrait {index}.";
                }
                catch { description = "Party speaker (portrait unavailable)."; return null; }
            }
            else description = $"Portrait_Table[{speaker}]";

            return Render(rom, index);
        }

        public static Bitmap? Render(byte[] rom, int index)
        {
            if (index < 0 || index >= PortraitEntries) return null;
            try
            {
                uint pointer = GBA.ReadUInt32(rom, PortraitTable + (uint)(index * 4));
                if (pointer == 0) return null;
                int deflatedSize = GBA.ReadInt32(rom, pointer + 4);
                int dataOffset = GBA.ToOffset(pointer) + 8;
                var palette = GBA.ReadPalette(rom, PalettePtr);
                var result = Jcalg1Decompress.Decompress(rom, dataOffset, deflatedSize);
                return GBA.Render8bpp(result.Data, 64, 64, palette);
            }
            catch { return null; }
        }
    }

    /// <summary>A 240x160 GBA screen (drawn at 2x) with the dialogue box where the engine will put it.</summary>
    internal sealed class DialogPreviewControl : Control
    {
        private const int Scale = 2;
        private byte _speaker;
        private BoxPosition _position;
        private bool _center;
        private string _text = "";
        private Image? _portrait;

        public DialogPreviewControl()
        {
            DoubleBuffered = true;
            Size = new Size(240 * Scale, 160 * Scale);
            BackColor = Color.Black;
        }

        public void SetContent(byte speaker, BoxPosition position, bool center, string text, Image? portrait)
        {
            _speaker = speaker; _position = position; _center = center; _text = text; _portrait = portrait;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(24, 40, 24)); // stand-in for the game screen
            using (var grid = new Pen(Color.FromArgb(40, 255, 255, 255)))
                for (int y = 40; y < 160; y += 40) g.DrawLine(grid, 0, y * Scale, 240 * Scale, y * Scale);
            using var faint = new Font("Segoe UI", 7f);
            for (int y = 40; y <= 120; y += 40) g.DrawString($"Y {y}", faint, Brushes.DimGray, 2, y * Scale - 12);
            g.DrawString("box Y and width are from the engine; box height and portrait placement are approximate", faint, Brushes.DimGray, 2, 160 * Scale - 14);

            int width = DialogPreview.BoxWidth(_speaker);
            int? fixedY = DialogPreview.BoxY(_speaker, _position);
            DrawBox(g, width, fixedY ?? 40, faded: fixedY == null && _speaker != 0, label: fixedY == null ? "Auto: top (40)" : null);
            if (fixedY == null) DrawBox(g, width, 120, faded: true, label: "...or bottom (120), if the player is on the top side", ghostOnly: true);
        }

        private void DrawBox(Graphics g, int width, int y, bool faded, string? label, bool ghostOnly = false)
        {
            const int height = 36; // approximate -- the real height isn't verified
            int x = (240 - width) / 2;
            var box = new Rectangle(x * Scale, y * Scale, width * Scale, height * Scale);
            int alpha = ghostOnly ? 70 : faded ? 210 : 255;

            using (var fill = new SolidBrush(Color.FromArgb(alpha, 16, 24, 88))) g.FillRectangle(fill, box);
            using (var border = new Pen(Color.FromArgb(alpha, 240, 240, 240), 2)) g.DrawRectangle(border, box);

            if (!ghostOnly)
            {
                int textLeft = box.Left + 6;
                int textWidth = box.Width - 12;
                if (_speaker != 0 && _portrait != null)
                {
                    // 64x64 portrait inside the 224 px box; the side and exact anchor are approximate.
                    var portrait = new Rectangle(box.Left + 4, box.Bottom - 64 * Scale + 4, 64 * Scale - 8, 64 * Scale - 8);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(_portrait, portrait);
                    textLeft = portrait.Right + 6;
                    textWidth = box.Right - textLeft - 6;
                }
                else if (_speaker != 0) textLeft += 64 * Scale; // no portrait bitmap, but keep the text area where it would be

                using var font = new Font("Consolas", 8f * Scale * 0.62f, FontStyle.Bold);
                var format = new StringFormat { Alignment = _center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Near };
                g.DrawString(_text, font, Brushes.White, new RectangleF(textLeft, box.Top + 4, textWidth, box.Height - 6), format);
            }

            if (label != null)
            {
                using var f = new Font("Segoe UI", 7.5f, FontStyle.Italic);
                g.DrawString(label, f, Brushes.LightGoldenrodYellow, box.Left + 4, box.Bottom + 2);
            }
        }
    }
}
