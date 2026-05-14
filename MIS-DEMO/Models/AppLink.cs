using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("LINKS")]
    public class AppLink
    {
        [Key]
        public string LinkName { get; set; }

        public string Url { get; set; }
    }
}
