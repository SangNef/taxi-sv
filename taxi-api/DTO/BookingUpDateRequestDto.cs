namespace taxi_api.DTO
{
    public class BookingUpDateRequestDto
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public int Count { get; set; }
        public string Types { get; set; }
        public DateOnly StartAt { get; set; }
        public int? PickUpId { get; set; }
        public string? PickUpAddress { get; set; }
        public int? DropOffId { get; set; }
        public string? DropOffAddress { get; set; }
        public bool HasFull { get; set; }
    }
}
