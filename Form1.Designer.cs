namespace SmartConveyorSystem
{
    partial class Form1
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.cmbPort = new System.Windows.Forms.ComboBox();
            this.trackX = new System.Windows.Forms.TrackBar();
            this.lblX = new System.Windows.Forms.Label();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnEmergency = new System.Windows.Forms.Button();
            this.trackY = new System.Windows.Forms.TrackBar();
            this.trackZ = new System.Windows.Forms.TrackBar();
            this.lblY = new System.Windows.Forms.Label();
            this.lblZ = new System.Windows.Forms.Label();
            this.pageSetupDialog1 = new System.Windows.Forms.PageSetupDialog();
            this.picCamera = new System.Windows.Forms.PictureBox();
            this.btnStartVision = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            this.btnSaveRecipe = new System.Windows.Forms.Button();
            this.btnLoadRecipe = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.btnExportCsv = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.trackX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackZ)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picCamera)).BeginInit();
            this.SuspendLayout();
            // 
            // cmbPort
            // 
            this.cmbPort.FormattingEnabled = true;
            this.cmbPort.Location = new System.Drawing.Point(111, 129);
            this.cmbPort.Name = "cmbPort";
            this.cmbPort.Size = new System.Drawing.Size(121, 23);
            this.cmbPort.TabIndex = 0;
            // 
            // trackX
            // 
            this.trackX.Location = new System.Drawing.Point(111, 177);
            this.trackX.Name = "trackX";
            this.trackX.Size = new System.Drawing.Size(104, 56);
            this.trackX.TabIndex = 1;
            this.trackX.Scroll += new System.EventHandler(this.trackX_Scroll);
            // 
            // lblX
            // 
            this.lblX.AutoSize = true;
            this.lblX.Location = new System.Drawing.Point(127, 236);
            this.lblX.Name = "lblX";
            this.lblX.Size = new System.Drawing.Size(30, 15);
            this.lblX.TabIndex = 2;
            this.lblX.Text = "lblX";
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(289, 128);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(174, 24);
            this.btnConnect.TabIndex = 3;
            this.btnConnect.Text = "btnConnect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnEmergency
            // 
            this.btnEmergency.Location = new System.Drawing.Point(516, 129);
            this.btnEmergency.Name = "btnEmergency";
            this.btnEmergency.Size = new System.Drawing.Size(193, 24);
            this.btnEmergency.TabIndex = 5;
            this.btnEmergency.Text = "btnEmergency";
            this.btnEmergency.UseVisualStyleBackColor = true;
            this.btnEmergency.Click += new System.EventHandler(this.btnEmergency_Click);
            // 
            // trackY
            // 
            this.trackY.Location = new System.Drawing.Point(248, 177);
            this.trackY.Name = "trackY";
            this.trackY.Size = new System.Drawing.Size(104, 56);
            this.trackY.TabIndex = 7;
            this.trackY.Scroll += new System.EventHandler(this.trackY_Scroll);
            // 
            // trackZ
            // 
            this.trackZ.Location = new System.Drawing.Point(381, 177);
            this.trackZ.Name = "trackZ";
            this.trackZ.Size = new System.Drawing.Size(104, 56);
            this.trackZ.TabIndex = 8;
            this.trackZ.Scroll += new System.EventHandler(this.trackZ_Scroll);
            // 
            // lblY
            // 
            this.lblY.AutoSize = true;
            this.lblY.Location = new System.Drawing.Point(262, 236);
            this.lblY.Name = "lblY";
            this.lblY.Size = new System.Drawing.Size(29, 15);
            this.lblY.TabIndex = 9;
            this.lblY.Text = "lblY";
            // 
            // lblZ
            // 
            this.lblZ.AutoSize = true;
            this.lblZ.Location = new System.Drawing.Point(397, 236);
            this.lblZ.Name = "lblZ";
            this.lblZ.Size = new System.Drawing.Size(30, 15);
            this.lblZ.TabIndex = 10;
            this.lblZ.Text = "lblZ";
            // 
            // picCamera
            // 
            this.picCamera.Location = new System.Drawing.Point(115, 303);
            this.picCamera.Name = "picCamera";
            this.picCamera.Size = new System.Drawing.Size(370, 274);
            this.picCamera.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picCamera.TabIndex = 11;
            this.picCamera.TabStop = false;
            // 
            // btnStartVision
            // 
            this.btnStartVision.Location = new System.Drawing.Point(505, 303);
            this.btnStartVision.Name = "btnStartVision";
            this.btnStartVision.Size = new System.Drawing.Size(174, 24);
            this.btnStartVision.TabIndex = 12;
            this.btnStartVision.Text = "btnStartVision";
            this.btnStartVision.UseVisualStyleBackColor = true;
            this.btnStartVision.Click += new System.EventHandler(this.btnStartVision_Click);
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(516, 177);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(193, 24);
            this.btnReset.TabIndex = 13;
            this.btnReset.Text = "btnReset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // formsPlot1
            // 
            this.formsPlot1.Location = new System.Drawing.Point(780, 128);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(594, 421);
            this.formsPlot1.TabIndex = 15;
            // 
            // btnSaveRecipe
            // 
            this.btnSaveRecipe.Location = new System.Drawing.Point(505, 374);
            this.btnSaveRecipe.Name = "btnSaveRecipe";
            this.btnSaveRecipe.Size = new System.Drawing.Size(174, 24);
            this.btnSaveRecipe.TabIndex = 16;
            this.btnSaveRecipe.Text = "btnSaveRecipe";
            this.btnSaveRecipe.UseVisualStyleBackColor = true;
            // 
            // btnLoadRecipe
            // 
            this.btnLoadRecipe.Location = new System.Drawing.Point(505, 439);
            this.btnLoadRecipe.Name = "btnLoadRecipe";
            this.btnLoadRecipe.Size = new System.Drawing.Size(174, 24);
            this.btnLoadRecipe.TabIndex = 17;
            this.btnLoadRecipe.Text = "btnLoadRecipe";
            this.btnLoadRecipe.UseVisualStyleBackColor = true;
            // 
            // txtLog
            // 
            this.txtLog.Location = new System.Drawing.Point(115, 641);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(594, 295);
            this.txtLog.TabIndex = 18;
            // 
            // btnExportCsv
            // 
            this.btnExportCsv.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.btnExportCsv.Location = new System.Drawing.Point(797, 651);
            this.btnExportCsv.Name = "btnExportCsv";
            this.btnExportCsv.Size = new System.Drawing.Size(174, 24);
            this.btnExportCsv.TabIndex = 19;
            this.btnExportCsv.Text = "btnExportCsv";
            this.btnExportCsv.UseVisualStyleBackColor = false;
            this.btnExportCsv.Click += new System.EventHandler(this.btnExportCsv_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1455, 1000);
            this.Controls.Add(this.btnExportCsv);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.btnLoadRecipe);
            this.Controls.Add(this.btnSaveRecipe);
            this.Controls.Add(this.formsPlot1);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnStartVision);
            this.Controls.Add(this.picCamera);
            this.Controls.Add(this.lblZ);
            this.Controls.Add(this.lblY);
            this.Controls.Add(this.trackZ);
            this.Controls.Add(this.trackY);
            this.Controls.Add(this.btnEmergency);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.lblX);
            this.Controls.Add(this.trackX);
            this.Controls.Add(this.cmbPort);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.trackX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackZ)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picCamera)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox cmbPort;
        private System.Windows.Forms.TrackBar trackX;
        private System.Windows.Forms.Label lblX;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnEmergency;
        private System.Windows.Forms.TrackBar trackY;
        private System.Windows.Forms.TrackBar trackZ;
        private System.Windows.Forms.Label lblY;
        private System.Windows.Forms.Label lblZ;
        private System.Windows.Forms.PageSetupDialog pageSetupDialog1;
        private System.Windows.Forms.PictureBox picCamera;
        private System.Windows.Forms.Button btnStartVision;
        private System.Windows.Forms.Button btnReset;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private System.Windows.Forms.Button btnSaveRecipe;
        private System.Windows.Forms.Button btnLoadRecipe;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnExportCsv;
    }
}

