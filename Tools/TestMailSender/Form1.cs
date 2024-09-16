
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using System.Windows.Forms;

namespace TestMailSender;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
    }

    private async void btnSend_Click(object sender, EventArgs e)
    {
        var host = txtHost.Text.Trim();
        var port = Int32.Parse(txtPort.Text.Trim());
        var userFrom = txtFrom.Text.Trim();
        var userName = txtUser.Text.Trim();
        var password = txtPassword.Text.Trim();
        var sendTo = txtSendTo.Text.Trim();


        var mm = new MimeMessage();
        mm.From.Add(new MailboxAddress(String.Empty, userFrom));
        mm.To.Add(new MailboxAddress(String.Empty, sendTo));
        mm.Subject = "Message Subject";
        mm.Body = new TextPart(TextFormat.Html)
        {
            Text = "Message body test"
        };

        var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (sender, cert, ch, err) => true;
        await client.ConnectAsync(host, port, SecureSocketOptions.Auto);
        await client.AuthenticateAsync(userName, password);
        await client.SendAsync(mm);
        await client.DisconnectAsync(true);

        MessageBox.Show("Sent successfully");
    }
}
