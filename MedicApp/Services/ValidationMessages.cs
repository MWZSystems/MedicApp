using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
namespace MedicApp.Services;

/// <summary>Gives length and range rules without their own message a UiText key, so they are shown in Spanish.</summary>
public sealed class ValidationMessages : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.Attributes.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessage is not null || attribute.ErrorMessageResourceName is not null) { continue; }
            var key = attribute switch
            {
                RangeAttribute { Minimum: 1, Maximum: int.MaxValue } => "ValidationChoose",
                RangeAttribute => "ValidationRange",
                MaxLengthAttribute or StringLengthAttribute => "ValidationMaxLength",
                MinLengthAttribute => "ValidationMinLength",
                _ => null
            };
            // Assigning null would mark the message as custom and make the attribute throw, so other rules keep their own.
            if (key is not null) { attribute.ErrorMessage = key; }
        }
    }
}
