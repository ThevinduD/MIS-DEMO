using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("NON_DELIVERED_DAYS")]
    public class NonDeliveredDays
    {
        [Column("Days")]
        public int Days { get; set; }
    }
}