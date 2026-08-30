namespace Legacy
{
    partial class StringDecompressor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            StringViewTB_InputRaw = new TextBox();
            groupBox1 = new GroupBox();
            StringViewBTN_Clear = new Button();
            StringViewBTN_Decompress = new Button();
            StringViewTB_Output = new TextBox();
            groupBox2 = new GroupBox();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // StringViewTB_InputRaw
            // 
            StringViewTB_InputRaw.Location = new Point(6, 22);
            StringViewTB_InputRaw.Multiline = true;
            StringViewTB_InputRaw.Name = "StringViewTB_InputRaw";
            StringViewTB_InputRaw.Size = new Size(588, 82);
            StringViewTB_InputRaw.TabIndex = 0;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(StringViewBTN_Clear);
            groupBox1.Controls.Add(StringViewBTN_Decompress);
            groupBox1.Controls.Add(StringViewTB_InputRaw);
            groupBox1.Location = new Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(600, 146);
            groupBox1.TabIndex = 1;
            groupBox1.TabStop = false;
            groupBox1.Text = "Hex Input";
            // 
            // StringViewBTN_Clear
            // 
            StringViewBTN_Clear.Location = new Point(469, 110);
            StringViewBTN_Clear.Name = "StringViewBTN_Clear";
            StringViewBTN_Clear.Size = new Size(125, 30);
            StringViewBTN_Clear.TabIndex = 2;
            StringViewBTN_Clear.Text = "Clear Input";
            StringViewBTN_Clear.UseVisualStyleBackColor = true;
            StringViewBTN_Clear.Click += StringViewBTN_Clear_Click;
            // 
            // StringViewBTN_Decompress
            // 
            StringViewBTN_Decompress.Location = new Point(6, 110);
            StringViewBTN_Decompress.Name = "StringViewBTN_Decompress";
            StringViewBTN_Decompress.Size = new Size(125, 30);
            StringViewBTN_Decompress.TabIndex = 1;
            StringViewBTN_Decompress.Text = "Decompress String";
            StringViewBTN_Decompress.UseVisualStyleBackColor = true;
            StringViewBTN_Decompress.Click += StringViewBTN_Decompress_Click;
            // 
            // StringViewTB_Output
            // 
            StringViewTB_Output.Location = new Point(6, 22);
            StringViewTB_Output.Multiline = true;
            StringViewTB_Output.Name = "StringViewTB_Output";
            StringViewTB_Output.ReadOnly = true;
            StringViewTB_Output.Size = new Size(576, 225);
            StringViewTB_Output.TabIndex = 2;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(StringViewTB_Output);
            groupBox2.Location = new Point(18, 176);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(588, 253);
            groupBox2.TabIndex = 3;
            groupBox2.TabStop = false;
            groupBox2.Text = "Output";
            // 
            // StringDecompressor
            // 
            ClientSize = new Size(624, 441);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "StringDecompressor";
            StartPosition = FormStartPosition.CenterParent;
            Text = "String Decompressor";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion


        private TextBox StringViewTB_InputRaw;
        private GroupBox groupBox1;
        private Button StringViewBTN_Clear;
        private Button StringViewBTN_Decompress;
        private TextBox StringViewTB_Output;
        private GroupBox groupBox2;
    }
}