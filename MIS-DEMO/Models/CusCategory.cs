using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("CUS_CATEGORY")]
    public class CusCategory
    {
        [Key]
        public string CusCategoryCode { get; set; }
        public string? CusCategoryName { get; set; }
    }
}