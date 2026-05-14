using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("MIS_DEFAULT_CONFIG")]
    public class MisDefaultConfig
    {
        public string? OutstandInvType { get; set; }
        public int? AgingDefaultDays { get; set; }
        public string? PdCheq { get; set; } // <--- ADD THIS
        public string? CollectionFreeze { get; set; }
        public string? CollectionType { get; set; }
        public int? NonDelDefDays { get; set; }
    }
}
