using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("RECEIPT_PAY_INFO")]
    public class ReceiptPayInfo
    {
        public string ComCode { get; set; }
        public string LocCode { get; set; }
        public string ReceiptNo { get; set; }
        public string PayType { get; set; }
        public string ChequeCardNo { get; set; }
        public string BankCode { get; set; }
        public string BranchCode { get; set; }
        public decimal? Amount { get; set; }
        public string ReferenceNo { get; set; }
    }
}