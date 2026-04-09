namespace CbsContractsDesktopClient.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // admin, manager, user и т.д.
        public string Token { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public User User { get; set; } = new();
        public string Token { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string DebugJson { get; set; } = string.Empty;
    }
}
