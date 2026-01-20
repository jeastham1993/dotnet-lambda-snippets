using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Services;

public interface INotificationService
{
    Task SendOrderConfirmationAsync(Order order);
}
