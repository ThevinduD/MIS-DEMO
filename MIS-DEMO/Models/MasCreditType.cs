using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("mascredittype")]
    public class MasCreditType
    {
        [Key]
        public int credittypeky { get; set; }

        public string code { get; set; }
        public string description { get; set; }
        public int creditdays { get; set; }
        public string? remarks { get; set; }
        public bool inactive { get; set; }
        public DateTime createdatetime { get; set; }
        public DateTime updatedatetime { get; set; }
        public int sessionid { get; set; }
    }
}