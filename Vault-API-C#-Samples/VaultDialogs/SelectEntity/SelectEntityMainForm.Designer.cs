namespace SelectEntity
{
    partial class SelectEntityMainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectEntityMainForm));
            btnLogin = new DevExpress.XtraEditors.SimpleButton();
            btnSelectEntity = new DevExpress.XtraEditors.SimpleButton();
            btnHelp = new DevExpress.XtraEditors.SimpleButton();
            SuspendLayout();
            // 
            // btnLogin
            // 
            btnLogin.Location = new System.Drawing.Point(12, 12);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new System.Drawing.Size(75, 23);
            btnLogin.TabIndex = 0;
            btnLogin.Text = "Login...";
            btnLogin.Click += btnLogin_Click;
            // 
            // btnSelectEntity
            // 
            btnSelectEntity.Enabled = false;
            btnSelectEntity.Location = new System.Drawing.Point(12, 41);
            btnSelectEntity.Name = "btnSelectEntity";
            btnSelectEntity.Size = new System.Drawing.Size(75, 23);
            btnSelectEntity.TabIndex = 1;
            btnSelectEntity.Text = "Select Files...";
            btnSelectEntity.Click += btnSelectEntity_Click;
            // 
            // btnHelp
            // 
            btnHelp.Location = new System.Drawing.Point(12, 307);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new System.Drawing.Size(75, 23);
            btnHelp.TabIndex = 2;
            btnHelp.Text = "Help";
            btnHelp.Click += btnHelp_Click;
            // 
            // SelectFiles
            // 
            Appearance.Options.UseFont = true;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(737, 342);
            Controls.Add(btnHelp);
            Controls.Add(btnSelectEntity);
            Controls.Add(btnLogin);
            Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            IconOptions.Icon = (System.Drawing.Icon)resources.GetObject("SelectFiles.IconOptions.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            Name = "SelectFiles";
            Text = "Vault API Sample - Select Files...";
            FormClosing += SelectFiles_FormClosing;
            ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnLogin;
        private DevExpress.XtraEditors.SimpleButton btnSelectEntity;
        private DevExpress.XtraEditors.SimpleButton btnHelp;
    }
}

