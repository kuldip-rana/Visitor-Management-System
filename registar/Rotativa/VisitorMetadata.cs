using System;
using System.ComponentModel.DataAnnotations;

namespace registar.Models
{
    // 1. Tell the system to look at the Metadata class for validation rules
    [MetadataType(typeof(VisitorAttributes))]
    public partial class Visitor
    {
        // Keep this empty. It just "links" the two classes.
    }

    // 2. Define your validation rules here
    public class VisitorAttributes
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Visitor Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mobile number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please enter a valid 10-digit mobile number")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Purpose of visit is required")]
        public string Purpose { get; set; }

        [Required(ErrorMessage = "Please select a visit date")]
        [DataType(DataType.Date)]
        public DateTime VisitDate { get; set; }

        [Required(ErrorMessage = "Please select a department")]
        public int DepartmentId { get; set; }
    }
}