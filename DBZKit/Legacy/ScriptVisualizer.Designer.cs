namespace Legacy
{
    partial class ScriptVisualizer
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
            SVisualizerTB = new TextBox();
            SVisualizerBTN_DecipherInput = new Button();
            SVisualizerBTN_ClearInput = new Button();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            SVisualizerLV = new ListView();
            OffsetHeader = new ColumnHeader();
            OpCodeHeader = new ColumnHeader();
            ArgumentsHeader = new ColumnHeader();
            groupBox3 = new GroupBox();
            SVisualizerWB = new Microsoft.Web.WebView2.WinForms.WebView2();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)SVisualizerWB).BeginInit();
            SuspendLayout();
            // 
            // SVisualizerTB
            // 
            SVisualizerTB.Location = new Point(6, 22);
            SVisualizerTB.Multiline = true;
            SVisualizerTB.Name = "SVisualizerTB";
            SVisualizerTB.Size = new Size(516, 128);
            SVisualizerTB.TabIndex = 2;
            // 
            // SVisualizerBTN_DecipherInput
            // 
            SVisualizerBTN_DecipherInput.Location = new Point(6, 156);
            SVisualizerBTN_DecipherInput.Name = "SVisualizerBTN_DecipherInput";
            SVisualizerBTN_DecipherInput.Size = new Size(125, 30);
            SVisualizerBTN_DecipherInput.TabIndex = 3;
            SVisualizerBTN_DecipherInput.Text = "Decode Input";
            SVisualizerBTN_DecipherInput.UseVisualStyleBackColor = true;
            SVisualizerBTN_DecipherInput.Click += SVisualizerBTN_DecipherInput_Click;
            // 
            // SVisualizerBTN_ClearInput
            // 
            SVisualizerBTN_ClearInput.Location = new Point(397, 156);
            SVisualizerBTN_ClearInput.Name = "SVisualizerBTN_ClearInput";
            SVisualizerBTN_ClearInput.Size = new Size(125, 30);
            SVisualizerBTN_ClearInput.TabIndex = 4;
            SVisualizerBTN_ClearInput.Text = "Clear Input";
            SVisualizerBTN_ClearInput.UseVisualStyleBackColor = true;
            SVisualizerBTN_ClearInput.Click += SVisualizerBTN_ClearInput_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(SVisualizerBTN_DecipherInput);
            groupBox1.Controls.Add(SVisualizerTB);
            groupBox1.Controls.Add(SVisualizerBTN_ClearInput);
            groupBox1.Location = new Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(528, 195);
            groupBox1.TabIndex = 7;
            groupBox1.TabStop = false;
            groupBox1.Text = "Hex Input";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(SVisualizerLV);
            groupBox2.Location = new Point(12, 213);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(528, 456);
            groupBox2.TabIndex = 8;
            groupBox2.TabStop = false;
            groupBox2.Text = "Script Logic";
            // 
            // SVisualizerLV
            // 
            SVisualizerLV.Columns.AddRange(new ColumnHeader[] { OffsetHeader, OpCodeHeader, ArgumentsHeader });
            SVisualizerLV.FullRowSelect = true;
            SVisualizerLV.GridLines = true;
            SVisualizerLV.Location = new Point(6, 22);
            SVisualizerLV.Name = "SVisualizerLV";
            SVisualizerLV.Size = new Size(516, 428);
            SVisualizerLV.TabIndex = 0;
            SVisualizerLV.UseCompatibleStateImageBehavior = false;
            SVisualizerLV.View = View.Details;
            // 
            // OffsetHeader
            // 
            OffsetHeader.Text = "Offset";
            // 
            // OpCodeHeader
            // 
            OpCodeHeader.Text = "OP Code";
            OpCodeHeader.Width = 260;
            // 
            // ArgumentsHeader
            // 
            ArgumentsHeader.Text = "Arguments";
            ArgumentsHeader.Width = 180;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(SVisualizerWB);
            groupBox3.Location = new Point(546, 12);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(706, 657);
            groupBox3.TabIndex = 9;
            groupBox3.TabStop = false;
            groupBox3.Text = "Diagram Viewer";
            // 
            // SVisualizerWB
            // 
            SVisualizerWB.AllowExternalDrop = true;
            SVisualizerWB.CreationProperties = null;
            SVisualizerWB.DefaultBackgroundColor = Color.White;
            SVisualizerWB.Dock = DockStyle.Fill;
            SVisualizerWB.Location = new Point(3, 19);
            SVisualizerWB.Name = "SVisualizerWB";
            SVisualizerWB.Size = new Size(700, 635);
            SVisualizerWB.TabIndex = 0;
            SVisualizerWB.ZoomFactor = 1D;
            // 
            // ScriptVisualizer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(groupBox3);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "ScriptVisualizer";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Script Visualizer";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)SVisualizerWB).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TextBox SVisualizerTB;
        private Button SVisualizerBTN_DecipherInput;
        private Button SVisualizerBTN_ClearInput;
        private GroupBox groupBox1;
        private GroupBox groupBox2;
        private GroupBox groupBox3;
        private ListView SVisualizerLV;
        private ColumnHeader OffsetHeader;
        private ColumnHeader OpCodeHeader;
        private ColumnHeader ArgumentsHeader;
        private Microsoft.Web.WebView2.WinForms.WebView2 SVisualizerWB;
    }
}