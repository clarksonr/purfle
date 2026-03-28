namespace Purfle.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("AgentDetailPage", typeof(Pages.AgentDetailPage));
    }
}
