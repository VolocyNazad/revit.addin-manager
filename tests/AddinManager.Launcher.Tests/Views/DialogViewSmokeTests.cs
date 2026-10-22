using System.Threading;
using System.Windows;
using AddinManager.Launcher.Views.Dialogs;

namespace AddinManager.Launcher.Tests.Views;

/// <summary>
/// Дымовая проверка XAML: реальные диалоги создаются поверх ресурсов <see cref="App"/>.
/// Компиляция XAML не ловит ошибки, которые WPF находит только при разборе BAML
/// (например, неразрешимый <c>Setter.Property</c> в общем стиле окна).
/// </summary>
public sealed class DialogViewSmokeTests
{
    [Fact]
    public void DialogViews_CanBeConstructed_WithApplicationResources()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App();
                application.InitializeComponent();

                _ = new ConfirmDialogView();
                _ = new AddFileDialogView();
                _ = new AddEntryDialogView();
                _ = new UpdateDialogView();
                _ = new MoveCopyDialogView();
            }
            catch (System.Windows.Markup.XamlParseException exception)
            {
                failure = new InvalidOperationException(
                    $"{exception.BaseUri} line {exception.LineNumber}:{exception.LinePosition}: {exception.Message}",
                    exception);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
