using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class PasswordForm
{
    [Required(ErrorMessage = "Required"), DataType(DataType.Password)] public string Current { get; set; } = "";
    [Required(ErrorMessage = "Required"), MinLength(12, ErrorMessage = "PasswordLength"), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "PasswordsDiffer"), DataType(DataType.Password)] public string Confirm { get; set; } = "";
}
