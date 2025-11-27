using System;
using System.ComponentModel.DataAnnotations;
namespace VEA.API.Models
{
    public class EmployeeBlock
    {
        [Key]
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public DateTime BlockDate { get; set; }
        public TimeSpan StartTime { get; set; } // Ex: 12:00
        public TimeSpan EndTime { get; set; } // Ex: 13:00
        public string Reason { get; set; } = string.Empty; // Folga, pausa
    }
}