using System.Text;
using ScintillaNET;
using static Legacy.SView_Decoder;

namespace Legacy
{
    public partial class ScriptVisualizer : Form
    {
        public ScriptVisualizer()
        {
            InitializeComponent();
            SVisualizerWB.EnsureCoreWebView2Async();
            ConfigureZenkaiEditor();

            // Default view: human-readable Zenkai source editor. "Show bytes instead"
            // (unchecked by default) swaps to the raw byte/opcode ListView for debugging.
            SVisualizerCB_ShowBytes.Checked = false;
            SVisualizerScriptTB.Visible = true;
            SVisualizerLV.Visible = false;
        }

        /// <summary>
        /// Wires up the ScintillaNET editor (fernandreu.ScintillaNET, already referenced in
        /// Legacy.csproj and used for the main Legacy_IDE control, but never actually given
        /// a lexer/styles anywhere in this codebase until now). There's no true Zenkai.g4-aware
        /// lexer here (that would mean a custom SCLEX_CONTAINER + StyleNeeded handler driven by
        /// the actual ANTLR token stream, e.g. ZenkaiLexer's tokens directly) - instead this
        /// uses Scintilla's built-in Cpp lexer, which is a reasonable-fit approximation for
        /// Zenkai's C-like call syntax (`Name(args);`, `label:`, `// comments`) since it already
        /// tokenizes identifiers/numbers/line-comments/labels correctly for that shape of text.
        /// Real opcode-aware keyword highlighting is wired via SetKeywords using the live
        /// OpcodeTable (all 145 names, including the new op_unk&lt;N&gt; ones) rather than a
        /// hardcoded list, so it can't silently drift out of sync with the assembler.
        /// </summary>
        private void ConfigureZenkaiEditor()
        {
            var sc = SVisualizerScriptTB;
            sc.Lexer = Lexer.Cpp;

            string keywords = string.Join(" ",
                Zenkai.OpcodeTable.ByIndex.Select(o => o.Name)
                    .Concat(new[] { "Jump", "JumpIfFalse", "LoopOrJump" })
                    .Distinct());
            sc.SetKeywords(0, keywords);

            sc.StyleResetDefault();
            sc.Styles[Style.Default].Font = "Consolas";
            sc.Styles[Style.Default].Size = 10;
            sc.StyleClearAll();

            sc.Styles[Style.Cpp.Default].ForeColor = Color.Black;
            sc.Styles[Style.Cpp.Identifier].ForeColor = Color.Black;
            sc.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
            sc.Styles[Style.Cpp.Word].Bold = true;
            sc.Styles[Style.Cpp.Number].ForeColor = Color.DarkRed;
            sc.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;
            sc.Styles[Style.Cpp.CommentLine].Italic = true;
            sc.Styles[Style.Cpp.Operator].ForeColor = Color.DimGray;

            sc.Margins[0].Width = 32; // line numbers - useful alongside the disassembler's auto-generated L0/L1/... labels
        }

        private void SVisualizerBTN_ClearInput_Click(object sender, EventArgs e)
        {
            SVisualizerTB.Clear();
        }

        private void SVisualizerBTN_DecipherInput_Click(object sender, EventArgs e)
        {
            SVisualizerLV.Items.Clear();

            if (!SView_Tools.TryParseHex(SVisualizerTB.Text, out byte[]? bytes) || bytes == null)
            {
                MessageBox.Show("Invalid hex input.");
                return;
            }

            try
            {
                var instructions = SView_Decoder.Decode(bytes);

                RenderInstructions(instructions);
                RenderMermaid(BuildMermaid(instructions));
            }
            catch
            {
                MessageBox.Show("Error in Parsing Instructions, Missing Data?");
                return;
            }

            // Also populate the human-readable Zenkai source editor from the same bytes,
            // via the ANTLR-based disassembler (Zenkai/ZenkaiDisassembler.cs) - this is
            // independent of SView_Decoder above (kept for the ListView/mermaid view) and
            // can legitimately disagree with it for opcodes the old hand-rolled decoder
            // didn't handle the same way; surface disassembly failures rather than silently
            // leaving stale text in the editor.
            try
            {
                SVisualizerScriptTB.Text = Zenkai.ZenkaiDisassembler.Disassemble(bytes);
                SetCompileStatus("", false);
            }
            catch (Exception ex)
            {
                SVisualizerScriptTB.Text = "";
                SetCompileStatus("Disassemble error: " + ex.Message, true);
            }
        }

        private void SVisualizerBTN_Compile_Click(object sender, EventArgs e)
        {
            byte[] bytes;
            try
            {
                bytes = Zenkai.ZenkaiAssembler.Assemble(SVisualizerScriptTB.Text);
            }
            catch (Zenkai.ZenkaiAssemblerException ex)
            {
                SetCompileStatus(ex.Message, true);
                return;
            }
            catch (Exception ex)
            {
                SetCompileStatus("Unexpected error: " + ex.Message, true);
                return;
            }

            // Compiled OK - reflect the (possibly edited) bytes back into the hex box and
            // the bytes-debug view (ListView + mermaid) so "Show bytes instead" shows what
            // was actually just compiled, not stale data from the last Decode click.
            SVisualizerTB.Text = string.Join(", ", bytes.Select(b => "0x" + b.ToString("X2")));
            SetCompileStatus($"Compiled OK - {bytes.Length} bytes.", false);

            try
            {
                var instructions = SView_Decoder.Decode(bytes);
                RenderInstructions(instructions);
                RenderMermaid(BuildMermaid(instructions));
            }
            catch
            {
                // Non-fatal for compile itself - the bytes-debug view just won't refresh.
            }
        }

        private void SVisualizerCB_ShowBytes_CheckedChanged(object sender, EventArgs e)
        {
            bool showBytes = SVisualizerCB_ShowBytes.Checked;
            SVisualizerLV.Visible = showBytes;
            SVisualizerScriptTB.Visible = !showBytes;
        }

        private void SetCompileStatus(string message, bool isError)
        {
            SVisualizerLBL_CompileStatus.Text = message;
            SVisualizerLBL_CompileStatus.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
        }

        private void RenderInstructions(List<Instruction> instructions)
        {
            SVisualizerLV.BeginUpdate();
            SVisualizerLV.Items.Clear();

            foreach (var ins in instructions)
            {
                var item = new ListViewItem(ins.Offset.ToString("X4"));

                item.SubItems.Add(ins.Name);
                item.SubItems.Add(ins.Args != null && ins.Args.Count > 0 ? string.Join(", ", ins.Args) : "");

                SVisualizerLV.Items.Add(item);
            }

            SVisualizerLV.EndUpdate();
        }

        private string BuildMermaid(List<Instruction> instructions)
        {
            var sb = new StringBuilder();

            sb.AppendLine("graph TD");

            for (int i = 0; i < instructions.Count; i++)
            {
                var ins = instructions[i];

                string nodeId = $"N{i}";
                string label = $"{ins.Name}";

                if (ins.Args != null && ins.Args.Count > 0)
                    label += $"\\n[{string.Join(", ", ins.Args)}]";

                sb.AppendLine($"{nodeId}[\"{label}\"]");
            }

            for (int i = 0; i < instructions.Count - 1; i++)
            {
                sb.AppendLine($"N{i} --> N{i + 1}");
            }

            return sb.ToString();
        }

        private async void RenderMermaid(string mermaid)
        {
            string html = $@"<html><head><script type='module'>import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs'; mermaid.initialize({{ startOnLoad: true }}); </script></head><body><div class='mermaid'>{mermaid}</div></body></html>";
            await SVisualizerWB.EnsureCoreWebView2Async();
            SVisualizerWB.NavigateToString(html);
        }
    }
}