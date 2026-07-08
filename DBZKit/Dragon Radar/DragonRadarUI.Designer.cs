namespace Dragon_Radar
{
    partial class DragonRadarUI
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            button1 = new Button();
            mapTreeView = new TreeView();
            mapPictureBox = new PictureBox();
            listView1 = new ListView();
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).BeginInit();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(326, 646);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 0;
            button1.Text = "button1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // mapTreeView
            // 
            mapTreeView.Dock = DockStyle.Left;
            mapTreeView.Location = new Point(0, 0);
            mapTreeView.Name = "mapTreeView";
            mapTreeView.Size = new Size(320, 681);
            mapTreeView.TabIndex = 1;
            mapTreeView.AfterSelect += mapTreeView_AfterSelect;
            // 
            // mapPictureBox
            // 
            mapPictureBox.BackColor = Color.Black;
            mapPictureBox.Location = new Point(326, 12);
            mapPictureBox.Name = "mapPictureBox";
            mapPictureBox.Size = new Size(505, 542);
            mapPictureBox.TabIndex = 3;
            mapPictureBox.TabStop = false;
            // 
            // listView1
            // 
            listView1.Location = new Point(837, 12);
            listView1.Name = "listView1";
            listView1.Size = new Size(415, 542);
            listView1.TabIndex = 4;
            listView1.UseCompatibleStateImageBehavior = false;
            // 
            // DragonRadarUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(listView1);
            Controls.Add(mapPictureBox);
            Controls.Add(mapTreeView);
            Controls.Add(button1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "DragonRadarUI";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)mapPictureBox).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Button button1;
        private TreeView mapTreeView;
        private PictureBox mapPictureBox;
        private ListView listView1;
    }
}
