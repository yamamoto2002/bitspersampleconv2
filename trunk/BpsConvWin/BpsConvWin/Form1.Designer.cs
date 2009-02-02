namespace BpsConvWin
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.buttonReadFile = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textBoxReadFilePath = new System.Windows.Forms.TextBox();
            this.textBoxOutput = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonConvStart = new System.Windows.Forms.Button();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonReadFile
            // 
            this.buttonReadFile.AccessibleDescription = null;
            this.buttonReadFile.AccessibleName = null;
            resources.ApplyResources(this.buttonReadFile, "buttonReadFile");
            this.buttonReadFile.BackgroundImage = null;
            this.buttonReadFile.Font = null;
            this.buttonReadFile.Name = "buttonReadFile";
            this.buttonReadFile.UseVisualStyleBackColor = true;
            this.buttonReadFile.Click += new System.EventHandler(this.buttonReadFile_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.AccessibleDescription = null;
            this.groupBox1.AccessibleName = null;
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.BackgroundImage = null;
            this.groupBox1.Controls.Add(this.textBoxReadFilePath);
            this.groupBox1.Controls.Add(this.buttonReadFile);
            this.groupBox1.Font = null;
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // textBoxReadFilePath
            // 
            this.textBoxReadFilePath.AccessibleDescription = null;
            this.textBoxReadFilePath.AccessibleName = null;
            resources.ApplyResources(this.textBoxReadFilePath, "textBoxReadFilePath");
            this.textBoxReadFilePath.BackgroundImage = null;
            this.textBoxReadFilePath.Font = null;
            this.textBoxReadFilePath.Name = "textBoxReadFilePath";
            this.textBoxReadFilePath.ReadOnly = true;
            // 
            // textBoxOutput
            // 
            this.textBoxOutput.AccessibleDescription = null;
            this.textBoxOutput.AccessibleName = null;
            resources.ApplyResources(this.textBoxOutput, "textBoxOutput");
            this.textBoxOutput.BackgroundImage = null;
            this.textBoxOutput.Font = null;
            this.textBoxOutput.Name = "textBoxOutput";
            this.textBoxOutput.ReadOnly = true;
            // 
            // groupBox2
            // 
            this.groupBox2.AccessibleDescription = null;
            this.groupBox2.AccessibleName = null;
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.BackgroundImage = null;
            this.groupBox2.Controls.Add(this.textBoxOutput);
            this.groupBox2.Font = null;
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // buttonConvStart
            // 
            this.buttonConvStart.AccessibleDescription = null;
            this.buttonConvStart.AccessibleName = null;
            resources.ApplyResources(this.buttonConvStart, "buttonConvStart");
            this.buttonConvStart.BackgroundImage = null;
            this.buttonConvStart.Font = null;
            this.buttonConvStart.Name = "buttonConvStart";
            this.buttonConvStart.UseVisualStyleBackColor = true;
            this.buttonConvStart.Click += new System.EventHandler(this.buttonConvStart_Click);
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            // 
            // Form1
            // 
            this.AccessibleDescription = null;
            this.AccessibleName = null;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImage = null;
            this.Controls.Add(this.buttonConvStart);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Font = null;
            this.Icon = null;
            this.Name = "Form1";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonReadFile;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textBoxReadFilePath;
        private System.Windows.Forms.TextBox textBoxOutput;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button buttonConvStart;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
    }
}

