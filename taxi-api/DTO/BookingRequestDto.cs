using System;
using System.ComponentModel.DataAnnotations;

namespace taxi_api.DTO
{
    public class BookingRequestDto
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Count is required.")]
        public int Count { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        public string Types { get; set; } 

        [Required(ErrorMessage ="Start At is required")] 
        public DateOnly StartAt { get; set; }
        public int? PickUpId { get; set; }
        public string? PickUpAddress { get; set; }

        public int? DropOffId { get; set; } 

        public string? DropOffAddress { get; set; } 
        public bool HasFull { get; set; }

        public DateTime CreatedAt { get; set; } 

        public DateTime UpdatedAt { get; set; }
    }
}
