using System.ComponentModel.DataAnnotations;

namespace taxi_api.DTO
{
    public class DriverLoginDto
    {
        [Required(ErrorMessage = "Phone is required.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }  
    }
}
