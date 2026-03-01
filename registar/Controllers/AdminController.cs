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
        // 1. DASHBOARD & HISTORY
        // ==========================================

        public ActionResult AdminDashboard()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            var pendingVisitors = db.Visitors.Where(v => v.Status == 1)
                                            .OrderByDescending(v => v.RegisteredAt)
                                            .ToList();
            return View(pendingVisitors);
        }

        public ActionResult VisitHistory()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            var history = db.Visitors.Where(v => v.Status == 2 || v.Status == 3)
                                     .OrderByDescending(v => v.RegisteredAt)
                                     .ToList();
            return View(history);
        }

        // ==========================================
        // 2. STATUS UPDATES & PASS GENERATION
        // ==========================================

        [HttpPost]
        public ActionResult UpdateStatus(int id, int status)
        {
            var visitor = db.Visitors.Find(id);
            if (visitor != null)
            {
                visitor.Status = status;

                if (status == 2) // Approved
                {
                    string passPath = GeneratePassPdf(visitor);
                    visitor.IdPassPath = passPath;
                    db.SaveChanges();
                    SendApprovalEmail(visitor);
                }
                else if (status == 3) // Rejected
                {
                    db.SaveChanges();
                    SendRejectionEmail(visitor.Email, visitor.Name);
                }
            }
            return RedirectToAction("AdminDashboard");
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
        //=================
        //GENERATE PDF
        //================
        private string GeneratePassPdf(Visitor visitor)
        {
            var pdfResult = new ActionAsPdf("PassTemplate", new { id = visitor.Id })
            {
                FileName = $"Pass_{visitor.Id}.pdf"
            };

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
                // REWRITE: Encode a full URL instead of just a string ID.
                // Replace 'yourdomain.com' with your actual hosting URL.
                string verifyUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/Admin/VerifyPass/" + visitor.GroupId;
                ViewBag.QrCode = GenerateQrCode(verifyUrl);
            }
            return View(visitor);
        }

        // NEW METHOD: Security Verification Page
        // This is what opens when the guard scans the QR code.
        [AllowAnonymous]
        public ActionResult VerifyPass(Guid id)
        {
            var visitor = db.Visitors.FirstOrDefault(v => v.GroupId == id);
            if (visitor == null)
            {
                ViewBag.Message = "Invalid Pass - Record Not Found";
                return View("VerificationResult");
            }
            return View(visitor);
        }

        // ==========================================
        // 3. EMAIL HELPERS
        // ==========================================

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
                        if (System.IO.File.Exists(fullPath))
                        {
                            mail.Attachments.Add(new Attachment(fullPath));
                        }
                    }

                    mail.Body = $@"
                        <div style='font-family: Arial; border: 1px solid #ddd; padding: 20px; border-radius: 10px;'>
                            <h2 style='color: #2563eb;'>Visit Approved</h2>
                            <p>Dear {visitor.Name}, your visit is approved. <b>Your entry pass is attached to this email.</b></p>
                            <p>Please present the attached PDF at the gate for scanning.</p>
                        </div>";

                    smtp.Send(mail);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
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
                    mail.Body = $"<p>Dear {name}, your entry request has been declined.</p>";
                    smtp.Send(mail);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        // ==========================================
        // 4. DEPARTMENT MANAGEMENT
        // ==========================================

        public ActionResult Departments()
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");
            return View(db.Departments.ToList());
        }

        [HttpPost]
        public ActionResult CreateDepartment(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                db.Departments.Add(new Department { Name = name, IsActive = true });
                db.SaveChanges();
            }
            return RedirectToAction("Departments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDepartment(int id, int? answer, int? expected)
        {
            if (answer != expected) return RedirectToAction("Departments");

            var dept = db.Departments.Find(id);
            if (dept != null && !dept.Visitors.Any())
            {
                db.Departments.Remove(dept);
                db.SaveChanges();
            }
            return RedirectToAction("Departments");
        }

        [HttpPost]
        public ActionResult ToggleDepartmentStatus(int id)
        {
            var dept = db.Departments.Find(id);
            if (dept != null)
            {
                dept.IsActive = !(dept.IsActive ?? false);
                db.SaveChanges();
            }
            return RedirectToAction("Departments");
        }

        private string GenerateQrCode(string content)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
                return "data:image/png;base64," + Convert.ToBase64String(qrCodeAsPngByteArr);
            }
        }


        //GROUP APPROVAL

        [HttpPost]
        public ActionResult UpdateGroupStatus(Guid groupId, int status)
        {
            if (Session["AdminEmail"] == null) return RedirectToAction("Index", "Home");

            // Fetch all pending visitors belonging to this specific CSV upload group
            var visitorsInGroup = db.Visitors.Where(v => v.GroupId == groupId && v.Status == 1).ToList();

            if (visitorsInGroup.Any())
            {
                foreach (var visitor in visitorsInGroup)
                {
                    visitor.Status = status;

                    if (status == 2) // Approved
                    {
                        string passPath = GeneratePassPdf(visitor);
                        visitor.IdPassPath = passPath;
                        SendApprovalEmail(visitor);
                    }
                    else if (status == 3) // Rejected
                    {
                        SendRejectionEmail(visitor.Email, visitor.Name);
                    }
                }
                db.SaveChanges();
                TempData["Success"] = $"Group of {visitorsInGroup.Count} processed successfully.";
            }

            return RedirectToAction("AdminDashboard");
        }
    }
}