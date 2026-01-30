namespace MIS_DEMO.Models.ViewModels
{
    public class SalesLineViewModel
    {
        public DateTime RefDate { get; set; }

        public string? CusCode { get; set; }
        public string? CusName { get; set; }

        public string? InvoDocNo { get; set; }
        public string? ManualNo { get; set; }

        public string? ItemCode { get; set; }
        public string? Description { get; set; }

        public decimal Qty { get; set; }
        public decimal SoldPrice { get; set; }
        public decimal LineTotal { get; set; }

        public string? SupName { get; set; }
        public string? SalesRepCode { get; set; }
        public string? SalesRepName { get; set; }
    }
}
