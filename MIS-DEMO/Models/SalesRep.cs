using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("SALES_REP")]
    public class SalesRep
    {
        [Key]
        [Column("SalesRepCode")]
        public string SalesRepCode { get; set; } = string.Empty; // Primary Key

        [Column("SalesRepName")]
        public string? SalesRepName { get; set; }

    }
}