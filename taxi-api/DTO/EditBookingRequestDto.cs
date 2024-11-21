namespace taxi_api.DTO
{
    public class EditBookingRequestDto
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public int? PickUpId { get; set; }
        public int? DropOffId { get; set; }
        public string PickUpAddress { get; set; }
        public string DropOffAddress { get; set; }
        public DateTime? StartAt { get; set; }
        public decimal? Price { get; set; }
        public string Status { get; set; }
    }
}
