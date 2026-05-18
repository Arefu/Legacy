
namespace Legacy
{
    public partial class Legacy : Form
    {
        public Legacy()
        {
            InitializeComponent();
        }

        private void Legacy_OpenROM_Click(object sender, EventArgs e)
        {
            //TODO: Hmm, how to work out which game it is?
        }

        private void Legacy_SaveROM_Click(object sender, EventArgs e)
        {
            //TODO: DUMP ALL THE BYTES
        }

        private void Legacy_QuitEditor_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
