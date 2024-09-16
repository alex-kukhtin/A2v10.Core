namespace TestMailSender
{
    partial class Form1
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
            txtHost = new TextBox();
            label1 = new Label();
            txtPort = new TextBox();
            label2 = new Label();
            txtUser = new TextBox();
            label3 = new Label();
            txtPassword = new TextBox();
            label4 = new Label();
            txtSendTo = new TextBox();
            label5 = new Label();
            btnSend = new Button();
            txtJson = new TextBox();
            label6 = new Label();
            txtFrom = new TextBox();
            label7 = new Label();
            SuspendLayout();
            // 
            // txtHost
            // 
            txtHost.Location = new Point(36, 42);
            txtHost.Margin = new Padding(4);
            txtHost.Name = "txtHost";
            txtHost.Size = new Size(288, 29);
            txtHost.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(36, 17);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(42, 21);
            label1.TabIndex = 1;
            label1.Text = "Host";
            // 
            // txtPort
            // 
            txtPort.Location = new Point(36, 101);
            txtPort.Margin = new Padding(4);
            txtPort.Name = "txtPort";
            txtPort.Size = new Size(153, 29);
            txtPort.TabIndex = 0;
            txtPort.Text = "587";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(36, 76);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(38, 21);
            label2.TabIndex = 1;
            label2.Text = "Port";
            // 
            // txtUser
            // 
            txtUser.Location = new Point(36, 246);
            txtUser.Margin = new Padding(4);
            txtUser.Name = "txtUser";
            txtUser.Size = new Size(288, 29);
            txtUser.TabIndex = 0;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(36, 221);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(42, 21);
            label3.TabIndex = 1;
            label3.Text = "User";
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(36, 304);
            txtPassword.Margin = new Padding(4);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(288, 29);
            txtPassword.TabIndex = 0;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(36, 279);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(76, 21);
            label4.TabIndex = 1;
            label4.Text = "Password";
            // 
            // txtSendTo
            // 
            txtSendTo.Location = new Point(36, 384);
            txtSendTo.Margin = new Padding(4);
            txtSendTo.Name = "txtSendTo";
            txtSendTo.Size = new Size(288, 29);
            txtSendTo.TabIndex = 0;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(36, 358);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(64, 21);
            label5.TabIndex = 1;
            label5.Text = "Send To";
            // 
            // btnSend
            // 
            btnSend.Location = new Point(36, 431);
            btnSend.Margin = new Padding(4);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(288, 73);
            btnSend.TabIndex = 2;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // txtJson
            // 
            txtJson.Font = new Font("Cascadia Code", 12F, FontStyle.Regular, GraphicsUnit.Point, 204);
            txtJson.Location = new Point(369, 42);
            txtJson.Margin = new Padding(4);
            txtJson.Multiline = true;
            txtJson.Name = "txtJson";
            txtJson.Size = new Size(476, 357);
            txtJson.TabIndex = 0;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(369, 17);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(128, 21);
            label6.TabIndex = 1;
            label6.Text = "MailSettings.json";
            // 
            // txtFrom
            // 
            txtFrom.Location = new Point(36, 164);
            txtFrom.Margin = new Padding(4);
            txtFrom.Name = "txtFrom";
            txtFrom.Size = new Size(288, 29);
            txtFrom.TabIndex = 0;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(36, 139);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(47, 21);
            label7.TabIndex = 1;
            label7.Text = "From";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 21F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(896, 548);
            Controls.Add(btnSend);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(txtJson);
            Controls.Add(label7);
            Controls.Add(label1);
            Controls.Add(txtSendTo);
            Controls.Add(txtPassword);
            Controls.Add(txtUser);
            Controls.Add(txtFrom);
            Controls.Add(txtPort);
            Controls.Add(txtHost);
            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 204);
            Margin = new Padding(4);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtHost;
        private Label label1;
        private TextBox txtPort;
        private Label label2;
        private TextBox txtUser;
        private Label label3;
        private TextBox txtPassword;
        private Label label4;
        private TextBox txtSendTo;
        private Label label5;
        private Button btnSend;
        private TextBox txtJson;
        private Label label6;
        private TextBox txtFrom;
        private Label label7;
    }
}
