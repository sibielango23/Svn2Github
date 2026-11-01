using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Views
{
    public partial class AiAssistantView : UserControl
    {
        public AiAssistantView()
        {
            InitializeComponent();
            Loaded += (_, _) => SubscribeToMessages();
            Unloaded += (_, _) => UnsubscribeFromMessages();
            DataContextChanged += (_, _) =>
            {
                UnsubscribeFromMessages();
                SubscribeToMessages();
            };
        }

        private void Question_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            e.Handled = true;
            if (DataContext is AiAssistantViewModel viewModel &&
                viewModel.SendCommand.CanExecute(null))
            {
                viewModel.SendCommand.Execute(null);
            }
        }

        private void SubscribeToMessages()
        {
            if (DataContext is AiAssistantViewModel viewModel)
            {
                viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
                viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
                ScrollConversationToLatest();
            }
        }

        private void UnsubscribeFromMessages()
        {
            if (DataContext is AiAssistantViewModel viewModel)
            {
                viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            }
        }

        private void Messages_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            ScrollConversationToLatest();
        }

        private void ScrollConversationToLatest()
        {
            if (!IsLoaded || Dispatcher.HasShutdownStarted)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (IsLoaded)
                {
                    ConversationScrollViewer.UpdateLayout();
                    ConversationScrollViewer.ScrollToEnd();
                }
            }));
        }
    }
}
