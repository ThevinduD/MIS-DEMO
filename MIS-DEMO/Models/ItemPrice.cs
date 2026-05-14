using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // Required for [Keyless]

namespace MIS_DEMO.Models
{
    [Keyless]
    [Table("ITEM_PRICE")]
    public class ITEM_PRICE
    {
        // Notice we removed the [Key] attribute from here!
        public string ItemRefNo { get; set; }
        public string Type_Code { get; set; }
        public decimal S_Price { get; set; }
        public decimal M_Price { get; set; }
        public decimal DiscountRate { get; set; }
    }
}