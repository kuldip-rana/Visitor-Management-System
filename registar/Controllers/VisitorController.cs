using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using registar.Models;
using System.Net.Mail;

namespace registar.Controllers
{
    public class VisitorController : Controller
    {
        private VMS_DatabaseEntities db = new VMS_DatabaseEntities();

        //===========================
        // NEW VISITOR REGISTRATION 
        //===========================
        public ActionResult Visitor_Registeration()
        {
            ViewBag.DepartmentId = new SelectList(db.Departments.Where(d => d.IsActive == true), "Id", "Name");

            // Check if the user is already logged in as a visitor
            if (Session["VisitorID"] != null && int.TryParse(Session["VisitorID"].ToString(), out int visitorId))
            {
                var visitor = db.Visitors.Find(visitorId);
                if (visitor != null)
                {
                    // Pass the existing email to the model for the view
                    return View(new Visitor { Email = visitor.Email });
                }
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Visitor_Registeration(Visitor model, HttpPostedFileBase csvFile)
        {
            // Check if visitor already exists in the system
            var existingVisitor = db.Visitors.FirstOrDefault(v => v.Email == model.Email);

            if (existingVisitor != null)
            {
                if (existingVisitor.Status == 2)
                {
                    // FIX: Set VisitorID session so MyDashboard can find it
                    Session["VisitorID"] = existingVisitor.Id;
                    return RedirectToAction("MyDashboard");
                }

                // If they are still pending (0 or 1), show the status message
                ModelState.AddModelError("Email", "You have a pending request. Please wait for Admin approval.");
                ViewBag.DepartmentId = new SelectList(db.Departments.Where(d => d.IsActive == true), "Id", "Name");
                return View(model);
            }

            // --- Original Registration Logic Starts Here ---
            Guid currentGroupId = Guid.NewGuid();
            string generatedOtp = new Random().Next(100000, 999999).ToString();

            if (csvFile != null && csvFile.ContentLength > 0)
            {
                // Add the lead visitor first
                model.GroupId = currentGroupId;
                model.Status = 0;
                model.RegisteredAt = DateTime.Now;
                db.Visitors.Add(model);

                // Then add the rest from CSV
                ProcessCsvImport(csvFile, model, currentGroupId);
            }
            else
            {
                model.GroupId = currentGroupId;
                model.Status = 0;
                model.RegisteredAt = DateTime.Now;
                db.Visitors.Add(model);
            }

            db.OtpLogs.Add(new OtpLog
            {
                Email = model.Email,
                OtpCode = generatedOtp,
                GeneratedAt = DateTime.Now,
                IsUsed = false
            });

            db.SaveChanges();
            SendEmail(model.Email, generatedOtp);

            TempData["TargetEmail"] = model.Email;
            return RedirectToAction("VerifyOtp");
        }
        //===================================
        // NEW: Visitor's Personal Dashboard
        //==================================
        public ActionResult MyDashboard()
        {
            if (Session["VisitorID"] == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (int.TryParse(Session["VisitorID"].ToString(), out int visitorId))
            {
                // 1. Get the current logged-in visitor record
                var currentVisitor = db.Visitors.SingleOrDefault(v => v.Id == visitorId);
                if (currentVisitor == null) return HttpNotFound();

                // 2. Get all visits for this user's email to show History and Upcoming
                // We pass the list to the View, or use a ViewModel. For now, let's use ViewBag for history.
                var allVisits = db.Visitors
                    .Where(v => v.Email == currentVisitor.Email)
                    .OrderByDescending(v => v.VisitDate)
                    .ToList();

                ViewBag.AllVisits = allVisits;

                return View(currentVisitor);
            }

            return RedirectToAction("Index", "Home");
        }


        //========================
        //SEND & VERIFY EMAIL + OTP
        //===========================
        private void SendEmail(string toEmail, string otp)
        {
            try
            {
                using (SmtpClient smtp = new SmtpClient())
                {
                    MailMessage mail = new MailMessage();
                    mail.To.Add(toEmail);
                    mail.Subject = "Visitor Verification OTP";
                    mail.IsBodyHtml = true;
                    mail.Body = $@"
                        <div style='font-family: Arial, sans-serif; border: 1px solid #ddd; padding: 20px; border-radius: 10px;'>
                            <h2 style='color: #2563eb;'>Security Verification</h2>
                            <p>Please use the following OTP to verify your email address:</p>
                            <div style='background: #f1f5f9; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px; color: #1d4ed8;'>
                                {otp}
                            </div>
                        </div>";

                    smtp.Send(mail);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Email Error: " + ex.Message);
            }
        }

        public ActionResult VerifyOtp()
        {
            ViewBag.Email = TempData["TargetEmail"];
            return View();
        }

        [HttpPost]
        public ActionResult VerifyOtp(string email, string otp)
        {
            var log = db.OtpLogs.FirstOrDefault(x => x.Email == email && x.OtpCode == otp && x.IsUsed == false);

            if (log != null)
            {
                log.IsUsed = true;
                var visitors = db.Visitors.Where(v => v.Email == email && v.Status == 0).ToList();
                foreach (var v in visitors) { v.Status = 1; }
                db.SaveChanges();
                return View("SuccessPage");
            }

            ViewBag.Error = "Invalid or expired OTP.";
            ViewBag.Email = email;
            return View();
        }
        //==============
        //CSV FORMAT
        //==============
        private void ProcessCsvImport(HttpPostedFileBase file, Visitor lead, Guid gid)
        {
            using (var reader = new StreamReader(file.InputStream))
            {
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var values = reader.ReadLine().Split(',');
                    db.Visitors.Add(new Visitor
                    {
                        Name = values[0],
                        Phone = values.Length > 1 ? values[1] : "",
                        Email = lead.Email,
                        DepartmentId = lead.DepartmentId,
                        Purpose = lead.Purpose,
                        VisitDate = lead.VisitDate,
                        GroupId = gid,
                        Status = 0,
                        RegisteredAt = DateTime.Now
                    });
                }
            }
        }

        //------------------
        // HISTORY
        //=================
        public ActionResult VisitHistory()
        {
            if (Session["VisitorID"] == null) return RedirectToAction("Index", "Home");

            if (int.TryParse(Session["VisitorID"].ToString(), out int visitorId))
            {
                // Get the current visitor to find their email
                var currentVisitor = db.Visitors.Find(visitorId);
                if (currentVisitor == null) return HttpNotFound();

                // Fetch all history for this email
                var history = db.Visitors
                    .Where(v => v.Email == currentVisitor.Email)
                    .OrderByDescending(v => v.VisitDate)
                    .ToList();

                return View(history);
            }
            return RedirectToAction("Index", "Home");
        }
    }
}