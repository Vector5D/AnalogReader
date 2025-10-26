namespace AnalogReader
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private Button btnShot;
        private PictureBox pbCroppedImage;
        private Label lblCropped;

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
            btnShot = new Button();
            pbCroppedImage = new PictureBox();
            lblCropped = new Label();
            btnSelectImage = new Button();
            openFileDialog1 = new OpenFileDialog();
            pbOriginalImage = new PictureBox();
            btnRTSP = new Button();
            lblOriginal = new Label();
            lblStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)pbCroppedImage).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbOriginalImage).BeginInit();
            SuspendLayout();
            // 
            // btnShot
            // 
            btnShot.Location = new Point(622, 18);
            btnShot.Margin = new Padding(3, 4, 3, 4);
            btnShot.Name = "btnShot";
            btnShot.Size = new Size(171, 40);
            btnShot.TabIndex = 0;
            btnShot.Text = "撮影";
            btnShot.UseVisualStyleBackColor = true;
            btnShot.Click += btnShot_Click;
            // 
            // pbCroppedImage
            // 
            pbCroppedImage.BorderStyle = BorderStyle.FixedSingle;
            pbCroppedImage.Location = new Point(513, 109);
            pbCroppedImage.Margin = new Padding(3, 4, 3, 4);
            pbCroppedImage.Name = "pbCroppedImage";
            pbCroppedImage.Size = new Size(457, 399);
            pbCroppedImage.SizeMode = PictureBoxSizeMode.Zoom;
            pbCroppedImage.TabIndex = 2;
            pbCroppedImage.TabStop = false;
            // 
            // lblCropped
            // 
            lblCropped.Location = new Point(513, 75);
            lblCropped.Name = "lblCropped";
            lblCropped.Size = new Size(114, 31);
            lblCropped.TabIndex = 1;
            lblCropped.Text = "電圧計";
            // 
            // btnSelectImage
            // 
            btnSelectImage.Location = new Point(799, 18);
            btnSelectImage.Margin = new Padding(3, 4, 3, 4);
            btnSelectImage.Name = "btnSelectImage";
            btnSelectImage.Size = new Size(171, 40);
            btnSelectImage.TabIndex = 7;
            btnSelectImage.Text = "画像ファイル選択";
            btnSelectImage.UseVisualStyleBackColor = true;
            btnSelectImage.Click += btnSelectImage_Click;
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // pbOriginalImage
            // 
            pbOriginalImage.BorderStyle = BorderStyle.FixedSingle;
            pbOriginalImage.Location = new Point(14, 109);
            pbOriginalImage.Margin = new Padding(3, 4, 3, 4);
            pbOriginalImage.Name = "pbOriginalImage";
            pbOriginalImage.Size = new Size(457, 399);
            pbOriginalImage.SizeMode = PictureBoxSizeMode.Zoom;
            pbOriginalImage.TabIndex = 8;
            pbOriginalImage.TabStop = false;
            // 
            // btnRTSP
            // 
            btnRTSP.Location = new Point(14, 18);
            btnRTSP.Margin = new Padding(3, 4, 3, 4);
            btnRTSP.Name = "btnRTSP";
            btnRTSP.Size = new Size(171, 40);
            btnRTSP.TabIndex = 10;
            btnRTSP.Text = "RTSP動画再生";
            btnRTSP.UseVisualStyleBackColor = true;
            btnRTSP.Click += btnRTSP_Click;
            // 
            // lblOriginal
            // 
            lblOriginal.Location = new Point(14, 75);
            lblOriginal.Name = "lblOriginal";
            lblOriginal.Size = new Size(114, 31);
            lblOriginal.TabIndex = 9;
            lblOriginal.Text = "RTSP動画";
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(14, 523);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(599, 41);
            lblStatus.TabIndex = 11;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(982, 573);
            Controls.Add(lblStatus);
            Controls.Add(btnRTSP);
            Controls.Add(lblOriginal);
            Controls.Add(pbOriginalImage);
            Controls.Add(btnSelectImage);
            Controls.Add(btnShot);
            Controls.Add(lblCropped);
            Controls.Add(pbCroppedImage);
            Margin = new Padding(3, 4, 3, 4);
            Name = "FormMain";
            Text = "電圧計検出アプリケーション";
            ((System.ComponentModel.ISupportInitialize)pbCroppedImage).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbOriginalImage).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private Button btnSelectImage;
        private OpenFileDialog openFileDialog1;
        private PictureBox pbOriginalImage;
        private Button btnRTSP;
        private Label lblOriginal;
        private Label lblStatus;
    }
}
