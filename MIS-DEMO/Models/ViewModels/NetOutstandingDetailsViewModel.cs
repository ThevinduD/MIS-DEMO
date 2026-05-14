namespace MIS_DEMO.Models.ViewModels
{
    public class NetOutstandingDetailsViewModel
    {
        // Keeping track of filters so the "Back" button makes sense
        public string SelectedTeam { get; set; }
        public DateTime SelectedDate { get; set; }
        public string OutstandingMode { get; set; }
        public string FreezeMode { get; set; }
        public string SelectedCategory { get; set; }

        // --> NEW PROPERTIES FOR FILTERS <--
        public string SearchInvo { get; set; }
        public string SearchRep { get; set; }
        public string SearchCustomer { get; set; }
        public List<string> AvailableTeams { get; set; }

        // The actual data
        public List<NetOutRow> Rows { get; set; }
        public decimal TotalNetOut { get; set; }
    }

    public class NetOutRow
    {
        public string InvoNo { get; set; }
        public DateTime? InvDate { get; set; }
        public DateTime? DelDate { get; set; }
        public int CreditPeriod { get; set; }
        public string Team { get; set; }
        public string RepName { get; set; }
        public string Customer { get; set; }
        public decimal InvAmount { get; set; }
        public decimal Balance { get; set; }
        public DateTime? DueDate { get; set; }
        public int Aging { get; set; }
    }
}
