using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("OUTSTANDING_DAYS")]
    public class OutstandingDays
    {
        public int Days { get; set; }
    }
}