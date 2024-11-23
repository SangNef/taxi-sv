namespace taxi_api.DTO
{
    public class UpdateAdminPasswordRequest
    {
        public string OldPassword { get; set; } 
        public string NewPassword { get; set; }  
    }

}
