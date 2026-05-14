using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("CHEQUE")]
    public class Cheque
    {
        [Key]
        public string ChequeRefNO { get; set; }
        public DateTime? ChequeRefDate { get; set; }
        public string? ChequeNo { get; set; }
        public string? BankCode { get; set; }
        public string? ChequeType { get; set; }
        public string? Origin { get; set; }
        public decimal Dr { get; set; }
        public decimal Cr { get; set; }
        public string? Deposit_Ref { get; set; }
        public bool? isRealized { get; set; }
        public DateTime? Deposit_Date { get; set; }
        public DateTime? Returned_Date { get; set; }
        public string? ChequeStatus { get; set; }
        public string? OwnerType { get; set; }

        [Column("ReceiptVoucherNo")]
        public string? ReceiptVoucherNo { get; set; }

        [Column("OwnerCode")]
        public string? OwnerCode { get; set; }

        // ==========================================
        // ADDED MISSING PROPERTIES HERE
        // ==========================================
        public DateTime? RealizedDate { get; set; }
        public DateTime? Realized_Date { get; set; }
    }
}