namespace MIS_DEMO.Models.ViewModels
{
    public class InvoiceDetailsViewModel
    {
        public string InvoNo { get; set; }
        public DateTime? RefDate { get; set; }
        public string CustomerName { get; set; }
        public string RepName { get; set; }
        public string Team { get; set; }
        public decimal TotalAmount { get; set; }

        public List<InvoiceLineItem> Lines { get; set; } = new List<InvoiceLineItem>();
    }

    public class InvoiceLineItem
    {
        public string ItemCode { get; set; }
        public string Description { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
