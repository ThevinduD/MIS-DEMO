using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("WKF_USER_REP_MAP")]
    public class UserRepMap
    {
        [Column("UserName")]
        public string UserName { get; set; } 

        [Column("Type")]
        public string Type { get; set; }

        [Column("SalesRepCode")]
        public string SalesRepCode { get; set; }

        [Column("TeamCode")]
        public string TeamCode { get; set; }
    }
}
