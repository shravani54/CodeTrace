namespace CodeTrace.Api.Models;

public class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
}