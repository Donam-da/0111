namespace namm
{
    public class BillItem
    {
        public int DrinkId { get; set; }
        public string DrinkName { get; set; } = string.Empty;
        public string DrinkType { get; set; } = string.Empty; // "Nguyên bản" hoặc "Pha chế"
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice => Quantity * Price;
    }
}