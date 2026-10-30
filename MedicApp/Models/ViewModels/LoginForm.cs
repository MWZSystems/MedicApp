using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class LoginForm
{
    [Required(ErrorMessage = "Required"), MaxLength(256), Display(Name = "Identifier")] public string Identifier { get; set; } = "";
    [Required(ErrorMessage = "Required"), DataType(DataType.Password), Display(Name = "Password")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
