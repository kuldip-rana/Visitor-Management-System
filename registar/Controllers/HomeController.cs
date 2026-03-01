using registar.Models;
using System;
using System.Linq;
using System.Net.Mail;
using System.Web.Mvc;
using System.Data.Entity.Validation;
using System.Text;

namespace registar.Controllers
{
    public class HomeController : Controller
    {
        private VMS_DatabaseEntities db = new VMS_DatabaseEntities();

        public ActionResult HomeLanding() => View();
        public ActionResult Index() => View();

        [HttpPost]
        public JsonResult SendOtp(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email is required." });

                bool isAdmin = email.Equals("kuldip1383@gmail.com", StringComparison.OrdinalIgnoreCase);
                bool isVisitor = db.Visitors.Any(v => v.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

                if (!isAdmin && !isVisitor)
                    return Json(new { success = false, message = "Access Denied. Email not registered." });

                string otp = new Random().Next(100000, 999999).ToString();

                db.OtpLogs.Add(new OtpLog
                {
                    Email = email,
                    OtpCode = otp,
                    GeneratedAt = DateTime.Now,
                    IsUsed = false
                });

                db.SaveChanges();

                if (SendLoginEmail(email, otp))
                    return Json(new { success = true });
                else
                    return Json(new { success = false, message = "Email server error. Check SMTP settings." });
            }
            catch (DbEntityValidationException ex)
            {
                var sb = new StringBuilder();
                foreach (var failure in ex.EntityValidationErrors)
                {
                    foreach (var error in failure.ValidationErrors)
                    {
                        sb.Append($"{error.PropertyName}: {error.ErrorMessage} ");
                    }
                }
                return Json(new { success = false, message = "DB Validation Error: " + sb.ToString() });
            }
            catch (Exception ex)
            {
                // We check InnerException because the main exception is often a generic "An error occurred"
                string detailedError = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Technical Error: " + detailedError });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyLogin(string email, string otp)
        {
            try
            {
                // DEBUG: If you are getting reloaded, check if these are null
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
                {
                    ViewBag.Error = "Email or OTP missing. Please try again.";
                    return View("Index");
                }

                var log = db.OtpLogs.OrderByDescending(x => x.GeneratedAt)
                                   .FirstOrDefault(x => x.Email == email && x.OtpCode == otp && x.IsUsed == false);

                if (log != null)
                {
                    // Check if OTP is expired (e.g., older than 15 minutes)
                    if (log.GeneratedAt < DateTime.Now.AddMinutes(-15))
                    {
                        ViewBag.Error = "OTP has expired. Please request a new one.";
                        return View("Index");
                    }

                    log.IsUsed = true;
                    db.SaveChanges();

                    if (email.Equals("kuldip1383@gmail.com", StringComparison.OrdinalIgnoreCase))
                    {
                        Session["AdminEmail"] = email;
                        return RedirectToAction("AdminDashboard", "Admin");
                    }
                    else
                    {
                        var visitor = db.Visitors.FirstOrDefault(v => v.Email == email);
                        if (visitor != null)
                        {
                            Session["VisitorID"] = visitor.Id;
                            return RedirectToAction("MyDashboard", "Visitor");
                        }
                        else
                        {
                            ViewBag.Error = "Visitor record not found in database.";
                        }
                    }
                }
                else
                {
                    ViewBag.Error = "Invalid Code. Please check and try again.";
                }
            }
            catch (Exception ex)
            {
                // This will now show the EXACT database error if it fails to save
                ViewBag.Error = "System Error: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return View("Index");
        }

        private bool SendLoginEmail(string email, string otp)
        {
            try
            {
                using (SmtpClient smtp = new SmtpClient())
                {
                    MailMessage mail = new MailMessage();
                    mail.To.Add(email);
                    mail.Subject = "Login Verification Code";
                    mail.IsBodyHtml = true;
                    mail.Body = $"<div style='font-family: sans-serif; padding:20px; border:1px solid #eee;'><h2>Code: {otp}</h2></div>";
                    smtp.Send(mail);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SMTP Error: " + ex.Message);
                return false;
            }
        }

        //==============
        //LOGOUT LOGIC
        //==============
        public ActionResult Logout()
        {
            // Clear all session variables
            Session.Clear();
            Session.Abandon();

            // Redirect to login page or home landing
            return RedirectToAction("HomeLanding", "Home");
        }
    }
}