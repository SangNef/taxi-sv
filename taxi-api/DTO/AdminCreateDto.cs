namespace taxi_api.DTO
{
    public enum AdminRole
    {
        Admin,
        SuperAdmin
    }

    public class AdminCreateDto
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public AdminRole Role { get; set; } 
    }
}
