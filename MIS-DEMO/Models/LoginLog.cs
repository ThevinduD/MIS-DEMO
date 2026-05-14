using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("LOGIN_LOGS")] // Using your standard uppercase naming convention
    public class LoginLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        public string Username { get; set; }
        public string RealName { get; set; }
        public DateTime LoginTime { get; set; }
        public string IpAddress { get; set; }

        // Bonus: Grabbing the browser info is usually very helpful for debugging!
        public string? UserAgent { get; set; }
    }
}