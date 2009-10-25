namespace HarmonyGen2
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナ変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナで生成されたコード

        /// <summary>
        /// デザイナ サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディタで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.radioBass3 = new System.Windows.Forms.RadioButton();
            this.radioBass2 = new System.Windows.Forms.RadioButton();
            this.radioBass1 = new System.Windows.Forms.RadioButton();
            this.radioBass0 = new System.Windows.Forms.RadioButton();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.radioType7 = new System.Windows.Forms.RadioButton();
            this.radioType3 = new System.Windows.Forms.RadioButton();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.radioNumber7 = new System.Windows.Forms.RadioButton();
            this.radioNumber6 = new System.Windows.Forms.RadioButton();
            this.radioNumber5 = new System.Windows.Forms.RadioButton();
            this.radioNumber4 = new System.Windows.Forms.RadioButton();
            this.radioNumber3 = new System.Windows.Forms.RadioButton();
            this.radioNumber2 = new System.Windows.Forms.RadioButton();
            this.radioNumber1 = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioButton4 = new System.Windows.Forms.RadioButton();
            this.radioButton3 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.menuStrip1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(929, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(51, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(106, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exit_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.splitContainer1.Panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.splitContainer1_Panel1_Paint);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBox6);
            this.splitContainer1.Panel2.Controls.Add(this.groupBox5);
            this.splitContainer1.Size = new System.Drawing.Size(929, 663);
            this.splitContainer1.SplitterDistance = 382;
            this.splitContainer1.TabIndex = 2;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.radioBass3);
            this.groupBox4.Controls.Add(this.radioBass2);
            this.groupBox4.Controls.Add(this.radioBass1);
            this.groupBox4.Controls.Add(this.radioBass0);
            this.groupBox4.Location = new System.Drawing.Point(246, 21);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(146, 134);
            this.groupBox4.TabIndex = 6;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "低音位";
            // 
            // radioBass3
            // 
            this.radioBass3.AutoSize = true;
            this.radioBass3.Enabled = false;
            this.radioBass3.Location = new System.Drawing.Point(6, 96);
            this.radioBass3.Name = "radioBass3";
            this.radioBass3.Size = new System.Drawing.Size(128, 19);
            this.radioBass3.TabIndex = 3;
            this.radioBass3.Text = "第3転回位置(&H)";
            this.radioBass3.UseVisualStyleBackColor = true;
            // 
            // radioBass2
            // 
            this.radioBass2.AutoSize = true;
            this.radioBass2.Location = new System.Drawing.Point(6, 71);
            this.radioBass2.Name = "radioBass2";
            this.radioBass2.Size = new System.Drawing.Size(128, 19);
            this.radioBass2.TabIndex = 2;
            this.radioBass2.Text = "第2転回位置(&G)";
            this.radioBass2.UseVisualStyleBackColor = true;
            // 
            // radioBass1
            // 
            this.radioBass1.AutoSize = true;
            this.radioBass1.Location = new System.Drawing.Point(6, 46);
            this.radioBass1.Name = "radioBass1";
            this.radioBass1.Size = new System.Drawing.Size(126, 19);
            this.radioBass1.TabIndex = 1;
            this.radioBass1.Text = "第1転回位置(&F)";
            this.radioBass1.UseVisualStyleBackColor = true;
            // 
            // radioBass0
            // 
            this.radioBass0.AutoSize = true;
            this.radioBass0.Checked = true;
            this.radioBass0.Location = new System.Drawing.Point(6, 21);
            this.radioBass0.Name = "radioBass0";
            this.radioBass0.Size = new System.Drawing.Size(103, 19);
            this.radioBass0.TabIndex = 0;
            this.radioBass0.TabStop = true;
            this.radioBass0.Text = "基本位置(&E)";
            this.radioBass0.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.radioType7);
            this.groupBox3.Controls.Add(this.radioType3);
            this.groupBox3.Location = new System.Drawing.Point(112, 21);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(128, 96);
            this.groupBox3.TabIndex = 5;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "和音の形体";
            // 
            // radioType7
            // 
            this.radioType7.AutoSize = true;
            this.radioType7.Location = new System.Drawing.Point(7, 46);
            this.radioType7.Name = "radioType7";
            this.radioType7.Size = new System.Drawing.Size(95, 19);
            this.radioType7.TabIndex = 1;
            this.radioType7.Text = "7の和音(&D)";
            this.radioType7.UseVisualStyleBackColor = true;
            this.radioType7.CheckedChanged += new System.EventHandler(this.radioType7_CheckedChanged);
            // 
            // radioType3
            // 
            this.radioType3.AutoSize = true;
            this.radioType3.Checked = true;
            this.radioType3.Location = new System.Drawing.Point(7, 21);
            this.radioType3.Name = "radioType3";
            this.radioType3.Size = new System.Drawing.Size(95, 19);
            this.radioType3.TabIndex = 0;
            this.radioType3.TabStop = true;
            this.radioType3.Text = "3の和音(&C)";
            this.radioType3.UseVisualStyleBackColor = true;
            this.radioType3.CheckedChanged += new System.EventHandler(this.radioType3_CheckedChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.radioNumber7);
            this.groupBox2.Controls.Add(this.radioNumber6);
            this.groupBox2.Controls.Add(this.radioNumber5);
            this.groupBox2.Controls.Add(this.radioNumber4);
            this.groupBox2.Controls.Add(this.radioNumber3);
            this.groupBox2.Controls.Add(this.radioNumber2);
            this.groupBox2.Controls.Add(this.radioNumber1);
            this.groupBox2.Location = new System.Drawing.Point(6, 21);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(100, 205);
            this.groupBox2.TabIndex = 4;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "和音の度数";
            // 
            // radioNumber7
            // 
            this.radioNumber7.AutoSize = true;
            this.radioNumber7.Location = new System.Drawing.Point(6, 171);
            this.radioNumber7.Name = "radioNumber7";
            this.radioNumber7.Size = new System.Drawing.Size(60, 19);
            this.radioNumber7.TabIndex = 6;
            this.radioNumber7.Text = "VII(&7)";
            this.radioNumber7.UseVisualStyleBackColor = true;
            // 
            // radioNumber6
            // 
            this.radioNumber6.AutoSize = true;
            this.radioNumber6.Location = new System.Drawing.Point(6, 146);
            this.radioNumber6.Name = "radioNumber6";
            this.radioNumber6.Size = new System.Drawing.Size(56, 19);
            this.radioNumber6.TabIndex = 5;
            this.radioNumber6.Text = "VI(&6)";
            this.radioNumber6.UseVisualStyleBackColor = true;
            // 
            // radioNumber5
            // 
            this.radioNumber5.AutoSize = true;
            this.radioNumber5.Location = new System.Drawing.Point(6, 121);
            this.radioNumber5.Name = "radioNumber5";
            this.radioNumber5.Size = new System.Drawing.Size(52, 19);
            this.radioNumber5.TabIndex = 4;
            this.radioNumber5.Text = "V(&5)";
            this.radioNumber5.UseVisualStyleBackColor = true;
            // 
            // radioNumber4
            // 
            this.radioNumber4.AutoSize = true;
            this.radioNumber4.Location = new System.Drawing.Point(6, 96);
            this.radioNumber4.Name = "radioNumber4";
            this.radioNumber4.Size = new System.Drawing.Size(56, 19);
            this.radioNumber4.TabIndex = 3;
            this.radioNumber4.Text = "IV(&4)";
            this.radioNumber4.UseVisualStyleBackColor = true;
            // 
            // radioNumber3
            // 
            this.radioNumber3.AutoSize = true;
            this.radioNumber3.Location = new System.Drawing.Point(6, 71);
            this.radioNumber3.Name = "radioNumber3";
            this.radioNumber3.Size = new System.Drawing.Size(60, 19);
            this.radioNumber3.TabIndex = 2;
            this.radioNumber3.Text = "III (&3)";
            this.radioNumber3.UseVisualStyleBackColor = true;
            // 
            // radioNumber2
            // 
            this.radioNumber2.AutoSize = true;
            this.radioNumber2.Location = new System.Drawing.Point(6, 46);
            this.radioNumber2.Name = "radioNumber2";
            this.radioNumber2.Size = new System.Drawing.Size(56, 19);
            this.radioNumber2.TabIndex = 1;
            this.radioNumber2.Text = "II (&2)";
            this.radioNumber2.UseVisualStyleBackColor = true;
            // 
            // radioNumber1
            // 
            this.radioNumber1.AutoSize = true;
            this.radioNumber1.Checked = true;
            this.radioNumber1.Location = new System.Drawing.Point(6, 21);
            this.radioNumber1.Name = "radioNumber1";
            this.radioNumber1.Size = new System.Drawing.Size(52, 19);
            this.radioNumber1.TabIndex = 0;
            this.radioNumber1.TabStop = true;
            this.radioNumber1.Text = "I (&1)";
            this.radioNumber1.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButton4);
            this.groupBox1.Controls.Add(this.radioButton3);
            this.groupBox1.Controls.Add(this.radioButton2);
            this.groupBox1.Controls.Add(this.radioButton1);
            this.groupBox1.Location = new System.Drawing.Point(6, 21);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(178, 134);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "パート";
            // 
            // radioButton4
            // 
            this.radioButton4.AutoSize = true;
            this.radioButton4.Checked = true;
            this.radioButton4.Location = new System.Drawing.Point(6, 96);
            this.radioButton4.Name = "radioButton4";
            this.radioButton4.Size = new System.Drawing.Size(56, 19);
            this.radioButton4.TabIndex = 3;
            this.radioButton4.TabStop = true;
            this.radioButton4.Text = "&Bass";
            this.radioButton4.UseVisualStyleBackColor = true;
            // 
            // radioButton3
            // 
            this.radioButton3.AutoSize = true;
            this.radioButton3.Location = new System.Drawing.Point(6, 71);
            this.radioButton3.Name = "radioButton3";
            this.radioButton3.Size = new System.Drawing.Size(63, 19);
            this.radioButton3.TabIndex = 2;
            this.radioButton3.Text = "&Tenor";
            this.radioButton3.UseVisualStyleBackColor = true;
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Location = new System.Drawing.Point(6, 46);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(50, 19);
            this.radioButton2.TabIndex = 1;
            this.radioButton2.Text = "&Alto";
            this.radioButton2.UseVisualStyleBackColor = true;
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.Location = new System.Drawing.Point(6, 21);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(77, 19);
            this.radioButton1.TabIndex = 0;
            this.radioButton1.Text = "&Soprano";
            this.radioButton1.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.groupBox1);
            this.groupBox5.Location = new System.Drawing.Point(551, 3);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(366, 262);
            this.groupBox5.TabIndex = 7;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "選択編集";
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.groupBox4);
            this.groupBox6.Controls.Add(this.groupBox3);
            this.groupBox6.Controls.Add(this.groupBox2);
            this.groupBox6.Location = new System.Drawing.Point(12, 3);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(468, 246);
            this.groupBox6.TabIndex = 8;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "和音の設定";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(929, 687);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "Form1";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton4;
        private System.Windows.Forms.RadioButton radioButton3;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.RadioButton radioNumber4;
        private System.Windows.Forms.RadioButton radioNumber3;
        private System.Windows.Forms.RadioButton radioNumber2;
        private System.Windows.Forms.RadioButton radioNumber1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.RadioButton radioType3;
        private System.Windows.Forms.RadioButton radioNumber7;
        private System.Windows.Forms.RadioButton radioNumber6;
        private System.Windows.Forms.RadioButton radioNumber5;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.RadioButton radioType7;
        private System.Windows.Forms.RadioButton radioBass3;
        private System.Windows.Forms.RadioButton radioBass2;
        private System.Windows.Forms.RadioButton radioBass1;
        private System.Windows.Forms.RadioButton radioBass0;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.GroupBox groupBox5;
    }
}

