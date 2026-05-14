namespace MIS_DEMO.Models.ViewModels
{
    public class UnrealizedDetailsViewModel
    {
        public string SelectedTeam { get; set; }
        public string SelectedCategory { get; set; }

        // Datalists for fast filtering
        public List<string> AvailableTeams { get; set; }
        public List<string> AvailableInvoices { get; set; }
        public List<string> AvailableReps { get; set; }
        public List<string> AvailableCustomers { get; set; }

        public List<UnrealizedRow> Rows { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class UnrealizedRow
    {
        public string ReceiptNo { get; set; }
        public string ChequeRefNo { get; set; }
        public string ChequeNo { get; set; }
        public DateTime? PayDate { get; set; }
        public string InvoNo { get; set; }
        public string Team { get; set; }
        public string RepName { get; set; }
        public string Customer { get; set; }
        public decimal Amount { get; set; }
    }
}
