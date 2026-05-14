using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("WKF_MAP_REP_ASM_MIS")]
    public class WkfMapRepAsmMis
    {
        [Column("UserName")]
        public string? UserName { get; set; } // Nullable because your SQL says NULL

        [Key]
        [Column("SalesRepCode")]
        public string SalesRepCode { get; set; } = string.Empty; // Primary Key and NOT NULL
    }
}