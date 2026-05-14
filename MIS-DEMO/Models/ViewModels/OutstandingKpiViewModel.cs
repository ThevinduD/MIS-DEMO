namespace MIS_DEMO.Models.ViewModels
{
    public class OutstandingKpiViewModel
    {
        // Dropdown States
        public string InvoiceType { get; set; } = "Delivered"; // Delivered, Non Delivered, All
        public int RangeDays { get; set; } = 120;        // e.g., 60, 90, 120
        public string PdMode { get; set; } = "Without PD"; // With PD, Without PD

        // Calculated Data
        public int BelowCount { get; set; }
        public decimal BelowAmount { get; set; }

        public int AboveCount { get; set; }
        public decimal AboveAmount { get; set; }

        public string SelectedTeam { get; set; } = "All";
        public List<string> AvailableTeams { get; set; } = new();
        public List<int> AvailableDays { get; set; } = new();
    }
}