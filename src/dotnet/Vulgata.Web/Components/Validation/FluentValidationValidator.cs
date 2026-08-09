using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Vulgata.Web.Components;

public sealed class FluentValidationValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? _messages;

    [CascadingParameter]
    private EditContext CurrentEditContext { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    protected override void OnInitialized()
    {
        ArgumentNullException.ThrowIfNull(CurrentEditContext);
        _messages = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += ValidateModel;
        CurrentEditContext.OnFieldChanged += ValidateModel;
    }

    private void ValidateModel(object? sender, EventArgs args)
    {
        _messages!.Clear();
        Type validatorType = typeof(IValidator<>).MakeGenericType(
            CurrentEditContext.Model.GetType());
        if (Services.GetService(validatorType) is IValidator validator)
        {
            FluentValidation.Results.ValidationResult result = validator.Validate(
                new ValidationContext<object>(CurrentEditContext.Model));
            foreach (FluentValidation.Results.ValidationFailure failure in result.Errors)
            {
                _messages.Add(
                    CurrentEditContext.Field(failure.PropertyName),
                    failure.ErrorMessage);
            }
        }

        CurrentEditContext.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (CurrentEditContext is not null)
        {
            CurrentEditContext.OnValidationRequested -= ValidateModel;
            CurrentEditContext.OnFieldChanged -= ValidateModel;
        }
    }
}
