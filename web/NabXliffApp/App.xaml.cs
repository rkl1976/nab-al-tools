namespace NabXliffApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "NAB XLIFF",
            Width = 1280,
            Height = 800,
            MinimumWidth = 900,
            MinimumHeight = 600
        };
    }
}
