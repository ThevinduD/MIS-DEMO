using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("TARGET_MAIN")]
    public class TargetMain
    {
        [Key]
        public string TranNo { get; set; }
        public string? UserName { get; set; }
        public string? TeamCode { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Frequence { get; set; }
        public string? Status { get; set; }
        public string? StatusMonth { get; set; }
        public decimal? MonthlyTarget { get; set; }
    }
}