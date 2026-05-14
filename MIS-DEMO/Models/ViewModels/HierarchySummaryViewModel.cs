namespace MIS_DEMO.Models.ViewModels
{
    public class HierarchySummaryViewModel
    {
        public string InvoiceType { get; set; } = string.Empty;
        public int RangeDays { get; set; }
        public string PdMode { get; set; } = string.Empty;
        public string Team { get; set; } = "All";

        public decimal GrandTotalOut { get; set; }
        public decimal GrandTotalPd { get; set; }
        public decimal GrandTotalGross { get; set; }

        // ---> CHANGED TO A FLAT LIST <---
        public List<HierarchyRepRow> Rows { get; set; } = new List<HierarchyRepRow>();
    }

    public class HierarchyRepRow
    {
        public string RepCode { get; set; } = string.Empty;
        public string RepName { get; set; } = string.Empty;

        public decimal OutstandingAmt { get; set; }
        public decimal PdAmt { get; set; }
        public decimal GrossAmt => OutstandingAmt - PdAmt;
    }
}