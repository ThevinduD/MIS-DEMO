namespace MIS_DEMO.Models.ViewModels
{
    public class DeliveredKpiViewModel
    {
        public string SelectedTeam { get; set; } = "All";
        public List<string> AvailableTeams { get; set; } = new();
        public string SelectedCategory { get; set; } = "All";
        public List<string> AvailableCategories { get; set; } = new();
        public decimal ThisMonthDeliveredTotal { get; set; }
    }
}