using System.Text;
using static Legacy.SView_Decoder;

namespace Legacy
{
    public partial class ScriptVisualizer : Form
    {
        public ScriptVisualizer()
        {
            InitializeComponent();
            SVisualizerWB.EnsureCoreWebView2Async();
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