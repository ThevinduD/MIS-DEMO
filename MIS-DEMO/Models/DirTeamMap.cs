using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("DIR_TEAM_MAP")]
    public class DirTeamMap
    {
        public string UserNameDir { get; set; }   // director username
        public string TeamCode { get; set; }   // mapped team code
    }
}
