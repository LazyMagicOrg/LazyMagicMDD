using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace LazyMagicVsExt
{

    public class LogEntry : INotifyPropertyChanged
    {
        public DateTime DateTime { get; set; }

        public int Index { get; set; }

        public string Message { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            _ = Application.Current.Dispatcher.BeginInvoke((Action)(() =>
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }));
        }
    }

    public class CollapsibleLogEntry : LogEntry
    {
        public List<LogEntry> Contents { get; set; }
    }


    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class LazyMagicLogToolWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LazyMagicLogToolWindowControl"/> class.
        /// </summary>
        public LazyMagicLogToolWindowControl()
        {
            this.InitializeComponent();
            DataContext = LogEntries = new ObservableCollection<LogEntry>();
        }

        private bool AutoScroll = true;

        public ObservableCollection<LogEntry> LogEntries { get; set; }

        private void ScrollView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // User scroll event : set or unset autoscroll mode
            if (e.ExtentHeightChange == 0)
            {   // Content unchanged : user scroll event
                if ((e.Source as ScrollViewer).VerticalOffset == (e.Source as ScrollViewer).ScrollableHeight)
                {   // Scroll bar is in bottom
                    // Set autoscroll mode
                    AutoScroll = true;
                }
                else
                {   // Scroll bar isn't in bottom
                    // Unset autoscroll mode
                    AutoScroll = false;
                }
            }

            // Content scroll event : autoscroll eventually
            if (AutoScroll && e.ExtentHeightChange != 0)
            {   // Content changed and autoscroll mode set
                // Autoscroll
                (e.Source as ScrollViewer).ScrollToVerticalOffset((e.Source as ScrollViewer).ExtentHeight);
            }
        }
    }
}