using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Table("CUSTOMER_INVOICE_MAIN")]
    public class CustomerInvoiceMain
    {
        public string InvoDocNo { get; set; }
        public string ComCode { get; set; }
        public string LocCode { get; set; }
        public string CusCode { get; set; }

        public DateTime? RefDate { get; set; }
        public DateTime? FDeliveryDate { get; set; }

        public string SalesRepCode { get; set; }
        public decimal InvoiceAmt { get; set; }

        public bool? isFinalDelivery { get; set; }
        public bool Cancel { get; set; } // or bool? depends on your DB
        public string Pat_Name { get; set; } // TeamCode in your DB design
        public int? CreditDays { get; set; } // <--- NEW
    }
}
