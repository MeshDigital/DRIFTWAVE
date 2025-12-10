using Microsoft.VisualBasic;
using SLSKDONET.Services;

namespace SLSKDONET.Views;

public class UserInputService : IUserInputService
{
    public string? GetInput(string prompt, string title, string defaultValue = "")
    {
        return Interaction.InputBox(prompt, title, defaultValue);
    }
}