using System.Net;
using System.Net.Mail;

namespace registar.Helpers
{
    public class EmailService
    {
        public static void SendEmail(string toEmail, string subject, string body)
        {
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress("yourgmail@gmail.com");
            mail.To.Add(toEmail);
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
            smtp.Credentials = new NetworkCredential(
                "kuldip1383@gmail.com",
                "cyzl idmw exro xrnr"
            );
            smtp.EnableSsl = true;

            smtp.Send(mail);
        }
    }
}
