using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("WKF_MAP_ASM_DIR")]
    public class WkfMapAsmDir
    {
        public string UserNameDir { get; set; }
        public string UserNameAsm { get; set; }
    }
}
