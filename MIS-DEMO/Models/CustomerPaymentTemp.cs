using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("CUSTOMER_PAYMENT_TEMP")]
    public class CustomerPaymentTemp
    {
        public string? PaymentNo { get; set; }
        public string? ReceiptNo { get; set; }
        public string? DocNo { get; set; }
        public DateTime? PayDate { get; set; }
        public decimal? PayAmt { get; set; }
        public string? PayType { get; set; }
        public string? Method { get; set; }
        public string? ChequeRefNo { get; set; }
        public string? CardRefNo { get; set; }
        public bool? Cancel { get; set; }
        public bool? isDeposited { get; set; }
        public string? CusCode { get; set; }
    }
}