using System.Text;

namespace Legacy
{
    public partial class StringDecompressor : Form
    {
        public StringDecompressor()
        {
            InitializeComponent();
        }

        private void StringViewBTN_Clear_Click(object sender, EventArgs e)
        {
            StringViewTB_InputRaw.Clear();
        }

        private void StringViewBTN_Decompress_Click(object sender, EventArgs e)
        {
            if (!SView_Tools.TryParseHex(StringViewTB_InputRaw.Text, out byte[]? data, false) || data == null)
            {
                MessageBox.Show("Invalid hex input");
                return;
            }
            try
            {
                var decompressed = Jcalg1Decompress.Decompress(data).Data;

                for (int i = 0; i < decompressed.Length; i++)
                {
                    if (decompressed[i] == 0x0B) decompressed[i] = (byte)'\n';
                }

                var text =

                StringViewTB_Output.Text = Encoding.ASCII.GetString(decompressed).Replace("\n", "\r\n");
            }
            catch (Exception)
            {
                MessageBox.Show("Error in Decompression, are you missing bytes?");
                return;
            }
        }
    }
}