using System;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using OutSystems.ExternalLibraries.SDK;

namespace ODC.HtmlEmail
{
    [OSInterface(
        Name = "HtmlEmailSender",
        Description = "Sends raw HTML emails via SMTP, bypassing ODC's native email escaping",
        IconResourceName = "ODC.HtmlEmail.icon.png")]
    public interface IHtmlEmailSender
    {
        [OSAction(Description = "Sends an HTML-formatted email via SMTP")]
        void SendHtmlEmail(
            [OSParameter(Description = "SMTP server port")] int smtpPort,
            [OSParameter(Description = "Sender email address")] string fromEmail,
            [OSParameter(Description = "Sender display name")] string fromName,
            [OSParameter(Description = "Whether to use SSL/TLS")] bool enableSsl,
            [OSParameter(Description = "Recipient(s), comma or semicolon separated")] string to,
            [OSParameter(Description = "Email subject")] string subject,
            [OSParameter(Description = "HTML email body")] string htmlBody,
            [OSParameter(Description = "CC recipient(s), optional")] string cc,
            [OSParameter(Description = "BCC recipient(s), optional")] string bcc
        );
    }

    public class HtmlEmailSender : IHtmlEmailSender
    {
        public void SendHtmlEmail(
            int smtpPort,
            string fromEmail,
            string fromName,
            bool enableSsl,
            string to,
            string subject,
            string htmlBody,
            string cc,
            string bcc)
        {
            var smtpHost = Environment.GetEnvironmentVariable("SECURE_GATEWAY");

            if (string.IsNullOrWhiteSpace(smtpHost))
                throw new Exception("SECURE_GATEWAY environment variable is not set or empty. Ensure Private Gateway is active for this stage.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));

            AddRecipients(message.To, to);
            if (!string.IsNullOrWhiteSpace(cc)) AddRecipients(message.Cc, cc);
            if (!string.IsNullOrWhiteSpace(bcc)) AddRecipients(message.Bcc, bcc);

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();

            try
            {
                var socketOptions = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                client.Connect(smtpHost, smtpPort, socketOptions);
                client.Send(message);
                client.Disconnect(true);
            }
            catch (Exception ex)
            {
                throw new Exception($"SendHtmlEmail failed. SECURE_GATEWAY='{smtpHost}', Port={smtpPort} | {ex.GetType().Name} - {ex.Message}", ex);
            }
        }

        private static void AddRecipients(InternetAddressList list, string addresses)
        {
            var split = addresses.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var addr in split)
            {
                list.Add(MailboxAddress.Parse(addr.Trim()));
            }
        }
    }
}