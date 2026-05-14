namespace MIS_DEMO.Models.ViewModels
{
    public class NonDeliveredKpiViewModel
    {
        public string SelectedTeam { get; set; } = "All";
        public List<string> AvailableTeams { get; set; } = new();

        public int RangeDays { get; set; }
        public List<int> AvailableDays { get; set; } = new();

        public string SelectedCategory { get; set; } = "All";
        public List<string> AvailableCategories { get; set; } = new();

        public decimal UnderAmount { get; set; }
        public decimal OverAmount { get; set; }
    }
}