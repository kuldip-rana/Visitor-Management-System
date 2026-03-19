using registar.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;

namespace registar.Controllers
{
    public class HomeController : Controller
    {
        private VMS_DatabaseEntities db = new VMS_DatabaseEntities();
        private const string AdminEmail = "kuldip1383@gmail.com";

        // GET: Landing Page
        public ActionResult HomeLanding() => View();

        // GET: Login/Signup Page
        public ActionResult Index() => View();

        //==================================================
        // LOGIN & OTP LOGIC (Keeping your older logic)
        //==================================================

        [HttpPost]
        public JsonResult SendOtp(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email is required." });

                bool isAdmin = email.Equals(AdminEmail, StringComparison.OrdinalIgnoreCase);
                bool isVisitor = db.Visitors.Any(v => v.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

                if (!isAdmin && !isVisitor)
                    return Json(new { success = false, message = "Email not found. Please register first." });

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

                return Json(new { success = false, message = "Mail server error." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Technical Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyLogin(string email, string otp)
        {
            try
            {
                var log = db.OtpLogs
                    .Where(x => x.Email == email && x.OtpCode == otp && x.IsUsed == false)
                    .OrderByDescending(x => x.GeneratedAt)
                    .FirstOrDefault();

                if (log != null)
                {
                    if (log.GeneratedAt < DateTime.Now.AddMinutes(-15))
                    {
                        ViewBag.Error = "OTP Expired.";
                        return View("Index");
                    }

                    log.IsUsed = true;
                    db.SaveChanges();

                    if (email.Equals(AdminEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        Session["AdminEmail"] = email;
                        Session["Role"] = "Admin";
                        return RedirectToAction("AdminDashboard", "Admin");
                    }
                    else
                    {
                        var visitor = db.Visitors.FirstOrDefault(v => v.Email == email);
                        if (visitor != null)
                        {
                            Session["VisitorID"] = visitor.Id;
                            Session["UserEmail"] = email;
                            Session["Role"] = "Visitor";
                            return RedirectToAction("MyDashboard", "Visitor");
                        }
                    }
                }
                ViewBag.Error = "Invalid Code.";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "System Error: " + ex.Message;
            }
            return View("Index");
        }

        //==================================================
        // REGISTRATION & CSV LOGIC (Moved from Visitor)
        //==================================================

        public ActionResult Visitor_Registration()
        {
            ViewBag.DepartmentId = new SelectList(db.Departments.Where(d => d.IsActive == true), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Visitor_Registration(Visitor model, HttpPostedFileBase csvFile)
        {
            try
            {
                // Check if already exists
                var existing = db.Visitors.FirstOrDefault(v => v.Email == model.Email);
                if (existing != null)
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                }
                else if (ModelState.IsValid)
                {
                    Guid currentGroupId = Guid.NewGuid();
                    string otp = new Random().Next(100000, 999999).ToString();

                    // 1. Save Lead Visitor
                    model.GroupId = currentGroupId;
                    model.Status = 0;
                    model.RegisteredAt = DateTime.Now;
                    db.Visitors.Add(model);

                    // 2. Process CSV for Group Members
                    if (csvFile != null && csvFile.ContentLength > 0)
                    {
                        ProcessCsvImport(csvFile, model, currentGroupId);
                    }

                    // 3. Save OTP for verification
                    db.OtpLogs.Add(new OtpLog
                    {
                        Email = model.Email,
                        OtpCode = otp,
                        GeneratedAt = DateTime.Now,
                        IsUsed = false
                    });

                    db.SaveChanges();

                    // 4. Send Verification Email
                    SendLoginEmail(model.Email, otp);

                    TempData["TargetEmail"] = model.Email;
                    return RedirectToAction("VerifyOtp");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Registration error: " + ex.Message);
            }

            ViewBag.DepartmentId = new SelectList(db.Departments.Where(d => d.IsActive == true), "Id", "Name");
            return View(model);
        }

        private void ProcessCsvImport(HttpPostedFileBase file, Visitor lead, Guid gid)
        {
            using (var reader = new StreamReader(file.InputStream))
            {
                reader.ReadLine(); // Skip Header
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',');
                    db.Visitors.Add(new Visitor
                    {
                        Name = values[0],
                        Phone = values.Length > 1 ? values[1] : "",
                        Email = lead.Email, // Linked to lead for verification
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

        public ActionResult VerifyOtp()
        {
            ViewBag.Email = TempData["TargetEmail"];
            return View();
        }

        //==================================================
        // UTILITIES (Email & Session)
        //==================================================

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("HomeLanding");
        }

        private bool SendLoginEmail(string email, string otp)
        {
            try
            {
                using (SmtpClient smtp = new SmtpClient())
                {
                    MailMessage mail = new MailMessage();
                    mail.To.Add(email);
                    mail.Subject = "Verification Code: " + otp;
                    mail.IsBodyHtml = true;
                    mail.Body = $@"<div style='font-family:sans-serif; text-align:center; padding:20px; border:1px solid #eee;'>
                                    <h2>Verification Code</h2>
                                    <p style='font-size:24px; font-weight:bold; color:#4338ca; letter-spacing:5px;'>{otp}</p>
                                    <p>This code expires in 15 minutes.</p>
                                   </div>";
                    smtp.Send(mail);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}