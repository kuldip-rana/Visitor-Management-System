using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using registar.Models;
using System.Net.Mail;
using System.IO;
using Rotativa;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace registar.Controllers
{
    public partial class AdminController : Controller
    {
        private VMS_DatabaseEntities db = new VMS_DatabaseEntities();

        // ==========================================
        // 1. DASHBOARD & REQUESTS
        // ==========================================

        public ActionResult AdminDashboard()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            DateTime startDate = DateTime.Now.Date.AddDays(-6);
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => startDate.AddDays(i))
                .ToList();

            // 1. Get Registrations from Visitors table
            var registrations = db.Visitors
                .Where(v => v.RegisteredAt >= startDate)
                .GroupBy(v => System.Data.Entity.DbFunctions.TruncateTime(v.RegisteredAt))
                .Select(g => new { Date = g.Key, Count = g.Count() }).ToList();

            // 2. Get Logins from OtpLogs
            var logins = db.OtpLogs
                .Where(o => o.GeneratedAt >= startDate)
                .GroupBy(o => System.Data.Entity.DbFunctions.TruncateTime(o.GeneratedAt))
                .Select(g => new { Date = g.Key, Count = g.Count() }).ToList();

            // 3. Get Check-ins from VisitorLogs
            var checkins = db.VisitorLogs
                .Where(l => l.CheckInTime >= startDate)
                .GroupBy(l => System.Data.Entity.DbFunctions.TruncateTime(l.CheckInTime))
                .Select(g => new { Date = g.Key, Count = g.Count() }).ToList();

            var stats = new AdminDashboardViewModel
            {
                TotalVisitors = db.Visitors.Count(),
                PendingCount = db.Visitors.Count(v => v.Status == 1),
                ApprovedCount = db.Visitors.Count(v => v.Status == 2),
                RejectedCount = db.Visitors.Count(v => v.Status == 3),

                // Map data to the last 7 days (filling zeros where no data exists)
                TimelineLabels = last7Days.Select(d => d.ToString("ddd")).ToList(),
                RegistrationData = last7Days.Select(d => registrations.FirstOrDefault(x => x.Date == d)?.Count ?? 0).ToList(),
                LoginData = last7Days.Select(d => logins.FirstOrDefault(x => x.Date == d)?.Count ?? 0).ToList(),
                CheckInData = last7Days.Select(d => checkins.FirstOrDefault(x => x.Date == d)?.Count ?? 0).ToList()
            };

            return View(stats);
        }

        // ==========================================
        // 2. STATUS UPDATES & PASS GENERATION
        // ==========================================

        [HttpPost]
        public ActionResult UpdateStatus(int id, int status)
        {
            try
            {
                var visitor = db.Visitors.Find(id);
                if (visitor != null)
                {
                    visitor.Status = status;

                    if (status == 2) // Approved
                    {
                        visitor.IdPassPath = GeneratePassPdf(visitor);

                        // Disable validation for incomplete CSV profiles
                        db.Configuration.ValidateOnSaveEnabled = false;
                        db.SaveChanges();
                        db.Configuration.ValidateOnSaveEnabled = true;

                        SendApprovalEmail(visitor);
                    }
                    else if (status == 3) // Rejected
                    {
                        db.Configuration.ValidateOnSaveEnabled = false;
                        db.SaveChanges();
                        db.Configuration.ValidateOnSaveEnabled = true;

                        SendRejectionEmail(visitor.Email, visitor.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return RedirectToAction("PendingRequests");
        }

        [HttpPost]
        public JsonResult UpdateGroupStatus(Guid groupId, int status)
        {
            if (Session["AdminEmail"] == null)
                return Json(new { success = false, message = "Session expired." });

            try
            {
                var visitorsInGroup = db.Visitors.Where(v => v.GroupId == groupId && v.Status == 1).ToList();
                foreach (var visitor in visitorsInGroup)
                {
                    visitor.Status = status;
                    if (status == 2) visitor.IdPassPath = GeneratePassPdf(visitor);
                }

                db.Configuration.ValidateOnSaveEnabled = false;
                db.SaveChanges();
                db.Configuration.ValidateOnSaveEnabled = true;

                foreach (var visitor in visitorsInGroup)
                {
                    if (status == 2) SendApprovalEmail(visitor);
                    else SendRejectionEmail(visitor.Email, visitor.Name);
                }
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult ResendPass(int id)
        {
            var visitor = db.Visitors.Find(id);
            if (visitor != null && !string.IsNullOrEmpty(visitor.IdPassPath))
            {
                SendApprovalEmail(visitor);
                TempData["Success"] = "Pass resent successfully.";
            }
            return RedirectToAction("VisitHistory");
        }

        // ==========================================
        // 3. HELPERS (PDF, QR, EMAIL)
        // ==========================================

        private string GeneratePassPdf(Visitor visitor)
        {
            var pdfResult = new ActionAsPdf("PassTemplate", new { id = visitor.Id }) { FileName = $"Pass_{visitor.Id}.pdf" };
            byte[] pdfBytes = pdfResult.BuildFile(this.ControllerContext);
            string fileName = $"Pass_{visitor.Id}_{Guid.NewGuid().ToString().Substring(0, 8)}.pdf";
            string relativePath = "/Content/Passes/" + fileName;
            string physicalPath = Server.MapPath(relativePath);
            string folder = Server.MapPath("~/Content/Passes/");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(physicalPath, pdfBytes);
            return relativePath;
        }

        [AllowAnonymous]
        public ActionResult PassTemplate(int id)
        {
            var visitor = db.Visitors.Find(id);
            if (visitor != null)
            {
                string verifyUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/Admin/VerifyPass/" + visitor.GroupId;
                ViewBag.QrCode = GenerateQrCode(verifyUrl);
            }
            return View(visitor);
        }

        [AllowAnonymous]
        public ActionResult VerifyPass(Guid id)
        {
            var visitor = db.Visitors.FirstOrDefault(v => v.GroupId == id);
            return View(visitor ?? new Visitor());
        }

        private string GenerateQrCode(string content)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
            {
                return "data:image/png;base64," + Convert.ToBase64String(qrCode.GetGraphic(20));
            }
        }

        private void SendApprovalEmail(Visitor visitor)
        {
            try
            {
                using (SmtpClient smtp = new SmtpClient())
                {
                    MailMessage mail = new MailMessage();
                    mail.To.Add(visitor.Email);
                    mail.Subject = "Entry Pass - " + visitor.Name;
                    mail.IsBodyHtml = true;
                    if (!string.IsNullOrEmpty(visitor.IdPassPath))
                    {
                        string fullPath = Server.MapPath(visitor.IdPassPath);
                        if (System.IO.File.Exists(fullPath)) mail.Attachments.Add(new Attachment(fullPath));
                    }
                    mail.Body = $"<h2>Visit Approved</h2><p>Dear {visitor.Name}, your pass is attached.</p>";
                    smtp.Send(mail);
                }
            }
            catch { }
        }

        private void SendRejectionEmail(string email, string name)
        {
            try
            {
                using (SmtpClient smtp = new SmtpClient())
                {
                    MailMessage mail = new MailMessage();
                    mail.To.Add(email);
                    mail.Subject = "Visit Request Update";
                    mail.IsBodyHtml = true;
                    mail.Body = $"<p>Dear {name}, your request was declined.</p>";
                    smtp.Send(mail);
                }
            }
            catch { }
        }

        // ==========================================
        // 4. MANAGEMENT (DEPT & USERS)
        // ==========================================

        public ActionResult Departments() { return View(db.Departments.ToList()); }

        [HttpPost]
        public ActionResult CreateDepartment(string name)
        {
            if (!string.IsNullOrEmpty(name)) { db.Departments.Add(new Department { Name = name, IsActive = true }); db.SaveChanges(); }
            return RedirectToAction("Departments");
        }

        [HttpPost]
        public ActionResult ToggleDepartmentStatus(int id)
        {
            var dept = db.Departments.Find(id);
            if (dept != null) { dept.IsActive = !(dept.IsActive ?? false); db.SaveChanges(); }
            return RedirectToAction("Departments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDepartment(int id)
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            var dept = db.Departments.Find(id);
            if (dept != null)
            {
                // Check if any visitors are assigned to this department
                bool hasVisitors = db.Visitors.Any(v => v.DepartmentId == id);

                if (hasVisitors)
                {
                    TempData["Error"] = "Cannot delete department. Visitors are currently assigned to it. Try 'Deactivating' it instead.";
                }
                else
                {
                    db.Departments.Remove(dept);
                    db.SaveChanges();
                    TempData["Success"] = "Department deleted successfully.";
                }
            }
            return RedirectToAction("Departments");
        }

        public ActionResult RegisteredUsers()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");
            var users = db.Visitors.GroupBy(v => v.Email).Select(g => g.OrderByDescending(v => v.RegisteredAt).FirstOrDefault()).ToList();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUserByEmail(string email)
        {
            // 1. Session Security Check
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Invalid user email.";
                return RedirectToAction("RegisteredUsers");
            }

            try
            {
                // 2. Find all visitor records associated with this email
                var userRecords = db.Visitors.Where(v => v.Email == email).ToList();

                if (userRecords.Any())
                {
                    // 3. Remove all records (RemoveRange is faster for multiple rows)
                    db.Visitors.RemoveRange(userRecords);
                    db.SaveChanges();

                    TempData["Success"] = $"User '{email}' and all their visit history have been deleted.";
                }
                else
                {
                    TempData["Error"] = "User record not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting user: " + ex.Message;
            }

            return RedirectToAction("RegisteredUsers");
        }



        //===============
        //PENDING REQUEST
        //===============
        public ActionResult PendingRequests()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            // Fetch only visitors with Status = 1 (Pending)
            // Include Department to prevent the "N/A" issue in your HTML
            var pending = db.Visitors.Include("Department")
                            .Where(v => v.Status == 1)
                            .OrderByDescending(v => v.RegisteredAt)
                            .ToList();

            return View(pending);
        }

        public ActionResult VisitHistory()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            // Fetch Approved (2) and Rejected (3) visitors for history
            var history = db.Visitors.Include("Department")
                            .Where(v => v.Status == 2 || v.Status == 3)
                            .OrderByDescending(v => v.RegisteredAt)
                            .ToList();

            return View(history);
        }

    }
}