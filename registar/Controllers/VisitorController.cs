using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using registar.Models;

namespace registar.Controllers
{
    public class VisitorController : Controller
    {
        private VMS_DatabaseEntities db = new VMS_DatabaseEntities();

        //===================================
        // Visitor's Personal Dashboard
        //==================================
        public ActionResult MyDashboard()
        {
            if (Session["VisitorID"] == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (int.TryParse(Session["VisitorID"].ToString(), out int visitorId))
            {
                var currentVisitor = db.Visitors.SingleOrDefault(v => v.Id == visitorId);
                if (currentVisitor == null) return HttpNotFound();

                // Get all visits for this user's email to show History
                var allVisits = db.Visitors
                    .Where(v => v.Email == currentVisitor.Email)
                    .OrderByDescending(v => v.VisitDate)
                    .ToList();

                ViewBag.AllVisits = allVisits;
                return View(currentVisitor);
            }

            return RedirectToAction("Index", "Home");
        }

        //=================
        // VISIT HISTORY
        //=================
        public ActionResult VisitHistory()
        {
            if (Session["VisitorID"] == null) return RedirectToAction("Index", "Home");

            int visitorId = (int)Session["VisitorID"];

            var history = db.VisitorLogs
                .Where(l => l.VisitorId == visitorId)
                .OrderByDescending(l => l.CheckInTime)
                .ToList();

            return View(history);
        }

        //=================
        // REVISIT REQUEST
        //=================
        public ActionResult RevisitRequest()
        {
            if (Session["VisitorID"] == null) return RedirectToAction("Index", "Home");

            int vId = (int)Session["VisitorID"];
            var user = db.Visitors.Find(vId);

            ViewBag.DepartmentId = new SelectList(db.Departments.Where(d => d.IsActive == true), "Id", "Name");

            var newRequest = new Visitor
            {
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone
            };

            return View(newRequest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitRevisit(Visitor model)
        {
            if (ModelState.IsValid)
            {
                model.GroupId = Guid.NewGuid();
                model.Status = 1; // Direct to pending approval for revisits
                model.RegisteredAt = DateTime.Now;

                db.Visitors.Add(model);
                db.SaveChanges();

                TempData["Success"] = "Your visit request has been sent for approval!";
                return RedirectToAction("MyDashboard");
            }

            ViewBag.DepartmentId = new SelectList(db.Departments.Where(d => d.IsActive == true), "Id", "Name");
            return View("RevisitRequest", model);
        }

        //=========================
        // CHECK IN / OUT LOGIC
        //=========================
        public ActionResult CheckInOut()
        {
            if (Session["VisitorID"] == null) return RedirectToAction("Index", "Home");

            int vId = (int)Session["VisitorID"];
            var visitor = db.Visitors.Find(vId);

            var lastLog = db.VisitorLogs
                .Where(l => l.VisitorId == vId)
                .OrderByDescending(l => l.CheckInTime)
                .FirstOrDefault();

            string currentStatus = "Checked Out";
            bool canCheckOut = false;

            if (lastLog != null && lastLog.CheckOutTime == null)
            {
                var timeSinceCheckIn = DateTime.Now - lastLog.CheckInTime.Value;

                if (timeSinceCheckIn.TotalMinutes <= 30)
                {
                    currentStatus = "Checked In";
                    canCheckOut = true;
                    ViewBag.CheckInTime = lastLog.CheckInTime.Value.ToString("hh:mm tt");
                    ViewBag.MinutesRemaining = Math.Round(30 - timeSinceCheckIn.TotalMinutes);
                }
                else
                {
                    // Auto-Checkout
                    lastLog.CheckOutTime = lastLog.CheckInTime.Value.AddMinutes(30);
                    lastLog.Remarks = (lastLog.Remarks ?? "") + " (Auto-Checkout)";
                    db.SaveChanges();
                    currentStatus = "Checked Out (Auto)";
                }
            }

            ViewBag.VisitorId = visitor.Id;
            ViewBag.VisitorName = visitor.Name;
            ViewBag.CurrentStatus = currentStatus;
            ViewBag.CanCheckOut = canCheckOut;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckInOut(string actionType, string remarks)
        {
            if (Session["VisitorID"] == null) return RedirectToAction("Index", "Home");

            int vId = (int)Session["VisitorID"];

            if (actionType == "In")
            {
                db.VisitorLogs.Add(new VisitorLog
                {
                    VisitorId = vId,
                    CheckInTime = DateTime.Now,
                    Remarks = remarks
                });
                TempData["Message"] = "Successfully checked in!";
            }
            else if (actionType == "Out")
            {
                var activeLog = db.VisitorLogs
                    .Where(l => l.VisitorId == vId && l.CheckOutTime == null)
                    .OrderByDescending(l => l.CheckInTime)
                    .FirstOrDefault();

                if (activeLog != null)
                {
                    activeLog.CheckOutTime = DateTime.Now;
                    if (!string.IsNullOrEmpty(remarks)) activeLog.Remarks = remarks;
                    TempData["Message"] = "Successfully checked out!";
                }
            }

            db.SaveChanges();
            return RedirectToAction("CheckInOut");
        }
    }
}