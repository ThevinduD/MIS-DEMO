namespace MIS_DEMO.Models.ViewModels
{
    public class InvoiceDetailViewModel
    {
        // Header Info
        public string InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string Team { get; set; }
        public string RepName { get; set; }
        public string CusName { get; set; }


        // Totals (Replace the old Gross/Return/Net properties with these)
        public decimal InvoiceAmount { get; set; }
        public decimal BalanceAmount { get; set; }

        // Line Items
        public List<InvoiceItemRow> Items { get; set; } = new();
        public List<InvoiceItemRow> ReturnedItems { get; set; } = new();
        public List<PaymentItemRow> Payments { get; set; } = new();
        public List<SettlementItemRow> Settlements { get; set; } = new();
        public List<PaymentItemRow> PdCheques { get; set; } = new();
    }

    public class InvoiceItemRow
    {
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaymentItemRow
    {
        public string ReceiptNo { get; set; }
        public DateTime? PayDate { get; set; }
        public string Type { get; set; }
        public string RefNo { get; set; }
        public decimal Amount { get; set; }
        public decimal ReceiptAmount { get; set; } // <--- ADD THIS
    }

    public class SettlementItemRow
    {
        public string SettlementNo { get; set; }
        public decimal Amount { get; set; }
        public decimal SetOffAmount { get; set; }
        public string Note { get; set; }
        public string RefNo { get; set; }
    }
}