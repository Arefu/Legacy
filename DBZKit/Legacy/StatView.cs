namespace Legacy
{
    public partial class StatView : Form
    {
        byte[]? _rom;
        public StatView(byte[]? rom)
        {
            InitializeComponent();
            _rom = rom;
        }


    }
}
