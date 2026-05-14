namespace MIS_DEMO.Models.ViewModels
{
    public class OutstandingDetailsViewModel
    {
        public string SelectedTeam { get; set; } // <--- ADD THIS
        public string InvoiceType { get; set; }
        public int RangeDays { get; set; }
        public string PdMode { get; set; }

        public List<OutstandingRow> Rows { get; set; } = new();
        public decimal TotalOutstanding { get; set; }
        public List<int> AvailableDays { get; set; } = new();
    }

    public class OutstandingRow
    {
        public string DocNo { get; set; }
        public DateTime? RefDate { get; set; }
        public DateTime? FDeliveryDate { get; set; }
        public string CusCode { get; set; }
        public string CusName { get; set; }
        public decimal InvoiceAmt { get; set; }
        public decimal BalanceAmt { get; set; }

        // Calculated Age
        public int AgeDays { get; set; }
        public string AgeBracket { get; set; } // e.g., "Above 120" or "Below 120"

        // Let's grab these safely if they exist in your invoice table
        public string RepCode { get; set; }
        public string RepName { get; set; }
        public string Team { get; set; }
    }
}