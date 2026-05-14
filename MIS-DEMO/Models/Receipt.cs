using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("RECEIPT")]
    public class Receipt
    {
        [Key]
        public string ReceiptNo { get; set; }
        public decimal? TotalPaidAmt { get; set; }
    }
}