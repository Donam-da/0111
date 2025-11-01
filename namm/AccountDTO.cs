namespace namm
{
    public class AccountDTO
    {
        public string UserName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Type { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}