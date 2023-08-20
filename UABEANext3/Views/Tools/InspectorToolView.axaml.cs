using Avalonia.ReactiveUI;
using AvaloniaEdit.Utils;
using ReactiveUI;
using System.Threading.Tasks;
using UABEANext3.Services;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.ViewModels.Tools;
using UABEANext3.Views.Dialogs;

namespace UABEANext3.Views.Tools
{
    public partial class InspectorToolView : ReactiveUserControl<InspectorToolViewModel>
    {
        public InspectorToolView()
        {
            InitializeComponent();

            this.WhenActivated(action => {
                action(ViewModel!.ShowEditData.RegisterHandler(DoShowEditDataAsync));
            });
        }

        private async Task DoShowEditDataAsync(InteractionContext<EditDataViewModel, byte[]?> interaction)
        {
            if (ViewModel == null)
                return;

            var dialogService = ViewModel.Container.GetService<IDialogService>();
            if (dialogService == null)
            {
                interaction.SetOutput(null);
                return;
            }

            var dialog = new EditDataView();
            dialog.DataContext = interaction.Input;

            var result = await dialogService.ShowDialog<byte[]?>(dialog);
            interaction.SetOutput(result);
        }
    }
}
