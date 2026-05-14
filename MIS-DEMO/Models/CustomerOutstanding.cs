using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("CUSTOMER_OUTSTANDING")]
    public class CustomerOutstanding
    {
        public string? ComCode { get; set; }
        public string? LocCode { get; set; }
        public string? CusCode { get; set; }
        public string? DocNo { get; set; }
        public DateTime? RefDate { get; set; }
        public decimal InvoiceAmt { get; set; }
        public decimal BalanceAmt { get; set; }
        public decimal ReturnAmt { get; set; }
        public string? Note { get; set; }
        public DateTime? DuePayDate { get; set; }
        public string? Bill_Com_Code { get; set; }
        public string? ManualNo { get; set; }
        public bool? isExcelUpload { get; set; }
        public bool? isSystmUpload { get; set; }
    }
}