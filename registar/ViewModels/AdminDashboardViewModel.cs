using System;
using System.Collections.Generic;

// Change this to match exactly what your error message says: registar.Models
namespace registar.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalVisitors { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }

        // Data for Charts
        public List<string> TimelineLabels { get; set; }
        public List<int> RegistrationData { get; set; }
        public List<int> LoginData { get; set; }
        public List<int> CheckInData { get; set; }
    }
}