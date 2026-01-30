using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("VW_SALES_FACT")]
    public class SalesFact
    {
        public string CusCode { get; set; }
        public string CusName { get; set; }
        public string InvoDocNo { get; set; }
        public DateTime RefDate { get; set; }

        public string ItemCode { get; set; }
        public string ItemDescription { get; set; }
        public string Pat_Name { get; set; }

        public decimal SoldPrice { get; set; }
        public decimal Qty { get; set; }
        public decimal LineTotal { get; set; }

        public string SupCode { get; set; }
        public string SupName { get; set; }

        public string SalesRepCode { get; set; }
        public string SalesRepName { get; set; }
    }
}
