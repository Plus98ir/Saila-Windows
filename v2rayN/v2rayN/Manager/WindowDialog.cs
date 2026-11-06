using v2rayN.Base;

namespace v2rayN.Manager;

public class WindowDialog : IWindowDialog
{
    public Task<bool> ShowDialogAsync<TViewModel>(TViewModel vm)
        where TViewModel : class
    {
        var owner = Application.Current.MainWindow;

        // Saila: server forms open inside the main window, in the Saila look.
        if (owner is v2rayN.Views.MainWindow main && vm is AddServerViewModel or AddServer2ViewModel)
        {
            return main.ShowEditPageAsync(vm);
        }

        var viewFor = SimpleViewLocator.Instance.ResolveView(vm);
        //var viewFor = SimpleViewLocator.Instance.ResolveView<TViewModel>();

        if (viewFor is not WindowBase<TViewModel> window)
        {
            return Task.FromResult(false);
        }

        window.Owner = owner;
        window.ViewModel = vm;

        if (vm is ServiceLib.Base.ICloseable closeable)
        {
            closeable.RequestClose += (_, _) => Dispatch(() => window.DialogResult = true);
        }

        var result = window.ShowDialog();

        return Task.FromResult(result ?? false);
    }

    private static void Dispatch(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }
}
